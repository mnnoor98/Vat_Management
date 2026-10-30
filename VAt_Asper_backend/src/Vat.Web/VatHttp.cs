using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json.Serialization;
using Vat.Api;
using Vat.Api.Errors;
using Vat.Api.Security;
using Vat.Application.Security;

namespace Vat.Web;

public interface IBackendReadiness
{
    Task<bool> IsReady(CancellationToken ct);
}

public sealed record BackendIdentity(string Name);

public static class VatHttp
{
    public static WebApplicationBuilder AddVatHttp(this WebApplicationBuilder builder, string serviceName)
    {
        builder.Services.AddSingleton(new BackendIdentity(serviceName));
        builder.Services.AddOptions<OidcSettings>().BindConfiguration("Authentication")
            .Validate(settings => Uri.TryCreate(settings.Authority, UriKind.Absolute, out var uri) && uri.Scheme == "https"
                && !string.IsNullOrWhiteSpace(settings.Audience), "Configure an HTTPS Authentication:Authority and Authentication:Audience.")
            .ValidateOnStart();
        builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 1_048_576);
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
            options.SerializerOptions.MaxDepth = 32;
        });
        builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = ctx =>
            ctx.ProblemDetails.Extensions["correlationId"] = ctx.HttpContext.TraceIdentifier);
        builder.Services.AddExceptionHandler<ApiExceptionHandler>();
        builder.Services.AddOpenApi();
        builder.Services.AddControllers().AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
            options.JsonSerializerOptions.MaxDepth = 32;
        });
        builder.Services.Configure<ApiBehaviorOptions>(options => options.InvalidModelStateResponseFactory = context =>
            new BadRequestObjectResult(new ProblemDetails
            {
                Status = 400, Title = "invalid_request", Detail = "The request body or parameters are invalid.",
                Extensions = { ["code"] = "invalid_request", ["correlationId"] = context.HttpContext.TraceIdentifier }
            }));
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<OidcSettings>>((options, settings) =>
            {
                // Every environment validates an external issuer; test hosts replace discovery explicitly.
                var isLocalDev = settings.Value.Authority.Contains("localhost");
                if (isLocalDev)
                {
                    var devKey = new SymmetricSecurityKey("EnterpriseVatSecretDevKeyForLocalTesting2026!#"u8.ToArray());
                    var oidcConfig = new Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectConfiguration { Issuer = "https://localhost:5080" };
                    oidcConfig.SigningKeys.Add(devKey);
                    options.Configuration = oidcConfig;
                    options.RequireHttpsMetadata = false;
                    options.MapInboundClaims = false;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = "https://localhost:5080",
                        ValidateAudience = true,
                        ValidAudience = settings.Value.Audience,
                        ValidateLifetime = true,
                        RequireSignedTokens = true,
                        RequireExpirationTime = true,
                        ClockSkew = TimeSpan.FromSeconds(30),
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = devKey,
                        NameClaimType = "sub"
                    };
                }
                else
                {
                    options.Authority = settings.Value.Authority;
                    options.Audience = settings.Value.Audience;
                    options.RequireHttpsMetadata = true;
                    options.MapInboundClaims = false;
                    options.IncludeErrorDetails = false;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true,
                        ValidateIssuerSigningKey = true, RequireSignedTokens = true, RequireExpirationTime = true,
                        ClockSkew = TimeSpan.FromSeconds(30), NameClaimType = "sub",
                        ValidAlgorithms = [SecurityAlgorithms.RsaSha256, SecurityAlgorithms.RsaSha384, SecurityAlgorithms.RsaSha512,
                            SecurityAlgorithms.RsaSsaPssSha256, SecurityAlgorithms.RsaSsaPssSha384, SecurityAlgorithms.RsaSsaPssSha512,
                            SecurityAlgorithms.EcdsaSha256, SecurityAlgorithms.EcdsaSha384, SecurityAlgorithms.EcdsaSha512]
                    };
                }
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        if (context.Principal is null || !ClaimsActor.TryRead(context.Principal, out _))
                            context.Fail("Required application claims are missing or invalid.");
                        return Task.CompletedTask;
                    }
                };
            });
        builder.Services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
            foreach (var permission in Permissions.All)
                options.AddPolicy(permission, policy => policy.RequireAuthenticatedUser().RequireClaim("permission", permission));
        });
        // Browser clients must explicitly configure their allowed origins.
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
        {
            if (origins.Length > 0) policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
        }));
        return builder;
    }

    public static WebApplication MapVatHttp(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            context.TraceIdentifier = Guid.TryParse(context.Request.Headers["X-Correlation-ID"].ToString(), out var id)
                && id != Guid.Empty ? id.ToString() : Guid.NewGuid().ToString();
            context.Response.Headers["X-Correlation-ID"] = context.TraceIdentifier;
            context.Response.Headers.CacheControl = "no-store";
            using var scope = app.Logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = context.TraceIdentifier });
            await next(context);
        });
        app.UseCors();
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGet("/health/live", (BackendIdentity identity) => Results.Ok(new { status = "Healthy", service = identity.Name })).AllowAnonymous();
        app.MapGet("/health/ready", async (IServiceProvider sp, CancellationToken ct) =>
        {
            var readiness = sp.GetService<IBackendReadiness>();
            if (readiness == null) return Results.Ok(new { status = "Healthy" });
            try
            {
                return await readiness.IsReady(ct) ? Results.Ok(new { status = "Healthy" })
                    : Results.Problem(statusCode: 503, title: "Not ready");
            }
            catch (Exception ex) when (ex is Npgsql.NpgsqlException or InvalidOperationException)
            {
                return Results.Problem(statusCode: 503, title: "Not ready");
            }
        }).AllowAnonymous();
        if (app.Environment.IsDevelopment()) app.MapOpenApi().AllowAnonymous();
        app.MapControllers();
        return app;
    }
}
