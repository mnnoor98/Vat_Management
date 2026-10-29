namespace Vat.Services.Inventory;

public sealed class TransferHeader
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid FromWarehouseId { get; set; }
    public Guid ToWarehouseId { get; set; }
    public string Reference { get; set; } = "";
    public DateOnly BusinessDate { get; set; }
    public string State { get; set; } = "Draft";
    public string? VehicleNumber { get; set; }
    public string? DriverName { get; set; }
    public string? Remarks { get; set; }
    public string? DispatchTime { get; set; }
    public string CreatedBy { get; set; } = "";
    public string? ApprovedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public int Version { get; set; } = 1;
}

public sealed class TransferLine
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid TransferId { get; set; }
    public Guid ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public string? Remarks { get; set; }
    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public int Version { get; set; } = 1;
}

