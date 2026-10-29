using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vat.Application;
using Vat.Application.Contracts;
using Vat.Infrastructure.Persistence;
using Vat.Messaging.Contracts.V1;

namespace Vat.Infrastructure.Messaging;

public sealed class ReportingInbox(VatDbContext db, TimeProvider clock) : IIncomingEventProcessor, IInboxDeliveryTargetV1
{
    public const string Consumer = "reporting-v1";
    public const int MaxBodyBytes = InboxDeliveryV1.MaxBodyBytes;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = false, MaxDepth = 32 };
    private static readonly HashSet<string> Notifications = [nameof(CustomerPaymentCorrectionPostedV1), nameof(CustomerPaymentPostedV1), nameof(CustomerPaymentAllocatedV1), nameof(SalesDebitPostedV1), nameof(SalesPriceCreditPostedV1), nameof(SalesPartialCreditPostedV1), nameof(SalesCreditPostedV1), nameof(SalesPostedV1), nameof(SalesInvoiceApprovedV1), nameof(PurchaseApprovedV1), nameof(PurchasePostedV1),
        nameof(StockReceiptPostedV1), nameof(TaxPostingCompletedV1), nameof(PurchaseReversedV1), nameof(PeriodClosedV1), nameof(TransferPostedV1)];

    public Task<ProcessedMessage> Process(ReceivedMessage message, CancellationToken ct) =>
        ProcessCore(message, message.Body.Length, Convert.ToHexString(SHA256.HashData(message.Body)), ct);

    public async Task<InboxCommitReceiptV1> Deliver(InboxDeliveryV1 delivery, CancellationToken ct)
    {
        if (!delivery.IsValid()) throw new InvalidOperationException("Invalid inbox delivery contract.");
        // Operator replay may enlist Process in its audit/idempotency transaction;
        // a broker delivery must never return a receipt for an uncommitted outer transaction.
        if (db.Database.CurrentTransaction is not null)
            throw new InvalidOperationException("Inbox delivery requires its own commit boundary.");
        var result = await ProcessCore(new(delivery.BodyPrefix, delivery.MessageId, delivery.EventType, delivery.ContentType),
            delivery.OriginalBodyLength, delivery.BodySha256, ct);
        return new(1, InboxDeliveryV1.Owner, delivery.Fingerprint(), result.Status, result.DeadLetterId);
    }

    private async Task<ProcessedMessage> ProcessCore(ReceivedMessage message, int originalBodyLength, string bodyHash, CancellationToken ct)
    {
        var fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { message.MessageId, message.EventType, message.ContentType, bodyHash })));
        IntegrationEvent<JsonElement>? envelope = null;
        FinancialEntryRecordedV1? financial = null;
        string? error = null;
        try
        {
            if (originalBodyLength > MaxBodyBytes) error = "message_too_large";
            else envelope = JsonSerializer.Deserialize<IntegrationEvent<JsonElement>>(message.Body, Json);
            if (error is null && (envelope is null || envelope.EventId == Guid.Empty || envelope.TenantId == Guid.Empty || envelope.CompanyId == Guid.Empty ||
                envelope.AggregateId == Guid.Empty || envelope.AggregateVersion < 1 || envelope.OccurredAt == default || envelope.Payload.ValueKind != JsonValueKind.Object ||
                !Guid.TryParse(envelope.CorrelationId, out var correlation) || correlation == Guid.Empty)) error = "invalid_envelope";
            if (error is null && (message.ContentType != "application/json" || message.MessageId != envelope!.EventId.ToString() || message.EventType != envelope.EventType))
                error = "metadata_mismatch";
            if (error is null && envelope!.SchemaVersion != 1) error = "unsupported_schema";
            if (error is null && envelope!.EventType == nameof(FinancialEntryRecordedV1))
            {
                financial = envelope.Payload.Deserialize<FinancialEntryRecordedV1>(Json);
                if (!Valid(financial, envelope)) error = "invalid_financial_payload";
            }
            else if (error is null && !Notifications.Contains(envelope!.EventType)) error = "unsupported_event";
        }
        catch (JsonException) { error = "invalid_json"; }
        await using var transaction = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(ct) : null;
        await Lock(envelope is { EventId: var id } && id != Guid.Empty ? $"inbox:{id}" : $"dead-letter:{fingerprint}", ct);
        var now = clock.GetUtcNow();
        if (error is null)
        {
            var previous = await db.Set<InboxMessage>().SingleOrDefaultAsync(x => x.Consumer == Consumer && x.EventId == envelope!.EventId, ct);
            if (previous is not null)
            {
                if (previous.PayloadHash != bodyHash) error = "event_id_payload_conflict";
                else
                {
                    if (transaction is not null) await transaction.CommitAsync(ct);
                    return new("Duplicate");
                }
            }
        }
        if (error is null)
        {
            await Lock($"reporting:{envelope!.TenantId}:{envelope.CompanyId}", ct);
            if (financial is not null)
            {
                var existing = await db.Set<ReportingEntry>().SingleOrDefaultAsync(x => x.TenantId == envelope.TenantId &&
                    x.CompanyId == envelope.CompanyId && x.TransactionId == financial.TransactionId, ct);
                if (existing is not null && !Matches(existing, financial)) error = "transaction_payload_conflict";
                else if (existing is null) db.Add(new ReportingEntry
                {
                    TenantId = envelope.TenantId, CompanyId = envelope.CompanyId, TransactionId = financial.TransactionId,
                    PurchaseId = financial.PurchaseId, BranchId = financial.BranchId, WarehouseId = financial.WarehouseId,
                    Kind = financial.Kind, BusinessDate = financial.BusinessDate, ExternalReference = financial.ExternalReference,
                    Currency = financial.Currency, NetAmount = financial.NetAmount, TaxAmount = financial.TaxAmount,
                    RecoverableTaxAmount = financial.RecoverableTaxAmount, ReversalOfTransactionId = financial.ReversalOfTransactionId, EventId = envelope.EventId
                });
            }
            if (error is null)
            {
                db.Add(new InboxMessage { Consumer = Consumer, EventId = envelope.EventId, TenantId = envelope.TenantId, CompanyId = envelope.CompanyId,
                    PayloadHash = bodyHash, Status = financial is null ? "Ignored" : "Processed", ProcessedAt = now });
                var checkpoint = await db.Set<ReportingCheckpoint>().FindAsync([envelope.TenantId, envelope.CompanyId], ct);
                if (checkpoint is null)
                    db.Add(new ReportingCheckpoint { TenantId = envelope.TenantId, CompanyId = envelope.CompanyId, LastProcessedAt = now, LatestObservedEventAt = envelope.OccurredAt });
                else
                {
                    checkpoint.LastProcessedAt = now;
                    if (envelope.OccurredAt > checkpoint.LatestObservedEventAt) checkpoint.LatestObservedEventAt = envelope.OccurredAt;
                }
                await db.Set<InboxDeadLetter>().Where(x => x.Fingerprint == fingerprint && x.ResolvedAt == null)
                    .ExecuteUpdateAsync(set => set.SetProperty(x => x.ResolvedAt, now), ct);
            }
        }
        Guid? deadLetterId = null;
        if (error is not null)
        {
            // Conflicting copies of one event may have different fingerprints: serialize each quarantine upsert as well.
            await Lock($"dead-letter:{fingerprint}", ct);
            var dead = await db.Set<InboxDeadLetter>().SingleOrDefaultAsync(x => x.Fingerprint == fingerprint, ct);
            if (dead is null)
            {
                dead = new InboxDeadLetter { Id = Guid.CreateVersion7(), Fingerprint = fingerprint,
                    TenantId = envelope?.TenantId == Guid.Empty ? null : envelope?.TenantId,
                    CompanyId = envelope?.CompanyId == Guid.Empty ? null : envelope?.CompanyId,
                    Body = message.Body.AsSpan(0, Math.Min(MaxBodyBytes, message.Body.Length)).ToArray(), IsTruncated = originalBodyLength > MaxBodyBytes,
                    MessageId = Limit(message.MessageId, 200), EventType = Limit(message.EventType, 200), ContentType = Limit(message.ContentType, 100), FirstSeenAt = now };
                db.Add(dead);
            }
            dead.ErrorCode = error;
            dead.LastSeenAt = now;
            dead.ResolvedAt = null;
            dead.Occurrences++;
            deadLetterId = dead.Id;
        }
        await db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return new(error is not null ? "Quarantined" : financial is null ? "Ignored" : "Processed", deadLetterId);
    }

    private async Task Lock(string scope, CancellationToken ct)
    {
        var key = BinaryPrimitives.ReadInt64BigEndian(SHA256.HashData(Encoding.UTF8.GetBytes(scope)));
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})", ct);
    }
    private static string? Limit(string? value, int max) => value is null ? null : value[..Math.Min(value.Length, max)];
    private static bool Valid(FinancialEntryRecordedV1? x, IntegrationEvent<JsonElement> envelope) => x is not null &&
        x.TransactionId != Guid.Empty && x.PurchaseId == envelope.AggregateId && x.BranchId != Guid.Empty && x.WarehouseId != Guid.Empty &&
        x.BusinessDate != default && x.Currency == "BDT" && !string.IsNullOrWhiteSpace(x.ExternalReference) && x.ExternalReference.Length <= 100 &&
        decimal.Round(x.NetAmount, 4) == x.NetAmount && decimal.Round(x.TaxAmount, 4) == x.TaxAmount && decimal.Round(x.RecoverableTaxAmount, 4) == x.RecoverableTaxAmount &&
        x.NetAmount is > -100_000_000_000_000_000_000m and < 100_000_000_000_000_000_000m &&
        x.TaxAmount is > -100_000_000_000_000_000_000m and < 100_000_000_000_000_000_000m &&
        ((x.Kind == "Purchase" && x.ReversalOfTransactionId is null && x.NetAmount >= 0 && x.TaxAmount >= 0 && x.RecoverableTaxAmount >= 0 && x.RecoverableTaxAmount <= x.TaxAmount) ||
         (x.Kind == "PurchaseReversal" && x.ReversalOfTransactionId is not null && x.ReversalOfTransactionId != Guid.Empty && x.ReversalOfTransactionId != x.TransactionId &&
          x.NetAmount <= 0 && x.TaxAmount <= 0 && x.RecoverableTaxAmount <= 0 && x.RecoverableTaxAmount >= x.TaxAmount));
    private static bool Matches(ReportingEntry row, FinancialEntryRecordedV1 x) => row.PurchaseId == x.PurchaseId && row.BranchId == x.BranchId && row.WarehouseId == x.WarehouseId &&
        row.Kind == x.Kind && row.BusinessDate == x.BusinessDate && row.ExternalReference == x.ExternalReference && row.Currency == x.Currency &&
        row.NetAmount == x.NetAmount && row.TaxAmount == x.TaxAmount && row.RecoverableTaxAmount == x.RecoverableTaxAmount && row.ReversalOfTransactionId == x.ReversalOfTransactionId;
}
