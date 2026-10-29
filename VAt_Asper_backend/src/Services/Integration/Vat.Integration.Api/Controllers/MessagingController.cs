using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vat.Api.Security;
using Vat.Application.Security;

namespace Vat.Services.Integration.Controllers;

[ApiController]
[Tags("Integration")]
public sealed class MessagingController(IntegrationOperatorService service) : ControllerBase
{
    [HttpGet("/api/v1/companies/{companyId:guid}/messaging/outbox", Name = "ListOutboxMessages")]
    [Authorize(Policy = Permissions.MessagingRead)]
    public Task<IActionResult> Outbox(Guid companyId, Guid? before, int? take, CancellationToken ct) =>
        Execute(MessageOperatorAction.ListOutbox, companyId, null, before, take ?? 25, null, ct);

    [HttpPost("/api/v1/companies/{companyId:guid}/messaging/outbox/{id:guid}/retry", Name = "RetryOutboxMessage")]
    [Authorize(Policy = Permissions.MessagingRetry)]
    public Task<IActionResult> Retry(Guid companyId, Guid id, OperatorRetryRequest request, CancellationToken ct) =>
        Execute(MessageOperatorAction.RetryOutbox, companyId, id, null, 25, request.Reason, ct);

    [HttpGet("/api/v1/companies/{companyId:guid}/messaging/dead-letters", Name = "ListInboxDeadLetters")]
    [Authorize(Policy = Permissions.MessagingRead)]
    public Task<IActionResult> DeadLetters(Guid companyId, Guid? before, int? take, CancellationToken ct) =>
        Execute(MessageOperatorAction.ListDeadLetters, companyId, null, before, take ?? 25, null, ct);

    [HttpPost("/api/v1/companies/{companyId:guid}/messaging/dead-letters/{id:guid}/replay", Name = "ReplayInboxDeadLetter")]
    [Authorize(Policy = Permissions.MessagingRetry)]
    public Task<IActionResult> Replay(Guid companyId, Guid id, OperatorRetryRequest request, CancellationToken ct) =>
        Execute(MessageOperatorAction.ReplayDeadLetter, companyId, id, null, 25, request.Reason, ct);

    private async Task<IActionResult> Execute(MessageOperatorAction action, Guid companyId, Guid? id, Guid? before, int take, string? reason, CancellationToken ct)
    {
        var result = await service.Execute(HttpContext.GetActor(), new(action, companyId, id, before, take, reason,
            Request.Headers.Authorization.ToString(), Request.Headers["Idempotency-Key"].ToString(), HttpContext.TraceIdentifier), ct);
        if (result.Replayed is { } replayed) Response.Headers["Idempotency-Replayed"] = replayed ? "true" : "false";
        return new ContentResult { StatusCode = result.StatusCode,
            ContentType = result.StatusCode >= 400 ? "application/problem+json" : "application/json", Content = result.Json };
    }
}
