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
        return Ok(new { actor.Subject, actor.TenantId, actor.CompanyIds, Permissions = actor.Permissions.Order().ToArray(),
            Roles = User.FindAll("role").Concat(User.FindAll("roles")).Concat(User.FindAll(System.Security.Claims.ClaimTypes.Role)).Select(c => c.Value).Distinct().ToArray(),
            Username = User.FindFirst("preferred_username")?.Value,
            DisplayName = User.FindFirst("name")?.Value,
            AssignedModuleCodes = User.HasClaim("local_account", "true") ? User.FindAll("assigned_module").Select(c => c.Value).ToArray() : null,
            BranchId = User.FindFirst("branch_id")?.Value,
            Email = User.FindFirst("email")?.Value });
    }
}
