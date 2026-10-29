namespace Vat.Services.Integration.Domain;

public sealed class IntegrationConnection
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = "";
    public int Version { get; set; }
    public string Code { get; set; } = "";
    public string Provider { get; set; } = "";
    public string Endpoint { get; set; } = "";
    public string SecretReference { get; set; } = "";
    public bool Enabled { get; set; }
}

public sealed class FieldMapping
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = "";
    public int Version { get; set; }
    public Guid ConnectionId { get; set; }
    public string DocumentType { get; set; } = "";
    public int Revision { get; set; }
    public string Mapping { get; set; } = "";
    public string? ApprovedBy { get; set; }
}

public sealed class InboundBatch
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = "";
    public int Version { get; set; }
    public Guid ConnectionId { get; set; }
    public Guid MappingId { get; set; }
    public string ExternalId { get; set; } = "";
    public string State { get; set; } = "";
    public string PayloadKey { get; set; } = "";
    public string PayloadDigest { get; set; } = "";
    public DateTimeOffset ReceivedAt { get; set; }
}

public sealed class InboundRow
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = "";
    public int Version { get; set; }
    public Guid BatchId { get; set; }
    public int RowNumber { get; set; }
    public string ExternalId { get; set; } = "";
    public string State { get; set; } = "";
    public string? Error { get; set; }
    public Guid? SourceId { get; set; }
}

public sealed class ExternalReference
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = "";
    public int Version { get; set; }
    public Guid ConnectionId { get; set; }
    public string SourceType { get; set; } = "";
    public string ExternalId { get; set; } = "";
    public Guid SourceId { get; set; }
}

public sealed class WebhookSubscription
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = "";
    public int Version { get; set; }
    public Guid ConnectionId { get; set; }
    public string EventType { get; set; } = "";
    public string Endpoint { get; set; } = "";
    public string SigningSecretReference { get; set; } = "";
    public bool Enabled { get; set; }
}

public sealed class DeliveryAttempt
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = "";
    public int Version { get; set; }
    public Guid SubscriptionId { get; set; }
    public Guid EventId { get; set; }
    public int AttemptNumber { get; set; }
    public string State { get; set; } = "";
    public DateTimeOffset? NextAttemptAt { get; set; }
    public DateTimeOffset? LeaseUntil { get; set; }
    public int? HttpStatus { get; set; }
    public string? Error { get; set; }
}

public sealed class IntegrationDeadLetter
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = "";
    public int Version { get; set; }
    public Guid ConnectionId { get; set; }
    public Guid? BatchId { get; set; }
    public Guid? EventId { get; set; }
    public string PayloadKey { get; set; } = "";
    public string Error { get; set; } = "";
    public DateTimeOffset FailedAt { get; set; }
    public DateTimeOffset? ReplayedAt { get; set; }
}
