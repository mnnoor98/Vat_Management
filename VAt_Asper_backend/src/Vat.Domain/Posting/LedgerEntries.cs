namespace Vat.Domain.Posting;

public sealed class StockMovement
{
    private StockMovement() { }
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid PurchaseId { get; private set; }
    public Guid PurchaseLineId { get; private set; }
    public Guid PlanId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public Guid ProductId { get; private set; }
    public decimal Quantity { get; private set; }
    public Guid? ReversesEntryId { get; private set; }
    public Guid? ReversalId { get; private set; }
    public DateOnly BusinessDate { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static StockMovement Receipt(PurchasePostingPlan plan, CalculatedLine line, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(), TenantId = plan.TenantId, CompanyId = plan.CompanyId, PurchaseId = plan.PurchaseId,
        PurchaseLineId = line.PurchaseLineId, PlanId = plan.Id, WarehouseId = plan.WarehouseId, ProductId = line.ProductId,
        Quantity = line.Quantity, BusinessDate = plan.Snapshot.BusinessDate, CreatedAt = now
    };

    public static StockMovement Correction(StockMovement original, PurchaseReversal reversal, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(), TenantId = original.TenantId, CompanyId = original.CompanyId, PurchaseId = original.PurchaseId,
        PurchaseLineId = original.PurchaseLineId, PlanId = original.PlanId, WarehouseId = original.WarehouseId, ProductId = original.ProductId,
        Quantity = -original.Quantity, BusinessDate = reversal.CorrectionDate, ReversesEntryId = original.Id, ReversalId = reversal.Id, CreatedAt = now
    };
}

public sealed class StockBalance
{
    public decimal ReservedQuantity { get; private set; }
    public decimal AvailableQuantity => Quantity - ReservedQuantity;
    public Guid TenantId { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public Guid ProductId { get; private set; }
    public decimal Quantity { get; private set; }
}

public sealed class TaxLedgerEntry
{
    private TaxLedgerEntry() { }
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid PurchaseId { get; private set; }
    public Guid PurchaseLineId { get; private set; }
    public Guid PlanId { get; private set; }
    public Guid TaxRuleId { get; private set; }
    public decimal NetAmount { get; private set; }
    public decimal TaxAmount { get; private set; }
    public decimal RecoverableTaxAmount { get; private set; }
    public Guid? ReversesEntryId { get; private set; }
    public Guid? ReversalId { get; private set; }
    public DateOnly BusinessDate { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static TaxLedgerEntry Input(PurchasePostingPlan plan, CalculatedLine line, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(), TenantId = plan.TenantId, CompanyId = plan.CompanyId, PurchaseId = plan.PurchaseId,
        PurchaseLineId = line.PurchaseLineId, PlanId = plan.Id, TaxRuleId = line.TaxRuleId,
        NetAmount = line.NetAmount, TaxAmount = line.TaxAmount, RecoverableTaxAmount = line.RecoverableTaxAmount,
        BusinessDate = plan.Snapshot.BusinessDate, CreatedAt = now
    };

    public static TaxLedgerEntry Correction(TaxLedgerEntry original, PurchaseReversal reversal, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(), TenantId = original.TenantId, CompanyId = original.CompanyId, PurchaseId = original.PurchaseId,
        PurchaseLineId = original.PurchaseLineId, PlanId = original.PlanId, TaxRuleId = original.TaxRuleId,
        NetAmount = -original.NetAmount, TaxAmount = -original.TaxAmount, RecoverableTaxAmount = -original.RecoverableTaxAmount,
        BusinessDate = reversal.CorrectionDate, ReversesEntryId = original.Id, ReversalId = reversal.Id, CreatedAt = now
    };
}
