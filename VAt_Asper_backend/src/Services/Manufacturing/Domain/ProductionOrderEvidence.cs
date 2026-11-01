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
public sealed class ProductionExecutionInbox
{
    public Guid TenantId { get; set; } public Guid CompanyId { get; set; } public Guid EventId { get; set; }
    public Guid OrderId { get; set; } public Guid SessionId { get; set; } public Guid? OperationId { get; set; }
    public string EventType { get; set; } = ""; public string PayloadSha256 { get; set; } = "";
    public long SessionVersion { get; set; } public string Status { get; set; } = "";
    public DateTimeOffset OccurredAt { get; set; } public DateTimeOffset ProcessedAt { get; set; }
}
public sealed class ProductionExecutionCheckpoint
{
    public Guid TenantId { get; set; } public Guid CompanyId { get; set; } public Guid OrderId { get; set; }
    public Guid SessionId { get; set; } public long SessionVersion { get; set; }
    public string ExecutionState { get; set; } = ""; public string WipValue { get; set; } = "0.0000";
    public string ReceivedQuantity { get; set; } = "0.000000"; public DateTimeOffset UpdatedAt { get; set; }
}
public sealed class ProductionExecutionDeadLetter
{
    public Guid Id { get; set; } public Guid? EventId { get; set; } public Guid? TenantId { get; set; } public Guid? CompanyId { get; set; }
    public string Fingerprint { get; set; } = ""; public string ErrorCode { get; set; } = ""; public byte[] Body { get; set; } = [];
    public int Occurrences { get; set; } public DateTimeOffset FirstSeenAt { get; set; } public DateTimeOffset LastSeenAt { get; set; }
}
