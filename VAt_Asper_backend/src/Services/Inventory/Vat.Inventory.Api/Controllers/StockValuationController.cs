using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vat.Api.Security;
using Vat.Application;
using Vat.Application.Security;
using Vat.Infrastructure.Persistence;

namespace Vat.Coordinated.Inventory.Controllers;

[ApiController, Tags("Inventory Valuation")]
[Route("api/v1/companies/{companyId:guid}/inventory")]
public sealed class StockValuationController(LandedCostService landed, ImportDeclarationService imports, ExportShipmentService exports, StockValuationService valuation, ICommandExecutor commands): ControllerBase
{
    [HttpPost("export-shipments"), Authorize(Policy=Permissions.CostingManage)]
    public Task<IActionResult> CreateExport(Guid companyId,CreateExportShipmentRequest request,CancellationToken ct)=>InventoryApiCommands.Execute(HttpContext,commands,companyId,"export-shipment.create",request,()=>exports.Create(HttpContext.GetActor(),companyId,request,HttpContext.TraceIdentifier,ct),ct);
    [HttpPost("export-shipments/{id:guid}/approve"), Authorize(Policy=Permissions.CostingApprove)]
    public Task<IActionResult> ApproveExport(Guid companyId,Guid id,ApproveCostingPolicyRequest request,CancellationToken ct)=>InventoryApiCommands.Execute(HttpContext,commands,companyId,$"export-shipment.approve:{id}",request,()=>exports.Approve(HttpContext.GetActor(),companyId,id,request,HttpContext.TraceIdentifier,ct),ct);
    [HttpGet("export-shipments/{id:guid}"), Authorize(Policy=Permissions.CostingRead)] public async Task<IActionResult> GetExport(Guid companyId,Guid id,CancellationToken ct)=>Ok(await exports.Get(HttpContext.GetActor(),companyId,id,ct));
    [HttpGet("export-shipments"), Authorize(Policy=Permissions.CostingRead)] public async Task<IActionResult> ListExports(Guid companyId,CancellationToken ct,Guid? before=null,int take=25)=>Ok(await exports.List(HttpContext.GetActor(),companyId,before,take,ct));
    [HttpGet("export-shipment-sources"), Authorize(Policy=Permissions.CostingRead)] public async Task<IActionResult> ExportSources(Guid companyId,CancellationToken ct)=>Ok(await exports.Sources(HttpContext.GetActor(),companyId,ct));
    [HttpPost("import-declarations"), Authorize(Policy=Permissions.CostingManage)]
    public Task<IActionResult> CreateImport(Guid companyId,CreateImportDeclarationRequest request,CancellationToken ct)=>
        InventoryApiCommands.Execute(HttpContext,commands,companyId,"import-declaration.create",request,()=>imports.Create(HttpContext.GetActor(),companyId,request,HttpContext.TraceIdentifier,ct),ct);
    [HttpPost("import-declarations/{id:guid}/approve"), Authorize(Policy=Permissions.CostingApprove)]
    public Task<IActionResult> ApproveImport(Guid companyId,Guid id,ApproveCostingPolicyRequest request,CancellationToken ct)=>
        InventoryApiCommands.Execute(HttpContext,commands,companyId,$"import-declaration.approve:{id}",request,()=>imports.Approve(HttpContext.GetActor(),companyId,id,request,HttpContext.TraceIdentifier,ct),ct);
    [HttpGet("import-declarations/{id:guid}"), Authorize(Policy=Permissions.CostingRead)]
    public async Task<IActionResult> GetImport(Guid companyId,Guid id,CancellationToken ct)=>Ok(await imports.Get(HttpContext.GetActor(),companyId,id,ct));
    [HttpGet("import-declarations"), Authorize(Policy=Permissions.CostingRead)]
    public async Task<IActionResult> ListImports(Guid companyId,CancellationToken ct,Guid? before=null,int take=25)=>Ok(await imports.List(HttpContext.GetActor(),companyId,before,take,ct));
    [HttpGet("landed-cost-sources/{id:guid}"), Authorize(Policy=Permissions.CostingRead)]
    public async Task<IActionResult> Source(Guid companyId,Guid id,[FromServices] StockCostPreparationQueries queries,CancellationToken ct)
    {
        var rows=await queries.Sources(HttpContext.GetActor(),companyId,null,ct,id);
        return rows.Count==0?NotFound():Ok(rows[0]);
    }
    [HttpGet("cost-opening-positions"), Authorize(Policy=Permissions.CostingRead)]
    public async Task<IActionResult> OpeningPositions(Guid companyId,[FromServices] StockCostPreparationQueries queries,CancellationToken ct,int skip=0)=>Ok(await queries.Positions(HttpContext.GetActor(),companyId,skip,ct));
    [HttpGet("landed-cost-sources"), Authorize(Policy=Permissions.CostingRead)]
    public async Task<IActionResult> Sources(Guid companyId,[FromServices] StockCostPreparationQueries queries,CancellationToken ct,Guid? before=null)=>Ok(await queries.Sources(HttpContext.GetActor(),companyId,before,ct));
    [HttpGet("cost-ledger/status"), Authorize(Policy=Permissions.CostingRead)]
    public async Task<IActionResult> Status(Guid companyId,CancellationToken ct)=>Ok(await valuation.Status(HttpContext.GetActor(),companyId,ct));
    [HttpGet("cost-ledger/entries"), Authorize(Policy=Permissions.CostingRead)]
    public async Task<IActionResult> Entries(Guid companyId,CancellationToken ct,long? before=null,int take=25)=>Ok(await valuation.Journal(HttpContext.GetActor(),companyId,before,take,ct));
    [HttpPost("landed-cost-reviews"), Authorize(Policy=Permissions.CostingManage)]
    public Task<IActionResult> Create(Guid companyId,CreateLandedCostRequest request,CancellationToken ct)=>
        InventoryApiCommands.Execute(HttpContext,commands,companyId,"landed-cost.create",request,()=>landed.Create(HttpContext.GetActor(),companyId,request,HttpContext.TraceIdentifier,ct),ct);
    [HttpPost("landed-cost-reviews/{id:guid}/approve"), Authorize(Policy=Permissions.CostingApprove)]
    public Task<IActionResult> Approve(Guid companyId,Guid id,ApproveCostingPolicyRequest request,CancellationToken ct)=>
        InventoryApiCommands.Execute(HttpContext,commands,companyId,$"landed-cost.approve:{id}",request,()=>landed.Approve(HttpContext.GetActor(),companyId,id,request,HttpContext.TraceIdentifier,ct),ct);
    [HttpGet("landed-cost-reviews/{id:guid}"), Authorize(Policy=Permissions.CostingRead)]
    public async Task<IActionResult> Get(Guid companyId,Guid id,CancellationToken ct)=>Ok(await landed.Get(HttpContext.GetActor(),companyId,id,ct));
    [HttpGet("landed-cost-reviews"), Authorize(Policy=Permissions.CostingRead)]
    public async Task<IActionResult> List(Guid companyId,CancellationToken ct,Guid? before=null,int take=25)=>Ok(await landed.List(HttpContext.GetActor(),companyId,before,take,ct));
}
