using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vat.Api.Security;
using Vat.Application.Security;
using Vat.Application;
using Vat.Application.Contracts;

namespace Vat.Coordinated.Company.Controllers;

[ApiController]
[Tags("Company")]
public sealed class FiscalPeriodsController : ControllerBase
{
    [HttpPost("/api/v1/companies/{companyId:guid}/periods", Name = "CreateFiscalPeriod")]
    [Authorize(Policy = Permissions.PeriodManage)]
    [ProducesResponseType(typeof(PeriodResponse), 201)]
    public async Task<IActionResult> CreateFiscalPeriod([FromRoute] Guid companyId, [FromBody] CreatePeriodRequest request, [FromServices] PeriodService service, [FromServices] ICommandExecutor commands, CancellationToken ct)
    {
        var ctx = HttpContext;
        return await CompanyApiCommands.Execute(ctx, commands, companyId, "period.create", request,
                () => service.Create(ctx.GetActor(), companyId, request, ctx.TraceIdentifier, ct), ct);
    }

    [HttpGet("/api/v1/companies/{companyId:guid}/periods", Name = "ListFiscalPeriods")]
    [Authorize(Policy = Permissions.PeriodRead)]
    public async Task<IActionResult> ListFiscalPeriods([FromRoute] Guid companyId, [FromQuery] Guid? before, [FromQuery] int? take, [FromServices] PeriodService service, CancellationToken ct)
    {
        var ctx = HttpContext;
        return Ok(await service.List(ctx.GetActor(), companyId, before, take ?? 25, ct));
    }

    [HttpGet("/api/v1/companies/{companyId:guid}/periods/{id:guid}", Name = "GetFiscalPeriod")]
    [Authorize(Policy = Permissions.PeriodRead)]
    public async Task<IActionResult> GetFiscalPeriod([FromRoute] Guid companyId, [FromRoute] Guid id, [FromServices] PeriodService service, CancellationToken ct)
    {
        var ctx = HttpContext;
        return Ok(await service.Get(ctx.GetActor(), companyId, id, ct));
    }

    [HttpPost("/api/v1/companies/{companyId:guid}/periods/{id:guid}/request-close", Name = "RequestFiscalPeriodClose")]
    [Authorize(Policy = Permissions.PeriodCloseRequest)]
    [ProducesResponseType(typeof(PeriodResponse), 200)]
    public async Task<IActionResult> RequestFiscalPeriodClose([FromRoute] Guid companyId, [FromRoute] Guid id, [FromBody] PeriodActionRequest request, [FromServices] PeriodService service, [FromServices] ICommandExecutor commands, CancellationToken ct)
    {
        var ctx = HttpContext;
        return await CompanyApiCommands.Execute(ctx, commands, companyId, $"period.request-close:{id}", request,
                () => service.RequestClose(ctx.GetActor(), companyId, id, request, ctx.TraceIdentifier, ct), ct);
    }

    [HttpPost("/api/v1/companies/{companyId:guid}/periods/{id:guid}/close", Name = "ApproveFiscalPeriodClose")]
    [Authorize(Policy = Permissions.PeriodCloseApprove)]
    [ProducesResponseType(typeof(PeriodResponse), 200)]
    public async Task<IActionResult> CloseFiscalPeriod([FromRoute] Guid companyId, [FromRoute] Guid id, [FromBody] PeriodActionRequest request, [FromServices] PeriodService service, [FromServices] ICommandExecutor commands, CancellationToken ct)
    {
        var ctx = HttpContext;
        return await CompanyApiCommands.Execute(ctx, commands, companyId, $"period.close:{id}", request,
                () => service.Close(ctx.GetActor(), companyId, id, request, ctx.TraceIdentifier, ct), ct);
    }

    [HttpPost("/api/v1/companies/{companyId:guid}/periods/{id:guid}/cancel-close", Name = "CancelFiscalPeriodClose")]
    [HttpPost("/api/v1/companies/{companyId:guid}/periods/{id:guid}/reopen", Name = "ReopenFiscalPeriod")]
    [Authorize(Policy = Permissions.PeriodManage)]
    [ProducesResponseType(typeof(PeriodResponse), 200)]
    public async Task<IActionResult> ReopenFiscalPeriod([FromRoute] Guid companyId, [FromRoute] Guid id, [FromBody] PeriodActionRequest request, [FromServices] PeriodService service, [FromServices] ICommandExecutor commands, CancellationToken ct)
    {
        var ctx = HttpContext;
        return await CompanyApiCommands.Execute(ctx, commands, companyId, $"period.reopen:{id}", request,
                () => service.Reopen(ctx.GetActor(), companyId, id, request, ctx.TraceIdentifier, ct), ct);
    }
}
