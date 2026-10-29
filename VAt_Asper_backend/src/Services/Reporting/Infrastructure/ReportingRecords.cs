namespace Vat.Infrastructure.Persistence;

public sealed class InboxMessage
{
    public string Consumer { get; set; } = "";
    public Guid EventId { get; set; }
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public string PayloadHash { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTimeOffset ProcessedAt { get; set; }
}

public sealed class ReportingEntry
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid TransactionId { get; set; }
    public Guid PurchaseId { get; set; }
    public Guid BranchId { get; set; }
    public Guid WarehouseId { get; set; }
    public string Kind { get; set; } = "";
    public DateOnly BusinessDate { get; set; }
    public string ExternalReference { get; set; } = "";
    public string Currency { get; set; } = "";
    public decimal NetAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal RecoverableTaxAmount { get; set; }
    public Guid? ReversalOfTransactionId { get; set; }
    public Guid EventId { get; set; }
}

public sealed class ReportingCheckpoint
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public DateTimeOffset LastProcessedAt { get; set; }
    public DateTimeOffset LatestObservedEventAt { get; set; }
}

public sealed class InboxDeadLetter
{
    public Guid Id { get; set; }
    public Guid? TenantId { get; set; }
    public Guid? CompanyId { get; set; }
    public string Fingerprint { get; set; } = "";
    public byte[] Body { get; set; } = [];
    public string? MessageId { get; set; }
    public string? EventType { get; set; }
    public string? ContentType { get; set; }
    public string ErrorCode { get; set; } = "";
    public int Occurrences { get; set; }
    public bool IsTruncated { get; set; }
    public DateTimeOffset FirstSeenAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
}
