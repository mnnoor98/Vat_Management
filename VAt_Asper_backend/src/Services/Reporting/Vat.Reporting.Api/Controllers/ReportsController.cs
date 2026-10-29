using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vat.Api.Security;
using Vat.Application;
using Vat.Application.Contracts;
using Vat.Application.Security;
using Vat.Hosting;

namespace Vat.Services.Reporting.Controllers;

[ApiController]
[Tags("Reporting")]
public sealed class ReportsController : VatControllerBase
{
    [HttpGet("/api/v1/companies/{companyId:guid}/reports/financial-summary", Name = "GetFinancialReport")]
    [Authorize(Policy = Permissions.ReportRead)]
    public async Task<IActionResult> GetFinancialReport([FromRoute] Guid companyId, [FromQuery, Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired] DateOnly from, [FromQuery, Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired] DateOnly until, [FromServices] IReportingService service, CancellationToken ct)
    {
        var ctx = HttpContext;
        return Respond(await service.Summary(ctx.GetActor(), companyId, from, until, ct));
    }

    [HttpGet("/api/v1/companies/{companyId:guid}/reports/financial-entries", Name = "ListFinancialReportEntries")]
    [Authorize(Policy = Permissions.ReportRead)]
    public async Task<IActionResult> ListFinancialReportEntries([FromRoute] Guid companyId, [FromQuery, Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired] DateOnly from, [FromQuery, Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired] DateOnly until, [FromQuery] Guid? before, [FromQuery] int? take, [FromServices] IReportingService service, CancellationToken ct)
    {
        var ctx = HttpContext;
        return Respond(await service.Entries(ctx.GetActor(), companyId, from, until, before, take ?? 25, ct));
    }

    [HttpPost("/api/v1/companies/{companyId:guid}/reports/backfill", Name = "BackfillFinancialReport")]
    [Authorize(Policy = Permissions.ReportManage)]
    [ProducesResponseType(typeof(BackfillResponse), 200)]
    public async Task<IActionResult> BackfillFinancialReport([FromRoute] Guid companyId, [FromBody] RetryMessageRequest request, [FromServices] IReportingService service, [FromServices] ICommandExecutor commands, CancellationToken ct)
    {
        var ctx = HttpContext;
        return Respond(await ApiCommands.Execute(ctx, commands, companyId, "reports.backfill", request,
                () => service.Backfill(ctx.GetActor(), companyId, request.Reason, ctx.TraceIdentifier, ct), ct));
    }

    [HttpPost("/api/v1/companies/{companyId:guid}/reports/rebuild", Name = "RebuildFinancialReport")]
    [Authorize(Policy = Permissions.ReportManage)]
    public async Task<IActionResult> RebuildFinancialReport([FromRoute] Guid companyId, [FromBody] RetryMessageRequest request, [FromServices] IReportingService service, [FromServices] ICommandExecutor commands, CancellationToken ct)
    {
        var ctx = HttpContext;
        return Respond(await ApiCommands.Execute(ctx, commands, companyId, "reports.rebuild", request,
                () => service.Rebuild(ctx.GetActor(), companyId, request.Reason, ctx.TraceIdentifier, ct), ct));
    }
}
