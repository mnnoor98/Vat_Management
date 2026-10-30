using Vat.Infrastructure.Persistence;
using Vat.Application.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vat.Api.Security;
using Vat.Application.Security;
using Vat.Application;

namespace Vat.Coordinated.Inventory.Controllers;

public static class InventoryApiCommands
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
[Tags("Inventory")]
public sealed class InventoryController : ControllerBase
{
    [HttpPost("/api/v1/companies/{companyId:guid}/warehouses", Name = "CreateWarehouse")]
    [Authorize(Policy = Permissions.InventoryManage)]
    [ProducesResponseType(typeof(WarehouseResponse), 201)]
    public async Task<IActionResult> CreateWarehouse([FromRoute] Guid companyId, [FromBody] CreateWarehouseRequest request,
        [FromServices] ReferenceDataService service, [FromServices] ICommandExecutor commands, CancellationToken ct)
    {
        var ctx = HttpContext;
        return await InventoryApiCommands.Execute(ctx, commands, companyId, "warehouse.create", request,
            () => service.Warehouse(ctx.GetActor(), companyId, request, ctx.TraceIdentifier, ct), ct);
    }

    [HttpGet("/api/v1/companies/{companyId:guid}/warehouses", Name = "ListWarehouses")]
    [Authorize(Policy = Permissions.InventoryRead)]
    public async Task<IActionResult> ListWarehouses([FromRoute] Guid companyId, [FromQuery] int? skip,
        [FromQuery] int? take, [FromServices] FinancialInventoryQueries service, CancellationToken ct)
    {
        return Ok(await service.Warehouses(HttpContext.GetActor(), companyId, skip ?? 0, take ?? 100, ct));
    }

    [HttpGet("/api/v1/companies/{companyId:guid}/warehouses/{id:guid}", Name = "GetWarehouse")]
    [Authorize(Policy = Permissions.InventoryRead)]
    public async Task<IActionResult> GetWarehouse([FromRoute] Guid companyId, [FromRoute] Guid id,
        [FromServices] ReferenceDataService service, CancellationToken ct)
    {
        return Ok(await service.GetWarehouse(HttpContext.GetActor(), companyId, id, ct));
    }

    [HttpGet("/api/v1/companies/{companyId:guid}/inventory/balances", Name = "GetStockBalances")]
    [Authorize(Policy = Permissions.InventoryRead)]
    public async Task<IActionResult> GetStockBalances([FromRoute] Guid companyId,
        [FromQuery, Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired] Guid warehouseId,
        [FromQuery] Guid? beforeProduct, [FromQuery] int? take,
        [FromServices] PostingService service, CancellationToken ct)
    {
        return Ok(await service.Balances(HttpContext.GetActor(), companyId, warehouseId, beforeProduct, take ?? 25, ct));
    }

    [HttpGet("/api/v1/companies/{companyId:guid}/inventory/all-balances", Name = "GetAllStockBalances")]
    [Authorize(Policy = Permissions.InventoryRead)]
    public async Task<IActionResult> GetAllStockBalances([FromRoute] Guid companyId,
        [FromQuery] Guid? warehouseId, [FromQuery] int? skip, [FromQuery] int? take,
        [FromQuery] string? method,
        [FromServices] FinancialInventoryQueries service, CancellationToken ct)
    {
        return Ok(await service.AllBalances(HttpContext.GetActor(), companyId, warehouseId, method ?? "WEIGHTED_AVERAGE", skip ?? 0, take ?? 100, ct));
    }

    [HttpGet("/api/v1/companies/{companyId:guid}/inventory/valuation-summary", Name = "GetInventoryValuationSummary")]
    [Authorize(Policy = Permissions.InventoryRead)]
    public async Task<IActionResult> GetInventoryValuationSummary([FromRoute] Guid companyId,
        [FromQuery] string? method,
        [FromServices] FinancialInventoryQueries service, CancellationToken ct)
    {
        return Ok(await service.ValuationSummary(HttpContext.GetActor(), companyId, method ?? "WEIGHTED_AVERAGE", ct));
    }

    [HttpPost("/api/v1/companies/{companyId:guid}/transfers", Name = "CreateTransfer")]
    [Authorize(Policy = Permissions.InventoryManage)]
    [ProducesResponseType(typeof(TransferResult), 201)]
    public async Task<IActionResult> CreateTransfer([FromRoute] Guid companyId, [FromBody] TransferCreateRequest request,
        [FromServices] TransferService service, [FromServices] ICommandExecutor commands, CancellationToken ct)
    {
        var ctx = HttpContext;
        return await InventoryApiCommands.Execute(ctx, commands, companyId, "transfer.create", request,
            () => service.Create(ctx.GetActor(), companyId, request, ctx.TraceIdentifier, ct), ct);
    }

    [HttpPost("/api/v1/companies/{companyId:guid}/transfers/{id:guid}/submit", Name = "SubmitTransfer")]
    [Authorize(Policy = Permissions.InventoryManage)]
    public Task<IActionResult> SubmitTransfer(Guid companyId, Guid id, TransferActionRequest request,
        [FromServices] TransferService service, [FromServices] ICommandExecutor commands, CancellationToken ct) =>
        Change(companyId, id, "submit", request, service, commands, ct);

    [HttpPost("/api/v1/companies/{companyId:guid}/transfers/{id:guid}/approve", Name = "ApproveTransfer")]
    [Authorize(Policy = Permissions.InventoryApprove)]
    public Task<IActionResult> ApproveTransfer(Guid companyId, Guid id, TransferActionRequest request,
        [FromServices] TransferService service, [FromServices] ICommandExecutor commands, CancellationToken ct) =>
        Change(companyId, id, "approve", request, service, commands, ct);

    [HttpPost("/api/v1/companies/{companyId:guid}/transfers/{id:guid}/post", Name = "PostTransfer")]
    [Authorize(Policy = Permissions.InventoryPost)]
    public Task<IActionResult> PostTransfer(Guid companyId, Guid id, TransferActionRequest request,
        [FromServices] TransferService service, [FromServices] ICommandExecutor commands, CancellationToken ct) =>
        Change(companyId, id, "post", request, service, commands, ct);

    [HttpPost("/api/v1/companies/{companyId:guid}/transfers/{id:guid}/cancel", Name = "CancelTransfer")]
    [Authorize(Policy = Permissions.InventoryManage)]
    public Task<IActionResult> CancelTransfer(Guid companyId, Guid id, TransferActionRequest request,
        [FromServices] TransferService service, [FromServices] ICommandExecutor commands, CancellationToken ct) =>
        Change(companyId, id, "cancel", request, service, commands, ct);

    private async Task<IActionResult> Change(Guid companyId, Guid id, string action, TransferActionRequest request,
        TransferService service, ICommandExecutor commands, CancellationToken ct) =>
        await InventoryApiCommands.Execute(HttpContext, commands, companyId, "transfer." + action, new { id, request },
            () => service.Transition(HttpContext.GetActor(), companyId, id, action, request, HttpContext.TraceIdentifier, ct), ct);

    [HttpGet("/api/v1/companies/{companyId:guid}/transfers", Name = "ListTransfers")]
    [Authorize(Policy = Permissions.InventoryRead)]
    public async Task<IActionResult> ListTransfers([FromRoute] Guid companyId, [FromQuery] int? skip,
        [FromQuery] int? take, [FromServices] InventoryReadService service, CancellationToken ct) =>
        Ok(await service.Transfers(HttpContext.GetActor(), companyId, skip ?? 0, take ?? 25, ct));
}
