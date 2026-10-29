namespace Vat.Messaging.Contracts.V1;

// Wire-compatible with the financial owner's FinancialEntryRecordedV1 envelope.
// The independently built consumer does not reference that owner's application assembly.
public sealed record ReportingFinancialPayloadV1(Guid TransactionId, Guid PurchaseId, Guid BranchId, Guid WarehouseId,
    string Kind, DateOnly BusinessDate, string ExternalReference, string Currency, decimal NetAmount,
    decimal TaxAmount, decimal RecoverableTaxAmount, Guid? ReversalOfTransactionId);
public sealed record ReportingFinancialEnvelopeV1(Guid EventId, string EventType, int SchemaVersion, DateTimeOffset OccurredAt,
    Guid TenantId, Guid CompanyId, Guid AggregateId, long AggregateVersion, string CorrelationId, ReportingFinancialPayloadV1 Payload);
public sealed record ReportingGenerationReceiptV1(Guid GenerationId, string DeliveryFingerprint, string Status, Guid? DeadLetterId = null)
{
    // PendingOriginal is durable receipt of evidence, never proof of projection completeness.
    public bool IsCommittedFor(Guid generation, InboxDeliveryV1 delivery) => GenerationId != Guid.Empty && GenerationId == generation
        && DeliveryFingerprint == delivery.Fingerprint()
        && (Status is "Processed" or "Duplicate" or "PendingOriginal" ? DeadLetterId is null
            : Status == "Quarantined" && DeadLetterId is { } id && id != Guid.Empty);
}
