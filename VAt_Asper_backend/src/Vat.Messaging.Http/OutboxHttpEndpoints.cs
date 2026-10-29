using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json.Serialization;
using Vat.Messaging.Contracts.V1;

namespace Vat.Messaging.Http;

public sealed record FailOutboxRequestV1(OutboxReceiptV1 Receipt, string ErrorCode);

public static class OutboxHttpEndpoints
{
    public const string Prefix = "/internal/v1/outbox/financial-coordinated";
    public const string Policy = "OutboxServiceOnly";

    public static IServiceCollection AddOutboxHttpServer(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<OutboxHttpServerSettings>().Bind(configuration.GetSection("OutboxServer"))
            .Validate(x => x.IsValid(), "Configure a 256-bit hexadecimal OutboxServer:ServiceKey.").ValidateOnStart();
        services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, OutboxServiceAuthentication>(OutboxServiceAuthentication.SchemeName, _ => { });
        services.AddAuthorization(options => options.AddPolicy(Policy, policy => policy
            .AddAuthenticationSchemes(OutboxServiceAuthentication.SchemeName).RequireAuthenticatedUser()));
        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
            options.SerializerOptions.MaxDepth = 16;
        });
        return services;
    }

    public static WebApplication MapOutboxHttpServer(this WebApplication endpoints)
    {
        // Middleware runs before minimal endpoint body binding, including chunked requests.
        endpoints.Use(async (context, next) =>
        {
            if (!context.Request.Path.StartsWithSegments(Prefix)) { await next(context); return; }
            context.Response.Headers.CacheControl = "no-store";
            var bodyLimit = context.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>();
            if (bodyLimit is { IsReadOnly: false }) bodyLimit.MaxRequestBodySize = 4096;
            if (context.Request.ContentLength > 4096) { context.Response.StatusCode = 413; return; }
            await next(context);
        });
        var group = endpoints.MapGroup(Prefix).RequireAuthorization(Policy);
        group.MapPost("/claim", async (IOutboxLeaseSourceV1 source, CancellationToken ct) =>
        {
            var claim = await source.Claim(ct);
            return claim is null ? Results.NoContent() : Results.Ok(claim);
        });
        group.MapPost("/confirm", async (OutboxReceiptV1 receipt, IOutboxLeaseSourceV1 source, CancellationToken ct) =>
            !Accepts(receipt) ? Results.BadRequest() : await source.Confirm(receipt, ct) ? Results.NoContent() : Results.Conflict());
        group.MapPost("/fail", async (FailOutboxRequestV1 request, IOutboxLeaseSourceV1 source, CancellationToken ct) =>
            !Accepts(request.Receipt) || request.ErrorCode is not ("publisher_rejected" or "broker_unavailable" or "publish_timeout" or "invalid_dispatch_contract")
                ? Results.BadRequest() : await source.Fail(request.Receipt, request.ErrorCode, ct) ? Results.NoContent() : Results.Conflict());
        return endpoints;
    }

    private static bool Accepts(OutboxReceiptV1? receipt) => receipt is not null && receipt.IsWellFormed()
        && receipt.SourceOwner == "financial-coordinated";
}
