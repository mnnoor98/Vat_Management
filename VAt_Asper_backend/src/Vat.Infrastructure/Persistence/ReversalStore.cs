using Microsoft.EntityFrameworkCore;
using Vat.Application;
using Vat.Domain;
using Vat.Domain.Posting;

namespace Vat.Infrastructure.Persistence;

public sealed class ReversalStore(VatDbContext db) : IReversalStore
{
    public Task<PurchaseReversal?> Find(Guid tenantId, Guid companyId, Guid purchaseId, Guid reversalId, CancellationToken ct) =>
        db.Set<PurchaseReversal>().SingleOrDefaultAsync(x => x.TenantId == tenantId && x.CompanyId == companyId &&
            x.PurchaseId == purchaseId && x.Id == reversalId, ct);

    public void Add(PurchaseReversal reversal) => db.Add(reversal);

    public async Task Correct(PurchaseReversal reversal, PostingReadSnapshot original, DateTimeOffset now, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Reversal requires a transaction.");
        var ledger = await StockCostLedger.Begin(db, reversal.TenantId, reversal.CompanyId, now, ct);
        var stock = original.StockEntries.Where(x => x.ReversesEntryId is null).ToArray();
        foreach (var group in stock.GroupBy(x => new { x.WarehouseId, x.ProductId }).OrderBy(x => x.Key.WarehouseId).ThenBy(x => x.Key.ProductId))
        {
            var warehouseId = group.Key.WarehouseId;
            var productId = group.Key.ProductId;
            var quantity = group.Sum(x => x.Quantity);
            var affected = await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE inventory.stock_balances SET "Quantity" = "Quantity" - {quantity}
                WHERE "TenantId" = {reversal.TenantId} AND "CompanyId" = {reversal.CompanyId}
                    AND "WarehouseId" = {warehouseId} AND "ProductId" = {productId} AND "Quantity" - "ReservedQuantity" >= {quantity}
                """, ct);
            if (affected != 1) throw new DomainException("insufficient_stock", "Reversal would consume reserved or unavailable warehouse stock.", 409);
        }
        foreach (var entry in stock.OrderBy(x => x.WarehouseId).ThenBy(x => x.ProductId).ThenBy(x => x.Id))
        {
            var correction = StockMovement.Correction(entry, reversal, now); db.Add(correction);
            if (ledger is not null) await ledger.Reverse(correction, ct);
        }
        foreach (var entry in original.TaxEntries.Where(x => x.ReversesEntryId is null)) db.Add(TaxLedgerEntry.Correction(entry, reversal, now));
    }
}
