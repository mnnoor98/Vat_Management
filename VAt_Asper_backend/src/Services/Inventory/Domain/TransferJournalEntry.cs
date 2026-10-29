namespace Vat.Services.Inventory;

// One immutable row represents both sides of one documented transfer line.
public sealed class TransferJournalEntry
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid Id { get; set; }
    public Guid TransferId { get; set; }
    public Guid TransferLineId { get; set; }
    public Guid FromWarehouseId { get; set; }
    public Guid ToWarehouseId { get; set; }
    public Guid ProductId { get; set; }
    public DateOnly BusinessDate { get; set; }
    public decimal Quantity { get; set; }
    public string EvidenceSha256 { get; set; } = "";
    public string RecordedBy { get; set; } = "";
    public DateTimeOffset RecordedAt { get; set; }
}
