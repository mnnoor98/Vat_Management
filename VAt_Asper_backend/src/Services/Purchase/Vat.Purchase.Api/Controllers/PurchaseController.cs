using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vat.Api.Security;
using Vat.Application.Security;
using Vat.Application;
using Vat.Application.Contracts;

namespace Vat.Coordinated.Purchase.Controllers;

public static class PurchaseApiCommands
{
    public static async Task<IActionResult> Execute(HttpContext context, ICommandExecutor commands,
        Guid companyId, string operation, object request, Func<Task<CommandOutcome>> action, CancellationToken ct)
    {
        var response = await commands.Execute(context.GetActor(), companyId, operation,
            context.Request.Headers["Idempotency-Key"].ToString(), request, action, ct);
        if (response.Location is not null) context.Response.Headers.Location = response.Location;
        context.Response.Headers["Idempotency-Replayed"] = response.Replayed ? "true" : "false";
        return new ContentResult
        {
            StatusCode = response.StatusCode,
            ContentType = "application/json",
            Content = response.Json
        };
    }
}

[ApiController]
[Tags("Purchase")]
public sealed class PurchaseController : ControllerBase
{
    [HttpPost("/api/v1/companies/{companyId:guid}/purchases", Name = "CreatePurchase")]
    [Authorize(Policy = Permissions.PurchaseCreate)]
    [ProducesResponseType(typeof(PurchaseResponse), 201)]
    public async Task<IActionResult> CreatePurchase([FromRoute] Guid companyId, [FromBody] CreatePurchaseRequest request, [FromServices] PurchaseService service, [FromServices] ICommandExecutor commands, CancellationToken ct)
    {
        var ctx = HttpContext;
        return await PurchaseApiCommands.Execute(ctx, commands, companyId, "purchase.create", request,
            () => service.Create(ctx.GetActor(), companyId, request, ctx.TraceIdentifier, ct), ct);
    }

    [HttpGet("/api/v1/companies/{companyId:guid}/purchases", Name = "ListPurchases")]
    [Authorize(Policy = Permissions.PurchaseRead)]
    public async Task<IActionResult> ListPurchases([FromRoute] Guid companyId, [FromQuery] Guid? before, [FromQuery] int? take, [FromServices] PurchaseService service, CancellationToken ct)
    {
        var ctx = HttpContext;
        return Ok(await service.List(ctx.GetActor(), companyId, before, take ?? 25, ct));
    }

    [HttpGet("/api/v1/companies/{companyId:guid}/purchases/{id:guid}", Name = "GetPurchase")]
    [Authorize(Policy = Permissions.PurchaseRead)]
    public async Task<IActionResult> GetPurchase([FromRoute] Guid companyId, [FromRoute] Guid id, [FromServices] PurchaseService service, CancellationToken ct)
    {
        var ctx = HttpContext;
        return Ok(await service.Get(ctx.GetActor(), companyId, id, ct));
    }

    [HttpPost("/api/v1/companies/{companyId:guid}/purchases/{id:guid}/submit", Name = "SubmitPurchase")]
    [Authorize(Policy = Permissions.PurchaseSubmit)]
    [ProducesResponseType(typeof(PurchaseResponse), 200)]
    public async Task<IActionResult> SubmitPurchase([FromRoute] Guid companyId, [FromRoute] Guid id, [FromBody] TransitionRequest request, [FromServices] PurchaseService service, [FromServices] ICommandExecutor commands, CancellationToken ct)
    {
        var ctx = HttpContext;
        return await PurchaseApiCommands.Execute(ctx, commands, companyId, $"purchase.submit:{id}", request,
            () => service.Submit(ctx.GetActor(), companyId, id, request, ctx.TraceIdentifier, ct), ct);
    }

    [HttpPost("/api/v1/companies/{companyId:guid}/purchases/{id:guid}/approve", Name = "ApprovePurchase")]
    [Authorize(Policy = Permissions.PurchaseApprove)]
    [ProducesResponseType(typeof(PurchaseResponse), 200)]
    public async Task<IActionResult> ApprovePurchase([FromRoute] Guid companyId, [FromRoute] Guid id, [FromBody] TransitionRequest request, [FromServices] PurchaseService service, [FromServices] ICommandExecutor commands, CancellationToken ct)
    {
        var ctx = HttpContext;
        return await PurchaseApiCommands.Execute(ctx, commands, companyId, $"purchase.approve:{id}", request,
            () => service.Approve(ctx.GetActor(), companyId, id, request, ctx.TraceIdentifier, ct), ct);
    }

    [HttpPost("/api/v1/companies/{companyId:guid}/purchases/{id:guid}/reject", Name = "RejectPurchase")]
    [Authorize(Policy = Permissions.PurchaseApprove)]
    [ProducesResponseType(typeof(PurchaseResponse), 200)]
    public async Task<IActionResult> RejectPurchase([FromRoute] Guid companyId, [FromRoute] Guid id, [FromBody] RejectPurchaseRequest request, [FromServices] PurchaseService service, [FromServices] ICommandExecutor commands, CancellationToken ct)
    {
        var ctx = HttpContext;
        return await PurchaseApiCommands.Execute(ctx, commands, companyId, $"purchase.reject:{id}", request,
            () => service.Reject(ctx.GetActor(), companyId, id, request, ctx.TraceIdentifier, ct), ct);
    }
}
