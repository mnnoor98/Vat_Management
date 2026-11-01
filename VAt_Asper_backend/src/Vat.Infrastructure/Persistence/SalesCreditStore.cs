using Microsoft.EntityFrameworkCore;
using Vat.Application;
using Vat.Domain;
using Vat.Domain.Sales;

namespace Vat.Infrastructure.Persistence;

public sealed class SalesCreditStore(VatDbContext db) : ISalesCreditStore
{
    public Task<SalesCreditNote?> Find(Guid tenantId, Guid companyId, Guid invoiceId, Guid id, CancellationToken ct) =>
        db.Set<SalesCreditNote>().SingleOrDefaultAsync(x => x.TenantId == tenantId && x.CompanyId == companyId && x.InvoiceId == invoiceId && x.Id == id, ct);
    public async Task<IReadOnlyList<SalesCreditNote>> List(Guid tenantId, Guid companyId, Guid invoiceId, Guid? before, int take, CancellationToken ct)
    {
        var query = db.Set<SalesCreditNote>().AsNoTracking().Where(x => x.TenantId == tenantId && x.CompanyId == companyId && x.InvoiceId == invoiceId);
        if (before is not null) query = query.Where(x => x.Id.CompareTo(before.Value) < 0);
        return await query.OrderByDescending(x => x.Id).Take(take).ToArrayAsync(ct);
    }
    public void Add(SalesCreditNote note) => db.Add(note);
    public async Task Return(SalesCreditNote note, SalesPostingRead original, DateTimeOffset now, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Stock returns require a transaction.");
        var entries = note.Lines.Select(line => new
        {
            Original = original.Entries.Single(x => x.Id == line.OriginalEntryId && x.IsOriginal),
            Allocation = line
        }).ToArray();
        var ledger = entries.Any(x => x.Allocation.Quantity > 0)
            ? await StockCostLedger.Begin(db, note.TenantId, note.CompanyId, now, ct) : null;
        foreach (var group in entries.Where(x => x.Allocation.Quantity > 0).GroupBy(x => new { x.Original.WarehouseId, x.Original.ProductId }).OrderBy(g => g.Key.WarehouseId).ThenBy(g => g.Key.ProductId))
        {
            var warehouse = group.Key.WarehouseId;
            var product = group.Key.ProductId;
            var quantity = group.Sum(x => x.Allocation.Quantity);
            var changed = await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE inventory.stock_balances SET "Quantity" = "Quantity" + {quantity}
                WHERE "TenantId" = {note.TenantId} AND "CompanyId" = {note.CompanyId}
                  AND "WarehouseId" = {warehouse} AND "ProductId" = {product}
                """, ct);
            if (changed != 1) throw new DomainException("balance_missing", "Original stock balance is missing; reconcile inventory before returning goods.", 409);
        }
        foreach (var entry in entries.OrderBy(x => x.Original.WarehouseId).ThenBy(x => x.Original.ProductId).ThenBy(x => x.Original.Id))
        {
            var returned = SalesPostingEntry.Return(entry.Original, note, entry.Allocation, now); db.Add(returned);
            if (ledger is not null) await ledger.Return(returned, ct);
        }
    }
}
