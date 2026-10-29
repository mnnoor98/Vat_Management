using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vat.Api.Security;
using Vat.Application.Security;
using Vat.Services.Sales;
using Vat.Services.Sales.Contracts;

namespace Vat.Services.Sales.Controllers;

[ApiController]
[Tags("Sales")]
public sealed class SalesPostingController : ControllerBase
{
    [HttpPost("/api/v1/companies/{companyId:guid}/sales/{id:guid}/reserve")]
    [Authorize(Policy = Permissions.SalesReserve)]
    [ProducesResponseType(typeof(SalesPostingResponse), 201)]
    public async Task<IActionResult> Reserve([FromRoute] Guid companyId, [FromRoute] Guid id, [FromBody] ReserveSalesRequest request, [FromServices] SalesPostingService service, [FromServices] ICommandExecutor commands, CancellationToken ct)
    {
        var ctx = HttpContext;
        return await SalesApiCommands.Execute(ctx, commands, companyId, $"sales.reserve:{id}", request,
            () => service.Reserve(ctx.GetActor(), companyId, id, request, ctx.TraceIdentifier, ct), ct);
    }

    [HttpPost("/api/v1/companies/{companyId:guid}/sales/{id:guid}/post")]
    [Authorize(Policy = Permissions.SalesPost)]
    [ProducesResponseType(typeof(SalesInvoiceResponse), 200)]
    public async Task<IActionResult> Post([FromRoute] Guid companyId, [FromRoute] Guid id, [FromBody] TransitionRequest request, [FromServices] SalesPostingService service, [FromServices] ICommandExecutor commands, CancellationToken ct)
    {
        var ctx = HttpContext;
        return await SalesApiCommands.Execute(ctx, commands, companyId, $"sales.post:{id}", request,
            () => service.Post(ctx.GetActor(), companyId, id, request, ctx.TraceIdentifier, ct), ct);
    }

    [HttpGet("/api/v1/companies/{companyId:guid}/sales/{id:guid}/fulfillment")]
    [Authorize(Policy = Permissions.SalesInvoiceRead)]
    public async Task<IActionResult> Fulfillment([FromRoute] Guid companyId, [FromRoute] Guid id, [FromServices] SalesPostingService service, CancellationToken ct)
    {
        var ctx = HttpContext;
        return Ok(await service.Get(ctx.GetActor(), companyId, id, ct));
    }

    [HttpGet("/api/v1/companies/{companyId:guid}/sales/{id:guid}/reconciliation")]
    [Authorize(Policy = Permissions.SalesInvoiceRead)]
    public async Task<IActionResult> Reconcile([FromRoute] Guid companyId, [FromRoute] Guid id, [FromServices] SalesPostingService service, CancellationToken ct)
    {
        var ctx = HttpContext;
        return Ok(await service.Reconcile(ctx.GetActor(), companyId, id, ct));
    }

    [HttpGet("/api/v1/companies/{companyId:guid}/sales/ledger")]
    [Authorize(Policy = Permissions.TaxRead)]
    public async Task<IActionResult> Ledger([FromRoute] Guid companyId, [FromQuery] Guid? before, [FromQuery] int? take, [FromServices] SalesPostingService service, CancellationToken ct)
    {
        var ctx = HttpContext;
        return Ok(await service.Ledger(ctx.GetActor(), companyId, before, take ?? 25, ct));
    }

    [HttpGet("/api/v1/companies/{companyId:guid}/sales/summary")]
    [Authorize(Policy = Permissions.ReportRead)]
    public async Task<IActionResult> Summary([FromRoute] Guid companyId, [FromQuery, Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired] DateOnly from, [FromQuery, Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired] DateOnly until, [FromServices] SalesPostingService service, CancellationToken ct)
    {
        var ctx = HttpContext;
        return Ok(await service.Summary(ctx.GetActor(), companyId, from, until, ct));
    }
}
