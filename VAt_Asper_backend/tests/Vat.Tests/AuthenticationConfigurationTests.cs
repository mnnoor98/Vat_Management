using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Vat.Web;
using Xunit;

namespace Vat.Tests;

public sealed class AuthenticationConfigurationTests
{
    [Theory]
    [InlineData("Production", "https://localhost:5080")]
    [InlineData("Production", "https://localhost.attacker.example")]
    [InlineData("Development", "https://localhost:5080")]
    public async Task Localhost_authority_never_installs_an_embedded_key_or_accepts_symmetric_tokens(string environment, string authority)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        { ["Authentication:Authority"] = authority, ["Authentication:Audience"] = "auth-regression", ["Logging:LogLevel:Default"] = "None" });
        builder.AddVatHttp("AuthRegression");
        await using var app = builder.Build();
        var options = app.Services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get(JwtBearerDefaults.AuthenticationScheme);
        Assert.Equal(authority, options.Authority); Assert.True(options.RequireHttpsMetadata);
        Assert.Null(options.Configuration); Assert.Null(options.TokenValidationParameters.IssuerSigningKey);
        Assert.DoesNotContain(SecurityAlgorithms.HmacSha256, options.TokenValidationParameters.ValidAlgorithms);
        using var rsa = RSA.Create(2048); var trusted = new RsaSecurityKey(rsa) { KeyId = "trusted-issuer" };
        var validation = options.TokenValidationParameters.Clone();
        validation.ValidIssuer = authority; validation.ValidAudience = "auth-regression"; validation.IssuerSigningKey = trusted;
        var handler = new JwtSecurityTokenHandler();
        string Token(SecurityKey key, string algorithm) => handler.WriteToken(new JwtSecurityToken(authority, "auth-regression",
            [new Claim("sub", "operator"), new Claim("tenant_id", Guid.NewGuid().ToString())], DateTime.UtcNow.AddMinutes(-1),
            DateTime.UtcNow.AddMinutes(5), new SigningCredentials(key, algorithm)));
        var valid = Token(trusted, SecurityAlgorithms.RsaSha256);
        Assert.NotNull(handler.ValidateToken(valid, validation, out _));
        var forged = Token(new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(64)), SecurityAlgorithms.HmacSha256);
        Assert.ThrowsAny<SecurityTokenException>(() => handler.ValidateToken(forged, validation, out _));
    }
}
