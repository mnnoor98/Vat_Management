using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vat.Application;
using Vat.Application.Security;
using Vat.Contracts.Manufacturing;
using Vat.Domain;
using Vat.Domain.Catalog;

namespace Vat.Infrastructure.Persistence;
public sealed record ImportProductionRequest(Guid OrderId);
public sealed record ProductionLineInput(Guid WarehouseId,Guid ProductId,string Quantity,Guid? OriginalIssueId=null,Guid? Id=null,string? UnitOfMeasure=null,string? InputQuantity=null);
public sealed record CancelProductionOperationRequest(int ExpectedVersion,string Reason);
public sealed record ProductionLine(Guid Id,Guid WarehouseId,Guid ProductId,decimal Quantity,Guid? OriginalIssueId,decimal? InputQuantity=null,string? InputUnitOfMeasure=null);
public sealed record CreateProductionOperationRequest(int ExpectedSessionVersion,string Kind,DateOnly BusinessDate,string Evidence,IReadOnlyList<ProductionLineInput> Lines,
    string Amount="0",string OutputQuantity="0",string Labor="0",string Utility="0",string Overhead="0",Guid? ReversesOperationId=null,string? OutputUnitOfMeasure=null,Guid? OutputProductId=null,Guid? OutputWarehouseId=null);
public sealed record ApproveProductionOperationRequest(int ExpectedVersion,int ExpectedSessionVersion);
public sealed record ProductionOperationView(Guid Id,Guid SessionId,string Kind,DateOnly BusinessDate,string State,int Version,int SessionVersion,string Evidence,
    IReadOnlyList<ProductionLineInput> Lines,string Amount,string OutputQuantity,string Labor,string Utility,string Overhead,string PostedValue,string CreatedBy,string? ApprovedBy,Guid? ReversesOperationId=null,string? OutputUnitOfMeasure=null,string? OutputInputQuantity=null,Guid? OutputProductId=null,Guid? OutputWarehouseId=null);
