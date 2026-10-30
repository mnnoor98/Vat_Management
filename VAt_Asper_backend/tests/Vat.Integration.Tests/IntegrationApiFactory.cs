using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Vat.Application.Security;
using Vat.Services.Integration.Infrastructure;
using Vat.Services.Integration;
using Xunit;

namespace Vat.Integration.Tests;

public sealed class IntegrationApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public RecordingOperatorClient Operators { get; } = new();
    private const string Issuer = "https://test-issuer.example";
    private const string Audience = "vat-api-tests";
    private readonly RSA _rsa = RSA.Create(2048);
    private readonly string _database = "integration_test_" + Guid.NewGuid().ToString("N");
    private string _adminConnection = "";
    private string _connection = "";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:VatDatabase"] = _connection,
            ["ConnectionStrings:IdentityDatabase"] = _connection,
            ["ConnectionStrings:CompanyDatabase"] = _connection,
            ["ConnectionStrings:MasterDataDatabase"] = _connection,
            ["ConnectionStrings:PurchaseDatabase"] = _connection,
            ["ConnectionStrings:SalesDatabase"] = _connection,
            ["ConnectionStrings:InventoryDatabase"] = _connection,
            ["ConnectionStrings:ManufacturingDatabase"] = _connection,
            ["ConnectionStrings:VatEngineDatabase"] = _connection,
            ["ConnectionStrings:ComplianceDatabase"] = _connection,
            ["ConnectionStrings:WorkflowDatabase"] = _connection,
            ["ConnectionStrings:ReportingDatabase"] = _connection,
            ["ConnectionStrings:IntegrationDatabase"] = _connection,

            ["Authentication:Authority"] = Issuer,
            ["Authentication:Audience"] = Audience,
            ["Posting:Enabled"] = "true",
            ["Messaging:Enabled"] = "false",
            ["Logging:LogLevel:Default"] = "Warning"
        }));
        // Keep real JWT signature/issuer/audience/lifetime checks; replace only discovery.
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IIntegrationOperatorClient>(Operators);
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
            var configuration = new OpenIdConnectConfiguration { Issuer = Issuer };
            configuration.SigningKeys.Add(new RsaSecurityKey(_rsa) { KeyId = "integration-test-key" });
            options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
            });
        });
    }

    public async Task InitializeAsync()
    {
        _adminConnection = Environment.GetEnvironmentVariable("VAT_TEST_POSTGRES")
            ?? throw new InvalidOperationException("Set VAT_TEST_POSTGRES to a disposable PostgreSQL server with CREATEDB privileges, or run scripts/Test-Local.ps1.");
        var settings = new NpgsqlConnectionStringBuilder(_adminConnection) { Database = _database, Pooling = false };
        _connection = settings.ConnectionString;
        await using (var admin = new NpgsqlConnection(_adminConnection))
        {
            await admin.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{_database}\"", admin);
            await command.ExecuteNonQueryAsync();
        }
        using var scope = Services.CreateScope();
        var effectiveConnections = scope.ServiceProvider.GetRequiredService<IConfiguration>()
            .GetSection("ConnectionStrings").GetChildren().ToArray();
        Assert.NotEmpty(effectiveConnections);
        Assert.All(effectiveConnections, entry => Assert.Equal(_connection, entry.Value));

        var db = scope.ServiceProvider.GetRequiredService<IntegrationDbContext>();
        await db.Database.MigrateAsync();
    }

    public HttpClient Client(Guid tenant, Guid company, string actor = "maker", string[]? permissions = null,
        string? issuer = null, string? audience = null, DateTime? expires = null, SecurityKey? signingKey = null)
    {
        var claims = new List<Claim> { new("sub", actor), new("tenant_id", tenant.ToString()), new("company_id", company.ToString()) };
        claims.AddRange((permissions ?? Permissions.All).Select(x => new Claim("permission", x)));
        var token = new JwtSecurityToken(issuer ?? Issuer, audience ?? Audience, claims,
            notBefore: DateTime.UtcNow.AddHours(-2), expires: expires ?? DateTime.UtcNow.AddMinutes(15),
            signingCredentials: new SigningCredentials(signingKey ?? new RsaSecurityKey(_rsa) { KeyId = "integration-test-key" }, SecurityAlgorithms.RsaSha256));
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        return client;
    }

    public async Task<T> Inspect<T>(Func<IntegrationDbContext, Task<T>> read)
    {
        using var scope = Services.CreateScope();
        return await read(scope.ServiceProvider.GetRequiredService<IntegrationDbContext>());
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        _rsa.Dispose();
        if (_adminConnection.Length == 0) return;
        await using var admin = new NpgsqlConnection(_adminConnection);
        await admin.OpenAsync();
        // Database name is generated internally; no user input is interpolated.
        // Factory disposal closes application connections; do not terminate unrelated backend work.
        await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_database}\"", admin) { CommandTimeout = 60 };
        await command.ExecuteNonQueryAsync();
    }
}

public sealed class RecordingOperatorClient : IIntegrationOperatorClient
{
    public System.Collections.Concurrent.ConcurrentQueue<OperatorRequest> Calls { get; } = new();
    public Func<OperatorRequest, OperatorReply> Reply { get; set; } = request =>
        request.Action is MessageOperatorAction.ListOutbox or MessageOperatorAction.ListDeadLetters ? new(200, "[]") : new(200, "{\"status\":\"Pending\"}", true);
    public Task<OperatorReply> Send(OperatorRequest request, CancellationToken ct)
    { Calls.Enqueue(request); return Task.FromResult(Reply(request)); }
}
