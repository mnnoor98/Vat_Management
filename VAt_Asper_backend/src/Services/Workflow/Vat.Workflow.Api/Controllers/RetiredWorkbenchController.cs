using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vat.Application.Security;

namespace Vat.Services.Workflow.Controllers;

[ApiController]
[Authorize(Policy = Permissions.WorkflowRead)]
public sealed class RetiredWorkbenchController : ControllerBase
{
    [HttpGet("api/v1/companies/{companyId:guid}/workflows/my-tasks")]
    [HttpPost("api/v1/companies/{companyId:guid}/workflows/tasks/{id}/approve")]
    [HttpPost("api/v1/companies/{companyId:guid}/workflows/tasks/{id}/reject")]
    public IActionResult Retired() => Problem(statusCode: 410, title: "workbench_adapter_retired",
        detail: "Read workflow/tasks for workflow records. Approve or reject source documents through their owning API with explicit permissions and ExpectedVersion.");
}
