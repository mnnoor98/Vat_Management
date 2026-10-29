using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vat.Api.Security;
using Vat.Application;
using Vat.Application.Security;

namespace Vat.Services.Identity.Controllers;

[ApiController]
[Tags("Identity")]
[Route("api/v1/companies/{companyId:guid}/identity")]
public sealed class IdentityController(IdentityReadService service) : ControllerBase
{
    [HttpGet("roles")]
    [Authorize(Policy = Permissions.IdentityRead)]
    public async Task<IActionResult> List(Guid companyId, [FromQuery] Guid? before, [FromQuery] int? take, CancellationToken ct) =>
        Ok(await service.List(HttpContext.GetActor(), companyId, before, take ?? 25, ct));
}
