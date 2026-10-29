namespace Vat.Services.Inventory;

public sealed class StockAdjustmentHeader
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid WarehouseId { get; set; }
    public string Reference { get; set; } = "";
    public DateOnly BusinessDate { get; set; }
    public string Reason { get; set; } = "";
    public string State { get; set; } = "Draft";
    public string? ApprovedBy { get; set; }
    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public int Version { get; set; } = 1;
}

public sealed class StockAdjustmentLine
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid AdjustmentId { get; set; }
    public Guid ProductId { get; set; }
    public decimal Quantity { get; set; }
    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public int Version { get; set; } = 1;
}

