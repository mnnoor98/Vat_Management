using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vat.Api.Security;
using Vat.Application.Security;
using Vat.Services.Inventory;

namespace Vat.Services.Inventory.Controllers;

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
    [Authorize(Policy = InventoryPermissions.InventoryManage)]
    [ProducesResponseType(typeof(WarehouseResponse), 201)]
    public async Task<IActionResult> CreateWarehouse([FromRoute] Guid companyId, [FromBody] CreateWarehouseRequest request,
        [FromServices] WarehouseService service, [FromServices] ICommandExecutor commands, CancellationToken ct)
    {
        var ctx = HttpContext;
        return await InventoryApiCommands.Execute(ctx, commands, companyId, "warehouse.create", request,
            () => service.Create(ctx.GetActor(), companyId, request, ctx.TraceIdentifier, ct), ct);
    }

    [HttpGet("/api/v1/companies/{companyId:guid}/warehouses", Name = "ListWarehouses")]
    [Authorize(Policy = InventoryPermissions.InventoryRead)]
    public async Task<IActionResult> ListWarehouses([FromRoute] Guid companyId, [FromQuery] int? skip,
        [FromQuery] int? take, [FromServices] WarehouseService service, CancellationToken ct)
    {
        return Ok(await service.List(HttpContext.GetActor(), companyId, skip ?? 0, take ?? 100, ct));
    }

    [HttpGet("/api/v1/companies/{companyId:guid}/warehouses/{id:guid}", Name = "GetWarehouse")]
    [Authorize(Policy = InventoryPermissions.InventoryRead)]
    public async Task<IActionResult> GetWarehouse([FromRoute] Guid companyId, [FromRoute] Guid id,
        [FromServices] WarehouseService service, CancellationToken ct)
    {
        var outcome = await service.Get(HttpContext.GetActor(), companyId, id, ct);
        return StatusCode(outcome.StatusCode, outcome.Body);
    }

    [HttpGet("/api/v1/companies/{companyId:guid}/inventory/balances", Name = "GetStockBalances")]
    [Authorize(Policy = InventoryPermissions.InventoryRead)]
    public async Task<IActionResult> GetStockBalances([FromRoute] Guid companyId,
        [FromQuery, Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired] Guid warehouseId,
        [FromQuery] Guid? beforeProduct, [FromQuery] int? take,
        [FromServices] StockService service, CancellationToken ct)
    {
        return Ok(await service.Balances(HttpContext.GetActor(), companyId, warehouseId, beforeProduct, take ?? 25, ct));
    }

    [HttpGet("/api/v1/companies/{companyId:guid}/inventory/all-balances", Name = "GetAllStockBalances")]
    [Authorize(Policy = InventoryPermissions.InventoryRead)]
    public async Task<IActionResult> GetAllStockBalances([FromRoute] Guid companyId,
        [FromQuery] Guid? warehouseId, [FromQuery] int? skip, [FromQuery] int? take,
        [FromServices] StockService service, CancellationToken ct)
    {
        return Ok(await service.AllBalances(HttpContext.GetActor(), companyId, warehouseId, skip ?? 0, take ?? 100, ct));
    }

    [HttpPost("/api/v1/companies/{companyId:guid}/transfers", Name = "CreateTransfer")]
    [Authorize(Policy = InventoryPermissions.InventoryManage)]
    [ProducesResponseType(typeof(TransferResult), 201)]
    public async Task<IActionResult> CreateTransfer([FromRoute] Guid companyId, [FromBody] TransferCreateRequest request,
        [FromServices] TransferService service, [FromServices] ICommandExecutor commands, CancellationToken ct)
    {
        var ctx = HttpContext;
        return await InventoryApiCommands.Execute(ctx, commands, companyId, "transfer.create", request,
            () => service.Create(ctx.GetActor(), companyId, request, ctx.TraceIdentifier, ct), ct);
    }

    [HttpPost("/api/v1/companies/{companyId:guid}/transfers/{id:guid}/submit", Name = "SubmitTransfer")]
    [Authorize(Policy = InventoryPermissions.InventoryManage)]
    public Task<IActionResult> SubmitTransfer(Guid companyId, Guid id, TransferActionRequest request,
        [FromServices] TransferService service, [FromServices] ICommandExecutor commands, CancellationToken ct) =>
        Change(companyId, id, "submit", request, service, commands, ct);

    [HttpPost("/api/v1/companies/{companyId:guid}/transfers/{id:guid}/approve", Name = "ApproveTransfer")]
    [Authorize(Policy = InventoryPermissions.InventoryApprove)]
    public Task<IActionResult> ApproveTransfer(Guid companyId, Guid id, TransferActionRequest request,
        [FromServices] TransferService service, [FromServices] ICommandExecutor commands, CancellationToken ct) =>
        Change(companyId, id, "approve", request, service, commands, ct);

    [HttpPost("/api/v1/companies/{companyId:guid}/transfers/{id:guid}/post", Name = "PostTransfer")]
    [Authorize(Policy = InventoryPermissions.InventoryPost)]
    public Task<IActionResult> PostTransfer(Guid companyId, Guid id, TransferActionRequest request,
        [FromServices] TransferService service, [FromServices] ICommandExecutor commands, CancellationToken ct) =>
        Change(companyId, id, "post", request, service, commands, ct);

    [HttpPost("/api/v1/companies/{companyId:guid}/transfers/{id:guid}/cancel", Name = "CancelTransfer")]
    [Authorize(Policy = InventoryPermissions.InventoryManage)]
    public Task<IActionResult> CancelTransfer(Guid companyId, Guid id, TransferActionRequest request,
        [FromServices] TransferService service, [FromServices] ICommandExecutor commands, CancellationToken ct) =>
        Change(companyId, id, "cancel", request, service, commands, ct);

    private async Task<IActionResult> Change(Guid companyId, Guid id, string action, TransferActionRequest request,
        TransferService service, ICommandExecutor commands, CancellationToken ct) =>
        await InventoryApiCommands.Execute(HttpContext, commands, companyId, "transfer." + action, new { id, request },
            () => service.Transition(HttpContext.GetActor(), companyId, id, action, request, HttpContext.TraceIdentifier, ct), ct);

    [HttpGet("/api/v1/companies/{companyId:guid}/transfers", Name = "ListTransfers")]
    [Authorize(Policy = InventoryPermissions.InventoryRead)]
    public async Task<IActionResult> ListTransfers([FromRoute] Guid companyId, [FromQuery] int? skip,
        [FromQuery] int? take, [FromServices] InventoryReadService service, CancellationToken ct) =>
        Ok(await service.Transfers(HttpContext.GetActor(), companyId, skip ?? 0, take ?? 25, ct));
}
