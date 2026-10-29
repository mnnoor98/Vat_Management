namespace Vat.Services.Manufacturing.Domain;

public sealed class BomHeader
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = "";
    public int Version { get; set; }
    public Guid ProductId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
}

public sealed class CoefficientVersion
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = "";
    public int Version { get; set; }
    public Guid BomId { get; set; }
    public int Revision { get; set; }
    public decimal OutputQuantity { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveUntil { get; set; }
    public string State { get; set; } = "";
    public string? ApprovedBy { get; set; }
}

public sealed class BomLine
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = "";
    public int Version { get; set; }
    public Guid CoefficientVersionId { get; set; }
    public Guid ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal WastagePercent { get; set; }
}

public sealed class ProductionOrder
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = "";
    public int Version { get; set; }
    public Guid CoefficientVersionId { get; set; }
    public Guid WarehouseId { get; set; }
    public string Reference { get; set; } = "";
    public DateOnly BusinessDate { get; set; }
    public decimal PlannedQuantity { get; set; }
    public string State { get; set; } = "";
    public string? ApprovedBy { get; set; }
}

public sealed class MaterialIssue
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = "";
    public int Version { get; set; }
    public Guid ProductionOrderId { get; set; }
    public Guid ProductId { get; set; }
    public Guid WarehouseId { get; set; }
    public decimal Quantity { get; set; }
    public DateOnly BusinessDate { get; set; }
    public string SourceReference { get; set; } = "";
}

public sealed class ProductionReceipt
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = "";
    public int Version { get; set; }
    public Guid ProductionOrderId { get; set; }
    public Guid ProductId { get; set; }
    public Guid WarehouseId { get; set; }
    public decimal Quantity { get; set; }
    public DateOnly BusinessDate { get; set; }
    public string SourceReference { get; set; } = "";
}

public sealed class WastageRecord
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = "";
    public int Version { get; set; }
    public Guid ProductionOrderId { get; set; }
    public Guid ProductId { get; set; }
    public decimal Quantity { get; set; }
    public string Reason { get; set; } = "";
    public string? ApprovedBy { get; set; }
}
