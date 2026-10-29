using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Vat.Application;
using Vat.Application.Security;
using Vat.Domain;
using Vat.Messaging.Contracts.V1;
using Vat.Services.Reporting.Application;
using Vat.Services.Reporting.Infrastructure;
using Xunit;

namespace Vat.Reporting.Tests;

public sealed class FinancialInboxTests(ReportingDatabase database) : IClassFixture<ReportingDatabase>
{
    private static readonly DateOnly Day = new(2026, 10, 24);
    private static Actor Operator() => new(Guid.NewGuid(), "operator", new HashSet<Guid> { Guid.NewGuid() },
        new HashSet<string> { Permissions.ReportRead, Permissions.ReportManage });
    private static FinancialGenerationInbox Inbox(ReportingDbContext db) => new(db, TimeProvider.System);
    private sealed class EmptySource(Actor actor, ReportingSourceRowV1[]? rows = null) : IReportingExportSource
    {
        private readonly ReportingSourceRowV1[] _rows = rows ?? [];
        public ReportingExportManifestV1 Data { get; } = new(1, Guid.NewGuid(), actor.TenantId, actor.CompanyIds.Single(), ReportingSourceV1.Scope,
            DateTimeOffset.UtcNow, rows?.Count(x => x.Kind == "Purchase") ?? 0, rows?.Count(x => x.Kind == "PurchaseReversal") ?? 0,
            rows?.Length ?? 0, true, ReportingSourceV1.Fingerprint(actor.TenantId, actor.CompanyIds.Single(), rows ?? []));
        public Task<ReportingExportManifestV1> Manifest(Guid companyId, Guid exportId, CancellationToken ct) => Task.FromResult(Data);
        public Task<ReportingExportPageV1> Page(Guid companyId, Guid exportId, int offset, int take, CancellationToken ct)
            => Task.FromResult(new ReportingExportPageV1(exportId, Data.FingerprintSha256, offset,
                offset + take < _rows.Length ? offset + take : null, _rows.Skip(offset).Take(take).ToArray()));
    }
    private async Task<Guid> Open(Actor actor, ReportingSourceRowV1[]? rows = null)
    {
        await using var db = database.Open(); var company = actor.CompanyIds.Single(); var source = new EmptySource(actor, rows);
        var service = new ReportingGenerationService(new ReportingGenerationStore(db, TimeProvider.System));
        var snapshot = await service.Bootstrap(actor, company, Guid.NewGuid(), new(source.Data.ExportId, "Baseline"), source, default);
        await Inbox(db).Open(actor, company, Guid.NewGuid(), new(snapshot.GenerationId, "Receive live evidence"), default);
        return snapshot.GenerationId;
    }
    private static ReportingFinancialEnvelopeV1 Original(Actor actor, string currency = "BDT") => new(Guid.NewGuid(), "FinancialEntryRecordedV1", 1,
        DateTimeOffset.UtcNow, actor.TenantId, actor.CompanyIds.Single(), Guid.NewGuid(), 5, Guid.NewGuid().ToString(),
        new(Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), "Purchase", Day, "SOURCE", currency, 100, 15, 6, null));
    private static ReportingFinancialEnvelopeV1 Fix(ReportingFinancialEnvelopeV1 envelope) => envelope with { Payload = envelope.Payload with { PurchaseId = envelope.AggregateId } };
    private static ReportingFinancialEnvelopeV1 Reversal(ReportingFinancialEnvelopeV1 original) => original with
    {
        EventId = Guid.NewGuid(), AggregateVersion = 7,
        Payload = original.Payload with { TransactionId = Guid.NewGuid(), Kind = "PurchaseReversal", BusinessDate = Day.AddDays(1),
            NetAmount = -100, TaxAmount = -15, RecoverableTaxAmount = -6, ReversalOfTransactionId = original.Payload.TransactionId }
    };
    private static InboxDeliveryV1 Delivery(ReportingFinancialEnvelopeV1 envelope) => InboxDeliveryV1.Create(new ReceivedMessage(
        JsonSerializer.SerializeToUtf8Bytes(envelope, ReportingSourceV1.Json), envelope.EventId.ToString(), envelope.EventType, "application/json"));

    [Fact]
    public async Task Baseline_overlap_is_counted_once_and_live_reversal_uses_frozen_original_amounts()
    {
        var actor = Operator(); var company = actor.CompanyIds.Single(); var original = Fix(Original(actor)); var p = original.Payload;
        var row = new ReportingSourceRowV1(original.EventId, original.AggregateVersion, p.TransactionId, p.PurchaseId, p.BranchId, p.WarehouseId,
            p.Kind, p.BusinessDate, p.ExternalReference, p.Currency, p.NetAmount, p.TaxAmount, p.RecoverableTaxAmount, null);
        var generation = await Open(actor, [row]); await using var db = database.Open(); var inbox = Inbox(db);
        Assert.Equal("Duplicate", (await inbox.Deliver(actor.TenantId, company, generation, Delivery(original), default)).Status);
        Assert.Equal(0, await db.Set<FinancialLiveEntry>().CountAsync(x => x.GenerationId == generation));
        await inbox.Deliver(actor.TenantId, company, generation, Delivery(Reversal(original)), default);
        var report = await inbox.Summary(actor, company, generation, Day, Day.AddDays(2), default);
        Assert.Equal(0, Assert.Single(report.Totals).NetAmount); Assert.Equal(2, report.Totals[0].EntryCount);
        var contradictory = original with { Payload = p with { TransactionId = Guid.NewGuid() } };
        Assert.Equal("Quarantined", (await inbox.Deliver(actor.TenantId, company, generation, Delivery(contradictory), default)).Status);
    }

    [Fact]
    public async Task Reversal_before_original_survives_restart_and_duplicate_races_do_not_double_count()
    {
        var actor = Operator(); var company = actor.CompanyIds.Single(); var generation = await Open(actor);
        var original = Fix(Original(actor)); var reversal = Delivery(Reversal(original));
        await using (var db = database.Open())
        {
            var result = await Inbox(db).Deliver(actor.TenantId, company, generation, reversal, default);
            Assert.Equal("PendingOriginal", result.Status); Assert.True(result.IsCommittedFor(generation, reversal));
            var summary = await Inbox(db).Summary(actor, company, generation, Day, Day.AddDays(2), default);
            Assert.Equal(1, summary.PendingOriginals); Assert.Empty(summary.Totals); Assert.False(summary.LiveCoverageVerified);
        }
        async Task<ReportingGenerationReceiptV1> Send()
        { await using var db = database.Open(); return await Inbox(db).Deliver(actor.TenantId, company, generation, Delivery(original), default); }
        var results = await Task.WhenAll(Send(), Send()); Assert.Contains(results, x => x.Status == "Processed"); Assert.Contains(results, x => x.Status == "Duplicate");
        await using var reopened = database.Open(); var inbox = Inbox(reopened);
        var all = await inbox.Summary(actor, company, generation, Day, Day.AddDays(2), default);
        var totals = Assert.Single(all.Totals); Assert.Equal(2, totals.EntryCount); Assert.Equal(0, totals.NetAmount); Assert.Equal(0, all.PendingOriginals);
        Assert.Equal(100, Assert.Single((await inbox.Summary(actor, company, generation, Day, Day.AddDays(1), default)).Totals).NetAmount);
        Assert.Equal(-100, Assert.Single((await inbox.Summary(actor, company, generation, Day.AddDays(1), Day.AddDays(2), default)).Totals).NetAmount);
        Assert.Equal("Duplicate", (await inbox.Deliver(actor.TenantId, company, generation, reversal, default)).Status);
    }
    [Theory]
    [InlineData("amount")][InlineData("transaction")][InlineData("scope")][InlineData("metadata")][InlineData("schema")]
    public async Task Contradictory_or_misscoped_messages_are_quarantined_without_altering_accepted_amounts(string fault)
    {
        var actor = Operator(); var company = actor.CompanyIds.Single(); var generation = await Open(actor); var original = Fix(Original(actor));
        await using var db = database.Open(); var inbox = Inbox(db);
        await inbox.Deliver(actor.TenantId, company, generation, Delivery(original), default);
        var changed = fault switch
        {
            "amount" => original with { Payload = original.Payload with { NetAmount = 101 } },
            "transaction" => original with { EventId = Guid.NewGuid() },
            "scope" => original with { TenantId = Guid.NewGuid() },
            "schema" => original with { SchemaVersion = 2 },
            _ => original
        };
        var delivery = Delivery(changed); if (fault == "metadata") delivery = delivery with { MessageId = Guid.NewGuid().ToString() };
        var receipt = await inbox.Deliver(actor.TenantId, company, generation, delivery, default);
        Assert.Equal("Quarantined", receipt.Status); Assert.True(receipt.IsCommittedFor(generation, delivery));
        var repeat = await inbox.Deliver(actor.TenantId, company, generation, delivery, default); Assert.Equal(receipt.DeadLetterId, repeat.DeadLetterId);
        var totals = await inbox.Summary(actor, company, generation, Day, Day.AddDays(2), default);
        Assert.Equal(100, Assert.Single(totals.Totals).NetAmount); Assert.Equal(1, totals.QuarantinedMessages);
    }
    [Fact]
    public async Task Invalid_delayed_reversal_does_not_reduce_the_original_and_is_quarantined_when_it_can_be_checked()
    {
        var actor = Operator(); var company = actor.CompanyIds.Single(); var generation = await Open(actor); var original = Fix(Original(actor));
        var bad = Reversal(original); bad = bad with { Payload = bad.Payload with { NetAmount = -99 } };
        await using var db = database.Open(); var inbox = Inbox(db);
        Assert.Equal("PendingOriginal", (await inbox.Deliver(actor.TenantId, company, generation, Delivery(bad), default)).Status);
        await inbox.Deliver(actor.TenantId, company, generation, Delivery(original), default);
        var report = await inbox.Summary(actor, company, generation, Day, Day.AddDays(2), default);
        Assert.Equal(100, Assert.Single(report.Totals).NetAmount); Assert.Equal(1, report.QuarantinedMessages); Assert.Equal(0, report.PendingOriginals);
    }
    [Fact]
    public async Task A_second_full_reversal_is_quarantined_instead_of_cancelling_an_original_twice()
    {
        var actor = Operator(); var company = actor.CompanyIds.Single(); var generation = await Open(actor); var original = Fix(Original(actor));
        await using var db = database.Open(); var inbox = Inbox(db);
        await inbox.Deliver(actor.TenantId, company, generation, Delivery(original), default);
        await inbox.Deliver(actor.TenantId, company, generation, Delivery(Reversal(original)), default);
        var receipt = await inbox.Deliver(actor.TenantId, company, generation, Delivery(Reversal(original)), default);
        Assert.Equal("Quarantined", receipt.Status);
        var report = await inbox.Summary(actor, company, generation, Day, Day.AddDays(2), default);
        Assert.Equal(0, Assert.Single(report.Totals).NetAmount); Assert.Equal(2, report.Totals[0].EntryCount); Assert.Equal(1, report.QuarantinedMessages);
    }

    [Fact]
    public async Task Generation_retirement_fences_late_delivery_and_cannot_be_reopened_by_a_new_key()
    {
        var actor = Operator(); var company = actor.CompanyIds.Single(); var generation = await Open(actor); var delivery = Delivery(Fix(Original(actor)));
        await using var db = database.Open(); var inbox = Inbox(db); var key = Guid.NewGuid(); var request = new RetireFinancialInboxRequest(generation, 1, "Replaced generation");
        var retired = await inbox.Retire(actor, company, key, request, default); Assert.Equal(2, retired.Version);
        Assert.Equal(retired, await inbox.Retire(actor, company, key, request, default));
        Assert.Equal("generation_retired", (await Assert.ThrowsAsync<DomainException>(() => inbox.Deliver(actor.TenantId, company, generation, delivery, default))).Code);
        Assert.Equal("generation_retired", (await Assert.ThrowsAsync<DomainException>(() => inbox.Open(actor, company, Guid.NewGuid(), new(generation, "Reopen"), default))).Code);
        Assert.Equal(0, await db.Set<FinancialLiveEntry>().CountAsync(x => x.GenerationId == generation));
    }
    [Fact]
    public async Task Retirement_from_another_scope_is_observed_by_a_reused_delivery_context()
    {
        var actor = Operator(); var company = actor.CompanyIds.Single(); var generation = await Open(actor);
        await using var stale = database.Open(); var inbox = Inbox(stale);
        await inbox.Deliver(actor.TenantId, company, generation, Delivery(Fix(Original(actor))), default);
        // Populate the reused context before retirement commits in another process/scope.
        await stale.Set<FinancialInboxGeneration>().SingleAsync(x => x.GenerationId == generation);
        await using (var other = database.Open())
            await Inbox(other).Retire(actor, company, Guid.NewGuid(), new(generation, 1, "Terminal fence"), default);
        Assert.Equal("generation_retired", (await Assert.ThrowsAsync<DomainException>(() => inbox.Deliver(actor.TenantId, company,
            generation, Delivery(Fix(Original(actor))), default))).Code);
        Assert.Equal("generation_retired", (await Assert.ThrowsAsync<DomainException>(() => inbox.Open(actor, company, Guid.NewGuid(), new(generation, "Reopen"), default))).Code);
        Assert.Equal(1, await stale.Set<FinancialLiveEntry>().CountAsync(x => x.GenerationId == generation));
    }

    [Fact]
    public async Task Reused_context_observes_external_pending_resolution_and_reopens_recurring_quarantine()
    {
        var actor = Operator(); var company = actor.CompanyIds.Single(); var generation = await Open(actor); var original = Fix(Original(actor));
        var reversal = Delivery(Reversal(original)); await using var stale = database.Open(); var inbox = Inbox(stale);
        await inbox.Deliver(actor.TenantId, company, generation, reversal, default);
        await using (var other = database.Open()) await Inbox(other).Deliver(actor.TenantId, company, generation, Delivery(original), default);
        Assert.Equal("Duplicate", (await inbox.Deliver(actor.TenantId, company, generation, reversal, default)).Status);
        var malformed = InboxDeliveryV1.Create(new(Encoding.UTF8.GetBytes("not-json"), "invalid", "FinancialEntryRecordedV1", "application/json"));
        var receipt = await inbox.Deliver(actor.TenantId, company, generation, malformed, default);
        await using (var other = database.Open())
        {
            var resolved = await other.Set<FinancialInboxDeadLetter>().SingleAsync(x => x.Id == receipt.DeadLetterId);
            resolved.ResolvedAt = DateTimeOffset.UtcNow; await other.SaveChangesAsync();
        }
        await inbox.Deliver(actor.TenantId, company, generation, malformed, default);
        Assert.Equal(1, (await inbox.Summary(actor, company, generation, Day, Day.AddDays(2), default)).QuarantinedMessages);
    }

    [Fact]
    public async Task Replay_audit_failure_rolls_back_projection_inbox_resolution_and_receipt_then_retry_commits_once()
    {
        var actor = Operator(); var company = actor.CompanyIds.Single(); var generation = await Open(actor); var delivery = Delivery(Fix(Original(actor)));
        var deadId = Guid.NewGuid(); var key = Guid.NewGuid();
        // Simulated retained quarantine from a prior parser: replay must use original evidence.
        await using (var setup = database.Open())
        {
            setup.Add(new FinancialInboxDeadLetter { TenantId = actor.TenantId, CompanyId = company, GenerationId = generation, Id = deadId,
                Fingerprint = delivery.Fingerprint(), DeliveryJson = JsonSerializer.Serialize(delivery, ReportingSourceV1.Json),
                ErrorCode = "prior_parser", ReceivedAt = DateTimeOffset.UtcNow }); await setup.SaveChangesAsync();
        }
        await using (var failing = database.Open(new RejectReplayAudit()))
            await Assert.ThrowsAsync<InvalidOperationException>(() => Inbox(failing).Replay(actor, company, generation, deadId, key, "Replay retained evidence", default));
        await using (var check = database.Open())
        {
            Assert.Equal(0, await check.Set<FinancialLiveEntry>().CountAsync(x => x.GenerationId == generation));
            Assert.Equal(0, await check.Set<FinancialInboxMessage>().CountAsync(x => x.GenerationId == generation));
            Assert.Null((await check.Set<FinancialInboxDeadLetter>().SingleAsync(x => x.Id == deadId)).ResolvedAt);
            Assert.Equal(0, await check.Set<SnapshotReceipt>().CountAsync(x => x.Key == key));
        }
        await using var good = database.Open(); var inbox = Inbox(good);
        var receipt = await inbox.Replay(actor, company, generation, deadId, key, "Replay retained evidence", default);
        Assert.Equal("Processed", receipt.Status); Assert.Equal(receipt, await inbox.Replay(actor, company, generation, deadId, key, "Replay retained evidence", default));
        Assert.NotNull((await good.Set<FinancialInboxDeadLetter>().SingleAsync(x => x.Id == deadId)).ResolvedAt);
        Assert.Equal(1, await good.Set<FinancialLiveEntry>().CountAsync(x => x.GenerationId == generation));
        Assert.Equal("snapshot_command_conflict", (await Assert.ThrowsAsync<DomainException>(() => inbox.Replay(actor, company, generation, deadId, key, "Changed reason", default))).Code);
    }
    [Fact]
    public async Task Scoped_generations_isolate_delivery_and_money_currencies_and_protect_evidence()
    {
        var actor = Operator(); var company = actor.CompanyIds.Single(); var first = await Open(actor); var second = await Open(actor);
        await using var db = database.Open(); var inbox = Inbox(db); var delivery = Delivery(Fix(Original(actor)));
        await inbox.Deliver(actor.TenantId, company, first, delivery, default);
        Assert.Empty((await inbox.Summary(actor, company, second, Day, Day.AddDays(2), default)).Totals);
        await inbox.Deliver(actor.TenantId, company, second, delivery, default);
        Assert.Equal(100, Assert.Single((await inbox.Summary(actor, company, second, Day, Day.AddDays(2), default)).Totals).NetAmount);
        await inbox.Deliver(actor.TenantId, company, first, Delivery(Fix(Original(actor, "USD"))), default);
        Assert.Equal(2, (await inbox.Summary(actor, company, first, Day, Day.AddDays(2), default)).Totals.Count);
        Assert.Equal("not_found", (await Assert.ThrowsAsync<DomainException>(() => inbox.Deliver(Guid.NewGuid(), company, first, delivery, default))).Code);
        var denied = actor with { Permissions = new HashSet<string>() };
        Assert.Equal("permission_denied", (await Assert.ThrowsAsync<DomainException>(() => inbox.Summary(denied, company, first, Day, Day.AddDays(2), default))).Code);
        await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE reporting.financial_inbox_messages SET \"BodyHash\" = 'tampered' WHERE \"GenerationId\" = {first}"));
        await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM reporting.financial_live_entries WHERE \"GenerationId\" = {first}"));
    }
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task Malformed_and_truncated_evidence_is_durably_quarantined(bool oversized)
    {
        var actor = Operator(); var company = actor.CompanyIds.Single(); var generation = await Open(actor);
        var bytes = oversized ? new byte[InboxDeliveryV1.MaxBodyBytes + 1] : Encoding.UTF8.GetBytes("not-json");
        var delivery = InboxDeliveryV1.Create(new(bytes, "invalid", "FinancialEntryRecordedV1", "application/json"));
        await using var db = database.Open(); var inbox = Inbox(db);
        var receipt = await inbox.Deliver(actor.TenantId, company, generation, delivery, default); Assert.Equal("Quarantined", receipt.Status);
        Assert.True(receipt.IsCommittedFor(generation, delivery));
        if (oversized) Assert.Equal("replay_unavailable", (await Assert.ThrowsAsync<DomainException>(() => inbox.Replay(actor, company, generation, receipt.DeadLetterId!.Value, Guid.NewGuid(), "Replay", default))).Code);
        Assert.False((await inbox.Summary(actor, company, generation, Day, Day.AddDays(2), default)).LiveCoverageVerified);
    }
    private sealed class RejectReplayAudit : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (data.Context!.ChangeTracker.Entries<SnapshotAudit>().Any(x => x.Entity.Action == "financial.inbox.replay-v1")) throw new InvalidOperationException("Injected replay audit failure");
            return base.SavingChangesAsync(data, result, ct);
        }
    }
}
