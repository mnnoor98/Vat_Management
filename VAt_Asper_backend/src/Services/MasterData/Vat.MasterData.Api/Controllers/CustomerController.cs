using Vat.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vat.Api.Security;
using Vat.Application.Security;
using Vat.Application.Contracts;

namespace Vat.Coordinated.MasterData.Controllers;

public static class MasterDataApiCommands
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
[Tags("MasterData")]
public sealed class CustomerController : ControllerBase
{
    [HttpGet("/api/v1/companies/{companyId:guid}/customers")]
    [Authorize(Policy = Permissions.MasterDataRead)]
    public async Task<IActionResult> List([FromRoute] Guid companyId, [FromQuery] Guid? before, [FromQuery] int? take, [FromServices] CustomerService service, CancellationToken ct)
    {
        var ctx = HttpContext;
        return Ok(await service.List(ctx.GetActor(), companyId, before, take ?? 25, ct));
    }

    [HttpGet("/api/v1/companies/{companyId:guid}/customers/{id:guid}")]
    [Authorize(Policy = Permissions.MasterDataRead)]
    public async Task<IActionResult> Get([FromRoute] Guid companyId, [FromRoute] Guid id, [FromServices] CustomerService service, CancellationToken ct)
    {
        var ctx = HttpContext;
        return Ok(await service.Get(ctx.GetActor(), companyId, id, ct));
    }

    [HttpPost("/api/v1/companies/{companyId:guid}/customers")]
    [Authorize(Policy = Permissions.MasterDataManage)]
    [ProducesResponseType(typeof(CustomerResponse), 201)]
    public async Task<IActionResult> Create([FromRoute] Guid companyId, [FromBody] CreateCustomerRequest request, [FromServices] CustomerService service, [FromServices] ICommandExecutor commands, CancellationToken ct)
    {
        var ctx = HttpContext;
        return await MasterDataApiCommands.Execute(ctx, commands, companyId, "customer.create", request,
                () => service.Create(ctx.GetActor(), companyId, request, ctx.TraceIdentifier, ct), ct);
    }

    [HttpPost("/api/v1/companies/{companyId:guid}/customers/{id:guid}/update")]
    [Authorize(Policy = Permissions.MasterDataManage)]
    [ProducesResponseType(typeof(CustomerResponse), 200)]
    public async Task<IActionResult> Update([FromRoute] Guid companyId, [FromRoute] Guid id, [FromBody] UpdateCustomerRequest request, [FromServices] CustomerService service, [FromServices] ICommandExecutor commands, CancellationToken ct)
    {
        var ctx = HttpContext;
        return await MasterDataApiCommands.Execute(ctx, commands, companyId, $"customer.update:{id}", request,
                () => service.Update(ctx.GetActor(), companyId, id, request, ctx.TraceIdentifier, ct), ct);
    }
}
