using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vat.Api.Security;
using Vat.Application;
using Vat.Application.Security;
using Vat.Infrastructure.Persistence;

namespace Vat.Coordinated.Inventory.Controllers;

[ApiController]
[Tags("Inventory Costing")]
[Route("api/v1/companies/{companyId:guid}/inventory")]
public sealed class CostingController(CostingPolicyService service, ICommandExecutor commands) : ControllerBase
{
    [HttpPost("cost-adoptions")]
    [Authorize(Policy = Permissions.CostingManage)]
    public Task<IActionResult> CreateOpening(Guid companyId, CreateCostAdoptionRequest request, [FromServices] StockCostAdoptionService openings, CancellationToken ct) =>
        InventoryApiCommands.Execute(HttpContext, commands, companyId, "cost-opening.create", request,
            () => openings.Create(HttpContext.GetActor(), companyId, request, HttpContext.TraceIdentifier, ct), ct);
    [HttpPost("cost-adoptions/{id:guid}/approve")]
    [Authorize(Policy = Permissions.CostingApprove)]
    public Task<IActionResult> ApproveOpening(Guid companyId, Guid id, ApproveCostingPolicyRequest request, [FromServices] StockCostAdoptionService openings, CancellationToken ct) =>
        InventoryApiCommands.Execute(HttpContext, commands, companyId, $"cost-opening.approve:{id}", request,
            () => openings.Approve(HttpContext.GetActor(), companyId, id, request, HttpContext.TraceIdentifier, ct), ct);
    [HttpGet("cost-adoptions/{id:guid}")]
    [Authorize(Policy = Permissions.CostingRead)]
    public async Task<IActionResult> Opening(Guid companyId, Guid id, [FromServices] StockCostAdoptionService openings, CancellationToken ct) =>
        Ok(await openings.Get(HttpContext.GetActor(), companyId, id, ct));
    [HttpGet("cost-adoptions")]
    [Authorize(Policy = Permissions.CostingRead)]
    public async Task<IActionResult> Openings(Guid companyId, [FromServices] StockCostAdoptionService openings, CancellationToken ct, Guid? before = null, int take = 25) =>
        Ok(await openings.List(HttpContext.GetActor(), companyId, before, take, ct));
    [HttpGet("cost-preview")]
    [Authorize(Policy = Permissions.CostingRead)]
    public async Task<IActionResult> Preview(Guid companyId,
        [FromQuery, Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired] Guid warehouseId,
        [FromQuery, Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired] Guid productId,
        [FromServices] CostPreviewService preview, CancellationToken ct) =>
        Ok(await preview.Read(HttpContext.GetActor(), companyId, warehouseId, productId, ct));
    [HttpPost("costing-policies")]
    [Authorize(Policy = Permissions.CostingManage)]
    public Task<IActionResult> Create(Guid companyId, CreateCostingPolicyRequest request, CancellationToken ct) =>
        InventoryApiCommands.Execute(HttpContext, commands, companyId, "costing-policy.create", request,
            () => service.Create(HttpContext.GetActor(), companyId, request, HttpContext.TraceIdentifier, ct), ct);

    [HttpPost("costing-policies/{id:guid}/approve")]
    [Authorize(Policy = Permissions.CostingApprove)]
    public Task<IActionResult> Approve(Guid companyId, Guid id, ApproveCostingPolicyRequest request, CancellationToken ct) =>
        InventoryApiCommands.Execute(HttpContext, commands, companyId, $"costing-policy.approve:{id}", request,
            () => service.Approve(HttpContext.GetActor(), companyId, id, request, HttpContext.TraceIdentifier, ct), ct);

    [HttpGet("costing-policies")]
    [Authorize(Policy = Permissions.CostingRead)]
    public async Task<IActionResult> List(Guid companyId, CancellationToken ct, Guid? before = null, int take = 25) =>
        Ok((await service.List(HttpContext.GetActor(), companyId, before, take, ct)).Select(CostingPolicyView.From));

    [HttpGet("costing-policies/{id:guid}")]
    [Authorize(Policy = Permissions.CostingRead)]
    public async Task<IActionResult> Get(Guid companyId, Guid id, CancellationToken ct) =>
        Ok(CostingPolicyView.From(await service.Get(HttpContext.GetActor(), companyId, id, ct)));

    [HttpGet("costing-policies/resolve")]
    [Authorize(Policy = Permissions.CostingRead)]
    public async Task<IActionResult> Resolve(Guid companyId,
        [FromQuery, Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired] Guid productId,
        [FromQuery, Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired] DateOnly businessDate, CancellationToken ct)
    {
        var policy = await service.Resolve(HttpContext.GetActor(), companyId, productId, businessDate, ct);
        return policy is null ? NotFound(new { code = "cost_policy_unassigned" }) : Ok(CostingPolicyView.From(policy));
    }

    [HttpGet("purchase-cost-receipts")]
    [Authorize(Policy = Permissions.CostingRead)]
    public async Task<IActionResult> Receipts(Guid companyId, CancellationToken ct, Guid? before = null, int take = 25) =>
        Ok(await service.Receipts(HttpContext.GetActor(), companyId, before, take, ct));
}
