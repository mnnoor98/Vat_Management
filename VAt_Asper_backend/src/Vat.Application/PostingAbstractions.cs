using Vat.Domain.Catalog;
using Vat.Domain.Posting;
using Vat.Domain.Purchasing;
using Vat.Domain.Tax;

namespace Vat.Application;

public interface IReferenceStore
{
    Task<Product?> ProductByReference(Guid tenantId, Guid companyId, string reference, CancellationToken ct);
    Task<Product?> ProductById(Guid tenantId, Guid companyId, Guid id, CancellationToken ct);
    Task<Supplier?> SupplierByReference(Guid tenantId, Guid companyId, string reference, CancellationToken ct);
    Task<Supplier?> SupplierById(Guid tenantId, Guid companyId, Guid id, CancellationToken ct);
    Task<Warehouse?> Warehouse(Guid tenantId, Guid companyId, Guid id, CancellationToken ct);
    Task<IReadOnlyList<TaxRule>> Rules(Guid tenantId, Guid companyId, int skip, int take, CancellationToken ct);
    Task<TaxRule?> Rule(Guid tenantId, Guid companyId, Guid id, CancellationToken ct);
    Task<TaxRule?> EffectiveRule(Guid tenantId, Guid companyId, string taxCode, DateOnly date, CancellationToken ct);
    Task<TaxRule?> EffectiveSalesRule(Guid tenantId, Guid companyId, string taxCode, DateOnly date, CancellationToken ct);
    Task<bool> HasPublishedOverlap(TaxRule rule, CancellationToken ct);
    Task LockTaxCode(Guid tenantId, Guid companyId, string taxCode, CancellationToken ct);
    void Add(Product value);
    void Add(Supplier value);
    void Add(Warehouse value);
    void Add(TaxRule value);
}

public sealed record PostingReadSnapshot(Purchase Purchase, PurchasePostingPlan? Plan,
    IReadOnlyList<StockMovement> StockEntries, IReadOnlyList<TaxLedgerEntry> TaxEntries, IReadOnlyList<PurchaseReversal> Reversals);

public interface IPostingStore
{
    Task<PurchasePostingPlan?> FindPlan(Guid tenantId, Guid companyId, Guid purchaseId, Guid planId, CancellationToken ct);
    Task<PurchasePostingPlan?> LatestPlan(Guid tenantId, Guid companyId, Guid purchaseId, CancellationToken ct);
    void Add(PurchasePostingPlan plan);
    Task Receive(PurchasePostingPlan plan, DateTimeOffset now, CancellationToken ct);
    void RecordTax(PurchasePostingPlan plan, DateTimeOffset now);
    Task<PostingReadSnapshot?> ReadReconciliation(Guid tenantId, Guid companyId, Guid purchaseId, CancellationToken ct);
    Task<IReadOnlyList<StockBalance>> Balances(Guid tenantId, Guid companyId, Guid warehouseId, Guid? beforeProduct, int take, CancellationToken ct);
    Task<IReadOnlyList<TaxLedgerEntry>> TaxLedger(Guid tenantId, Guid companyId, Guid? purchaseId, Guid? before, int take, CancellationToken ct);
}

public sealed record PostingPolicy(bool Enabled);