public sealed record ProductionSessionView(Guid Id,Guid CompanyId,Guid OrderId,string Reference,string State,int Version,string WipValue,string ReceivedQuantity,string PlannedQuantity,ProductionOrderEvidence Source);
public sealed class ProductionCostService(VatDbContext db,IProductionExecutionReferences references,IPeriodGuard periods,IPeriodStore periodStore,
    IAuditWriter audit,IEventWriter events,PostingPolicy policy,TimeProvider clock)
{
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
    public async Task<CommandOutcome> Import(Actor actor,Guid company,ImportProductionRequest request,string bearer,string correlation,CancellationToken ct)
    {
        Require(actor,company,Permissions.ManufacturingRequest,Permissions.CostingManage);Guard.Id(request.OrderId,"Approved production order");
        var source=await references.Order(actor,company,request.OrderId,bearer,ct);ProductionExecutionReferenceClient.Validate(source,actor,company,request.OrderId);
        await periods.RequireOpen(actor.TenantId,company,source.Evidence.BusinessDate,ct);
        var ledger=await StockCostLedger.Begin(db,actor.TenantId,company,clock.GetUtcNow(),ct)??throw new DomainException("cost_opening_required","Approve stock cost adoption before production execution.",409);
        if(await db.Set<ProductionCostSession>().AnyAsync(x=>x.TenantId==actor.TenantId&&x.CompanyId==company&&x.OrderId==request.OrderId,ct))throw new DomainException("production_execution_exists","This owner order already has a financial execution; reload it rather than importing again.",409);
        await VerifyProducts(actor,company,source,ct);
        var json=JsonSerializer.Serialize(source,Json);var h=new ProductionCostSession{TenantId=actor.TenantId,CompanyId=company,Id=Guid.CreateVersion7(),OrderId=request.OrderId,Reference=source.Reference,
            SourceJson=json,SourceSha256=Hash(source),CreatedBy=actor.Subject,CreatedAt=clock.GetUtcNow()};db.Add(h);
        audit.Append(actor,company,h.Id,"ProductionExecutionAccepted",correlation,1);events.Append(actor.TenantId,company,h.Id,1,nameof(ProductionExecutionAcceptedV1),correlation,new ProductionExecutionAcceptedV1(h.Id,h.OrderId,h.SourceSha256));
        return new(201,View(h),$"/api/v1/companies/{company}/inventory/production-executions/{h.Id}");
    }
    public async Task<CommandOutcome> Create(Actor actor,Guid company,Guid session,CreateProductionOperationRequest r,string correlation,CancellationToken ct)
    {
        Require(actor,company,Permissions.ManufacturingRequest,Permissions.CostingManage);Guard.Text(r.Evidence,"Actual production/charge/variance evidence",1000);
        if(r.BusinessDate==default||r.Kind is not("Issue" or "Return" or "Conversion" or "Receipt" or "ByProduct" or "Loss" or "Close" or "Reverse")||r.Lines is null||r.Lines.Count>100||r.Lines.Any(x=>x is null))throw new DomainException("validation_failed","Require a date, supported production operation and at most 100 actual material lines.");
        await periods.RequireOpen(actor.TenantId,company,r.BusinessDate,ct);await CostingStore.LockScope(db,actor.TenantId,company,false,ct);var h=await Find(actor,company,session,ct);Open(h,r.ExpectedSessionVersion);
        var source=Source(h);if(r.BusinessDate<source.Evidence.BusinessDate)throw new DomainException("production_date_invalid","Operation cannot predate the approved production order.");
        var lines=r.Lines.Select(x=>{
            var product=source.Evidence.Materials.SingleOrDefault(m=>m.Product.Id==x.ProductId)?.Product??throw new DomainException("production_material_scope","Require a BOM material.",404);
            var input=Number(x.Quantity,6,true);var unit=x.UnitOfMeasure is null?null:ProductionUnits.Unit(x.UnitOfMeasure);
            return new ProductionLine(Guid.CreateVersion7(),x.WarehouseId,x.ProductId,ProductionUnits.ToStock(source.Evidence,product,input,unit),x.OriginalIssueId,unit is null?null:input,unit);
        }).ToArray();
        var amount=Number(r.Amount,4);var output=Number(r.OutputQuantity,6);decimal? outputInput=null;string? outputUnit=null;ProductionProduct? outputProduct=null;
        if(r.Kind=="Receipt"){if(r.OutputProductId is not null||r.OutputWarehouseId is not null)throw new DomainException("validation_failed","Finished receipt product and warehouse come from the approved order.");outputProduct=source.Evidence.FinishedProduct;}
        if(r.Kind=="ByProduct"){if(r.OutputProductId is null||r.OutputWarehouseId is null)throw new DomainException("validation_failed","By-product receipt requires an approved product and company warehouse.");outputProduct=source.Evidence.ByProducts?.SingleOrDefault(x=>x.Product.Id==r.OutputProductId)?.Product??throw new DomainException("production_byproduct_scope","Require a by-product/scrap product frozen on the approved order.",404);if(!await db.Set<Warehouse>().AnyAsync(x=>x.TenantId==actor.TenantId&&x.CompanyId==company&&x.Id==r.OutputWarehouseId,ct))throw new DomainException("production_byproduct_scope","Require a company warehouse.",404);}
        if(r.OutputUnitOfMeasure is not null){if(outputProduct is null)throw new DomainException("validation_failed","Only approved production outputs may specify an input unit.");outputUnit=ProductionUnits.Unit(r.OutputUnitOfMeasure);outputInput=output;output=ProductionUnits.ToStock(source.Evidence,outputProduct,output,outputUnit);}var labor=Number(r.Labor,4);var utility=Number(r.Utility,4);var overhead=Number(r.Overhead,4);
        if(r.Kind is "Issue" or "Return")
        {
            if(lines.Length==0||amount!=0||output!=0||labor+utility+overhead!=0||lines.Select(x=>(x.WarehouseId,x.ProductId,x.OriginalIssueId)).Distinct().Count()!=lines.Length)throw new DomainException("validation_failed","Material operations require unique actual material lines only.");
            foreach(var l in lines){if(!source.Evidence.Materials.Any(x=>x.Product.Id==l.ProductId)||!await db.Set<Warehouse>().AnyAsync(x=>x.TenantId==actor.TenantId&&x.CompanyId==company&&x.Id==l.WarehouseId,ct))throw new DomainException("production_material_scope","Require a BOM material and a warehouse in this company.",404);
                if((r.Kind=="Issue"&&l.OriginalIssueId!=null)||(r.Kind=="Return"&&l.OriginalIssueId==null))throw new DomainException("production_return_source_required","Returns must identify their original production issue; issues cannot specify a return source.");}
        }
        else if(lines.Length!=0)throw new DomainException("validation_failed","Only material operations have material lines.");
        if(r.Kind=="Conversion"&&(amount!=0||output!=0||labor+utility+overhead<=0))throw new DomainException("validation_failed","Conversion requires explicit positive labor/utility/overhead charges only.");
        if(r.Kind=="Receipt"&&(output<=0||labor+utility+overhead!=0))throw new DomainException("validation_failed","Receipt requires actual output quantity and the reviewed WIP value to release.");
        if(r.Kind=="ByProduct"&&(output<=0||amount<=0||labor+utility+overhead!=0))throw new DomainException("validation_failed","By-product receipt requires a positive quantity and reviewed WIP value allocation.");
        if(r.Kind=="Loss"&&(amount<=0||output!=0||labor+utility+overhead!=0))throw new DomainException("validation_failed","WIP loss requires a positive reviewed loss amount only.");
        if(r.Kind=="Close"&&(amount!=0||output!=0||labor+utility+overhead!=0))throw new DomainException("validation_failed","Close carries no stock or charge amount.");
        if((r.Kind=="Reverse")!=(r.ReversesOperationId is not null))throw new DomainException("validation_failed","Only reversals must identify an original posted operation.");
        if(r.Kind=="Reverse"){
            if(amount!=0||labor+utility+overhead!=0)throw new DomainException("validation_failed","Reversal values come exclusively from the original posted source.");
            var original=await ReversalSource(actor,company,session,r.ReversesOperationId!.Value,r.BusinessDate,ct);
            if(original.Kind is "Receipt" or "ByProduct"){
                var remaining=await OutputRemaining(actor,company,original,ct);if(output==0)output=remaining.Quantity;
                if(output<=0||output>remaining.Quantity)throw new DomainException("production_reversal_quantity_invalid","Output correction quantity must not exceed the unreversed original quantity.",409);
            }
            else if(output!=0)throw new DomainException("validation_failed","Only production-output reversals may specify a correction quantity.");
        }
        var o=new ProductionCostOperation{TenantId=actor.TenantId,CompanyId=company,Id=Guid.CreateVersion7(),SessionId=session,SessionVersion=h.Version,Kind=r.Kind,BusinessDate=r.BusinessDate,Evidence=r.Evidence.Trim(),
            Lines=JsonSerializer.Serialize(lines,Json),Amount=amount,OutputQuantity=output,Labor=labor,Utility=utility,Overhead=overhead,OutputInputQuantity=outputInput,OutputUnitOfMeasure=outputUnit,OutputProductId=r.OutputProductId,OutputWarehouseId=r.OutputWarehouseId,ReversesOperationId=r.ReversesOperationId,CreatedBy=actor.Subject,CreatedAt=clock.GetUtcNow()};db.Add(o);
        audit.Append(actor,company,o.Id,"ProductionOperationCreated",correlation,1);return new(201,View(o),$"/api/v1/companies/{company}/inventory/production-executions/{session}/operations/{o.Id}");
    }
    public async Task<CommandOutcome> Approve(Actor actor,Guid company,Guid session,Guid id,ApproveProductionOperationRequest r,string correlation,CancellationToken ct)
    {
        Require(actor,company,Permissions.ManufacturingApprove,Permissions.CostingApprove,Permissions.InventoryPost);
        if(!policy.Enabled)throw new DomainException("posting_disabled","Financial posting is disabled.",503);
        await periodStore.LockCompany(actor.TenantId,company,false,ct);await CostingStore.LockScope(db,actor.TenantId,company,false,ct);
        var h=await Find(actor,company,session,ct);Open(h,r.ExpectedSessionVersion);var o=await Operation(actor,company,session,id,ct);
        if(o.State!="Draft"||o.Version!=r.ExpectedVersion||o.SessionVersion!=h.Version)throw new DomainException("version_conflict","Reload execution; this draft or WIP version is stale.",409);
        if(o.CreatedBy==actor.Subject)throw new DomainException("self_approval","A different user must approve actual production posting.",403);
        await periods.RequireOpen(actor.TenantId,company,o.BusinessDate,ct);var source=Source(h);await VerifyProducts(actor,company,source,ct);
        var ledger=await StockCostLedger.Begin(db,actor.TenantId,company,clock.GetUtcNow(),ct)??throw new DomainException("cost_opening_required","Approved cost ledger required.",409);
        var lines=JsonSerializer.Deserialize<ProductionLine[]>(o.Lines,Json)!;
        foreach(var line in lines){var product=source.Evidence.Materials.Single(x=>x.Product.Id==line.ProductId).Product;if((line.InputQuantity is null)!=(line.InputUnitOfMeasure is null)||ProductionUnits.ToStock(source.Evidence,product,line.InputQuantity??line.Quantity,line.InputUnitOfMeasure)!=line.Quantity)throw new DomainException("production_uom_changed","Reviewed material conversion evidence does not reconcile.",409);}
        var reviewedOutput=o.Kind=="Receipt"?source.Evidence.FinishedProduct:o.Kind=="ByProduct"?source.Evidence.ByProducts?.SingleOrDefault(x=>x.Product.Id==o.OutputProductId)?.Product:null;
        if((o.OutputInputQuantity is null)!=(o.OutputUnitOfMeasure is null)||(o.OutputUnitOfMeasure is not null&&(reviewedOutput is null||ProductionUnits.ToStock(source.Evidence,reviewedOutput,o.OutputInputQuantity!.Value,o.OutputUnitOfMeasure)!=o.OutputQuantity)))throw new DomainException("production_uom_changed","Reviewed output conversion evidence does not reconcile.",409);
        decimal actual=0;decimal wipDelta=0;
        if(o.Kind is "Issue" or "Return")foreach(var l in lines.OrderBy(x=>x.WarehouseId).ThenBy(x=>x.ProductId).ThenBy(x=>x.Id)){
            var m=new ProductionStockMovement{TenantId=actor.TenantId,CompanyId=company,Id=l.Id,SessionId=session,OperationId=o.Id,Kind=o.Kind,WarehouseId=l.WarehouseId,ProductId=l.ProductId,Quantity=o.Kind=="Issue"?-l.Quantity:l.Quantity,OriginalIssueId=l.OriginalIssueId,BusinessDate=o.BusinessDate};
            if(o.Kind=="Return"){
                var original=await db.Set<ProductionStockMovement>().AsNoTracking().SingleOrDefaultAsync(x=>x.TenantId==actor.TenantId&&x.CompanyId==company&&x.SessionId==session&&x.Id==l.OriginalIssueId&&x.Kind=="Issue",ct);
                if(original is null||original.WarehouseId!=l.WarehouseId||original.ProductId!=l.ProductId||original.BusinessDate>o.BusinessDate)throw new DomainException("production_return_source_invalid","Return must reference an earlier issue of this execution and warehouse/product.",409);
            }
            m.Value=await ledger.Production(m,ct);db.Add(m);await Physical(m,ct);actual+=Math.Abs(m.Value);
        }
        if(o.Kind=="Issue")wipDelta=actual;
        if(o.Kind=="Return")wipDelta=-actual;
        if(o.Kind=="Conversion"){actual=o.Labor+o.Utility+o.Overhead;wipDelta=actual;}
        if(o.Kind=="Receipt"){
            if(h.ReceivedQuantity+o.OutputQuantity>source.Evidence.PlannedQuantity)throw new DomainException("production_output_exceeds_plan","Output exceeds the approved order quantity.",409);
            if(o.Amount>h.WipValue)throw new DomainException("production_wip_insufficient","Receipt cost exceeds available actual WIP.",409);
            var m=new ProductionStockMovement{TenantId=actor.TenantId,CompanyId=company,Id=Guid.CreateVersion7(),SessionId=session,OperationId=o.Id,Kind="Receipt",WarehouseId=source.Evidence.WarehouseId,ProductId=source.Evidence.FinishedProduct.Id,Quantity=o.OutputQuantity,Value=o.Amount,BusinessDate=o.BusinessDate};
            _=await ledger.Production(m,ct);db.Add(m);await Physical(m,ct);actual=o.Amount;wipDelta=-actual;h.ReceivedQuantity+=o.OutputQuantity;
        }
        if(o.Kind=="ByProduct"){
            if(o.Amount>h.WipValue)throw new DomainException("production_wip_insufficient","By-product allocation exceeds available actual WIP.",409);
            if(reviewedOutput is null||o.OutputWarehouseId is null)throw new DomainException("production_byproduct_scope","Reviewed by-product source is missing.",409);
            var m=new ProductionStockMovement{TenantId=actor.TenantId,CompanyId=company,Id=Guid.CreateVersion7(),SessionId=session,OperationId=o.Id,Kind="Receipt",WarehouseId=o.OutputWarehouseId.Value,ProductId=reviewedOutput.Id,Quantity=o.OutputQuantity,Value=o.Amount,BusinessDate=o.BusinessDate};
            _=await ledger.Production(m,ct);db.Add(m);await Physical(m,ct);actual=o.Amount;wipDelta=-actual;
        }
        if(o.Kind=="Loss"){actual=o.Amount;wipDelta=-actual;}
        if(o.Kind=="Reverse"){
            var original=await ReversalSource(actor,company,session,o.ReversesOperationId!.Value,o.BusinessDate,ct);
            var entry=await db.Set<ProductionWipEntry>().AsNoTracking().SingleAsync(x=>x.TenantId==actor.TenantId&&x.CompanyId==company&&x.OperationId==original.Id,ct);
            actual=original.PostedValue;wipDelta=-entry.Value;
            if(original.Kind is "Receipt" or "ByProduct"){
                var receipt=await db.Set<ProductionStockMovement>().AsNoTracking().SingleAsync(x=>x.TenantId==actor.TenantId&&x.CompanyId==company&&x.OperationId==original.Id&&x.Kind=="Receipt",ct);
                var remaining=await OutputRemaining(actor,company,original,ct);if(o.OutputQuantity<=0||o.OutputQuantity>remaining.Quantity)throw new DomainException("production_reversal_quantity_invalid","Output correction quantity exceeds the unreversed original quantity.",409);
                actual=o.OutputQuantity==remaining.Quantity?remaining.Value:decimal.Round(receipt.Value*o.OutputQuantity/receipt.Quantity,4,MidpointRounding.ToEven);wipDelta=actual;
                var m=new ProductionStockMovement{TenantId=actor.TenantId,CompanyId=company,Id=Guid.CreateVersion7(),SessionId=session,OperationId=o.Id,Kind="ReceiptReversal",WarehouseId=receipt.WarehouseId,ProductId=receipt.ProductId,Quantity=-o.OutputQuantity,Value=-actual,OriginalIssueId=receipt.Id,BusinessDate=o.BusinessDate};
                _=await ledger.Production(m,ct);db.Add(m);await Physical(m,ct);if(original.Kind=="Receipt")h.ReceivedQuantity-=o.OutputQuantity;
            }
            if(original.Kind=="Return")foreach(var returned in await db.Set<ProductionStockMovement>().AsNoTracking().Where(x=>x.TenantId==actor.TenantId&&x.CompanyId==company&&x.OperationId==original.Id&&x.Kind=="Return").OrderBy(x=>x.Id).ToArrayAsync(ct)){
                var m=new ProductionStockMovement{TenantId=actor.TenantId,CompanyId=company,Id=Guid.CreateVersion7(),SessionId=session,OperationId=o.Id,Kind="ReturnReversal",WarehouseId=returned.WarehouseId,ProductId=returned.ProductId,Quantity=-returned.Quantity,Value=-returned.Value,OriginalIssueId=returned.Id,BusinessDate=o.BusinessDate};
                _=await ledger.Production(m,ct);db.Add(m);await Physical(m,ct);
            }
        }
        if(h.WipValue+wipDelta<0)throw new DomainException("production_wip_insufficient","Return/loss exceeds the unreleased actual WIP pool.",409);
        if(o.Kind=="Close"){
            if(h.WipValue!=0)throw new DomainException("production_wip_remaining","Allocate, return or independently write off all WIP before closing.",409);
            if(await db.Set<ProductionCostOperation>().AnyAsync(x=>x.TenantId==actor.TenantId&&x.CompanyId==company&&x.SessionId==session&&x.Id!=id&&x.State=="Draft",ct))throw new DomainException("production_pending_operations","Cancel or resolve other draft operations before closing.",409);
            h.State="Closed";
        }
        h.WipValue+=wipDelta;h.Version++;o.State="Posted";o.Version=2;o.ApprovedBy=actor.Subject;o.PostedAt=clock.GetUtcNow();o.PostedValue=actual;
        db.Add(new ProductionWipEntry{TenantId=actor.TenantId,CompanyId=company,SessionId=session,OperationId=id,SessionVersion=h.Version,Kind=o.Kind,Value=wipDelta,BusinessDate=o.BusinessDate});
        audit.Append(actor,company,id,"ProductionOperationPosted",correlation,2);events.Append(actor.TenantId,company,session,h.Version,nameof(ProductionCostPostedV1),correlation,new ProductionCostPostedV1(session,h.OrderId,id,o.Kind,h.Version,V(h.WipValue),Q(h.ReceivedQuantity)));
        return new(200,View(o));
    }
    public async Task<ProductionSessionView> Get(Actor actor,Guid company,Guid id,CancellationToken ct){Require(actor,company,Permissions.ManufacturingRead,Permissions.CostingRead);return View(await Find(actor,company,id,ct));}
    public async Task<CommandOutcome> Cancel(Actor actor,Guid company,Guid session,Guid id,CancelProductionOperationRequest r,string correlation,CancellationToken ct){
        Require(actor,company,Permissions.ManufacturingRequest,Permissions.CostingManage);Guard.Text(r.Reason,"Cancellation reason",1000);
        await periodStore.LockCompany(actor.TenantId,company,false,ct);await CostingStore.LockScope(db,actor.TenantId,company,false,ct);var o=await Operation(actor,company,session,id,ct);
        if(o.State!="Draft"||o.Version!=r.ExpectedVersion)throw new DomainException("version_conflict","Only the current draft can be cancelled.",409);
        if(o.CreatedBy!=actor.Subject&&!actor.Permissions.Contains(Permissions.ManufacturingApprove))throw new DomainException("permission_denied","Only the maker or a production approver may cancel a draft.",403);
        o.State="Cancelled";o.Version=2;audit.Append(actor,company,id,"ProductionOperationCancelled",correlation,2,r.Reason);return new(200,View(o));
    }
    public async Task<IReadOnlyList<ProductionSessionView>> List(Actor actor,Guid company,Guid? before,int take,CancellationToken ct){Require(actor,company,Permissions.ManufacturingRead,Permissions.CostingRead);Page(take);return(await db.Set<ProductionCostSession>().AsNoTracking().Where(x=>x.TenantId==actor.TenantId&&x.CompanyId==company&&(before==null||x.Id.CompareTo(before.Value)<0)).OrderByDescending(x=>x.Id).Take(take).ToArrayAsync(ct)).Select(View).ToArray();}
    public async Task<ProductionOperationView> GetOperation(Actor actor,Guid company,Guid session,Guid id,CancellationToken ct){Require(actor,company,Permissions.ManufacturingRead,Permissions.CostingRead);return View(await Operation(actor,company,session,id,ct));}
    public async Task<IReadOnlyList<ProductionOperationView>> Operations(Actor actor,Guid company,Guid session,Guid? before,int take,CancellationToken ct){Require(actor,company,Permissions.ManufacturingRead,Permissions.CostingRead);Page(take);await Find(actor,company,session,ct);return(await db.Set<ProductionCostOperation>().AsNoTracking().Where(x=>x.TenantId==actor.TenantId&&x.CompanyId==company&&x.SessionId==session&&(before==null||x.Id.CompareTo(before.Value)<0)).OrderByDescending(x=>x.Id).Take(take).ToArrayAsync(ct)).Select(View).ToArray();}
    private async Task Physical(ProductionStockMovement m,CancellationToken ct){if(m.Quantity<0){var changed=await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE inventory.stock_balances SET \"Quantity\"=\"Quantity\"+{m.Quantity} WHERE \"TenantId\"={m.TenantId} AND \"CompanyId\"={m.CompanyId} AND \"WarehouseId\"={m.WarehouseId} AND \"ProductId\"={m.ProductId} AND \"Quantity\"-\"ReservedQuantity\">={-m.Quantity}",ct);if(changed!=1)throw new DomainException("insufficient_stock","Production cannot consume reserved or unavailable material.",409);}else await db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO inventory.stock_balances ("TenantId","CompanyId","WarehouseId","ProductId","Quantity") VALUES ({m.TenantId},{m.CompanyId},{m.WarehouseId},{m.ProductId},{m.Quantity})
        ON CONFLICT ("TenantId","CompanyId","WarehouseId","ProductId") DO UPDATE SET "Quantity"=inventory.stock_balances."Quantity"+EXCLUDED."Quantity"
        """,ct);}
    private async Task VerifyProducts(Actor a,Guid c,ProductionOrderSource s,CancellationToken ct){var ids=s.Evidence.Materials.Select(x=>x.Product).Append(s.Evidence.FinishedProduct).Concat(s.Evidence.ByProducts?.Select(x=>x.Product)??[]).ToArray();var products=await db.Set<Product>().AsNoTracking().Where(x=>x.TenantId==a.TenantId&&x.CompanyId==c&&ids.Select(p=>p.Id).Contains(x.Id)).ToArrayAsync(ct);
        if(ids.Any(p=>!products.Any(x=>x.Id==p.Id&&x.Reference==p.Reference&&x.UnitOfMeasure==p.UnitOfMeasure))||!await db.Set<Warehouse>().AnyAsync(x=>x.TenantId==a.TenantId&&x.CompanyId==c&&x.Id==s.Evidence.WarehouseId,ct))throw new DomainException("production_reference_mismatch","Production product/UOM/warehouse no longer matches scoped financial references.",409);}
    private Task<ProductionCostSession> Find(Actor a,Guid c,Guid id,CancellationToken ct)=>FindCore(a,c,id,ct);
    private async Task<ProductionCostSession> FindCore(Actor a,Guid c,Guid id,CancellationToken ct)=>await db.Set<ProductionCostSession>().SingleOrDefaultAsync(x=>x.TenantId==a.TenantId&&x.CompanyId==c&&x.Id==id,ct)??throw new DomainException("not_found","Production execution not found.",404);
    private async Task<ProductionCostOperation> Operation(Actor a,Guid c,Guid session,Guid id,CancellationToken ct)=>await db.Set<ProductionCostOperation>().SingleOrDefaultAsync(x=>x.TenantId==a.TenantId&&x.CompanyId==c&&x.SessionId==session&&x.Id==id,ct)??throw new DomainException("not_found","Production operation not found.",404);
    private static ProductionOrderSource Source(ProductionCostSession h){var s=JsonSerializer.Deserialize<ProductionOrderSource>(h.SourceJson,Json)!;if(Hash(s)!=h.SourceSha256)throw new DomainException("production_source_changed","Frozen execution source has changed.",409);return s;}
    private static ProductionSessionView View(ProductionCostSession h){var s=Source(h);return new(h.Id,h.CompanyId,h.OrderId,h.Reference,h.State,h.Version,V(h.WipValue),Q(h.ReceivedQuantity),Q(s.Evidence.PlannedQuantity),s.Evidence);}
    private static ProductionOperationView View(ProductionCostOperation o)=>new(o.Id,o.SessionId,o.Kind,o.BusinessDate,o.State,o.Version,o.SessionVersion,o.Evidence,JsonSerializer.Deserialize<ProductionLine[]>(o.Lines,Json)!.Select(x=>new ProductionLineInput(x.WarehouseId,x.ProductId,Q(x.Quantity),x.OriginalIssueId,x.Id,x.InputUnitOfMeasure,x.InputQuantity is null?null:Q(x.InputQuantity.Value))).ToArray(),V(o.Amount),Q(o.OutputQuantity),V(o.Labor),V(o.Utility),V(o.Overhead),V(o.PostedValue),o.CreatedBy,o.ApprovedBy,o.ReversesOperationId,o.OutputUnitOfMeasure,o.OutputInputQuantity is null?null:Q(o.OutputInputQuantity.Value),o.OutputProductId,o.OutputWarehouseId);
    private async Task<ProductionCostOperation> ReversalSource(Actor actor,Guid company,Guid session,Guid originalId,DateOnly date,CancellationToken ct){
        var original=await Operation(actor,company,session,originalId,ct);
        if(original.State!="Posted"||original.Kind is not("Return" or "Conversion" or "Loss" or "Receipt" or "ByProduct")||original.BusinessDate>date)throw new DomainException("production_reversal_source_invalid","Reverse an earlier posted material return, conversion, loss or production output in this execution.",409);
        if(original.Kind is not("Receipt" or "ByProduct")&&await db.Set<ProductionCostOperation>().AnyAsync(x=>x.TenantId==actor.TenantId&&x.CompanyId==company&&x.ReversesOperationId==originalId&&x.State=="Posted",ct))throw new DomainException("production_already_reversed","This operation already has a posted reversal.",409);
        return original;
    }
    private async Task<(decimal Quantity,decimal Value)> OutputRemaining(Actor actor,Guid company,ProductionCostOperation original,CancellationToken ct){
        var receipt=await db.Set<ProductionStockMovement>().AsNoTracking().SingleAsync(x=>x.TenantId==actor.TenantId&&x.CompanyId==company&&x.OperationId==original.Id&&x.Kind=="Receipt",ct);
        var reversed=await(from m in db.Set<ProductionStockMovement>().AsNoTracking() join o in db.Set<ProductionCostOperation>().AsNoTracking() on new{m.TenantId,m.CompanyId,Id=m.OperationId} equals new{o.TenantId,o.CompanyId,Id=o.Id} where m.TenantId==actor.TenantId&&m.CompanyId==company&&o.ReversesOperationId==original.Id&&o.State=="Posted"&&m.Kind=="ReceiptReversal" select new{m.Quantity,m.Value}).ToArrayAsync(ct);
        var quantity=receipt.Quantity+reversed.Sum(x=>x.Quantity);var value=receipt.Value+reversed.Sum(x=>x.Value);
        if(quantity<=0||value<0)throw new DomainException("production_already_reversed","This production output is already fully reversed.",409);return(quantity,value);
    }
    private static decimal Number(string s,int scale,bool positive=false){if(!decimal.TryParse(s,NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var v)||v<0||(positive&&v==0)||v>=(scale==6?1000000000000m:100000000000000000000m)||decimal.Round(v,scale)!=v)throw new DomainException("validation_failed","Require nonnegative decimal strings at quantity scale 6 / BDT scale 4.");return v;}
    private static void Open(ProductionCostSession h,int expected){if(h.State!="Open"||h.Version!=expected)throw new DomainException("version_conflict","Reload an open execution at the expected version.",409);}
    private static string Hash(ProductionOrderSource s)=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(s,Json)));
    private static string Q(decimal x)=>x.ToString("0.000000",CultureInfo.InvariantCulture);private static string V(decimal x)=>x.ToString("0.0000",CultureInfo.InvariantCulture);
    private static void Page(int take){if(take is <1 or >100)throw new DomainException("validation_failed","Require take 1-100.");}
    private static void Require(Actor a,Guid c,params string[] permissions){a.RequireCompany(c);if(permissions.Any(p=>!a.Permissions.Contains(p)))throw new DomainException("permission_denied","Manufacturing and financial cost permissions required.",403);}
}
