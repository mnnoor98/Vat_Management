namespace Vat.Services.Manufacturing.Domain;

public sealed class CoefficientDocument
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid Id { get; set; }
    public string SnapshotJson { get; set; } = "";
    public string SnapshotSha256 { get; set; } = "";
}

public sealed class CoefficientCommand
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid Id { get; set; }
    public string Actor { get; set; } = "";
    public string RequestHash { get; set; } = "";
    public string ResponseJson { get; set; } = "";
    public int StatusCode { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class CoefficientAudit
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid Id { get; set; }
    public Guid CoefficientId { get; set; }
    public string Actor { get; set; } = "";
    public string Action { get; set; } = "";
    public string? Reason { get; set; }
    public string CorrelationId { get; set; } = "";
    public int Version { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
