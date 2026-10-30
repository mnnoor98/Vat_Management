using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vat.Api.Security;
using Vat.Application.Security;
using Vat.Application;
using Vat.Application.Contracts;

namespace Vat.Coordinated.Sales.Controllers;

public static class SalesApiCommands
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
[Tags("Sales")]
public sealed class SalesController : ControllerBase
{
    [HttpPost("/api/v1/companies/{companyId:guid}/sales", Name = "CreateSalesInvoice")]
    [Authorize(Policy = Permissions.SalesInvoiceCreate)]
    [ProducesResponseType(typeof(SalesInvoiceResponse), 201)]
    public async Task<IActionResult> CreateSalesInvoice([FromRoute] Guid companyId, [FromBody] CreateSalesInvoiceRequest request, [FromServices] SalesInvoiceService service, [FromServices] ICommandExecutor commands, CancellationToken ct)
    {
        var ctx = HttpContext;
        return await SalesApiCommands.Execute(ctx, commands, companyId, "sales.create", request,
            () => service.Create(ctx.GetActor(), companyId, request, ctx.TraceIdentifier, ct), ct);
    }

    [HttpGet("/api/v1/companies/{companyId:guid}/sales", Name = "ListSalesInvoices")]
    [Authorize(Policy = Permissions.SalesInvoiceRead)]
    public async Task<IActionResult> ListSalesInvoices([FromRoute] Guid companyId, [FromQuery] Guid? before, [FromQuery] int? take, [FromServices] SalesInvoiceService service, CancellationToken ct)
    {
        var ctx = HttpContext;
        return Ok(await service.List(ctx.GetActor(), companyId, before, take ?? 25, ct));
    }

    [HttpGet("/api/v1/companies/{companyId:guid}/sales/{id:guid}", Name = "GetSalesInvoice")]
    [Authorize(Policy = Permissions.SalesInvoiceRead)]
    public async Task<IActionResult> GetSalesInvoice([FromRoute] Guid companyId, [FromRoute] Guid id, [FromServices] SalesInvoiceService service, CancellationToken ct)
    {
        var ctx = HttpContext;
        return Ok(await service.Get(ctx.GetActor(), companyId, id, ct));
    }

    [HttpPost("/api/v1/companies/{companyId:guid}/sales/{id:guid}/submit", Name = "SubmitSalesInvoice")]
    [Authorize(Policy = Permissions.SalesInvoiceSubmit)]
    [ProducesResponseType(typeof(SalesInvoiceResponse), 200)]
    public async Task<IActionResult> SubmitSalesInvoice([FromRoute] Guid companyId, [FromRoute] Guid id, [FromBody] TransitionRequest request, [FromServices] SalesInvoiceService service, [FromServices] ICommandExecutor commands, CancellationToken ct)
    {
        var ctx = HttpContext;
        return await SalesApiCommands.Execute(ctx, commands, companyId, $"sales.submit:{id}", request,
            () => service.Submit(ctx.GetActor(), companyId, id, request, ctx.TraceIdentifier, ct), ct);
    }

    [HttpPost("/api/v1/companies/{companyId:guid}/sales/{id:guid}/approve", Name = "ApproveSalesInvoice")]
    [Authorize(Policy = Permissions.SalesInvoiceApprove)]
    [ProducesResponseType(typeof(SalesInvoiceResponse), 200)]
    public async Task<IActionResult> ApproveSalesInvoice([FromRoute] Guid companyId, [FromRoute] Guid id, [FromBody] TransitionRequest request, [FromServices] SalesInvoiceService service, [FromServices] ICommandExecutor commands, CancellationToken ct)
    {
        var ctx = HttpContext;
        return await SalesApiCommands.Execute(ctx, commands, companyId, $"sales.approve:{id}", request,
            () => service.Approve(ctx.GetActor(), companyId, id, request, ctx.TraceIdentifier, ct), ct);
    }

    [HttpPost("/api/v1/companies/{companyId:guid}/sales/{id:guid}/reject", Name = "RejectSalesInvoice")]
    [Authorize(Policy = Permissions.SalesInvoiceApprove)]
    [ProducesResponseType(typeof(SalesInvoiceResponse), 200)]
    public async Task<IActionResult> RejectSalesInvoice([FromRoute] Guid companyId, [FromRoute] Guid id, [FromBody] RejectSalesInvoiceRequest request, [FromServices] SalesInvoiceService service, [FromServices] ICommandExecutor commands, CancellationToken ct)
    {
        var ctx = HttpContext;
        return await SalesApiCommands.Execute(ctx, commands, companyId, $"sales.reject:{id}", request,
            () => service.Reject(ctx.GetActor(), companyId, id, request, ctx.TraceIdentifier, ct), ct);
    }

    [HttpPost("/api/v1/companies/{companyId:guid}/sales/{id:guid}/cancel", Name = "CancelSalesInvoice")]
    [Authorize(Policy = Permissions.SalesInvoiceCancel)]
    public async Task<IActionResult> CancelSalesInvoice([FromRoute] Guid companyId, [FromRoute] Guid id, [FromBody] CancelSalesInvoiceRequest request, [FromServices] SalesInvoiceService service, [FromServices] ICommandExecutor commands, CancellationToken ct)
    {
        var ctx = HttpContext;
        return await SalesApiCommands.Execute(ctx, commands, companyId, $"sales.cancel:{id}", request,
            () => service.Cancel(ctx.GetActor(), companyId, id, request, ctx.TraceIdentifier, ct), ct);
    }
}
