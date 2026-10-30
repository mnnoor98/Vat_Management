using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vat.Api.Security;
using Vat.Application.Security;
using Vat.Application;
using Vat.Application.Contracts;

namespace Vat.Coordinated.Purchase.Controllers;

[ApiController]
[Tags("Purchase")]
public sealed class PurchasePostingController : ControllerBase
{
    [HttpGet("/api/v1/companies/{companyId:guid}/purchases/{purchaseId:guid}/reconciliation", Name = "ReconcilePurchase")]
    [Authorize(Policy = Permissions.PurchaseRead)]
    public async Task<IActionResult> Reconciliation(Guid companyId, Guid purchaseId, [FromServices] PostingService service, CancellationToken ct) =>
        Ok(await service.Reconcile(HttpContext.GetActor(), companyId, purchaseId, ct));

    [HttpPost("/api/v1/companies/{companyId:guid}/purchases/{purchaseId:guid}/posting-plans", Name = "PreparePurchasePosting")]
    [Authorize(Policy = Permissions.PurchasePrepare)]
    [ProducesResponseType(typeof(PostingPlanResponse), 201)]
    public async Task<IActionResult> PreparePurchasePosting([FromRoute] Guid companyId, [FromRoute] Guid purchaseId, [FromBody] PreparePostingRequest request, [FromServices] PostingService service, [FromServices] ICommandExecutor commands, CancellationToken ct)
    {
        var ctx = HttpContext;
        return await PurchaseApiCommands.Execute(ctx, commands, companyId, $"purchase.prepare:{purchaseId}", request,
            () => service.Prepare(ctx.GetActor(), companyId, purchaseId, request, ctx.TraceIdentifier, ct), ct);
    }

    [HttpGet("/api/v1/companies/{companyId:guid}/purchases/{purchaseId:guid}/posting-plans/{planId:guid}", Name = "GetPurchasePostingPlan")]
    [Authorize(Policy = Permissions.PurchaseRead)]
    public async Task<IActionResult> GetPurchasePostingPlan([FromRoute] Guid companyId, [FromRoute] Guid purchaseId, [FromRoute] Guid planId, [FromServices] PostingService service, CancellationToken ct)
    {
        var ctx = HttpContext;
        return Ok(await service.GetPlan(ctx.GetActor(), companyId, purchaseId, planId, ct));
    }

    [HttpPost("/api/v1/companies/{companyId:guid}/purchases/{purchaseId:guid}/posting-plans/{planId:guid}/post", Name = "PostPurchase")]
    [Authorize(Policy = Permissions.PurchasePost)]
    [ProducesResponseType(typeof(PurchaseResponse), 200)]
    public async Task<IActionResult> PostPurchase([FromRoute] Guid companyId, [FromRoute] Guid purchaseId, [FromRoute] Guid planId, [FromBody] TransitionRequest request, [FromServices] PostingService service, [FromServices] ICommandExecutor commands, CancellationToken ct)
    {
        var ctx = HttpContext;
        return await PurchaseApiCommands.Execute(ctx, commands, companyId, $"purchase.post:{purchaseId}:{planId}", request,
            () => service.Post(ctx.GetActor(), companyId, purchaseId, planId, request, ctx.TraceIdentifier, ct), ct);
    }

    [HttpPost("/api/v1/companies/{companyId:guid}/purchases/{purchaseId:guid}/posting-plans/{planId:guid}/cancel", Name = "CancelPurchasePosting")]
    [Authorize(Policy = Permissions.PurchasePrepare)]
    [ProducesResponseType(typeof(PurchaseResponse), 200)]
    public async Task<IActionResult> CancelPurchasePosting([FromRoute] Guid companyId, [FromRoute] Guid purchaseId, [FromRoute] Guid planId, [FromBody] CancelPostingRequest request, [FromServices] PostingService service, [FromServices] ICommandExecutor commands, CancellationToken ct)
    {
        var ctx = HttpContext;
        return await PurchaseApiCommands.Execute(ctx, commands, companyId, $"purchase.cancel-posting:{purchaseId}:{planId}", request,
            () => service.Cancel(ctx.GetActor(), companyId, purchaseId, planId, request, ctx.TraceIdentifier, ct), ct);
    }

    [HttpGet("/api/v1/companies/{companyId:guid}/purchases/{purchaseId:guid}/posting-status", Name = "GetPurchasePostingStatus")]
    [Authorize(Policy = Permissions.PurchaseRead)]
    public async Task<IActionResult> GetPurchasePostingStatus([FromRoute] Guid companyId, [FromRoute] Guid purchaseId, [FromServices] PostingService service, CancellationToken ct)
    {
        var ctx = HttpContext;
        return Ok(await service.Status(ctx.GetActor(), companyId, purchaseId, ct));
    }
}
