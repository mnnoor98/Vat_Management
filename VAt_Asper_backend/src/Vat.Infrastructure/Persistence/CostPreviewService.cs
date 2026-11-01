using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Vat.Application.Security;
using Vat.Domain;
using Vat.Domain.Catalog;
using Vat.Domain.Costing;
using Vat.Domain.Inventory;
using Vat.Domain.Posting;
using Vat.Domain.Sales;

namespace Vat.Infrastructure.Persistence;

public sealed record CostAllocationView(Guid? ReceiptId, string Quantity, string Value);
public sealed record CostIssueView(Guid SourceId, string Quantity, string Value, IReadOnlyList<CostAllocationView> Allocations);
public sealed record CostCalculationView(string Method, string ReceivedQuantity, string ReceivedValue, string IssuedQuantity,
    string IssuedValue, string ClosingQuantity, string ClosingValue, IReadOnlyList<CostIssueView> Issues);
public sealed record CostPreviewView(Guid CompanyId, Guid WarehouseId, Guid ProductId, string State, string? Reason,
    string Basis, string? SupportedSourceSequence, int SourceCount, DateTimeOffset CapturedAt, CostCalculationView? Calculation);

// A scoped purchase-net-only preview. This does not promote the stock valuation read model.
public sealed class CostPreviewService(VatDbContext db, TimeProvider clock)
{
    public async Task<CostPreviewView> Read(Actor actor, Guid company, Guid warehouse, Guid product, CancellationToken ct)
    {
        actor.RequireCompany(company); Guard.Id(warehouse, nameof(warehouse)); Guard.Id(product, nameof(product));
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        if (!await db.Set<Warehouse>().AnyAsync(x => x.TenantId == actor.TenantId && x.CompanyId == company && x.Id == warehouse, ct) ||
            !await db.Set<Product>().AnyAsync(x => x.TenantId == actor.TenantId && x.CompanyId == company && x.Id == product, ct))
            throw new DomainException("not_found", "Warehouse or product is outside the granted company.", 404);
        var head = await db.Set<CostSourceHead>().AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == actor.TenantId && x.CompanyId == company, ct);
        CostPreviewView Unavailable(string reason, int count = 0) => new(company, warehouse, product, "Unavailable", reason,
            "PurchaseNetOnly", head?.Sequence.ToString(CultureInfo.InvariantCulture), count, clock.GetUtcNow(), null);
        if (head is null) return Unavailable("cost_source_journal_missing");
        if (!head.EmptyOpeningProven) return Unavailable("opening_cost_adoption_required");
        var stock = db.Set<StockMovement>().Where(x => x.TenantId == actor.TenantId && x.CompanyId == company && x.WarehouseId == warehouse && x.ProductId == product);
        var sales = db.Set<SalesPostingEntry>().Where(x => x.TenantId == actor.TenantId && x.CompanyId == company && x.WarehouseId == warehouse && x.ProductId == product);
        if (await db.Set<ProductionStockMovement>().AnyAsync(x=>x.TenantId==actor.TenantId&&x.CompanyId==company&&x.WarehouseId==warehouse&&x.ProductId==product,ct)||await stock.AnyAsync(x => x.ReversesEntryId != null, ct) ||
            await sales.AnyAsync(x => (x.CreditNoteId != null || x.DebitNoteId != null) && x.Quantity != 0, ct) ||
            await db.Set<TransferJournalEntry>().AnyAsync(x => x.TenantId == actor.TenantId && x.CompanyId == company && x.ProductId == product &&
                (x.FromWarehouseId == warehouse || x.ToWarehouseId == warehouse), ct))
            return Unavailable("unsupported_cost_movements");
        var events = db.Set<CostSourceEvent>().Where(x => x.TenantId == actor.TenantId && x.CompanyId == company && x.WarehouseId == warehouse && x.ProductId == product);
        var receipts = db.Set<PurchaseCostReceipt>().Where(x => x.TenantId == actor.TenantId && x.CompanyId == company && x.WarehouseId == warehouse && x.ProductId == product);
        if (await stock.AnyAsync(s => !receipts.Any(r => r.SourceMovementId == s.Id && events.Any(e => e.ReceiptId == r.Id)), ct) ||
            await sales.Where(s => s.CreditNoteId == null && s.DebitNoteId == null && s.Quantity != 0).AnyAsync(s => !events.Any(e => e.SalesEntryId == s.Id), ct))
            return Unavailable("cost_source_coverage_gap");
        if (await events.Where(e => e.SalesEntryId != null).AnyAsync(e => !sales.Any(s => s.Id == e.SalesEntryId &&
            s.CreditNoteId == null && s.DebitNoteId == null && s.Quantity == e.Quantity), ct))
            return Unavailable("cost_source_mismatch");
        var rows = await events.AsNoTracking().OrderBy(x => x.Sequence).Take(10001).ToArrayAsync(ct);
        if (rows.Length > 10000) return Unavailable("cost_source_limit_exceeded", rows.Length);
        if (rows.Length == 0) return Unavailable("cost_sources_empty");
        if (rows.Any(x => x.PolicyId is null || x.PolicyVersion != 2 || x.Method is null)) return Unavailable("cost_policy_unassigned", rows.Length);
        var methods = rows.Select(x => x.Method!).Distinct().ToArray();
        if (methods.Length != 1) return Unavailable("cost_policy_conversion_required", rows.Length);
        var receiptIds = rows.Where(x => x.ReceiptId != null).Select(x => x.ReceiptId!.Value).ToArray();
        var source = await receipts.AsNoTracking().Where(x => receiptIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        if (source.Values.Any(x => x.Currency != "BDT")) return Unavailable("cost_currency_not_supported", rows.Length);
        var inputs = new List<CostInput>();
        foreach (var row in rows)
        {
            if (row.ReceiptId is { } id)
            {
                if (!source.TryGetValue(id, out var receipt) || receipt.Quantity != row.Quantity || receipt.PolicyId != row.PolicyId ||
                    receipt.PolicyVersion != row.PolicyVersion || receipt.Method != row.Method) return Unavailable("cost_source_mismatch", rows.Length);
                inputs.Add(new(row.Sequence, id, "Receipt", row.Quantity, receipt.NetAmount));
            }
            else inputs.Add(new(row.Sequence, row.SalesEntryId!.Value, "Issue", -row.Quantity, 0));
        }
        CostReplayResult replay;
        try { replay = CostReplay.Calculate(methods[0], inputs); }
        catch (DomainException ex) { return Unavailable(ex.Code, rows.Length); }
        var physical = await db.Set<StockBalance>().Where(x => x.TenantId == actor.TenantId && x.CompanyId == company && x.WarehouseId == warehouse && x.ProductId == product)
            .Select(x => (decimal?)x.Quantity).SingleOrDefaultAsync(ct) ?? 0;
        if (physical != replay.ClosingQuantity) return Unavailable("cost_stock_reconciliation_failed", rows.Length);
        var result = new CostPreviewView(company, warehouse, product, "Preview", null, "PurchaseNetOnly",
            head.Sequence.ToString(CultureInfo.InvariantCulture), rows.Length, clock.GetUtcNow(),
            new(replay.Method, Q(replay.ReceivedQuantity), V(replay.ReceivedValue), Q(replay.IssuedQuantity), V(replay.IssuedValue),
                Q(replay.ClosingQuantity), V(replay.ClosingValue), replay.Issues.Select(i => new CostIssueView(i.SourceId, Q(i.Quantity), V(i.Value),
                    i.Allocations.Select(a => new CostAllocationView(a.ReceiptId, Q(a.Quantity), V(a.Value))).ToArray())).ToArray()));
        await tx.CommitAsync(ct); return result;
    }
    private static string Q(decimal value) => value.ToString("0.000000", CultureInfo.InvariantCulture);
    private static string V(decimal value) => value.ToString("0.0000", CultureInfo.InvariantCulture);
}
