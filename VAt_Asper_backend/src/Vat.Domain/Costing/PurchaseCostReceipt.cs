using Vat.Domain.Posting;

namespace Vat.Domain.Costing;

// Immutable source evidence; receipt coverage alone is not an inventory valuation.
public sealed class PurchaseCostReceipt
{
    private PurchaseCostReceipt() { }
    public Guid TenantId { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid Id { get; private set; }
    public Guid SourceMovementId { get; private set; }
    public Guid PurchaseId { get; private set; }
    public Guid PurchaseLineId { get; private set; }
    public Guid PlanId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public Guid ProductId { get; private set; }
    public DateOnly BusinessDate { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal NetAmount { get; private set; }
    public decimal TaxAmount { get; private set; }
    public decimal RecoverableTaxAmount { get; private set; }
    public string Currency { get; private set; } = "";
    public Guid? PolicyId { get; private set; }
    public int? PolicyVersion { get; private set; }
    public string? Method { get; private set; }

    public static PurchaseCostReceipt Capture(PurchasePostingPlan plan, CalculatedLine line, StockMovement movement,
        CostingPolicy? policy, DateTimeOffset now)
    {
        if (plan.Status != PostingPlanStatus.Posted || !plan.Snapshot.Lines.Contains(line) || movement.PlanId != plan.Id || movement.ProductId != line.ProductId ||
            movement.TenantId != plan.TenantId || movement.CompanyId != plan.CompanyId || movement.PurchaseId != plan.PurchaseId ||
            movement.WarehouseId != plan.WarehouseId || movement.BusinessDate != plan.Snapshot.BusinessDate ||
            movement.PurchaseLineId != line.PurchaseLineId || movement.Quantity != line.Quantity || movement.ReversalId is not null || movement.ReversesEntryId is not null)
            throw new DomainException("cost_source_mismatch", "Require an original posted receipt and its frozen calculation line.", 409);
        if (policy is not null && (policy.State != "Approved" || policy.TenantId != plan.TenantId || policy.CompanyId != plan.CompanyId ||
            (policy.ProductId is not null && policy.ProductId != line.ProductId) || policy.EffectiveFrom > movement.BusinessDate || policy.EffectiveUntil <= movement.BusinessDate))
            throw new DomainException("cost_policy_mismatch", "The approved policy must cover this receipt scope and business date.", 409);
        return new() { TenantId = plan.TenantId, CompanyId = plan.CompanyId, Id = Guid.CreateVersion7(), SourceMovementId = movement.Id,
            PurchaseId = plan.PurchaseId, PurchaseLineId = line.PurchaseLineId, PlanId = plan.Id, WarehouseId = plan.WarehouseId,
            ProductId = line.ProductId, BusinessDate = movement.BusinessDate, CreatedAt = now, Quantity = line.Quantity,
            NetAmount = line.NetAmount, TaxAmount = line.TaxAmount, RecoverableTaxAmount = line.RecoverableTaxAmount,
            Currency = plan.Snapshot.Currency, PolicyId = policy?.Id, PolicyVersion = policy?.Version, Method = policy?.Method };
    }
}
