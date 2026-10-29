using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Vat.Services.Identity.Controllers;

[ApiController]
public sealed class RetiredDevelopmentAuthController : ControllerBase
{
    [HttpGet("api/v1/auth/dev-token")]
    [AllowAnonymous]
    public IActionResult Retired() => Problem(statusCode: 410, title: "development_token_shortcut_retired",
        detail: "Obtain an access token from the configured OIDC provider.");
}
