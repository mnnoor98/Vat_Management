namespace Vat.Services.Manufacturing.Domain;

public sealed class ProductionOrderDocument
{
    public Guid TenantId { get; set; } public Guid CompanyId { get; set; } public Guid Id { get; set; }
    public string Json { get; set; } = ""; public string Sha256 { get; set; } = "";
}
public sealed class ProductionOrderAudit
{
    public Guid TenantId { get; set; } public Guid CompanyId { get; set; } public Guid Id { get; set; } public Guid OrderId { get; set; }
    public string Action { get; set; } = ""; public string Actor { get; set; } = ""; public int Version { get; set; }
    public string? Reason { get; set; } public string CorrelationId { get; set; } = ""; public DateTimeOffset CreatedAt { get; set; }
}
