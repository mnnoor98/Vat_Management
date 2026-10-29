using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vat.Api.Security;
using Vat.Application.Security;
using Vat.Services.VatEngine;

namespace Vat.Services.VatEngine.Controllers;

public static class VatEngineApiCommands
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
[Tags("VatEngine")]
public sealed class TaxController : ControllerBase
{
    [HttpPost("/api/v1/companies/{companyId:guid}/tax/rules", Name = "CreateTaxRule")]
    [Authorize(Policy = VatEnginePermissions.TaxRuleManage)]
    [ProducesResponseType(typeof(TaxRuleResponse), 201)]
    public async Task<IActionResult> CreateTaxRule([FromRoute] Guid companyId, [FromBody] CreateTaxRuleRequest request,
        [FromServices] TaxRuleService service, [FromServices] ICommandExecutor commands, CancellationToken ct)
    {
        var ctx = HttpContext;
        return await VatEngineApiCommands.Execute(ctx, commands, companyId, "tax-rule.create", request,
            () => service.Create(ctx.GetActor(), companyId, request, ctx.TraceIdentifier, ct), ct);
    }

    [HttpPost("/api/v1/companies/{companyId:guid}/tax/rules/{id:guid}/publish", Name = "PublishTaxRule")]
    [Authorize(Policy = VatEnginePermissions.TaxRulePublish)]
    [ProducesResponseType(typeof(TaxRuleResponse), 200)]
    public async Task<IActionResult> PublishTaxRule([FromRoute] Guid companyId, [FromRoute] Guid id,
        [FromBody] TransitionRequest request, [FromServices] TaxRuleService service, [FromServices] ICommandExecutor commands, CancellationToken ct)
    {
        var ctx = HttpContext;
        return await VatEngineApiCommands.Execute(ctx, commands, companyId, $"tax-rule.publish:{id}", request,
            () => service.Publish(ctx.GetActor(), companyId, id, request, ctx.TraceIdentifier, ct), ct);
    }

    [HttpGet("/api/v1/companies/{companyId:guid}/tax/rules/{id:guid}", Name = "GetTaxRule")]
    [Authorize(Policy = VatEnginePermissions.TaxRead)]
    public async Task<IActionResult> GetTaxRule([FromRoute] Guid companyId, [FromRoute] Guid id,
        [FromServices] TaxRuleService service, CancellationToken ct)
    {
        var outcome = await service.Get(HttpContext.GetActor(), companyId, id, ct);
        return StatusCode(outcome.StatusCode, outcome.Body);
    }

    [HttpGet("/api/v1/companies/{companyId:guid}/tax/ledger", Name = "GetTaxLedger")]
    [Authorize(Policy = VatEnginePermissions.TaxRead)]
    public async Task<IActionResult> GetTaxLedger([FromRoute] Guid companyId, [FromQuery] Guid? purchaseId,
        [FromQuery] Guid? before, [FromQuery] int? take, [FromServices] TaxLedgerService service, CancellationToken ct)
    {
        return Ok(await service.TaxLedger(HttpContext.GetActor(), companyId, purchaseId, before, take ?? 25, ct));
    }
}
