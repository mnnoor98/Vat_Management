namespace Vat.Application.Contracts;

public sealed record FinancialEntryRecordedV1(Guid TransactionId, Guid PurchaseId, Guid BranchId, Guid WarehouseId,
    string Kind, DateOnly BusinessDate, string ExternalReference, string Currency, decimal NetAmount,
    decimal TaxAmount, decimal RecoverableTaxAmount, Guid? ReversalOfTransactionId);
public sealed record ReportEntryResponse(Guid TransactionId, Guid PurchaseId, string Kind, DateOnly BusinessDate,
    string ExternalReference, decimal NetAmount, decimal TaxAmount, decimal RecoverableTaxAmount, Guid? ReversalOfTransactionId);
public sealed record ReportFreshness(DateTimeOffset? LastProcessedAt, DateTimeOffset? LatestObservedEventAt,
    int UnprocessedFinancialEvents, int QuarantinedMessages, bool IsCaughtUp);
public sealed record FinancialReportResponse(DateOnly From, DateOnly Until, string Currency, int TransactionCount,
    decimal NetAmount, decimal TaxAmount, decimal RecoverableTaxAmount, ReportFreshness Freshness);
public sealed record RetryMessageRequest(string Reason);
public sealed record MessageStatusResponse(Guid Id, string EventType, DateTimeOffset CreatedAt, DateTimeOffset? DispatchedAt,
    int Attempts, int TotalAttempts, DateTimeOffset? NextAttemptAt, DateTimeOffset? QuarantinedAt, string? LastError);
public sealed record DeadLetterResponse(Guid Id, string? MessageId, string? EventType, string ErrorCode,
    int Occurrences, DateTimeOffset FirstSeenAt, DateTimeOffset LastSeenAt, DateTimeOffset? ResolvedAt, bool IsTruncated);
public sealed record BackfillResponse(int EventsScheduled);
