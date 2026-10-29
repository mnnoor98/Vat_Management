using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vat.Application.Security;
using Vat.Domain;
using Vat.Messaging.Contracts.V1;

namespace Vat.Infrastructure.Persistence;

public sealed class ReportingSourceJournal(VatDbContext db)
{
    private sealed record Metadata(long Sequence, Guid EventId, long Bytes);
    private sealed record Body(long Sequence, Guid EventId, string EnvelopeJson);

    public async Task<ReportingSourceJournalPageV1> Read(Actor actor, Guid companyId, Guid epoch,
        long after, long through, int take, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        if (epoch == Guid.Empty || after < 0 || through < after || take is < 1 or > ReportingSourceJournalV1.MaxTake)
            throw new DomainException("validation_failed", "Require a nonempty epoch, 0 <= after <= through and take 1..100.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        ct = deadline.Token;
        var head = await db.Database.SqlQuery<ReportingSourceBoundaryV1>($"""
            SELECT "Epoch", "Sequence", "LegacyThroughSequence" FROM platform.reporting_source_heads
            WHERE "TenantId" = {actor.TenantId} AND "CompanyId" = {companyId}
            """).SingleOrDefaultAsync(ct) ?? throw new DomainException("checkpoint_unavailable", "Prepare an export before reading the journal.", 409);
        if (head.Epoch != epoch)
            throw new DomainException("source_epoch_conflict", "Source epoch changed; bootstrap from a verified export.", 409);
        if (through > head.Sequence)
            throw new DomainException("source_checkpoint_ahead", "Requested boundary is ahead of the committed source.", 409);

        // This prefix is immutable and was committed atomically with the observed head.
        // Inspect sizes without materializing payloads, even if a corrupt/large event exists.
        var metadata = await db.Database.SqlQuery<Metadata>($"""
            SELECT "Sequence", "EventId", octet_length("Payload"::text)::bigint AS "Bytes"
            FROM platform.reporting_source_journal
            WHERE "TenantId" = {actor.TenantId} AND "CompanyId" = {companyId}
              AND "Sequence" > {after} AND "Sequence" <= {through}
            ORDER BY "Sequence" LIMIT {take}
            """).ToArrayAsync(ct);
        var expectedCount = (int)Math.Min(take, through - after);
        if (metadata.Length != expectedCount || metadata.Where((x, i) => x.Sequence != after + i + 1).Any())
            throw new DomainException("source_sequence_gap", "Source journal has a missing position; operator recovery is required.", 409);

        long last = after, bytes = 0;
        foreach (var entry in metadata)
        {
            // A JSON string may escape every UTF-8 byte using six ASCII bytes.
            if (entry.Bytes > ReportingSourceJournalV1.MaxEventBytes ||
                bytes + entry.Bytes * 6 + 512 > ReportingSourceJournalV1.MaxPageBytes - 4096)
            {
                if (last == after)
                    throw new DomainException("source_event_limit", "Next event exceeds the V1 journal body budget; it cannot be skipped.", 409);
                break;
            }
            bytes += entry.Bytes * 6 + 512;
            last = entry.Sequence;
        }
        var bodies = last == after ? [] : await db.Database.SqlQuery<Body>($"""
            SELECT "Sequence", "EventId", "Payload"::text AS "EnvelopeJson" FROM platform.reporting_source_journal
            WHERE "TenantId" = {actor.TenantId} AND "CompanyId" = {companyId}
              AND "Sequence" > {after} AND "Sequence" <= {last}
            ORDER BY "Sequence"
            """).ToArrayAsync(ct);
        if (bodies.Length != last - after || bodies.Where((x, i) => x.Sequence != metadata[i].Sequence || x.EventId != metadata[i].EventId).Any())
            throw new DomainException("source_sequence_gap", "Source journal changed while reading; retry after operator review.", 409);
        var page = new ReportingSourceJournalPageV1(1, actor.TenantId, companyId, ReportingSourceV1.Scope,
            epoch, head.LegacyThroughSequence, after, through, last, last == through,
            bodies.Select(x => new ReportingSourceJournalEventV1(x.Sequence, x.EventId, x.EnvelopeJson)).ToArray());
        if (Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(page, ReportingSourceV1.Json)) > ReportingSourceJournalV1.MaxPageBytes)
            throw new DomainException("source_event_limit", "Serialized journal page exceeds the V1 body budget.", 409);
        return page;
    }
}
