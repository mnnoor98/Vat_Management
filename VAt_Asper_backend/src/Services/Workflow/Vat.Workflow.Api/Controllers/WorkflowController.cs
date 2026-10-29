using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vat.Api.Security;
using Vat.Application;
using Vat.Application.Security;

namespace Vat.Services.Workflow.Controllers;

[ApiController]
[Tags("Workflow")]
[Route("api/v1/companies/{companyId:guid}/workflow")]
public sealed class WorkflowController(WorkflowReadService service) : ControllerBase
{
    [HttpGet("tasks")]
    [Authorize(Policy = Permissions.WorkflowRead)]
    public async Task<IActionResult> List(Guid companyId, [FromQuery] Guid? before, [FromQuery] int? take, CancellationToken ct) =>
        Ok(await service.List(HttpContext.GetActor(), companyId, before, take ?? 25, ct));
}
