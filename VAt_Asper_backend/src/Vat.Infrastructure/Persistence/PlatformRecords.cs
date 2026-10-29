namespace Vat.Infrastructure.Persistence;

public sealed class OutboxMessage
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid AggregateId { get; set; }
    public long AggregateVersion { get; set; }
    public string EventType { get; set; } = "";
    public string Payload { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DispatchedAt { get; set; }
    public int AttemptCount { get; set; }
    public int TotalAttempts { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public Guid? LeaseToken { get; set; }
    public DateTimeOffset? LeaseExpiresAt { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset? QuarantinedAt { get; set; }
}

public sealed class AuditEntry
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid ResourceId { get; set; }
    public string ActorId { get; set; } = "";
    public string Action { get; set; } = "";
    public string CorrelationId { get; set; } = "";
    public long? DocumentVersion { get; set; }
    public string? Reason { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}

public sealed class IdempotencyRecord
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public string Operation { get; set; } = "";
    public string Key { get; set; } = "";
    public string ActorId { get; set; } = "";
    public string RequestHash { get; set; } = "";
    public int StatusCode { get; set; }
    public string ResponseJson { get; set; } = "";
    public string? Location { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
