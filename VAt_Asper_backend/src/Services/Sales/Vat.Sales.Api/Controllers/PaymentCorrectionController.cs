using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vat.Api.Security;
using Vat.Application.Security;
using Vat.Services.Sales;
using Vat.Services.Sales.Contracts;

namespace Vat.Services.Sales.Controllers;

[ApiController]
[Tags("Sales")]
public sealed class PaymentCorrectionController : ControllerBase
{
    [HttpPost("/api/v1/companies/{companyId:guid}/payments/{paymentId:guid}/reversals")]
    [Authorize(Policy = Permissions.PaymentReverseRequest)]
    [ProducesResponseType(typeof(PaymentCorrectionResponse), 201)]
    public async Task<IActionResult> RequestReversal([FromRoute] Guid companyId, [FromRoute] Guid paymentId, [FromBody] RequestReceiptReversal request, [FromServices] PaymentCorrectionService service, [FromServices] ICommandExecutor commands, CancellationToken ct)
    {
        var ctx = HttpContext;
        return await SalesApiCommands.Execute(ctx, commands, companyId, $"payment.reversal.request:{paymentId}", request,
            () => service.Request(ctx.GetActor(), companyId, paymentId, request.ExpectedVersion, request.CorrectionDate, "Reversal", null, null, request.Reason, request.EvidenceReference, null, ctx.TraceIdentifier, ct), ct);
    }

    [HttpPost("/api/v1/companies/{companyId:guid}/payments/{paymentId:guid}/refunds")]
    [Authorize(Policy = Permissions.PaymentRefundRequest)]
    [ProducesResponseType(typeof(PaymentCorrectionResponse), 201)]
    public async Task<IActionResult> RequestRefund([FromRoute] Guid companyId, [FromRoute] Guid paymentId, [FromBody] RequestCustomerRefund request, [FromServices] PaymentCorrectionService service, [FromServices] ICommandExecutor commands, CancellationToken ct)
    {
        var ctx = HttpContext;
        return await SalesApiCommands.Execute(ctx, commands, companyId, $"payment.refund.request:{paymentId}", request,
            () => service.Request(ctx.GetActor(), companyId, paymentId, request.ExpectedVersion, request.CorrectionDate, "Refund", request.Amount, request.Method, request.Reason, request.EvidenceReference, request.Releases, ctx.TraceIdentifier, ct), ct);
    }

    [HttpPost("/api/v1/companies/{companyId:guid}/payments/{paymentId:guid}/reversals/{id:guid}/approve")]
    [Authorize(Policy = Permissions.PaymentReverseApprove)]
    [ProducesResponseType(typeof(PaymentCorrectionResponse), 200)]
    public async Task<IActionResult> ApproveReversal([FromRoute] Guid companyId, [FromRoute] Guid paymentId, [FromRoute] Guid id, [FromBody] TransitionRequest request, [FromServices] PaymentCorrectionService service, [FromServices] ICommandExecutor commands, CancellationToken ct)
    {
        var ctx = HttpContext;
        return await SalesApiCommands.Execute(ctx, commands, companyId, $"payment.reversals.approve:{paymentId}:{id}", request,
            () => service.Approve(ctx.GetActor(), companyId, paymentId, id, "Reversal", request, ctx.TraceIdentifier, ct), ct);
    }

    [HttpPost("/api/v1/companies/{companyId:guid}/payments/{paymentId:guid}/reversals/{id:guid}/cancel")]
    [Authorize(Policy = Permissions.PaymentReverseRequest)]
    [ProducesResponseType(typeof(PaymentCorrectionResponse), 200)]
    public async Task<IActionResult> CancelReversal([FromRoute] Guid companyId, [FromRoute] Guid paymentId, [FromRoute] Guid id, [FromBody] CancelSalesInvoiceRequest request, [FromServices] PaymentCorrectionService service, [FromServices] ICommandExecutor commands, CancellationToken ct)
    {
        var ctx = HttpContext;
        return await SalesApiCommands.Execute(ctx, commands, companyId, $"payment.reversals.cancel:{paymentId}:{id}", request,
            () => service.Cancel(ctx.GetActor(), companyId, paymentId, id, "Reversal", request, ctx.TraceIdentifier, ct), ct);
    }

    [HttpPost("/api/v1/companies/{companyId:guid}/payments/{paymentId:guid}/refunds/{id:guid}/approve")]
    [Authorize(Policy = Permissions.PaymentRefundApprove)]
    [ProducesResponseType(typeof(PaymentCorrectionResponse), 200)]
    public async Task<IActionResult> ApproveRefund([FromRoute] Guid companyId, [FromRoute] Guid paymentId, [FromRoute] Guid id, [FromBody] TransitionRequest request, [FromServices] PaymentCorrectionService service, [FromServices] ICommandExecutor commands, CancellationToken ct)
    {
        var ctx = HttpContext;
        return await SalesApiCommands.Execute(ctx, commands, companyId, $"payment.refunds.approve:{paymentId}:{id}", request,
            () => service.Approve(ctx.GetActor(), companyId, paymentId, id, "Refund", request, ctx.TraceIdentifier, ct), ct);
    }

    [HttpPost("/api/v1/companies/{companyId:guid}/payments/{paymentId:guid}/refunds/{id:guid}/cancel")]
    [Authorize(Policy = Permissions.PaymentRefundRequest)]
    [ProducesResponseType(typeof(PaymentCorrectionResponse), 200)]
    public async Task<IActionResult> CancelRefund([FromRoute] Guid companyId, [FromRoute] Guid paymentId, [FromRoute] Guid id, [FromBody] CancelSalesInvoiceRequest request, [FromServices] PaymentCorrectionService service, [FromServices] ICommandExecutor commands, CancellationToken ct)
    {
        var ctx = HttpContext;
        return await SalesApiCommands.Execute(ctx, commands, companyId, $"payment.refunds.cancel:{paymentId}:{id}", request,
            () => service.Cancel(ctx.GetActor(), companyId, paymentId, id, "Refund", request, ctx.TraceIdentifier, ct), ct);
    }

    [HttpGet("/api/v1/companies/{companyId:guid}/payments/{paymentId:guid}/corrections")]
    [Authorize(Policy = Permissions.PaymentRead)]
    public async Task<IActionResult> List([FromRoute] Guid companyId, [FromRoute] Guid paymentId, [FromServices] PaymentCorrectionService service, CancellationToken ct)
    {
        var ctx = HttpContext;
        return Ok(await service.List(ctx.GetActor(), companyId, paymentId, ct));
    }

    [HttpGet("/api/v1/companies/{companyId:guid}/payments/{paymentId:guid}/corrections/{id:guid}")]
    [Authorize(Policy = Permissions.PaymentRead)]
    public async Task<IActionResult> Get([FromRoute] Guid companyId, [FromRoute] Guid paymentId, [FromRoute] Guid id, [FromServices] PaymentCorrectionService service, CancellationToken ct)
    {
        var ctx = HttpContext;
        return Ok(await service.Get(ctx.GetActor(), companyId, paymentId, id, ct));
    }
}
