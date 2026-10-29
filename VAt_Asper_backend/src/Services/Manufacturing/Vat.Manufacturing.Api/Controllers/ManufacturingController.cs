using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vat.Api.Security;
using Vat.Application.Security;

namespace Vat.Services.Manufacturing.Controllers;

[ApiController]
[Tags("Manufacturing")]
[Route("api/v1/companies/{companyId:guid}/manufacturing")]
public sealed class ManufacturingController(ManufacturingReadService service, CoefficientService coefficients) : ControllerBase
{
    [HttpGet("production-orders")]
    [Authorize(Policy = Permissions.ManufacturingRead)]
    public async Task<IActionResult> List(Guid companyId, [FromQuery] Guid? before, [FromQuery] int? take, CancellationToken ct) =>
        Ok(await service.List(HttpContext.GetActor(), companyId, before, take ?? 25, ct));

    [HttpGet("coefficients")]
    [Authorize(Policy = Permissions.ManufacturingRead)]
    public async Task<IActionResult> GetCoefficients(Guid companyId, [FromQuery] Guid? before, [FromQuery] int? take, CancellationToken ct) =>
        Ok(await coefficients.List(HttpContext.GetActor(), companyId, before, take ?? 25, ct));

    [HttpPost("coefficients")]
    [Authorize(Policy = Permissions.ManufacturingRequest)]
    public async Task<IActionResult> CreateCoefficient(Guid companyId, [FromBody] CoefficientCreateRequest request, CancellationToken ct) =>
        Reply(await coefficients.Create(HttpContext.GetActor(), companyId, Request.Headers["Idempotency-Key"].ToString(), request,
            Request.Headers.Authorization.ToString(), HttpContext.TraceIdentifier, ct));

    [HttpPost("coefficients/{id:guid}/submit")]
    [Authorize(Policy = Permissions.ManufacturingRequest)]
    public Task<IActionResult> SubmitCoefficient(Guid companyId, Guid id, CoefficientActionRequest request, CancellationToken ct) =>
        Change(companyId, id, "submit", request, ct);

    [HttpPut("coefficients/{id:guid}/approve")]
    [Authorize(Policy = Permissions.ManufacturingApprove)]
    public Task<IActionResult> ApproveCoefficient(Guid companyId, Guid id, CoefficientActionRequest request, CancellationToken ct) =>
        Change(companyId, id, "approve", request, ct);

    [HttpPost("coefficients/{id:guid}/cancel")]
    [Authorize(Policy = Permissions.ManufacturingRequest)]
    public Task<IActionResult> CancelCoefficient(Guid companyId, Guid id, CoefficientActionRequest request, CancellationToken ct) =>
        Change(companyId, id, "cancel", request, ct);

    private async Task<IActionResult> Change(Guid companyId, Guid id, string action, CoefficientActionRequest request, CancellationToken ct) =>
        Reply(await coefficients.Transition(HttpContext.GetActor(), companyId, id, Request.Headers["Idempotency-Key"].ToString(), action,
            request, HttpContext.TraceIdentifier, ct));
    private ContentResult Reply(CoefficientResponse response)
    {
        Response.Headers["Idempotency-Replayed"] = response.Replayed ? "true" : "false";
        return new() { StatusCode = response.StatusCode, ContentType = "application/json", Content = response.Json };
    }
}
