using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Vat.Api.Security;
using Vat.Application;
using Vat.Application.Security;
using Vat.Messaging.Contracts.V1;

namespace Vat.Hosting;

// This adapter stays at the data owner: replay effects, audit and stored response share one transaction.
public static class MessageOperatorEndpoints
{
    public static WebApplication MapMessageOperatorV1(this WebApplication app)
    {
        var group = app.MapGroup(MessageOperatorV1.Prefix).WithTags("Message operator V1");
        group.MapGet("/outbox", async (HttpContext context, Guid companyId, Guid? before, int? take,
            IReportingService reports, CancellationToken ct) =>
            Results.Ok(await reports.Outbox(context.GetActor(), companyId, before, take ?? 25, ct)))
            .RequireAuthorization(Permissions.MessagingRead);
        group.MapGet("/dead-letters", async (HttpContext context, Guid companyId, Guid? before, int? take,
            IReportingService reports, CancellationToken ct) =>
            Results.Ok(await reports.DeadLetters(context.GetActor(), companyId, before, take ?? 25, ct)))
            .RequireAuthorization(Permissions.MessagingRead);
        group.MapPost("/outbox/{id:guid}/retry", (HttpContext context, Guid companyId, Guid id, RetryMessageV1 request,
            IReportingService reports, ICommandExecutor commands, CancellationToken ct) =>
            ApiCommands.Execute(context, commands, companyId, $"outbox.retry:{id}", request,
                () => reports.RetryOutbox(context.GetActor(), companyId, id, request.Reason, context.TraceIdentifier, ct), ct))
            .RequireAuthorization(Permissions.MessagingRetry);
        group.MapPost("/dead-letters/{id:guid}/replay", (HttpContext context, Guid companyId, Guid id, RetryMessageV1 request,
            IReportingService reports, ICommandExecutor commands, CancellationToken ct) =>
            ApiCommands.Execute(context, commands, companyId, $"inbox.replay:{id}", request,
                () => reports.Replay(context.GetActor(), companyId, id, request.Reason, context.TraceIdentifier, ct), ct))
            .RequireAuthorization(Permissions.MessagingRetry);
        return app;
    }
}
