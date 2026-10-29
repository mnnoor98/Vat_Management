using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vat.Api.Security;

namespace Vat.Services.Identity.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/identity")]
public sealed class ProfileController : ControllerBase
{
    [HttpGet("me")]
    public IActionResult Me()
    {
        var actor = HttpContext.GetActor();
        return Ok(new { actor.Subject, actor.TenantId, actor.CompanyIds, Permissions = actor.Permissions.Order().ToArray() });
    }
}
