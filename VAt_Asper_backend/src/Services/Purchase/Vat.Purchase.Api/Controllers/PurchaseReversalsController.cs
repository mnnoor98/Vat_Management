using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vat.Api.Security;
using Vat.Application.Security;
using Vat.Application;
using Vat.Application.Contracts;

namespace Vat.Coordinated.Purchase.Controllers;

[ApiController]
[Tags("Purchase")]
public sealed class PurchaseReversalsController : ControllerBase
{
    [HttpPost("/api/v1/companies/{companyId:guid}/purchases/{purchaseId:guid}/reversals", Name = "RequestPurchaseReversal")]
    [Authorize(Policy = Permissions.PurchaseReverseRequest)]
    [ProducesResponseType(typeof(ReversalResponse), 201)]
    public async Task<IActionResult> RequestPurchaseReversal([FromRoute] Guid companyId, [FromRoute] Guid purchaseId, [FromBody] RequestReversalRequest request, [FromServices] ReversalService service, [FromServices] ICommandExecutor commands, CancellationToken ct)
    {
        var ctx = HttpContext;
        return await PurchaseApiCommands.Execute(ctx, commands, companyId, $"reversal.request:{purchaseId}", request,
            () => service.Request(ctx.GetActor(), companyId, purchaseId, request, ctx.TraceIdentifier, ct), ct);
    }

    [HttpGet("/api/v1/companies/{companyId:guid}/purchases/{purchaseId:guid}/reversals/{id:guid}", Name = "GetPurchaseReversal")]
    [Authorize(Policy = Permissions.PurchaseRead)]
    public async Task<IActionResult> GetPurchaseReversal([FromRoute] Guid companyId, [FromRoute] Guid purchaseId, [FromRoute] Guid id, [FromServices] ReversalService service, CancellationToken ct)
    {
        var ctx = HttpContext;
        return Ok(await service.Get(ctx.GetActor(), companyId, purchaseId, id, ct));
    }

    [HttpPost("/api/v1/companies/{companyId:guid}/purchases/{purchaseId:guid}/reversals/{id:guid}/approve", Name = "ApprovePurchaseReversal")]
    [Authorize(Policy = Permissions.PurchaseReverseApprove)]
    [ProducesResponseType(typeof(PurchaseResponse), 200)]
    public async Task<IActionResult> ApprovePurchaseReversal([FromRoute] Guid companyId, [FromRoute] Guid purchaseId, [FromRoute] Guid id, [FromBody] TransitionRequest request, [FromServices] ReversalService service, [FromServices] ICommandExecutor commands, CancellationToken ct)
    {
        var ctx = HttpContext;
        return await PurchaseApiCommands.Execute(ctx, commands, companyId, $"reversal.approve:{purchaseId}:{id}", request,
            () => service.Authorize(ctx.GetActor(), companyId, purchaseId, id, request, ctx.TraceIdentifier, ct), ct);
    }

    [HttpPost("/api/v1/companies/{companyId:guid}/purchases/{purchaseId:guid}/reversals/{id:guid}/cancel", Name = "CancelPurchaseReversal")]
    [Authorize(Policy = Permissions.PurchaseReverseRequest)]
    [ProducesResponseType(typeof(PurchaseResponse), 200)]
    public async Task<IActionResult> CancelPurchaseReversal([FromRoute] Guid companyId, [FromRoute] Guid purchaseId, [FromRoute] Guid id, [FromBody] CancelPostingRequest request, [FromServices] ReversalService service, [FromServices] ICommandExecutor commands, CancellationToken ct)
    {
        var ctx = HttpContext;
        return await PurchaseApiCommands.Execute(ctx, commands, companyId, $"reversal.cancel:{purchaseId}:{id}", request,
            () => service.Cancel(ctx.GetActor(), companyId, purchaseId, id, request, ctx.TraceIdentifier, ct), ct);
    }

    [HttpPost("/api/v1/companies/{companyId:guid}/purchases/{purchaseId:guid}/cancel-unposted", Name = "CancelUnpostedPurchase")]
    [Authorize(Policy = Permissions.PurchaseCancel)]
    [ProducesResponseType(typeof(PurchaseResponse), 200)]
    public async Task<IActionResult> CancelUnpostedPurchase([FromRoute] Guid companyId, [FromRoute] Guid purchaseId, [FromBody] CancelPostingRequest request, [FromServices] ReversalService service, [FromServices] ICommandExecutor commands, CancellationToken ct)
    {
        var ctx = HttpContext;
        return await PurchaseApiCommands.Execute(ctx, commands, companyId, $"purchase.cancel-unposted:{purchaseId}", request,
            () => service.CancelUnposted(ctx.GetActor(), companyId, purchaseId, request, ctx.TraceIdentifier, ct), ct);
    }
}
