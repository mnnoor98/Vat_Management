using System.Text.Json;
using Vat.Messaging.Contracts.V1;

namespace Vat.Services.Reporting.Domain;

public static class FinancialDelivery
{
    public const string EventType = "FinancialEntryRecordedV1";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = false, MaxDepth = 32 };
    public static (ReportingSourceRowV1? Row, string? Error) Parse(InboxDeliveryV1 delivery, Guid tenant, Guid company)
    {
        if (delivery.OriginalBodyLength > InboxDeliveryV1.MaxBodyBytes) return (null, "message_too_large");
        try
        {
            var e = JsonSerializer.Deserialize<ReportingFinancialEnvelopeV1>(delivery.BodyPrefix, Json);
            if (e is null || e.EventId == Guid.Empty || e.TenantId != tenant || e.CompanyId != company || e.AggregateId == Guid.Empty
                || e.AggregateVersion < 1 || e.OccurredAt == default || !Guid.TryParse(e.CorrelationId, out var correlation) || correlation == Guid.Empty)
                return (null, "invalid_envelope_scope");
            if (delivery.ContentType != "application/json" || delivery.MessageId != e.EventId.ToString() || delivery.EventType != e.EventType)
                return (null, "metadata_mismatch");
            if (e.SchemaVersion != 1) return (null, "unsupported_schema");
            if (e.EventType != EventType) return (null, "unsupported_event");
            var p = e.Payload;
            if (p is null || p.PurchaseId != e.AggregateId) return (null, "invalid_financial_payload");
            var row = new ReportingSourceRowV1(e.EventId, e.AggregateVersion, p.TransactionId, p.PurchaseId, p.BranchId, p.WarehouseId,
                p.Kind, p.BusinessDate, p.ExternalReference, p.Currency, p.NetAmount, p.TaxAmount, p.RecoverableTaxAmount, p.ReversalOfTransactionId);
            return Valid(row) ? (row, null) : (null, "invalid_financial_payload");
        }
        catch (JsonException) { return (null, "invalid_json"); }
    }
    public static bool Valid(ReportingSourceRowV1 r) => r.EventId != Guid.Empty && r.TransactionId != Guid.Empty && r.PurchaseId != Guid.Empty
        && r.BranchId != Guid.Empty && r.WarehouseId != Guid.Empty && r.AggregateVersion > 0 && r.BusinessDate != default
        && !string.IsNullOrWhiteSpace(r.ExternalReference) && r.ExternalReference.Length <= 100
        && r.Currency is { Length: 3 } && r.Currency.All(c => c is >= 'A' and <= 'Z')
        && Money(r.NetAmount) && Money(r.TaxAmount) && Money(r.RecoverableTaxAmount)
        && (r.Kind == "Purchase" && r.ReversalOfTransactionId is null && r.NetAmount >= 0 && r.TaxAmount >= 0
                && r.RecoverableTaxAmount >= 0 && r.RecoverableTaxAmount <= r.TaxAmount
            || r.Kind == "PurchaseReversal" && r.ReversalOfTransactionId is { } id && id != Guid.Empty && id != r.TransactionId
                && r.NetAmount <= 0 && r.TaxAmount <= 0 && r.RecoverableTaxAmount <= 0 && r.RecoverableTaxAmount >= r.TaxAmount);
    public static bool Reverses(ReportingSourceRowV1 reversal, ReportingSourceRowV1 original) => original.Kind == "Purchase"
        && reversal.Kind == "PurchaseReversal" && reversal.ReversalOfTransactionId == original.TransactionId
        && reversal.PurchaseId == original.PurchaseId && reversal.BranchId == original.BranchId && reversal.WarehouseId == original.WarehouseId
        && reversal.Currency == original.Currency && reversal.ExternalReference == original.ExternalReference
        && reversal.BusinessDate >= original.BusinessDate && reversal.AggregateVersion > original.AggregateVersion
        && reversal.NetAmount == -original.NetAmount && reversal.TaxAmount == -original.TaxAmount
        && reversal.RecoverableTaxAmount == -original.RecoverableTaxAmount;
    private static bool Money(decimal value) => value is > -100000000000000000000m and < 100000000000000000000m && decimal.Round(value, 4) == value;
}
