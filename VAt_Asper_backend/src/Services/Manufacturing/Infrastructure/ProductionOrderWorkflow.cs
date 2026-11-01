using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Vat.Application.Security;
using Vat.Contracts.Manufacturing;
using Vat.Domain;
using Vat.Services.Manufacturing.Domain;

namespace Vat.Services.Manufacturing.Infrastructure;
public sealed class ProductionOrderWorkflow(ManufacturingDbContext db,IProductionWarehouseReferences references,ICoefficientReferences productReferences,TimeProvider clock):IProductionOrderWorkflow
{
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
    public Task<CoefficientResponse> Create(Actor actor,Guid company,Guid key,CreateProductionOrderRequest r,string bearer,string correlation,CancellationToken ct)
    {
        Require(actor,company,Permissions.ManufacturingRequest);Guard.Text(r.Reference,"Production reference",100);Guard.Id(r.CoefficientId,"Coefficient");Guard.Id(r.WarehouseId,"Warehouse");
        if(r.BusinessDate==default||!decimal.TryParse(r.PlannedQuantity,NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var quantity)||quantity<=0||quantity>=1000000000000m||decimal.Round(quantity,6)!=quantity)throw new DomainException("validation_failed","Require a date and positive exact planned quantity at scale 6.");
        return Execute(actor,company,key,new{action="production-order.create",request=r},async()=>{
            await Lock($"production-reference:{actor.TenantId}:{company}:{r.Reference.Trim()}",ct);
            if(await db.Orders.AnyAsync(x=>x.TenantId==actor.TenantId&&x.CompanyId==company&&x.Reference==r.Reference.Trim(),ct))throw new DomainException("duplicate_reference","Production reference already exists.",409);
            var coefficient=await Coefficient(actor,company,r.CoefficientId,r.BusinessDate,ct);
            var warehouse=await references.Warehouse(actor,company,r.WarehouseId,bearer,ct);
            var snapshot=JsonSerializer.Deserialize<CoefficientSnapshot>(coefficient.Document.SnapshotJson,Json)!;
            var evidence=new ProductionOrderEvidence(coefficient.Value.Id,coefficient.Value.Revision,coefficient.Value.Version,coefficient.Document.SnapshotSha256,
                warehouse.Id,warehouse.Name,r.BusinessDate,new(snapshot.FinishedProduct.Id,snapshot.FinishedProduct.Reference,snapshot.FinishedProduct.UnitOfMeasure),snapshot.BatchSize,quantity,
                snapshot.Materials.Select(x=>new ProductionMaterial(new(x.Product.Id,x.Product.Reference,x.Product.UnitOfMeasure),x.Quantity,x.WastagePercent)).ToArray());
            evidence=evidence with{ByProducts=await FreezeByProducts(actor,company,r.ByProducts,snapshot,bearer,ct)};
            evidence=evidence with{UomConversions=ProductionUnits.Freeze(evidence,r.UomConversions)};
            var now=clock.GetUtcNow();var order=new ProductionOrder{TenantId=actor.TenantId,CompanyId=company,Id=Guid.CreateVersion7(),Reference=r.Reference.Trim(),CoefficientVersionId=r.CoefficientId,
                WarehouseId=r.WarehouseId,BusinessDate=r.BusinessDate,PlannedQuantity=quantity,State="Draft",Version=1,CreatedBy=actor.Subject,CreatedAt=now,UpdatedAt=now};
            var json=JsonSerializer.Serialize(evidence,Json);db.Add(order);db.Add(new ProductionOrderDocument{TenantId=actor.TenantId,CompanyId=company,Id=order.Id,Json=json,Sha256=Hash(json)});
            Audit(actor,order,"create",null,correlation);return(201,View(order,evidence));
        },ct);
    }
    private async Task<IReadOnlyList<ProductionByProduct>?> FreezeByProducts(Actor actor,Guid company,IReadOnlyList<ProductionByProductRequest>? requested,CoefficientSnapshot snapshot,string bearer,CancellationToken ct)
    {
        if(requested is null or {Count:0})return null;
        if(requested.Count>20||requested.Any(x=>x is null)||requested.Select(x=>x.ProductId).Distinct().Count()!=requested.Count)throw new DomainException("validation_failed","Require at most 20 distinct by-product/scrap products.");
        var excluded=snapshot.Materials.Select(x=>x.Product.Id).Append(snapshot.FinishedProduct.Id).ToHashSet();var result=new List<ProductionByProduct>();
        foreach(var item in requested.OrderBy(x=>x.ProductId)){
            if(item.ProductId==Guid.Empty||excluded.Contains(item.ProductId)||item.Classification is not("ByProduct" or "Scrap"))throw new DomainException("validation_failed","By-product products must be distinct from BOM products and classified as ByProduct or Scrap.");
            var p=await productReferences.Product(actor,company,item.ProductId,bearer,ct);result.Add(new(new(p.Id,p.Reference,p.UnitOfMeasure),item.Classification));
        }
        return result;
    }
    public Task<CoefficientResponse> Transition(Actor actor,Guid company,Guid id,Guid key,string action,CoefficientActionRequest r,string correlation,CancellationToken ct)
    {
        Require(actor,company,action=="approve"?Permissions.ManufacturingApprove:Permissions.ManufacturingRequest);
        if(r.ExpectedVersion<1)throw new DomainException("validation_failed","Require an expected version.");
        if(action=="cancel")Guard.Text(r.Reason??"","Cancellation reason",1000);
        return Execute(actor,company,key,new{action="production-order."+action,id,request=r},async()=>{
            await Lock($"production-order:{actor.TenantId}:{company}:{id}",ct);var(o,e)=await Read(actor,company,id,ct);
            if(o.Version!=r.ExpectedVersion)throw new DomainException("version_conflict","Reload the production order.",409);
            if(action=="approve"&&o.CreatedBy==actor.Subject)throw new DomainException("self_approval","A different user must approve production.",403);
            var next=(o.State,action) switch{("Draft","submit")=>"Submitted",("Submitted","approve")=>"Approved",("Draft" or "Submitted","cancel")=>"Cancelled",_=>throw new DomainException("invalid_transition","Order is not in the required state.",409)};
            var coefficient=await Coefficient(actor,company,e.CoefficientId,e.BusinessDate,ct);
            if(coefficient.Value.Version!=e.CoefficientVersion||coefficient.Document.SnapshotSha256!=e.CoefficientSha256)throw new DomainException("production_coefficient_changed","The reviewed coefficient evidence changed.",409);
            o.State=next;o.Version++;o.UpdatedAt=clock.GetUtcNow();if(action=="approve")o.ApprovedBy=actor.Subject;
            Audit(actor,o,action,r.Reason,correlation);return(200,View(o,e));
        },ct);
    }
    public async Task<ProductionOrderView> Get(Actor actor,Guid company,Guid id,CancellationToken ct){Require(actor,company,Permissions.ManufacturingRead);var(o,e)=await Read(actor,company,id,ct);return View(o,e);}
    public async Task<ProductionOrderSource> Source(Actor actor,Guid company,Guid id,CancellationToken ct)
    {
        Require(actor,company,Permissions.ManufacturingRead);var(o,e)=await Read(actor,company,id,ct);
        if(o.State!="Approved"||o.Version!=3||string.IsNullOrWhiteSpace(o.ApprovedBy))throw new DomainException("production_order_not_approved","Only an independently approved order can be executed.",409);
        var c=await Coefficient(actor,company,e.CoefficientId,e.BusinessDate,ct);if(c.Document.SnapshotSha256!=e.CoefficientSha256||c.Value.Version!=e.CoefficientVersion)throw new DomainException("production_coefficient_changed","Frozen coefficient changed.",409);
        return new(actor.TenantId,company,o.Id,o.Version,o.State,o.Reference,o.CreatedBy,o.ApprovedBy,Hash(JsonSerializer.Serialize(e,Json)),e);
    }
    private async Task<(ProductionOrder,ProductionOrderEvidence)> Read(Actor a,Guid c,Guid id,CancellationToken ct)
    {
        var o=await db.Orders.SingleOrDefaultAsync(x=>x.TenantId==a.TenantId&&x.CompanyId==c&&x.Id==id,ct)??throw new DomainException("not_found","Production order not found.",404);
        var d=await db.Set<ProductionOrderDocument>().SingleOrDefaultAsync(x=>x.TenantId==a.TenantId&&x.CompanyId==c&&x.Id==id,ct);
        if(d is null||Hash(d.Json)!=d.Sha256)throw new DomainException("production_snapshot_invalid","A verified production snapshot is required.",409);
        var e=JsonSerializer.Deserialize<ProductionOrderEvidence>(d.Json,Json)!;
        if(e.CoefficientId!=o.CoefficientVersionId||e.WarehouseId!=o.WarehouseId||e.BusinessDate!=o.BusinessDate||e.PlannedQuantity!=o.PlannedQuantity)throw new DomainException("production_snapshot_invalid","Order and snapshot disagree.",409);
        return(o,e);
    }
    private async Task<(CoefficientVersion Value,CoefficientDocument Document)> Coefficient(Actor a,Guid c,Guid id,DateOnly date,CancellationToken ct)
    {
        var v=await db.Set<CoefficientVersion>().AsNoTracking().SingleOrDefaultAsync(x=>x.TenantId==a.TenantId&&x.CompanyId==c&&x.Id==id,ct)??throw new DomainException("not_found","Coefficient not found.",404);
        var d=await db.Set<CoefficientDocument>().AsNoTracking().SingleOrDefaultAsync(x=>x.TenantId==a.TenantId&&x.CompanyId==c&&x.Id==id,ct);
        if(v.State!="Approved"||v.EffectiveFrom>date||v.EffectiveUntil<date||d is null||Hash(d.SnapshotJson)!=d.SnapshotSha256)throw new DomainException("production_coefficient_required","Require verified approved coefficient evidence covering the order date.",409);
        return(v,d);
    }
    private async Task<CoefficientResponse> Execute(Actor a,Guid c,Guid key,object request,Func<Task<(int Status,ProductionOrderView Value)>> action,CancellationToken ct)
    {
        Guard.Id(key,"Idempotency key");await using var tx=await db.Database.BeginTransactionAsync(ct);await Lock($"coefficient-command:{a.TenantId}:{c}:{key}",ct);
        var hash=Hash(JsonSerializer.Serialize(request,Json));var prior=await db.Set<CoefficientCommand>().FindAsync([a.TenantId,c,key],ct);
        if(prior is not null){if(prior.Actor!=a.Subject||prior.RequestHash!=hash)throw new DomainException("idempotency_conflict","Key belongs to another actor/request.",409);await tx.CommitAsync(ct);return new(prior.StatusCode,prior.ResponseJson,true);}
        var result=await action();var json=JsonSerializer.Serialize(result.Value,Json);db.Add(new CoefficientCommand{TenantId=a.TenantId,CompanyId=c,Id=key,Actor=a.Subject,RequestHash=hash,ResponseJson=json,StatusCode=result.Status,CreatedAt=clock.GetUtcNow()});
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return new(result.Status,json);
    }
    private void Audit(Actor a,ProductionOrder o,string action,string? reason,string correlation)=>db.Add(new ProductionOrderAudit{TenantId=a.TenantId,CompanyId=o.CompanyId,Id=Guid.CreateVersion7(),OrderId=o.Id,Action=action,Actor=a.Subject,Version=o.Version,Reason=reason,CorrelationId=correlation,CreatedAt=clock.GetUtcNow()});
    private Task Lock(string s,CancellationToken ct)=>db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({BinaryPrimitives.ReadInt64BigEndian(SHA256.HashData(Encoding.UTF8.GetBytes(s)))})",ct);
    private static string Hash(string s)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)));
    private static ProductionOrderView View(ProductionOrder o,ProductionOrderEvidence e)=>new(o.Id,o.CompanyId,o.Reference,o.State,o.Version,o.CreatedBy,o.ApprovedBy,e,o.PlannedQuantity.ToString("0.000000",CultureInfo.InvariantCulture));
    private static void Require(Actor a,Guid c,string p){a.RequireCompany(c);if(!a.Permissions.Contains(p))throw new DomainException("permission_denied","Manufacturing permission required.",403);}
}
