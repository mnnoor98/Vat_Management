using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vat.Api.Security;
using Vat.Application;
using Vat.Application.Contracts;
using Vat.Application.Security;
using Vat.Hosting;

namespace Vat.Api.Compatibility;

[ApiController]
[Tags("Integration")]
public sealed class CompatibilityMessagingController : VatControllerBase
{
    [HttpGet("/api/v1/companies/{companyId:guid}/messaging/outbox", Name = "ListOutboxMessages")]
    [Authorize(Policy = Permissions.MessagingRead)]
    public async Task<IActionResult> ListOutboxMessages([FromRoute] Guid companyId, [FromQuery] Guid? before, [FromQuery] int? take, [FromServices] IReportingService service, CancellationToken ct)
    {
        var ctx = HttpContext;
        return Respond(await service.Outbox(ctx.GetActor(), companyId, before, take ?? 25, ct));
    }

    [HttpPost("/api/v1/companies/{companyId:guid}/messaging/outbox/{id:guid}/retry", Name = "RetryOutboxMessage")]
    [Authorize(Policy = Permissions.MessagingRetry)]
    public async Task<IActionResult> RetryOutboxMessage([FromRoute] Guid companyId, [FromRoute] Guid id, [FromBody] RetryMessageRequest request, [FromServices] IReportingService service, [FromServices] ICommandExecutor commands, CancellationToken ct)
    {
        var ctx = HttpContext;
        return Respond(await ApiCommands.Execute(ctx, commands, companyId, $"outbox.retry:{id}", request,
                () => service.RetryOutbox(ctx.GetActor(), companyId, id, request.Reason, ctx.TraceIdentifier, ct), ct));
    }

    [HttpGet("/api/v1/companies/{companyId:guid}/messaging/dead-letters", Name = "ListInboxDeadLetters")]
    [Authorize(Policy = Permissions.MessagingRead)]
    public async Task<IActionResult> ListInboxDeadLetters([FromRoute] Guid companyId, [FromQuery] Guid? before, [FromQuery] int? take, [FromServices] IReportingService service, CancellationToken ct)
    {
        var ctx = HttpContext;
        return Respond(await service.DeadLetters(ctx.GetActor(), companyId, before, take ?? 25, ct));
    }

    [HttpPost("/api/v1/companies/{companyId:guid}/messaging/dead-letters/{id:guid}/replay", Name = "ReplayInboxDeadLetter")]
    [Authorize(Policy = Permissions.MessagingRetry)]
    [ProducesResponseType(typeof(ProcessedMessage), 200)]
    public async Task<IActionResult> ReplayInboxDeadLetter([FromRoute] Guid companyId, [FromRoute] Guid id, [FromBody] RetryMessageRequest request, [FromServices] IReportingService service, [FromServices] ICommandExecutor commands, CancellationToken ct)
    {
        var ctx = HttpContext;
        return Respond(await ApiCommands.Execute(ctx, commands, companyId, $"inbox.replay:{id}", request,
                () => service.Replay(ctx.GetActor(), companyId, id, request.Reason, ctx.TraceIdentifier, ct), ct));
    }
}
