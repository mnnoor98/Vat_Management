using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vat.Api.Security;
using Vat.Application;
using Vat.Application.Security;
using Vat.Infrastructure.Persistence;
namespace Vat.Coordinated.Inventory.Controllers;
[ApiController,Tags("Production cost execution")]
[Route("api/v1/companies/{companyId:guid}/inventory/production-executions")]
public sealed class ProductionExecutionController(ProductionCostService service,ICommandExecutor commands):ControllerBase
{
    [HttpPost,Authorize(Policy=Permissions.ManufacturingRequest),Authorize(Policy=Permissions.CostingManage)]
    public Task<IActionResult> Import(Guid companyId,ImportProductionRequest request,CancellationToken ct)=>Run(companyId,"production.import",request,()=>service.Import(HttpContext.GetActor(),companyId,request,Request.Headers.Authorization.ToString(),HttpContext.TraceIdentifier,ct),ct);
    [HttpGet,Authorize(Policy=Permissions.ManufacturingRead),Authorize(Policy=Permissions.CostingRead)]
    public async Task<IActionResult> List(Guid companyId,CancellationToken ct,Guid? before=null,int take=25)=>Ok(await service.List(HttpContext.GetActor(),companyId,before,take,ct));
    [HttpGet("{id:guid}"),Authorize(Policy=Permissions.ManufacturingRead),Authorize(Policy=Permissions.CostingRead)]
    public async Task<IActionResult> Get(Guid companyId,Guid id,CancellationToken ct)=>Ok(await service.Get(HttpContext.GetActor(),companyId,id,ct));
    [HttpPost("{id:guid}/operations"),Authorize(Policy=Permissions.ManufacturingRequest),Authorize(Policy=Permissions.CostingManage)]
    public Task<IActionResult> Create(Guid companyId,Guid id,CreateProductionOperationRequest request,CancellationToken ct)=>Run(companyId,$"production.operation.create:{id}",request,()=>service.Create(HttpContext.GetActor(),companyId,id,request,HttpContext.TraceIdentifier,ct),ct);
    [HttpPost("{id:guid}/operations/{operation:guid}/approve"),Authorize(Policy=Permissions.ManufacturingApprove),Authorize(Policy=Permissions.CostingApprove),Authorize(Policy=Permissions.InventoryPost)]
    public Task<IActionResult> Approve(Guid companyId,Guid id,Guid operation,ApproveProductionOperationRequest request,CancellationToken ct)=>Run(companyId,$"production.operation.approve:{operation}",request,()=>service.Approve(HttpContext.GetActor(),companyId,id,operation,request,HttpContext.TraceIdentifier,ct),ct);
    [HttpPost("{id:guid}/operations/{operation:guid}/cancel"),Authorize(Policy=Permissions.ManufacturingRequest),Authorize(Policy=Permissions.CostingManage)]
    public Task<IActionResult> Cancel(Guid companyId,Guid id,Guid operation,CancelProductionOperationRequest request,CancellationToken ct)=>Run(companyId,$"production.operation.cancel:{operation}",request,()=>service.Cancel(HttpContext.GetActor(),companyId,id,operation,request,HttpContext.TraceIdentifier,ct),ct);
    [HttpGet("{id:guid}/operations"),Authorize(Policy=Permissions.ManufacturingRead),Authorize(Policy=Permissions.CostingRead)]
    public async Task<IActionResult> Operations(Guid companyId,Guid id,CancellationToken ct,Guid? before=null,int take=25)=>Ok(await service.Operations(HttpContext.GetActor(),companyId,id,before,take,ct));
    [HttpGet("{id:guid}/operations/{operation:guid}"),Authorize(Policy=Permissions.ManufacturingRead),Authorize(Policy=Permissions.CostingRead)]
    public async Task<IActionResult> Operation(Guid companyId,Guid id,Guid operation,CancellationToken ct)=>Ok(await service.GetOperation(HttpContext.GetActor(),companyId,id,operation,ct));
    private Task<IActionResult> Run(Guid company,string action,object body,Func<Task<CommandOutcome>> command,CancellationToken ct)=>InventoryApiCommands.Execute(HttpContext,commands,company,action,body,command,ct);
}
