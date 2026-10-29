using Microsoft.EntityFrameworkCore;
using Vat.Domain.Posting;
using Vat.Domain.Purchasing;
using Vat.Services.Purchase;

namespace Vat.Services.Purchase.Infrastructure;

public sealed class PurchaseStore(PurchaseDbContext db) : IPurchaseStore, IPurchasePostingStore, IPurchaseReversalStore
{
    public Task<Vat.Domain.Purchasing.Purchase?> Find(Guid tenantId, Guid companyId, Guid purchaseId, CancellationToken ct) =>
        db.Purchases.Include(x => x.Lines)
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.CompanyId == companyId && x.Id == purchaseId, ct);

    public async Task<IReadOnlyList<Vat.Domain.Purchasing.Purchase>> List(Guid tenantId, Guid companyId, Guid? before, int take, CancellationToken ct)
    {
        var query = db.Purchases.Include(x => x.Lines).Where(x => x.TenantId == tenantId && x.CompanyId == companyId);
        if (before is not null) query = query.Where(x => x.Id.CompareTo(before.Value) < 0);
        return await query.OrderByDescending(x => x.Id).Take(take).ToArrayAsync(ct);
    }

    public void Add(Vat.Domain.Purchasing.Purchase purchase) => db.Purchases.Add(purchase);

    public Task<PurchasePostingPlan?> FindPlan(Guid tenantId, Guid companyId, Guid purchaseId, Guid planId, CancellationToken ct) =>
        db.PostingPlans.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.CompanyId == companyId && x.PurchaseId == purchaseId && x.Id == planId, ct);

    public Task<PurchasePostingPlan?> LatestPlan(Guid tenantId, Guid companyId, Guid purchaseId, CancellationToken ct) =>
        db.PostingPlans.Where(x => x.TenantId == tenantId && x.CompanyId == companyId && x.PurchaseId == purchaseId)
            .OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync(ct);

    public void AddPlan(PurchasePostingPlan plan) => db.PostingPlans.Add(plan);

    public Task<PurchaseReversal?> Find(Guid tenantId, Guid companyId, Guid purchaseId, Guid reversalId, CancellationToken ct) =>
        db.Reversals.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.CompanyId == companyId && x.PurchaseId == purchaseId && x.Id == reversalId, ct);

    public void Add(PurchaseReversal reversal) => db.Reversals.Add(reversal);
}
