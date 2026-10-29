using Vat.Domain.Catalog;
using Vat.Domain.Posting;
using Vat.Domain.Tax;

namespace Vat.Application.Contracts;

public sealed record CreateProductRequest(string Reference, string Description, string UnitOfMeasure, string TaxCode);
public sealed record CreateSupplierRequest(string Reference, string Name);
public sealed record CreateWarehouseRequest(Guid BranchId, string Code, string Name);
public sealed record CreateTaxRuleRequest(string TaxCode, DateOnly EffectiveFrom, DateOnly EffectiveUntil,
    decimal RatePercent, decimal RecoverablePercent, string LegalBasis, string EvidenceReference, string Treatment = "Purchase");
public sealed record PreparePostingRequest(long ExpectedVersion, Guid WarehouseId);
public sealed record CancelPostingRequest(long ExpectedVersion, string Reason);

public sealed record ProductResponse(Guid Id, string Reference, string Description, string UnitOfMeasure, string TaxCode)
{
    public static ProductResponse From(Product x) => new(x.Id, x.Reference, x.Description, x.UnitOfMeasure, x.TaxCode);
}
public sealed record SupplierResponse(Guid Id, string Reference, string Name)
{
    public static SupplierResponse From(Supplier x) => new(x.Id, x.Reference, x.Name);
}
public sealed record WarehouseResponse(Guid Id, Guid BranchId, string Code, string Name)
{
    public static WarehouseResponse From(Warehouse x) => new(x.Id, x.BranchId, x.Code, x.Name);
}
public sealed record TaxRuleResponse(Guid Id, string TaxCode, DateOnly EffectiveFrom, DateOnly EffectiveUntil,
    decimal RatePercent, decimal RecoverablePercent, string LegalBasis, string EvidenceReference,
    string CreatedBy, string? PublishedBy, DateTimeOffset? PublishedAt, long Version, string Treatment = "Purchase")
{
    public static TaxRuleResponse From(TaxRule x) => new(x.Id, x.TaxCode, x.EffectiveFrom, x.EffectiveUntil,
        x.RatePercent, x.RecoverablePercent, x.LegalBasis, x.EvidenceReference, x.CreatedBy, x.PublishedBy, x.PublishedAt, x.Version, x.Treatment);
}
public sealed record PostingPlanResponse(Guid Id, Guid PurchaseId, Guid WarehouseId, string Status,
    long SourceVersion, string PreparedBy, string? PostedBy, DateTimeOffset CreatedAt, DateTimeOffset? PostedAt,
    CalculationSnapshot Snapshot, string? CancellationReason)
{
    public static PostingPlanResponse From(PurchasePostingPlan x) => new(x.Id, x.PurchaseId, x.WarehouseId,
        x.Status.ToString(), x.SourceVersion, x.PreparedBy, x.PostedBy, x.CreatedAt, x.PostedAt, x.Snapshot, x.CancellationReason);
}
public sealed record PostingStatusResponse(Guid PurchaseId, string Status, long Version, PostingPlanResponse? LatestPlan);
public sealed record ReconciliationResponse(Guid PurchaseId, string Status, bool IsReconciled,
    int StockEntryCount, int TaxEntryCount, decimal NetAmount, decimal TaxAmount, decimal RecoverableTaxAmount,
    IReadOnlyList<string> Differences);
public sealed record StockBalanceResponse(Guid WarehouseId, Guid ProductId, decimal Quantity, decimal ReservedQuantity = 0, decimal AvailableQuantity = 0);
public sealed record TaxLedgerResponse(Guid Id, Guid PurchaseId, Guid PurchaseLineId, Guid PlanId,
    Guid TaxRuleId, DateOnly BusinessDate, decimal NetAmount, decimal TaxAmount, decimal RecoverableTaxAmount,
    Guid? ReversesEntryId, Guid? ReversalId);
public sealed record PurchasePostedV1(Guid PurchaseId, Guid PlanId, Guid WarehouseId, long DocumentVersion,
    string Currency, decimal NetAmount, decimal TaxAmount, decimal RecoverableTaxAmount);
public sealed record StockReceiptPostedV1(Guid PurchaseId, Guid PlanId, Guid WarehouseId, int LineCount);
public sealed record TaxPostingCompletedV1(Guid PurchaseId, Guid PlanId, decimal TaxAmount, decimal RecoverableTaxAmount);
