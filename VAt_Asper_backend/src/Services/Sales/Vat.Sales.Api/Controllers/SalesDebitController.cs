using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vat.Api.Security;
using Vat.Application.Security;
using Vat.Application;
using Vat.Application.Contracts;

namespace Vat.Coordinated.Sales.Controllers;

[ApiController]
[Tags("Sales")]
public sealed class SalesDebitController : ControllerBase
{
    [HttpPost("/api/v1/companies/{companyId:guid}/sales/{invoiceId:guid}/debit-notes")]
    [Authorize(Policy = Permissions.SalesDebitRequest)]
    [ProducesResponseType(typeof(SalesDebitResponse), 201)]
    public async Task<IActionResult> RequestDebit([FromRoute] Guid companyId, [FromRoute] Guid invoiceId, [FromBody] RequestSalesDebit request, [FromServices] SalesDebitService service, [FromServices] ICommandExecutor commands, CancellationToken ct)
    {
        var ctx = HttpContext;
        return await SalesApiCommands.Execute(ctx, commands, companyId, $"sales.debit.request:{invoiceId}", request,
            () => service.Request(ctx.GetActor(), companyId, invoiceId, request, ctx.TraceIdentifier, ct), ct);
    }

    [HttpGet("/api/v1/companies/{companyId:guid}/sales/{invoiceId:guid}/debit-notes")]
    [Authorize(Policy = Permissions.SalesInvoiceRead)]
    public async Task<IActionResult> List([FromRoute] Guid companyId, [FromRoute] Guid invoiceId, [FromQuery] Guid? before, [FromQuery] int? take, [FromServices] SalesDebitService service, CancellationToken ct)
    {
        var ctx = HttpContext;
        return Ok(await service.List(ctx.GetActor(), companyId, invoiceId, before, take ?? 25, ct));
    }

    [HttpGet("/api/v1/companies/{companyId:guid}/sales/{invoiceId:guid}/debit-notes/{id:guid}")]
    [Authorize(Policy = Permissions.SalesInvoiceRead)]
    public async Task<IActionResult> Get([FromRoute] Guid companyId, [FromRoute] Guid invoiceId, [FromRoute] Guid id, [FromServices] SalesDebitService service, CancellationToken ct)
    {
        var ctx = HttpContext;
        return Ok(await service.Get(ctx.GetActor(), companyId, invoiceId, id, ct));
    }

    [HttpPost("/api/v1/companies/{companyId:guid}/sales/{invoiceId:guid}/debit-notes/{id:guid}/approve")]
    [Authorize(Policy = Permissions.SalesDebitApprove)]
    [ProducesResponseType(typeof(SalesDebitResponse), 200)]
    public async Task<IActionResult> Approve([FromRoute] Guid companyId, [FromRoute] Guid invoiceId, [FromRoute] Guid id, [FromBody] TransitionRequest request, [FromServices] SalesDebitService service, [FromServices] ICommandExecutor commands, CancellationToken ct)
    {
        var ctx = HttpContext;
        return await SalesApiCommands.Execute(ctx, commands, companyId, $"sales.debit.approve:{invoiceId}:{id}", request,
            () => service.Approve(ctx.GetActor(), companyId, invoiceId, id, request, ctx.TraceIdentifier, ct), ct);
    }

    [HttpPost("/api/v1/companies/{companyId:guid}/sales/{invoiceId:guid}/debit-notes/{id:guid}/cancel")]
    [Authorize(Policy = Permissions.SalesDebitRequest)]
    [ProducesResponseType(typeof(SalesDebitResponse), 200)]
    public async Task<IActionResult> Cancel([FromRoute] Guid companyId, [FromRoute] Guid invoiceId, [FromRoute] Guid id, [FromBody] CancelSalesInvoiceRequest request, [FromServices] SalesDebitService service, [FromServices] ICommandExecutor commands, CancellationToken ct)
    {
        var ctx = HttpContext;
        return await SalesApiCommands.Execute(ctx, commands, companyId, $"sales.debit.cancel:{invoiceId}:{id}", request,
            () => service.Cancel(ctx.GetActor(), companyId, invoiceId, id, request, ctx.TraceIdentifier, ct), ct);
    }
}
