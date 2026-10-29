using System.Data;
using Microsoft.EntityFrameworkCore;
using Vat.Services.Sales;
using Vat.Services.Sales.Contracts;
using Vat.Domain;
using Vat.Domain.Sales;

namespace Vat.Services.Sales.Infrastructure;

public sealed class SalesPostingStore(SalesDbContext db) : ISalesPostingStore
{
    public Task<SalesFulfillment?> Find(Guid tenantId, Guid companyId, Guid invoiceId, CancellationToken ct) =>
        db.Set<SalesFulfillment>().SingleOrDefaultAsync(x => x.TenantId == tenantId && x.CompanyId == companyId && x.InvoiceId == invoiceId, ct);

    public async Task Reserve(SalesFulfillment plan, CancellationToken ct)
    {
        RequireTransaction();
        foreach (var group in plan.Snapshot.Lines.GroupBy(x => x.ProductId).OrderBy(x => x.Key))
        {
            var product = group.Key;
            var quantity = group.Sum(x => x.Quantity);
            var changed = await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE inventory.stock_balances SET "ReservedQuantity" = "ReservedQuantity" + {quantity}
                WHERE "TenantId" = {plan.TenantId} AND "CompanyId" = {plan.CompanyId}
                  AND "WarehouseId" = {plan.WarehouseId} AND "ProductId" = {product}
                  AND "Quantity" - "ReservedQuantity" >= {quantity}
                """, ct);
            if (changed != 1) throw new DomainException("insufficient_stock", "Insufficient available stock for this reservation.", 409);
        }
        db.Add(plan);
    }

    public async Task Issue(SalesFulfillment plan, DateTimeOffset now, CancellationToken ct)
    {
        RequireTransaction();
        foreach (var group in plan.Snapshot.Lines.GroupBy(x => x.ProductId).OrderBy(x => x.Key))
        {
            var product = group.Key;
            var quantity = group.Sum(x => x.Quantity);
            var changed = await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE inventory.stock_balances SET "Quantity" = "Quantity" - {quantity}, "ReservedQuantity" = "ReservedQuantity" - {quantity}
                WHERE "TenantId" = {plan.TenantId} AND "CompanyId" = {plan.CompanyId}
                  AND "WarehouseId" = {plan.WarehouseId} AND "ProductId" = {product}
                  AND "ReservedQuantity" >= {quantity} AND "Quantity" >= {quantity}
                """, ct);
            if (changed != 1) throw new DomainException("reservation_mismatch", "Reserved inventory is inconsistent.", 409);
        }
        foreach (var line in plan.Snapshot.Lines) db.Add(SalesPostingEntry.Issue(plan, line, now));
    }

    public async Task Release(SalesFulfillment plan, CancellationToken ct)
    {
        RequireTransaction();
        foreach (var group in plan.Snapshot.Lines.GroupBy(x => x.ProductId).OrderBy(x => x.Key))
        {
            var product = group.Key;
            var quantity = group.Sum(x => x.Quantity);
            var changed = await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE inventory.stock_balances SET "ReservedQuantity" = "ReservedQuantity" - {quantity}
                WHERE "TenantId" = {plan.TenantId} AND "CompanyId" = {plan.CompanyId}
                  AND "WarehouseId" = {plan.WarehouseId} AND "ProductId" = {product} AND "ReservedQuantity" >= {quantity}
                """, ct);
            if (changed != 1) throw new DomainException("reservation_mismatch", "Reserved inventory is inconsistent.", 409);
        }
        plan.Cancel();
    }

    public async Task<SalesPostingRead?> Read(Guid tenantId, Guid companyId, Guid invoiceId, CancellationToken ct)
    {
        await using var transaction = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct) : null;
        var invoice = await db.SalesInvoices.AsNoTracking().Include(x => x.Lines).SingleOrDefaultAsync(x =>
            x.TenantId == tenantId && x.CompanyId == companyId && x.Id == invoiceId, ct);
        if (invoice is null) return null;
        var plan = await Find(tenantId, companyId, invoiceId, ct);
        var entries = await db.Set<SalesPostingEntry>().AsNoTracking().Where(x =>
            x.TenantId == tenantId && x.CompanyId == companyId && x.InvoiceId == invoiceId).ToArrayAsync(ct);
        var notes = await db.Set<SalesCreditNote>().AsNoTracking().Where(x =>
            x.TenantId == tenantId && x.CompanyId == companyId && x.InvoiceId == invoiceId).ToArrayAsync(ct);
        var debits = await db.Set<SalesDebitNote>().AsNoTracking().Where(x => x.TenantId == tenantId && x.CompanyId == companyId && x.InvoiceId == invoiceId).ToArrayAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return new(invoice, plan, entries, notes, debits);
    }

    public async Task<SalesLedgerPage> Ledger(Guid tenantId, Guid companyId, Guid? before, int take, CancellationToken ct)
    {
        var query = db.Set<SalesPostingEntry>().AsNoTracking().Where(x => x.TenantId == tenantId && x.CompanyId == companyId);
        if (before is not null) query = query.Where(x => x.Id.CompareTo(before.Value) < 0);
        var rows = await query.OrderByDescending(x => x.Id).Take(take + 1).ToArrayAsync(ct);
        var page = rows.Take(take).ToArray();
        return new(page.Select(SalesLedgerEntryResponse.From).ToArray(), rows.Length > take ? page[^1].Id : null);
    }

    public async Task<SalesSummary> Summary(Guid tenantId, Guid companyId, DateOnly from, DateOnly until, CancellationToken ct)
    {
        // One SQL statement gives a committed, synchronous ledger view, independent of RabbitMQ freshness.
        var totals = await db.Set<SalesPostingEntry>().Where(x => x.TenantId == tenantId && x.CompanyId == companyId && from <= x.BusinessDate && x.BusinessDate < until)
            .GroupBy(x => x.CompanyId).Select(g => new { Count = g.Where(x => x.CreditNoteId == null && x.DebitNoteId == null).Select(x => x.InvoiceId).Distinct().Count(),
                Credits = g.Where(x => x.CreditNoteId != null).Select(x => x.CreditNoteId).Distinct().Count(),
                PriceCount = g.Where(x => x.EntryType == "PriceCredit").Select(x => x.CreditNoteId).Distinct().Count(),
                Debits = g.Where(x => x.DebitNoteId != null).Select(x => x.DebitNoteId).Distinct().Count(),
                Net = g.Sum(x => x.NetAmount), Tax = g.Sum(x => x.OutputTaxAmount) }).SingleOrDefaultAsync(ct);
        return new(from, until, totals?.Count ?? 0, totals?.Net ?? 0, totals?.Tax ?? 0, (totals?.Net ?? 0) + (totals?.Tax ?? 0), totals?.Credits ?? 0, totals?.PriceCount ?? 0, totals?.Debits ?? 0);
    }

    private void RequireTransaction()
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Sales stock changes require a transaction.");
    }
}
