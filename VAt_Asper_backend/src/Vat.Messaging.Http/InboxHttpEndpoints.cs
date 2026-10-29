using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json.Serialization;
using Vat.Messaging.Contracts.V1;

namespace Vat.Messaging.Http;

public static class InboxHttpEndpoints
{
    public const string Prefix = "/internal/v1/inbox/reporting-v1";
    public const string Policy = "InboxServiceOnly";
    public const int MaxRequestBytes = 2 * 1024 * 1024;

    public static IServiceCollection AddInboxHttpServer(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<InboxHttpServerSettings>().Bind(configuration.GetSection("InboxServer"))
            .Validate(x => x.IsValid(), "Configure a 256-bit hexadecimal InboxServer:ServiceKey.").ValidateOnStart();
        services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, InboxServiceAuthentication>(InboxServiceAuthentication.SchemeName, _ => { });
        services.AddAuthorization(options => options.AddPolicy(Policy, policy => policy
            .AddAuthenticationSchemes(InboxServiceAuthentication.SchemeName).RequireAuthenticatedUser()));
        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
            options.SerializerOptions.MaxDepth = 16;
        });
        return services;
    }

    public static WebApplication MapInboxHttpServer(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            if (!context.Request.Path.StartsWithSegments(Prefix)) { await next(context); return; }
            context.Response.Headers.CacheControl = "no-store";
            var limit = context.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>();
            if (limit is { IsReadOnly: false }) limit.MaxRequestBodySize = MaxRequestBytes;
            if (context.Request.ContentLength > MaxRequestBytes) { context.Response.StatusCode = 413; return; }
            await next(context);
        });
        app.MapPost(Prefix + "/deliver", async (InboxDeliveryV1 delivery, IInboxDeliveryTargetV1 target, CancellationToken ct) =>
        {
            if (!delivery.IsValid()) return Results.BadRequest();
            var receipt = await target.Deliver(delivery, ct);
            return receipt is not null && receipt.IsCommittedFor(delivery)
                ? Results.Ok(receipt) : Results.Problem(statusCode: 503, title: "Inbox commit unavailable");
        }).RequireAuthorization(Policy);
        return app;
    }
}
