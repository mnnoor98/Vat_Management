namespace Vat.Domain.Inventory;

// One immutable row represents both sides of one documented transfer line.
public sealed class TransferJournalEntry
{
    public Guid TenantId { get; init; }
    public Guid CompanyId { get; init; }
    public Guid Id { get; init; }
    public Guid TransferId { get; init; }
    public Guid TransferLineId { get; init; }
    public Guid FromWarehouseId { get; init; }
    public Guid ToWarehouseId { get; init; }
    public Guid ProductId { get; init; }
    public DateOnly BusinessDate { get; init; }
    public decimal Quantity { get; init; }
    public string EvidenceSha256 { get; init; } = "";
    public string RecordedBy { get; init; } = "";
    public DateTimeOffset RecordedAt { get; init; }
}
