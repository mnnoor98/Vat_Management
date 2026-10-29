using Vat.Domain.Purchasing;
using Vat.Domain.Catalog;
using Vat.Domain.Tax;

namespace Vat.Domain.Posting;

public sealed record CalculatedLine(Guid PurchaseLineId, Guid ProductId, string ProductReference,
    string Description, string UnitOfMeasure, decimal Quantity, decimal UnitPrice, decimal NetAmount,
    Guid TaxRuleId, long TaxRuleVersion, string TaxCode, decimal RatePercent, decimal RecoverablePercent,
    decimal TaxAmount, decimal RecoverableTaxAmount, string LegalBasis, string EvidenceReference);

public sealed record CalculationSnapshot(string CalculationVersion, string Currency, Guid SupplierId,
    string SupplierReference, string SupplierName, DateOnly BusinessDate, IReadOnlyList<CalculatedLine> Lines,
    decimal NetAmount, decimal TaxAmount, decimal RecoverableTaxAmount, decimal GrossAmount);

public static class PurchaseCalculator
{
    public const string Version = "domestic-goods-exclusive-v1";

    public static CalculatedLine Calculate(PurchaseLine line, Product product, TaxRule rule, DateOnly businessDate)
    {
        if (line.TenantId != product.TenantId || line.CompanyId != product.CompanyId ||
            line.TenantId != rule.TenantId || line.CompanyId != rule.CompanyId || line.ProductReference != product.Reference)
            throw new DomainException("reference_scope_mismatch", "Product and tax rule must belong to the source line's company and reference.", 422);
        if (rule.Treatment != "Purchase" || rule.PublishedAt is null || rule.TaxCode != product.TaxCode || businessDate < rule.EffectiveFrom || businessDate >= rule.EffectiveUntil)
            throw new DomainException("tax_rule_unavailable", "A published rule for this product and business date is required.", 409);
        var tax = Round(line.NetAmount * rule.RatePercent / 100m);
        return new(line.Id, product.Id, product.Reference, product.Description, product.UnitOfMeasure,
            line.Quantity, line.UnitPrice, line.NetAmount, rule.Id, rule.Version, rule.TaxCode, rule.RatePercent,
            rule.RecoverablePercent, tax, Round(tax * rule.RecoverablePercent / 100m), rule.LegalBasis, rule.EvidenceReference);
    }

    public static CalculationSnapshot Snapshot(Supplier supplier, Purchase purchase, IReadOnlyList<CalculatedLine> lines) => new(
        Version, purchase.Currency, supplier.Id, supplier.Reference, supplier.Name, purchase.BusinessDate,
        Array.AsReadOnly(lines.ToArray()), lines.Sum(x => x.NetAmount), lines.Sum(x => x.TaxAmount),
        lines.Sum(x => x.RecoverableTaxAmount), lines.Sum(x => x.NetAmount + x.TaxAmount));

    private static decimal Round(decimal value) => decimal.Round(value, 4, MidpointRounding.AwayFromZero);
}

public enum PostingPlanStatus { Prepared, Posted, Cancelled }

public sealed class PurchasePostingPlan
{
    private PurchasePostingPlan() { }
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid PurchaseId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public long SourceVersion { get; private set; }
    public long Version { get; private set; }
    public string PreparedBy { get; private set; } = "";
    public string? PostedBy { get; private set; }
    public string? CancellationReason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? PostedAt { get; private set; }
    public PostingPlanStatus Status { get; private set; }
    public CalculationSnapshot Snapshot { get; private set; } = null!;

    public static PurchasePostingPlan Create(Purchase purchase, Guid warehouseId, string actor, CalculationSnapshot snapshot, DateTimeOffset now)
    {
        if (purchase.Status != PurchaseStatus.PostingPrepared)
            throw new DomainException("invalid_state", "Purchase must be prepared before creating its posting plan.", 409);
        return new()
        {
            Id = Guid.CreateVersion7(), TenantId = purchase.TenantId, CompanyId = purchase.CompanyId, PurchaseId = purchase.Id,
            WarehouseId = Guard.Id(warehouseId, nameof(warehouseId)), SourceVersion = purchase.Version,
            Version = 1, PreparedBy = Guard.Text(actor, nameof(actor), 200), Snapshot = snapshot, CreatedAt = now
        };
    }

    public void Post(string actor, string purchaseMaker, DateTimeOffset now)
    {
        RequirePrepared();
        if (actor == PreparedBy || actor == purchaseMaker)
            throw new DomainException("self_approval_forbidden", "The purchase maker and plan preparer cannot authorize posting.", 403);
        PostedBy = Guard.Text(actor, nameof(actor), 200);
        PostedAt = now;
        Status = PostingPlanStatus.Posted;
        Version++;
    }

    public void Cancel(string reason)
    {
        RequirePrepared();
        CancellationReason = Guard.Text(reason, nameof(reason), 1000);
        Status = PostingPlanStatus.Cancelled;
        Version++;
    }

    private void RequirePrepared()
    {
        if (Status != PostingPlanStatus.Prepared)
            throw new DomainException("invalid_state", "Only a prepared plan can be posted or cancelled.", 409);
    }
}
