using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vat.Api.Security;
using Vat.Application.Security;
using Vat.Services.Sales;
using Vat.Services.Sales.Contracts;

namespace Vat.Services.Sales.Controllers;

[ApiController]
[Tags("Sales")]
public sealed class SalesCreditController : ControllerBase
{
    [HttpGet("/api/v1/companies/{companyId:guid}/sales/{invoiceId:guid}/returnable-lines")]
    [Authorize(Policy = Permissions.SalesInvoiceRead)]
    public async Task<IActionResult> ReturnableLines([FromRoute] Guid companyId, [FromRoute] Guid invoiceId, [FromServices] SalesCreditService service, CancellationToken ct)
    {
        var ctx = HttpContext;
        return Ok(await service.Returnable(ctx.GetActor(), companyId, invoiceId, ct));
    }

    [HttpPost("/api/v1/companies/{companyId:guid}/sales/{invoiceId:guid}/price-credits")]
    [Authorize(Policy = Permissions.SalesCreditRequest)]
    [ProducesResponseType(typeof(SalesCreditResponse), 201)]
    public async Task<IActionResult> RequestPriceCredit([FromRoute] Guid companyId, [FromRoute] Guid invoiceId, [FromBody] RequestSalesPriceCredit request, [FromServices] SalesCreditService service, [FromServices] ICommandExecutor commands, CancellationToken ct)
    {
        var ctx = HttpContext;
        return await SalesApiCommands.Execute(ctx, commands, companyId, $"sales.price-credit.request:{invoiceId}", request,
            () => service.RequestPrice(ctx.GetActor(), companyId, invoiceId, request, ctx.TraceIdentifier, ct), ct);
    }

    [HttpPost("/api/v1/companies/{companyId:guid}/sales/{invoiceId:guid}/credit-notes")]
    [Authorize(Policy = Permissions.SalesCreditRequest)]
    [ProducesResponseType(typeof(SalesCreditResponse), 201)]
    public async Task<IActionResult> RequestCredit([FromRoute] Guid companyId, [FromRoute] Guid invoiceId, [FromBody] RequestSalesCredit request, [FromServices] SalesCreditService service, [FromServices] ICommandExecutor commands, CancellationToken ct)
    {
        var ctx = HttpContext;
        return await SalesApiCommands.Execute(ctx, commands, companyId, $"sales.credit.request:{invoiceId}", request,
            () => service.Request(ctx.GetActor(), companyId, invoiceId, request, ctx.TraceIdentifier, ct), ct);
    }

    [HttpGet("/api/v1/companies/{companyId:guid}/sales/{invoiceId:guid}/credit-notes")]
    [Authorize(Policy = Permissions.SalesInvoiceRead)]
    public async Task<IActionResult> List([FromRoute] Guid companyId, [FromRoute] Guid invoiceId, [FromQuery] Guid? before, [FromQuery] int? take, [FromServices] SalesCreditService service, CancellationToken ct)
    {
        var ctx = HttpContext;
        return Ok(await service.List(ctx.GetActor(), companyId, invoiceId, before, take ?? 25, ct));
    }

    [HttpGet("/api/v1/companies/{companyId:guid}/sales/{invoiceId:guid}/credit-notes/{id:guid}")]
    [Authorize(Policy = Permissions.SalesInvoiceRead)]
    public async Task<IActionResult> Get([FromRoute] Guid companyId, [FromRoute] Guid invoiceId, [FromRoute] Guid id, [FromServices] SalesCreditService service, CancellationToken ct)
    {
        var ctx = HttpContext;
        return Ok(await service.Get(ctx.GetActor(), companyId, invoiceId, id, ct));
    }

    [HttpPost("/api/v1/companies/{companyId:guid}/sales/{invoiceId:guid}/credit-notes/{id:guid}/approve")]
    [Authorize(Policy = Permissions.SalesCreditApprove)]
    [ProducesResponseType(typeof(SalesCreditResponse), 200)]
    public async Task<IActionResult> Approve([FromRoute] Guid companyId, [FromRoute] Guid invoiceId, [FromRoute] Guid id, [FromBody] TransitionRequest request, [FromServices] SalesCreditService service, [FromServices] ICommandExecutor commands, CancellationToken ct)
    {
        var ctx = HttpContext;
        return await SalesApiCommands.Execute(ctx, commands, companyId, $"sales.credit.approve:{invoiceId}:{id}", request,
            () => service.Approve(ctx.GetActor(), companyId, invoiceId, id, request, ctx.TraceIdentifier, ct), ct);
    }

    [HttpPost("/api/v1/companies/{companyId:guid}/sales/{invoiceId:guid}/credit-notes/{id:guid}/cancel")]
    [Authorize(Policy = Permissions.SalesCreditRequest)]
    [ProducesResponseType(typeof(SalesCreditResponse), 200)]
    public async Task<IActionResult> Cancel([FromRoute] Guid companyId, [FromRoute] Guid invoiceId, [FromRoute] Guid id, [FromBody] CancelSalesInvoiceRequest request, [FromServices] SalesCreditService service, [FromServices] ICommandExecutor commands, CancellationToken ct)
    {
        var ctx = HttpContext;
        return await SalesApiCommands.Execute(ctx, commands, companyId, $"sales.credit.cancel:{invoiceId}:{id}", request,
            () => service.Cancel(ctx.GetActor(), companyId, invoiceId, id, request, ctx.TraceIdentifier, ct), ct);
    }
}
