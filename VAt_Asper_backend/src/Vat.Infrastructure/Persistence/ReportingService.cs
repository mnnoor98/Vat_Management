using System.Data;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Vat.Application;
using Vat.Application.Contracts;
using Vat.Application.Security;
using Vat.Domain;
using Vat.Domain.Posting;
using Vat.Infrastructure.Messaging;

namespace Vat.Infrastructure.Persistence;

public sealed class ReportingService(VatDbContext db, IIncomingEventProcessor inbox, IPeriodStore periods,
    IPostingStore postings, IEventWriter events, IAuditWriter audit, TimeProvider clock) : IReportingService
{
    public async Task<FinancialReportResponse> Summary(Actor actor, Guid companyId, DateOnly from, DateOnly until, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        Dates(from, until);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var query = db.Set<ReportingEntry>().Where(x => x.TenantId == actor.TenantId && x.CompanyId == companyId && from <= x.BusinessDate && x.BusinessDate < until);
        var count = await query.CountAsync(ct);
        var net = await query.SumAsync(x => x.NetAmount, ct);
        var tax = await query.SumAsync(x => x.TaxAmount, ct);
        var recoverable = await query.SumAsync(x => x.RecoverableTaxAmount, ct);
        var pending = await db.Outbox.CountAsync(x => x.TenantId == actor.TenantId && x.CompanyId == companyId && x.EventType == nameof(FinancialEntryRecordedV1) &&
            !db.Set<InboxMessage>().Any(i => i.Consumer == ReportingInbox.Consumer && i.EventId == x.Id && i.Status == "Processed"), ct);
        var quarantined = await db.Set<InboxDeadLetter>().CountAsync(x => x.TenantId == actor.TenantId && x.CompanyId == companyId && x.ResolvedAt == null, ct);
        var checkpoint = await db.Set<ReportingCheckpoint>().FindAsync([actor.TenantId, companyId], ct);
        // Legacy transactions without the new event are incomplete until explicitly backfilled.
        var missingOriginal = await db.Set<PurchasePostingPlan>().AnyAsync(x => x.TenantId == actor.TenantId && x.CompanyId == companyId && x.Status == PostingPlanStatus.Posted &&
            !db.Set<ReportingEntry>().Any(r => r.TenantId == actor.TenantId && r.CompanyId == companyId && r.TransactionId == x.Id), ct);
        var missingReversal = await db.Set<PurchaseReversal>().AnyAsync(x => x.TenantId == actor.TenantId && x.CompanyId == companyId && x.Status == ReversalStatus.Posted &&
            !db.Set<ReportingEntry>().Any(r => r.TenantId == actor.TenantId && r.CompanyId == companyId && r.TransactionId == x.Id), ct);
        await transaction.CommitAsync(ct);
        return new(from, until, "BDT", count, net, tax, recoverable,
            new(checkpoint?.LastProcessedAt, checkpoint?.LatestObservedEventAt, pending, quarantined, pending == 0 && quarantined == 0 && !missingOriginal && !missingReversal));
    }

    public async Task<IReadOnlyList<ReportEntryResponse>> Entries(Actor actor, Guid companyId, DateOnly from, DateOnly until, Guid? before, int take, CancellationToken ct)
    {
        actor.RequireCompany(companyId); Dates(from, until); Page(take);
        var query = db.Set<ReportingEntry>().AsNoTracking().Where(x => x.TenantId == actor.TenantId && x.CompanyId == companyId && from <= x.BusinessDate && x.BusinessDate < until);
        if (before is not null) query = query.Where(x => x.TransactionId.CompareTo(before.Value) < 0);
        return await query.OrderByDescending(x => x.TransactionId).Take(take).Select(x => new ReportEntryResponse(x.TransactionId,
            x.PurchaseId, x.Kind, x.BusinessDate, x.ExternalReference, x.NetAmount, x.TaxAmount, x.RecoverableTaxAmount, x.ReversalOfTransactionId)).ToArrayAsync(ct);
    }

    public async Task<CommandOutcome> Backfill(Actor actor, Guid companyId, string reason, string correlationId, CancellationToken ct)
    {
        actor.RequireCompany(companyId); Guard.Text(reason, nameof(reason), 1000);
        await periods.LockCompany(actor.TenantId, companyId, true, ct);
        if (!await db.Companies.AnyAsync(x => x.TenantId == actor.TenantId && x.Id == companyId, ct)) throw new DomainException("not_found", "Company not found.", 404);
        // Restore original identity/envelope when this source already has sequence evidence.
        // This command's transaction and audit cover recovery; dispatch metadata starts fresh.
        var restored = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO platform.outbox_messages
              ("Id", "TenantId", "CompanyId", "AggregateId", "AggregateVersion", "EventType", "Payload", "CreatedAt", "AttemptCount", "TotalAttempts")
            SELECT j."EventId", j."TenantId", j."CompanyId", (j."Payload"->>'aggregateId')::uuid,
              (j."Payload"->>'aggregateVersion')::bigint, 'FinancialEntryRecordedV1', j."Payload",
              (j."Payload"->>'occurredAt')::timestamptz, 0, 0
            FROM platform.reporting_source_journal j
            WHERE j."TenantId" = {actor.TenantId} AND j."CompanyId" = {companyId}
              AND NOT EXISTS (SELECT 1 FROM platform.outbox_messages o WHERE o."Id" = j."EventId")
            """, ct);
        var plans = await db.Set<PurchasePostingPlan>().Where(x => x.TenantId == actor.TenantId && x.CompanyId == companyId && x.Status == PostingPlanStatus.Posted).ToArrayAsync(ct);
        var existing = (await db.Outbox.Where(x => x.TenantId == actor.TenantId && x.CompanyId == companyId && x.EventType == nameof(FinancialEntryRecordedV1))
            .Select(x => new { x.AggregateId, x.AggregateVersion }).ToArrayAsync(ct)).Select(x => (x.AggregateId, x.AggregateVersion)).ToHashSet();
        var count = restored;
        foreach (var plan in plans)
        {
            var original = await postings.ReadReconciliation(actor.TenantId, companyId, plan.PurchaseId, ct);
            if (original is null || !PurchaseReconciler.Evaluate(original).IsReconciled)
                throw new DomainException("reconciliation_failed", "Reconcile source ledgers before reporting backfill.", 409);
            var purchase = original.Purchase;
            void Schedule(Guid transactionId, long version, string kind, DateOnly date, decimal sign, Guid? reversalOf)
            {
                if (!existing.Add((purchase.Id, version))) return;
                events.Append(actor.TenantId, companyId, purchase.Id, version, nameof(FinancialEntryRecordedV1), correlationId,
                    new FinancialEntryRecordedV1(transactionId, purchase.Id, purchase.BranchId, plan.WarehouseId, kind, date,
                        purchase.ExternalReference, purchase.Currency, sign * plan.Snapshot.NetAmount, sign * plan.Snapshot.TaxAmount, sign * plan.Snapshot.RecoverableTaxAmount, reversalOf));
                count++;
            }
            Schedule(plan.Id, purchase.PostedVersion!.Value, "Purchase", purchase.BusinessDate, 1, null);
            foreach (var reversal in original.Reversals.Where(x => x.Status == ReversalStatus.Posted))
                Schedule(reversal.Id, reversal.SourceVersion + 1, "PurchaseReversal", reversal.CorrectionDate, -1, plan.Id);
        }
        audit.Append(actor, companyId, companyId, "ReportingBackfillScheduled", correlationId, reason: reason);
        return new(200, new BackfillResponse(count));
    }

    public async Task<CommandOutcome> Rebuild(Actor actor, Guid companyId, string reason, string correlationId, CancellationToken ct)
    {
        actor.RequireCompany(companyId); Guard.Text(reason, nameof(reason), 1000);
        var key = BinaryPrimitives.ReadInt64BigEndian(SHA256.HashData(Encoding.UTF8.GetBytes($"reporting:{actor.TenantId}:{companyId}")));
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})", ct);
        var removed = await db.Set<ReportingEntry>().Where(x => x.TenantId == actor.TenantId && x.CompanyId == companyId).ExecuteDeleteAsync(ct);
        await db.Set<InboxMessage>().Where(x => x.Consumer == ReportingInbox.Consumer && x.TenantId == actor.TenantId && x.CompanyId == companyId).ExecuteDeleteAsync(ct);
        await db.Set<ReportingCheckpoint>().Where(x => x.TenantId == actor.TenantId && x.CompanyId == companyId).ExecuteDeleteAsync(ct);
        var queued = await db.Outbox.Where(x => x.TenantId == actor.TenantId && x.CompanyId == companyId && x.EventType == nameof(FinancialEntryRecordedV1))
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.DispatchedAt, (DateTimeOffset?)null).SetProperty(x => x.AttemptCount, 0)
                .SetProperty(x => x.NextAttemptAt, clock.GetUtcNow()).SetProperty(x => x.QuarantinedAt, (DateTimeOffset?)null)
                .SetProperty(x => x.LastError, (string?)null).SetProperty(x => x.LeaseToken, (Guid?)null)
                .SetProperty(x => x.LeaseExpiresAt, (DateTimeOffset?)null), ct);
        audit.Append(actor, companyId, companyId, "ReportingRebuildRequested", correlationId, reason: reason);
        return new(200, new { removedEntries = removed, queuedMessages = queued });
    }

    public async Task<IReadOnlyList<MessageStatusResponse>> Outbox(Actor actor, Guid companyId, Guid? before, int take, CancellationToken ct)
    {
        actor.RequireCompany(companyId); Page(take);
        var query = db.Outbox.AsNoTracking().Where(x => x.TenantId == actor.TenantId && x.CompanyId == companyId);
        if (before is not null) query = query.Where(x => x.Id.CompareTo(before.Value) < 0);
        return await query.OrderByDescending(x => x.Id).Take(take).Select(x => new MessageStatusResponse(x.Id, x.EventType, x.CreatedAt,
            x.DispatchedAt, x.AttemptCount, x.TotalAttempts, x.NextAttemptAt, x.QuarantinedAt, x.LastError)).ToArrayAsync(ct);
    }

    public async Task<CommandOutcome> RetryOutbox(Actor actor, Guid companyId, Guid id, string reason, string correlationId, CancellationToken ct)
    {
        actor.RequireCompany(companyId); Guard.Text(reason, nameof(reason), 1000);
        var now = clock.GetUtcNow();
        var affected = await db.Outbox.Where(x => x.Id == id && x.TenantId == actor.TenantId && x.CompanyId == companyId && x.DispatchedAt == null &&
            (x.QuarantinedAt != null || x.LastError != null) && (x.LeaseExpiresAt == null || x.LeaseExpiresAt <= now))
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.AttemptCount, 0).SetProperty(x => x.QuarantinedAt, (DateTimeOffset?)null)
                .SetProperty(x => x.NextAttemptAt, now).SetProperty(x => x.LastError, (string?)null)
                .SetProperty(x => x.LeaseToken, (Guid?)null).SetProperty(x => x.LeaseExpiresAt, (DateTimeOffset?)null), ct);
        if (affected == 0) throw new DomainException("retry_unavailable", "Message is missing, delivered, in flight or not failed.", 409);
        audit.Append(actor, companyId, id, "OutboxRetryRequested", correlationId, reason: reason);
        return new(200, new { id, status = "Pending" });
    }

    public async Task<IReadOnlyList<DeadLetterResponse>> DeadLetters(Actor actor, Guid companyId, Guid? before, int take, CancellationToken ct)
    {
        actor.RequireCompany(companyId); Page(take);
        var query = db.Set<InboxDeadLetter>().AsNoTracking().Where(x => x.TenantId == actor.TenantId && x.CompanyId == companyId);
        if (before is not null) query = query.Where(x => x.Id.CompareTo(before.Value) < 0);
        return await query.OrderByDescending(x => x.Id).Take(take).Select(x => new DeadLetterResponse(x.Id, x.MessageId,
            x.EventType, x.ErrorCode, x.Occurrences, x.FirstSeenAt, x.LastSeenAt, x.ResolvedAt, x.IsTruncated)).ToArrayAsync(ct);
    }

    public async Task<CommandOutcome> Replay(Actor actor, Guid companyId, Guid id, string reason, string correlationId, CancellationToken ct)
    {
        actor.RequireCompany(companyId); Guard.Text(reason, nameof(reason), 1000);
        var dead = await db.Set<InboxDeadLetter>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.TenantId == actor.TenantId && x.CompanyId == companyId, ct)
            ?? throw new DomainException("not_found", "Dead letter not found.", 404);
        if (dead.IsTruncated) throw new DomainException("replay_unavailable", "An oversized, truncated message cannot be replayed.", 409);
        var result = await inbox.Process(new(dead.Body, dead.MessageId, dead.EventType, dead.ContentType), ct);
        audit.Append(actor, companyId, id, "InboxReplayRequested", correlationId, reason: reason);
        return new(200, result);
    }

    private static void Dates(DateOnly from, DateOnly until)
    {
        if (from == default || until <= from) throw new DomainException("validation_failed", "Report dates require a non-empty [from, until) interval.");
    }
    private static void Page(int take)
    {
        if (take is < 1 or > 100) throw new DomainException("validation_failed", "take must be between 1 and 100.");
    }
}
