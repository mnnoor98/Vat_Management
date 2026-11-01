using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vat.Api.Security;
using Vat.Application.Security;
using Vat.Services.Compliance.Infrastructure;

namespace Vat.Services.Compliance.Controllers;

[ApiController]
[Tags("Compliance")]
[Route("api/v1/companies/{companyId:guid}/compliance")]
public sealed class ComplianceController(ComplianceReadService service) : ControllerBase
{
    [HttpGet("returns")]
    [Authorize(Policy = Permissions.ComplianceRead)]
    public async Task<IActionResult> List(Guid companyId, [FromQuery] Guid? before, [FromQuery] int? take, CancellationToken ct) =>
        Ok(await service.List(HttpContext.GetActor(), companyId, before, take ?? 25, ct));

    [HttpGet("returns/9-1")]
    [HttpGet("vds-certificates")]
    [HttpGet("vat-ledger")]
    [HttpGet("adjustments")]
    [Authorize(Policy = Permissions.ComplianceRead)]
    public IActionResult UnavailableCalculation(Guid companyId) => Unavailable(companyId);

    [HttpPost("returns/9-1/submit")]
    [Authorize(Policy = Permissions.ComplianceSubmit)]
    public IActionResult SubmitReturn(Guid companyId) => Unavailable(companyId);

    [HttpPost("vds-certificates")]
    [HttpPost("vds-certificates/{id:guid}/deposit")]
    [HttpPost("vat-ledger/deposit")]
    [HttpPost("credit-notes")]
    [HttpPost("debit-notes")]
    [Authorize(Policy = Permissions.ComplianceManage)]
    public IActionResult UnavailableWrite(Guid companyId) => Unavailable(companyId);

    private IActionResult Unavailable(Guid companyId)
    {
        HttpContext.GetActor().RequireCompany(companyId);
        return Problem(statusCode: 503, title: "Compliance execution unavailable",
            detail: "Validated calculation, approval and filing contracts are required before this operation can execute.",
            extensions: new Dictionary<string, object?> { ["code"] = "compliance_execution_unavailable" });
    }
}
