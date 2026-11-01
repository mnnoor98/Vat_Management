using Npgsql;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vat.Application.Security;
using Vat.Services.Identity.Infrastructure;
using Vat.Services.Identity.Domain;
using Xunit;
namespace Vat.Tests;

[Collection("PostgreSQL integration")]
public sealed class LocalAccountsApiTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    [Fact]
    public async Task LocalAccountCreationPersistsHashScopesGrantsAndRejectsInvalidRequests()
    {
        var identityName = "vat_identity_test_" + Guid.NewGuid().ToString("N");
        var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("VAT_TEST_POSTGRES"));
        await using var databaseAdmin = new NpgsqlConnection(connection.ConnectionString);
        await databaseAdmin.OpenAsync();
        await using (var createDatabase = new NpgsqlCommand($"CREATE DATABASE \"{identityName}\"", databaseAdmin)) await createDatabase.ExecuteNonQueryAsync();
        connection.Database = identityName;
        try
        {
        using var local = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string,string?> { ["Authentication:Authority"] = "https://localhost:5080", ["ConnectionStrings:IdentityDatabase"] = connection.ConnectionString })));
        using (var scope = local.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.MigrateAsync();
        var tenant = Guid.NewGuid(); var company = Guid.NewGuid();
        using var source = factory.Client(tenant, company);
        using var admin = local.CreateClient(); admin.DefaultRequestHeaders.Authorization = source.DefaultRequestHeaders.Authorization;
        var root = $"/api/v1/companies/{company}/identity/users";
        var username = "test" + Guid.NewGuid().ToString("N");
        var request = new { username, password = "Test!Password123", fullName = "Local test user", fullNameBn = "Local test user", email = "test@example.com", phone = "", role = "Maker", roles = new[] { "Maker" }, companyId = company, branchId = Guid.NewGuid(), assignedModuleCodes = new[] { "MOD_PURCHASES" } };
        var created = await admin.PostAsJsonAsync(root, request);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.DoesNotContain("passwordHash", await created.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        using (var scope = local.Services.CreateScope())
        {
            var account = await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Set<LocalAccount>().SingleAsync(u => u.Username == username);
            Assert.NotEqual(request.password, account.PasswordHash); Assert.True(account.PasswordHash.Length > 50);
        }
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync(root, request)).StatusCode);
        var login = await admin.PostAsJsonAsync("/api/v1/auth/login", new { username, password = request.password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var loginBody = await login.Content.ReadAsStringAsync();
        Assert.Contains("purchase.create", loginBody); Assert.Contains("inventory.read", loginBody); Assert.Contains("masterdata.read", loginBody); Assert.DoesNotContain("inventory.manage", loginBody); Assert.DoesNotContain("identity.manage", loginBody); Assert.DoesNotContain("sales.create", loginBody);
        Assert.Equal(HttpStatusCode.Unauthorized, (await admin.PostAsJsonAsync("/api/v1/auth/login", new { username, password = "wrong-password" })).StatusCode);
        using var deniedSource = factory.Client(tenant, company, permissions: [Permissions.IdentityRead]);
        using var denied = local.CreateClient(); denied.DefaultRequestHeaders.Authorization = deniedSource.DefaultRequestHeaders.Authorization;
        Assert.Equal(HttpStatusCode.Forbidden, (await denied.PostAsJsonAsync(root, request)).StatusCode);
        using var foreignSource = factory.Client(Guid.NewGuid(), company);
        using var foreign = local.CreateClient(); foreign.DefaultRequestHeaders.Authorization = foreignSource.DefaultRequestHeaders.Authorization;
        Assert.Equal("[]", await foreign.GetStringAsync(root));
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(root, new { request.username, password = "short", request.fullName, request.email, request.companyId, request.branchId, request.roles, request.assignedModuleCodes })).StatusCode);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var dropDatabase = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{identityName}\" WITH (FORCE)", databaseAdmin);
            await dropDatabase.ExecuteNonQueryAsync();
        }
    }
}
