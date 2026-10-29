using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vat.Api.Security;
using Vat.Application.Security;
using Vat.Services.Sales;
using Vat.Services.Sales.Contracts;

namespace Vat.Services.Sales.Controllers;

[ApiController]
[Tags("Sales")]
public sealed class PaymentController : ControllerBase
{
    [HttpPost("/api/v1/companies/{companyId:guid}/payments")]
    [Authorize(Policy = Permissions.PaymentRequest)]
    [ProducesResponseType(typeof(PaymentResponse), 201)]
    public async Task<IActionResult> Create([FromRoute] Guid companyId, [FromBody] CreatePaymentRequest request, [FromServices] PaymentService service, [FromServices] ICommandExecutor commands, CancellationToken ct)
    {
        var ctx = HttpContext;
        return await SalesApiCommands.Execute(ctx, commands, companyId, "payment.create", request,
            () => service.Create(ctx.GetActor(), companyId, request, ctx.TraceIdentifier, ct), ct);
    }

    [HttpGet("/api/v1/companies/{companyId:guid}/payments")]
    [Authorize(Policy = Permissions.PaymentRead)]
    public async Task<IActionResult> List([FromRoute] Guid companyId, [FromQuery] Guid? customerId, [FromQuery] Guid? before, [FromQuery] int? take, [FromServices] PaymentService service, CancellationToken ct)
    {
        var ctx = HttpContext;
        return Ok(await service.List(ctx.GetActor(), companyId, customerId, before, take ?? 25, ct));
    }

    [HttpGet("/api/v1/companies/{companyId:guid}/payments/{id:guid}")]
    [Authorize(Policy = Permissions.PaymentRead)]
    public async Task<IActionResult> Get([FromRoute] Guid companyId, [FromRoute] Guid id, [FromServices] PaymentService service, CancellationToken ct)
    {
        var ctx = HttpContext;
        return Ok(await service.Get(ctx.GetActor(), companyId, id, ct));
    }

    [HttpPost("/api/v1/companies/{companyId:guid}/payments/{id:guid}/post")]
    [Authorize(Policy = Permissions.PaymentApprove)]
    public async Task<IActionResult> Post([FromRoute] Guid companyId, [FromRoute] Guid id, [FromBody] TransitionRequest request, [FromServices] PaymentService service, [FromServices] ICommandExecutor commands, CancellationToken ct)
    {
        var ctx = HttpContext;
        return await SalesApiCommands.Execute(ctx, commands, companyId, $"payment.post:{id}", request,
            () => service.Approve(ctx.GetActor(), companyId, id, request, ctx.TraceIdentifier, ct), ct);
    }

    [HttpPost("/api/v1/companies/{companyId:guid}/payments/{id:guid}/allocate")]
    [Authorize(Policy = Permissions.PaymentRequest)]
    public async Task<IActionResult> Allocate([FromRoute] Guid companyId, [FromRoute] Guid id, [FromBody] AllocatePaymentRequest request, [FromServices] PaymentService service, [FromServices] ICommandExecutor commands, CancellationToken ct)
    {
        var ctx = HttpContext;
        return await SalesApiCommands.Execute(ctx, commands, companyId, $"payment.allocate:{id}", request,
            () => service.Allocate(ctx.GetActor(), companyId, id, request, ctx.TraceIdentifier, ct), ct);
    }

    [HttpPost("/api/v1/companies/{companyId:guid}/payments/{id:guid}/cancel")]
    [Authorize(Policy = Permissions.PaymentRequest)]
    public async Task<IActionResult> Cancel([FromRoute] Guid companyId, [FromRoute] Guid id, [FromBody] CancelSalesInvoiceRequest request, [FromServices] PaymentService service, [FromServices] ICommandExecutor commands, CancellationToken ct)
    {
        var ctx = HttpContext;
        return await SalesApiCommands.Execute(ctx, commands, companyId, $"payment.cancel:{id}", request,
            () => service.Cancel(ctx.GetActor(), companyId, id, request, ctx.TraceIdentifier, ct), ct);
    }

    [HttpGet("/api/v1/companies/{companyId:guid}/customers/{customerId:guid}/receivables")]
    [Authorize(Policy = Permissions.PaymentRead)]
    public async Task<IActionResult> Receivables([FromRoute] Guid companyId, [FromRoute] Guid customerId, [FromServices] PaymentService service, CancellationToken ct)
    {
        var ctx = HttpContext;
        return Ok(await service.Receivables(ctx.GetActor(), companyId, customerId, ct));
    }
}
