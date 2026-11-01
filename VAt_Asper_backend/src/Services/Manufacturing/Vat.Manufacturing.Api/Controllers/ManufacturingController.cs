using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vat.Api.Security;
using Vat.Application.Security;

namespace Vat.Services.Manufacturing.Controllers;

[ApiController]
[Tags("Manufacturing")]
[Route("api/v1/companies/{companyId:guid}/manufacturing")]
public sealed class ManufacturingController(ManufacturingReadService service, CoefficientService coefficients,IProductionOrderWorkflow orders) : ControllerBase
{
    [HttpPost("production-orders"),Authorize(Policy=Permissions.ManufacturingRequest)]
    public async Task<IActionResult> CreateOrder(Guid companyId,CreateProductionOrderRequest request,CancellationToken ct)=>Reply(await orders.Create(HttpContext.GetActor(),companyId,Key(),request,Request.Headers.Authorization.ToString(),HttpContext.TraceIdentifier,ct));
    [HttpGet("production-orders/{id:guid}"),Authorize(Policy=Permissions.ManufacturingRead)]
    public async Task<IActionResult> Order(Guid companyId,Guid id,CancellationToken ct)=>Ok(await orders.Get(HttpContext.GetActor(),companyId,id,ct));
    [HttpGet("production-orders/{id:guid}/execution-source"),Authorize(Policy=Permissions.ManufacturingRead)]
    public async Task<IActionResult> OrderSource(Guid companyId,Guid id,CancellationToken ct)=>Ok(await orders.Source(HttpContext.GetActor(),companyId,id,ct));
    [HttpPost("production-orders/{id:guid}/submit"),Authorize(Policy=Permissions.ManufacturingRequest)]
    public Task<IActionResult> SubmitOrder(Guid companyId,Guid id,CoefficientActionRequest request,CancellationToken ct)=>OrderChange(companyId,id,"submit",request,ct);
    [HttpPost("production-orders/{id:guid}/approve"),Authorize(Policy=Permissions.ManufacturingApprove)]
    public Task<IActionResult> ApproveOrder(Guid companyId,Guid id,CoefficientActionRequest request,CancellationToken ct)=>OrderChange(companyId,id,"approve",request,ct);
    [HttpPost("production-orders/{id:guid}/cancel"),Authorize(Policy=Permissions.ManufacturingRequest)]
    public Task<IActionResult> CancelOrder(Guid companyId,Guid id,CoefficientActionRequest request,CancellationToken ct)=>OrderChange(companyId,id,"cancel",request,ct);
    private async Task<IActionResult> OrderChange(Guid companyId,Guid id,string action,CoefficientActionRequest request,CancellationToken ct)=>Reply(await orders.Transition(HttpContext.GetActor(),companyId,id,Key(),action,request,HttpContext.TraceIdentifier,ct));
    private Guid Key()=>Guid.TryParse(Request.Headers["Idempotency-Key"],out var key)&&key!=Guid.Empty?key:throw new Vat.Domain.DomainException("idempotency_key_required","Require a nonempty UUID idempotency key.",400);
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
