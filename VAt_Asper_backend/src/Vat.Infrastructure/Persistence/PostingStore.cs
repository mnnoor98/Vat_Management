using System.Data;
using Microsoft.EntityFrameworkCore;
using Vat.Application;
using Vat.Domain.Posting;

namespace Vat.Infrastructure.Persistence;

public sealed class PostingStore(VatDbContext db) : IPostingStore
{
    public Task<PurchasePostingPlan?> FindPlan(Guid tenantId, Guid companyId, Guid purchaseId, Guid planId, CancellationToken ct) =>
        db.Set<PurchasePostingPlan>().SingleOrDefaultAsync(x => x.TenantId == tenantId && x.CompanyId == companyId && x.PurchaseId == purchaseId && x.Id == planId, ct);

    public Task<PurchasePostingPlan?> LatestPlan(Guid tenantId, Guid companyId, Guid purchaseId, CancellationToken ct) =>
        db.Set<PurchasePostingPlan>().Where(x => x.TenantId == tenantId && x.CompanyId == companyId && x.PurchaseId == purchaseId)
            .OrderByDescending(x => x.SourceVersion).FirstOrDefaultAsync(ct);

    public void Add(PurchasePostingPlan plan) => db.Add(plan);

    public async Task Receive(PurchasePostingPlan plan, DateTimeOffset now, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Stock posting requires a transaction.");
        // Stable product order prevents cross-receipt deadlocks. Atomic upsert prevents lost increments.
        foreach (var group in plan.Snapshot.Lines.GroupBy(x => x.ProductId).OrderBy(x => x.Key))
        {
            var productId = group.Key;
            var quantity = group.Sum(x => x.Quantity);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO inventory.stock_balances ("TenantId", "CompanyId", "WarehouseId", "ProductId", "Quantity")
                VALUES ({plan.TenantId}, {plan.CompanyId}, {plan.WarehouseId}, {productId}, {quantity})
                ON CONFLICT ("TenantId", "CompanyId", "WarehouseId", "ProductId")
                DO UPDATE SET "Quantity" = inventory.stock_balances."Quantity" + EXCLUDED."Quantity"
                """, ct);
        }
        foreach (var line in plan.Snapshot.Lines) db.Add(StockMovement.Receipt(plan, line, now));
    }

    public void RecordTax(PurchasePostingPlan plan, DateTimeOffset now)
    {
        foreach (var line in plan.Snapshot.Lines) db.Add(TaxLedgerEntry.Input(plan, line, now));
    }

    public async Task<PostingReadSnapshot?> ReadReconciliation(Guid tenantId, Guid companyId, Guid purchaseId, CancellationToken ct)
    {
        // A consistent read across source and participants, even while another request commits posting.
        await using var transaction = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct) : null;
        var purchase = await db.Purchases.AsNoTracking().Include(x => x.Lines).SingleOrDefaultAsync(x =>
            x.TenantId == tenantId && x.CompanyId == companyId && x.Id == purchaseId, ct);
        if (purchase is null) return null;
        var plan = await LatestPlan(tenantId, companyId, purchaseId, ct);
        var stock = await db.Set<StockMovement>().AsNoTracking().Where(x => x.TenantId == tenantId && x.CompanyId == companyId && x.PurchaseId == purchaseId).ToArrayAsync(ct);
        var tax = await db.Set<TaxLedgerEntry>().AsNoTracking().Where(x => x.TenantId == tenantId && x.CompanyId == companyId && x.PurchaseId == purchaseId).ToArrayAsync(ct);
        var reversals = await db.Set<PurchaseReversal>().AsNoTracking().Where(x => x.TenantId == tenantId && x.CompanyId == companyId && x.PurchaseId == purchaseId).ToArrayAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return new(purchase, plan, stock, tax, reversals);
    }

    public async Task<IReadOnlyList<StockBalance>> Balances(Guid tenantId, Guid companyId, Guid warehouseId, Guid? beforeProduct, int take, CancellationToken ct)
    {
        var query = db.Set<StockBalance>().AsNoTracking().Where(x => x.TenantId == tenantId && x.CompanyId == companyId && x.WarehouseId == warehouseId);
        if (beforeProduct is not null) query = query.Where(x => x.ProductId.CompareTo(beforeProduct.Value) < 0);
        return await query.OrderByDescending(x => x.ProductId).Take(take).ToArrayAsync(ct);
    }

    public async Task<IReadOnlyList<TaxLedgerEntry>> TaxLedger(Guid tenantId, Guid companyId, Guid? purchaseId, Guid? before, int take, CancellationToken ct)
    {
        var query = db.Set<TaxLedgerEntry>().AsNoTracking().Where(x => x.TenantId == tenantId && x.CompanyId == companyId);
        if (purchaseId is not null) query = query.Where(x => x.PurchaseId == purchaseId.Value);
        if (before is not null) query = query.Where(x => x.Id.CompareTo(before.Value) < 0);
        return await query.OrderByDescending(x => x.Id).Take(take).ToArrayAsync(ct);
    }
}
