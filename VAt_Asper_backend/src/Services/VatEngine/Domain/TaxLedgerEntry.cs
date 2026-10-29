namespace Vat.Services.VatEngine;

public sealed class TaxLedgerEntry
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid PurchaseId { get; set; }
    public Guid PurchaseLineId { get; set; }
    public Guid PlanId { get; set; }
    public Guid TaxRuleId { get; set; }
    public decimal NetAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal RecoverableTaxAmount { get; set; }
    public Guid? ReversesEntryId { get; set; }
    public Guid? ReversalId { get; set; }
    public DateOnly BusinessDate { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
