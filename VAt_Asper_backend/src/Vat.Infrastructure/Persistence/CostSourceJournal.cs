using Microsoft.EntityFrameworkCore;
using Vat.Domain.Costing;
using Vat.Domain.Posting;
using Vat.Domain.Sales;
using Vat.Domain.Inventory;

namespace Vat.Infrastructure.Persistence;

public sealed class CostSourceHead
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public long Sequence { get; set; }
    public bool EmptyOpeningProven { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
public sealed class CostSourceEvent
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public long Sequence { get; set; }
    public Guid? ReceiptId { get; set; }
    public Guid? SalesEntryId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid ProductId { get; set; }
    public decimal Quantity { get; set; }
    public Guid? PolicyId { get; set; }
    public int? PolicyVersion { get; set; }
    public string? Method { get; set; }
}

internal static class CostSourceJournal
{
    public static async Task<CostSourceHead> Begin(VatDbContext db, Guid tenant, Guid company, DateTimeOffset now, CancellationToken ct)
    {
        // The company fence is acquired before stock rows and held until the source commit.
        await CostingStore.LockScope(db, tenant, company, false, ct);
        var head = db.Set<CostSourceHead>().Local.SingleOrDefault(x => x.TenantId == tenant && x.CompanyId == company) ??
            await db.Set<CostSourceHead>().SingleOrDefaultAsync(x => x.TenantId == tenant && x.CompanyId == company, ct);
        if (head is not null) return head;
        var legacy = await db.Set<StockBalance>().AnyAsync(x => x.TenantId == tenant && x.CompanyId == company && x.Quantity != 0, ct) ||
            await db.Set<StockMovement>().AnyAsync(x => x.TenantId == tenant && x.CompanyId == company, ct) ||
            await db.Set<SalesPostingEntry>().AnyAsync(x => x.TenantId == tenant && x.CompanyId == company && x.Quantity != 0, ct) ||
            await db.Set<TransferJournalEntry>().AnyAsync(x => x.TenantId == tenant && x.CompanyId == company, ct)||
            await db.Set<ProductionStockMovement>().AnyAsync(x=>x.TenantId==tenant&&x.CompanyId==company,ct);
        head = new() { TenantId = tenant, CompanyId = company, EmptyOpeningProven = !legacy, CreatedAt = now };
        db.Add(head); return head;
    }
    public static void Receipt(VatDbContext db, CostSourceHead head, PurchaseCostReceipt receipt)
    {
        db.Add(new CostSourceEvent { TenantId = head.TenantId, CompanyId = head.CompanyId, Sequence = ++head.Sequence,
            ReceiptId = receipt.Id, WarehouseId = receipt.WarehouseId, ProductId = receipt.ProductId, Quantity = receipt.Quantity,
            PolicyId = receipt.PolicyId, PolicyVersion = receipt.PolicyVersion, Method = receipt.Method });
    }
    public static void Issue(VatDbContext db, CostSourceHead head, SalesPostingEntry entry, CostingPolicy? policy)
    {
        db.Add(new CostSourceEvent { TenantId = head.TenantId, CompanyId = head.CompanyId, Sequence = ++head.Sequence,
            SalesEntryId = entry.Id, WarehouseId = entry.WarehouseId, ProductId = entry.ProductId, Quantity = entry.Quantity,
            PolicyId = policy?.Id, PolicyVersion = policy?.Version, Method = policy?.Method });
    }
}
