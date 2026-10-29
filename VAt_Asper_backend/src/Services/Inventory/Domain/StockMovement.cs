namespace Vat.Services.Inventory;

public sealed class StockMovement
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid? PurchaseId { get; set; }
    public Guid? PurchaseLineId { get; set; }
    public Guid? PlanId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid ProductId { get; set; }
    public decimal Quantity { get; set; }
    public Guid? ReversesEntryId { get; set; }
    public Guid? ReversalId { get; set; }
    public DateOnly BusinessDate { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

