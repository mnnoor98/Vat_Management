using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vat.Application.Security;

namespace Vat.Services.Sales.Controllers;

[ApiController]
public sealed class RetiredPostingShortcutController : ControllerBase
{
    [HttpPost("api/v1/companies/{companyId:guid}/sales/{id:guid}/post-ledger")]
    [Authorize(Policy = Permissions.SalesPost)]
    public IActionResult Retired() => Problem(statusCode: 410, title: "posting_shortcut_retired", detail: "Use reserve and the versioned post action with the authenticated maker/checker.");
}
