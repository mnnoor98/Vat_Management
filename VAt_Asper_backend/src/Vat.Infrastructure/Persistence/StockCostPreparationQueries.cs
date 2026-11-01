using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Vat.Application.Security;
using Vat.Domain;
using Vat.Domain.Catalog;
using Vat.Domain.Posting;

namespace Vat.Infrastructure.Persistence;

public sealed record CostOpeningPosition(Guid WarehouseId,string Warehouse,Guid ProductId,string Product,string Quantity);
public sealed record LandedSourceLine(Guid PurchaseLineId,string Product,string Quantity,string NetAmount);
public sealed record LandedSource(Guid PlanId,string PurchaseReference,string Warehouse,DateOnly BusinessDate,IReadOnlyList<LandedSourceLine> Lines);
public sealed class StockCostPreparationQueries(VatDbContext db)
{
    public async Task<IReadOnlyList<CostOpeningPosition>> Positions(Actor actor,Guid company,int skip,CancellationToken ct)
    {
        Require(actor,company); if(skip is <0 or >10000) throw new DomainException("validation_failed","Opening page offset must be 0-10000.");
        var rows=await (from s in db.Set<StockBalance>().AsNoTracking()
            join w in db.Set<Warehouse>() on new{s.TenantId,s.CompanyId,Id=s.WarehouseId} equals new{w.TenantId,w.CompanyId,w.Id}
            join p in db.Set<Product>() on new{s.TenantId,s.CompanyId,Id=s.ProductId} equals new{p.TenantId,p.CompanyId,p.Id}
            where s.TenantId==actor.TenantId && s.CompanyId==company && s.Quantity>0
            orderby s.WarehouseId,s.ProductId
            select new{s.WarehouseId,Warehouse=w.Name,s.ProductId,Product=p.Reference, s.Quantity}).Skip(skip).Take(100).ToArrayAsync(ct);
        return rows.Select(x=>new CostOpeningPosition(x.WarehouseId,x.Warehouse,x.ProductId,x.Product,x.Quantity.ToString("0.000000",CultureInfo.InvariantCulture))).ToArray();
    }
    public async Task<IReadOnlyList<LandedSource>> Sources(Actor actor,Guid company,Guid? before,CancellationToken ct,Guid? planId=null)
    {
        Require(actor,company);
        var rows=await (from plan in db.Set<PurchasePostingPlan>().AsNoTracking()
            join p in db.Purchases on new{plan.TenantId,plan.CompanyId,Id=plan.PurchaseId} equals new{p.TenantId,p.CompanyId,p.Id}
            join w in db.Set<Warehouse>() on new{plan.TenantId,plan.CompanyId,Id=plan.WarehouseId} equals new{w.TenantId,w.CompanyId,w.Id}
            where plan.TenantId==actor.TenantId && plan.CompanyId==company && (planId!=null ? plan.Id==planId : plan.Status==PostingPlanStatus.Prepared) && (before==null || plan.Id.CompareTo(before.Value)<0)
            orderby plan.Id descending
            select new{Plan=plan,Reference=p.ExternalReference,Warehouse=w.Name}).Take(25).ToArrayAsync(ct);
        return rows.Select(x=>new LandedSource(x.Plan.Id,x.Reference,x.Warehouse,x.Plan.Snapshot.BusinessDate,
            x.Plan.Snapshot.Lines.Select(l=>new LandedSourceLine(l.PurchaseLineId,l.ProductReference,l.Quantity.ToString("0.000000",CultureInfo.InvariantCulture),l.NetAmount.ToString("0.0000",CultureInfo.InvariantCulture))).ToArray())).ToArray();
    }
    private static void Require(Actor a,Guid c) { a.RequireCompany(c); if(!a.Permissions.Contains(Permissions.CostingRead))throw new DomainException("permission_denied","Costing read permission required.",403); }
}
