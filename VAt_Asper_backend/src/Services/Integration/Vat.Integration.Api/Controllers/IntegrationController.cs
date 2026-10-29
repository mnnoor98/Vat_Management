using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vat.Api.Security;
using Vat.Application;
using Vat.Application.Security;

namespace Vat.Services.Integration.Controllers;

[ApiController]
[Tags("Integration")]
[Route("api/v1/companies/{companyId:guid}/integration")]
public sealed class IntegrationController(IntegrationReadService service) : ControllerBase
{
    [HttpGet("connections")]
    [Authorize(Policy = Permissions.IntegrationRead)]
    public async Task<IActionResult> List(Guid companyId, [FromQuery] Guid? before, [FromQuery] int? take, CancellationToken ct) =>
        Ok(await service.List(HttpContext.GetActor(), companyId, before, take ?? 25, ct));
}
