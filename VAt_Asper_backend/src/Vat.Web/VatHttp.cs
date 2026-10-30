using Microsoft.Extensions.DependencyInjection.Extensions;
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
using Scalar.AspNetCore;
using Swashbuckle.AspNetCore.SwaggerUI;
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
        builder.Services.TryAddSingleton(TimeProvider.System);
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
        builder.Services.AddEndpointsApiExplorer();
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
        
        // Browser clients and Swagger UI cross-origin calls
        builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
        {
            if (builder.Environment.IsDevelopment())
            {
                policy.SetIsOriginAllowed(_ => true)
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials();
            }
            else
            {
                var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
                if (origins.Length > 0) policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
            }
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

        if (app.Environment.IsDevelopment())
        {
            app.UseSwaggerUI(options =>
            {
                var serviceName = app.Services.GetService<BackendIdentity>()?.Name ?? "VAT";
                if (serviceName is "Compatibility" or "Gateway")
                {
                    options.SwaggerEndpoint("/openapi/v1.json", "Unified API Gateway (Port 5080)");
                    options.SwaggerEndpoint("http://localhost:5101/openapi/v1.json", "Identity Service (Port 5101)");
                    options.SwaggerEndpoint("http://localhost:5102/openapi/v1.json", "Company Service (Port 5102)");
                    options.SwaggerEndpoint("http://localhost:5103/openapi/v1.json", "MasterData Service (Port 5103)");
                    options.SwaggerEndpoint("http://localhost:5104/openapi/v1.json", "Purchase Service (Port 5104)");
                    options.SwaggerEndpoint("http://localhost:5105/openapi/v1.json", "Sales Service (Port 5105)");
                    options.SwaggerEndpoint("http://localhost:5106/openapi/v1.json", "Inventory Service (Port 5106)");
                    options.SwaggerEndpoint("http://localhost:5107/openapi/v1.json", "Manufacturing Service (Port 5107)");
                    options.SwaggerEndpoint("http://localhost:5108/openapi/v1.json", "VatEngine Service (Port 5108)");
                    options.SwaggerEndpoint("http://localhost:5109/openapi/v1.json", "Compliance Service (Port 5109)");
                    options.SwaggerEndpoint("http://localhost:5110/openapi/v1.json", "Workflow Service (Port 5110)");
                    options.SwaggerEndpoint("http://localhost:5111/openapi/v1.json", "Reporting Service (Port 5111)");
                    options.SwaggerEndpoint("http://localhost:5112/openapi/v1.json", "Integration Service (Port 5112)");
                    options.DocumentTitle = "Enterprise VAT Management - API Portal";
                }
                else
                {
                    options.SwaggerEndpoint("/openapi/v1.json", $"{serviceName} API v1");
                    options.DocumentTitle = $"{serviceName} - Swagger UI";
                }
                options.RoutePrefix = "swagger";
            });
        }

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

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi().AllowAnonymous();
            app.MapScalarApiReference(options =>
            {
                var serviceName = app.Services.GetService<BackendIdentity>()?.Name ?? "VAT";
                options.WithTitle($"{serviceName} API Documentation");
            }).AllowAnonymous();
            app.MapGet("/", () => Results.Redirect("/swagger")).AllowAnonymous().ExcludeFromDescription();
        }

        app.MapControllers();
        return app;
    }
}
