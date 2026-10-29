using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Vat.Infrastructure.Persistence;
using Vat.Messaging.Contracts.V1;

namespace Vat.Infrastructure.Messaging;

// The coordinated financial owner keeps all row/transaction/retry policy here.
// Used directly or behind the dedicated authenticated FinancialOutbox HTTP host.
public sealed class FinancialOutboxLeaseSourceV1(VatDbContext db, TimeProvider clock,
    IOptions<MessagingSettings> settings) : IOutboxLeaseSourceV1
{
    public const string SourceOwner = "financial-coordinated";

    public async Task<ClaimedOutboxMessageV1?> Claim(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        await db.Outbox.Where(x => x.DispatchedAt == null && x.QuarantinedAt == null && x.AttemptCount >= settings.Value.MaxAttempts &&
            (x.LeaseExpiresAt == null || x.LeaseExpiresAt <= now)).ExecuteUpdateAsync(set =>
                set.SetProperty(x => x.QuarantinedAt, now).SetProperty(x => x.LastError, "retry_limit_reached")
                    .SetProperty(x => x.LeaseToken, (Guid?)null).SetProperty(x => x.LeaseExpiresAt, (DateTimeOffset?)null), ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var row = await db.Outbox.FromSqlInterpolated($"""
            SELECT * FROM platform.outbox_messages
            WHERE "DispatchedAt" IS NULL AND "QuarantinedAt" IS NULL
              AND "AttemptCount" < {settings.Value.MaxAttempts}
              AND ("NextAttemptAt" IS NULL OR "NextAttemptAt" <= {now})
              AND ("LeaseExpiresAt" IS NULL OR "LeaseExpiresAt" <= {now})
            ORDER BY "CreatedAt", "Id" LIMIT 1 FOR UPDATE SKIP LOCKED
            """).SingleOrDefaultAsync(ct);
        if (row is null) return null;
        row.LeaseToken = Guid.NewGuid();
        row.LeaseExpiresAt = now.AddSeconds(settings.Value.LeaseSeconds);
        row.AttemptCount++;
        row.TotalAttempts++;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        var payload = Encoding.UTF8.GetBytes(row.Payload);
        var message = new ClaimedOutboxMessageV1(new(1, SourceOwner, row.Id, row.TenantId, row.CompanyId, row.LeaseToken.Value),
            row.AggregateId, row.AggregateVersion, row.EventType, row.CreatedAt, payload,
            Convert.ToHexString(SHA256.HashData(payload)), row.LeaseExpiresAt.Value, row.AttemptCount);
        db.Entry(row).State = EntityState.Detached;
        return message;
    }

    private IQueryable<OutboxMessage> Active(OutboxReceiptV1 receipt, DateTimeOffset now) =>
        db.Outbox.Where(x => x.Id == receipt.MessageId && x.TenantId == receipt.TenantId && x.CompanyId == receipt.CompanyId
            && x.LeaseToken == receipt.LeaseToken && x.LeaseExpiresAt > now && x.DispatchedAt == null && x.QuarantinedAt == null);

    private static bool Accepts(OutboxReceiptV1 receipt) => receipt is not null && receipt.IsWellFormed()
        && receipt.SourceOwner == SourceOwner;

    public async Task<bool> Confirm(OutboxReceiptV1 receipt, CancellationToken ct)
    {
        if (!Accepts(receipt)) return false;
        var now = clock.GetUtcNow();
        return await Active(receipt, now).ExecuteUpdateAsync(set => set.SetProperty(x => x.DispatchedAt, now)
            .SetProperty(x => x.LeaseToken, (Guid?)null).SetProperty(x => x.LeaseExpiresAt, (DateTimeOffset?)null)
            .SetProperty(x => x.LastError, (string?)null), ct) == 1;
    }

    public async Task<bool> Fail(OutboxReceiptV1 receipt, string errorCode, CancellationToken ct)
    {
        if (!Accepts(receipt)) return false;
        // Attempt/retry policy comes from the owner row, never a caller-supplied attempt.
        var attempt = await Active(receipt, clock.GetUtcNow()).Select(x => (int?)x.AttemptCount).SingleOrDefaultAsync(ct);
        if (attempt is null) return false;
        var now = clock.GetUtcNow();
        var next = now.AddSeconds(Math.Min(300, Math.Pow(2, Math.Min(8, attempt.Value - 1))));
        DateTimeOffset? quarantined = attempt.Value >= settings.Value.MaxAttempts ? now : null;
        var code = errorCode is "publisher_rejected" or "broker_unavailable" or "publish_timeout" or "invalid_dispatch_contract"
            ? errorCode : "broker_unavailable";
        return await Active(receipt, now).ExecuteUpdateAsync(set => set.SetProperty(x => x.LastError, code)
            .SetProperty(x => x.NextAttemptAt, next).SetProperty(x => x.QuarantinedAt, quarantined)
            .SetProperty(x => x.LeaseToken, (Guid?)null).SetProperty(x => x.LeaseExpiresAt, (DateTimeOffset?)null), ct) == 1;
    }
}
