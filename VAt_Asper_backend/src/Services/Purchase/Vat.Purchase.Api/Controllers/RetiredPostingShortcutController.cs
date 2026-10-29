using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vat.Application.Security;

namespace Vat.Services.Purchase.Controllers;

[ApiController]
public sealed class RetiredPostingShortcutController : ControllerBase
{
    [HttpPost("api/v1/companies/{companyId:guid}/purchases/{purchaseId:guid}/post-ledger")]
    [Authorize(Policy = Permissions.PurchasePost)]
    public IActionResult Retired() => Problem(statusCode: 410, title: "posting_shortcut_retired", detail: "Use posting-plans and the versioned post action with the authenticated maker/checker.");
}
