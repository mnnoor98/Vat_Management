using System.Data;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vat.Application.Security;
using Vat.Domain;
using Vat.Domain.Costing;

namespace Vat.Infrastructure.Persistence;

public sealed record StockCostAllocationView(Guid Id, Guid Origin, string Quantity, string Value);
public sealed record StockCostEntryView(string Sequence, Guid WarehouseId, Guid ProductId, string Kind, Guid SourceId, Guid? OriginalSourceId,
    DateOnly BusinessDate, string Method, string Quantity, string Value, IReadOnlyList<StockCostAllocationView> Allocations);
public sealed class StockValuationService(VatDbContext db)
{
    public async Task<CostLedgerStatus> Status(Actor actor, Guid company, CancellationToken ct)
    {
        Require(actor,company); await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead,ct);
        if(!await db.Companies.AnyAsync(x=>x.TenantId==actor.TenantId && x.Id==company,ct)) throw new DomainException("not_found","Company not found.",404);
        var h=await db.Set<StockCostHead>().AsNoTracking().SingleOrDefaultAsync(x=>x.TenantId==actor.TenantId && x.CompanyId==company,ct);
        if(h is null) return new(company,"AdoptionRequired",null,null,null,"cost_opening_not_approved",null);
        var reason=await new FinancialInventoryQueries(db).CostAvailability(actor,company,ct);
        var value=reason is null ? await db.Set<StockCostPosition>().Where(x=>x.TenantId==actor.TenantId && x.CompanyId==company).SumAsync(x=>x.Value,ct) : (decimal?)null;
        await tx.CommitAsync(ct);
        return new(company,reason is null?"Reconciled":"Unavailable",h.Basis,h.OpeningDate,h.Sequence.ToString(CultureInfo.InvariantCulture),reason,value?.ToString("0.0000",CultureInfo.InvariantCulture));
    }
    public async Task<IReadOnlyList<StockCostEntryView>> Journal(Actor actor,Guid company,long? before,int take,CancellationToken ct)
    {
        Require(actor,company); if(take is <1 or >100 || before is <=0) throw new DomainException("validation_failed","Require take 1-100 and a positive sequence cursor.");
        var rows=await db.Set<StockCostEntry>().AsNoTracking().Where(x=>x.TenantId==actor.TenantId && x.CompanyId==company && (before==null || x.Sequence<before))
            .OrderByDescending(x=>x.Sequence).Take(take).ToArrayAsync(ct);
        return rows.Select(x=>new StockCostEntryView(x.Sequence.ToString(CultureInfo.InvariantCulture),x.WarehouseId,x.ProductId,x.Kind,x.SourceId,x.OriginalSourceId,x.BusinessDate,x.Method,
            Q(x.Quantity),V(x.Value),JsonSerializer.Deserialize<CostChunk[]>(x.Allocations)!.Select(a=>new StockCostAllocationView(a.Id,a.Origin,Q(a.Quantity),V(a.Value))).ToArray())).ToArray();
    }
    private static string Q(decimal x)=>x.ToString("0.000000",CultureInfo.InvariantCulture);
    private static string V(decimal x)=>x.ToString("0.0000",CultureInfo.InvariantCulture);
    private static void Require(Actor actor,Guid company) { actor.RequireCompany(company); if(!actor.Permissions.Contains(Permissions.CostingRead)) throw new DomainException("permission_denied","Costing read permission required.",403); }
}
