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
public sealed class CustomerReportingController : VatControllerBase
{
    [HttpGet("/api/v1/companies/{companyId:guid}/customers/{customerId:guid}/statement")]
    [Authorize(Policy = Permissions.ReportRead)]
    [ProducesResponseType(typeof(CustomerStatement), 200)]
    public async Task<IActionResult> Statement([FromRoute] Guid companyId, [FromRoute] Guid customerId, [FromQuery, Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired] DateOnly from, [FromQuery, Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired] DateOnly until, [FromServices] CustomerReportingService service, CancellationToken ct)
    {
        var ctx = HttpContext;
        return Respond(await service.Statement(ctx.GetActor(), companyId, customerId, from, until, ct));
    }

    [HttpGet("/api/v1/companies/{companyId:guid}/customers/{customerId:guid}/ageing")]
    [Authorize(Policy = Permissions.ReportRead)]
    [ProducesResponseType(typeof(CustomerAgeing), 200)]
    public async Task<IActionResult> Ageing([FromRoute] Guid companyId, [FromRoute] Guid customerId, [FromQuery, Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired] DateOnly asOf, [FromServices] CustomerReportingService service, CancellationToken ct)
    {
        var ctx = HttpContext;
        return Respond(await service.Ageing(ctx.GetActor(), companyId, customerId, asOf, ct));
    }
}
