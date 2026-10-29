using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Vat.Application.Security;
using Vat.Domain;
using Vat.Messaging.Contracts.V1;
using Vat.Services.Reporting.Application;
using Vat.Services.Reporting.Domain;
using Vat.Services.Reporting.Infrastructure;
using Xunit;

namespace Vat.Reporting.Tests;

public sealed class GenerationTests(ReportingDatabase database) : IClassFixture<ReportingDatabase>
{
    private static Actor Actor(Guid? tenant = null, Guid? company = null, string subject = "operator", params string[] permissions)
        => new(tenant ?? Guid.NewGuid(), subject, new HashSet<Guid> { company ?? Guid.NewGuid() }, new HashSet<string>(permissions.Length == 0 ? [Permissions.ReportRead, Permissions.ReportManage] : permissions));
    private static ReportingGenerationService Service(ReportingDbContext db) => new(new ReportingGenerationStore(db, TimeProvider.System));
    private sealed class Source : IReportingExportSource
    {
        public ReportingSourceRowV1[] Rows { get; set; }
        public ReportingExportManifestV1 Data { get; set; }
        public int Calls { get; private set; }
        public Func<int, Task>? BeforePage { get; set; }
        public bool BreakPage { get; set; }
        public bool Offline { get; set; }
        public Source(Actor actor, bool reversal = true)
        {
            var original = new ReportingSourceRowV1(Guid.NewGuid(), 5, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                "Purchase", new(2026, 10, 23), "VERIFIED-SOURCE", "BDT", 250m, 18.75m, 7.5m, null);
            Rows = (reversal ? new[] { original, original with { EventId = Guid.NewGuid(), AggregateVersion = 7, TransactionId = Guid.NewGuid(),
                Kind = "PurchaseReversal", BusinessDate = new(2026, 10, 24), NetAmount = -250m, TaxAmount = -18.75m,
                RecoverableTaxAmount = -7.5m, ReversalOfTransactionId = original.TransactionId } } : [original])
                .OrderBy(x => x.TransactionId.ToString("D"), StringComparer.Ordinal).ToArray();
            Data = new(1, Guid.NewGuid(), actor.TenantId, actor.CompanyIds.Single(), ReportingSourceV1.Scope, DateTimeOffset.UtcNow,
                1, reversal ? 1 : 0, Rows.Length, true, ReportingSourceV1.Fingerprint(actor.TenantId, actor.CompanyIds.Single(), Rows));
        }
        public Task<ReportingExportManifestV1> Manifest(Guid companyId, Guid exportId, CancellationToken ct)
        { Calls++; if (Offline) throw new DomainException("source_offline", "Source offline.", 503); return Task.FromResult(Data); }
        public async Task<ReportingExportPageV1> Page(Guid companyId, Guid exportId, int offset, int take, CancellationToken ct)
        {
            Calls++; if (BeforePage is not null) await BeforePage(offset);
            ct.ThrowIfCancellationRequested();
            return new(exportId, Data.FingerprintSha256, offset, !BreakPage && offset + 1 < Rows.Length ? offset + 1 : null, Rows.Skip(offset).Take(1).ToArray());
        }
    }

    [Fact]
    public async Task Bootstrap_is_snapshot_only_scoped_hashed_and_preserves_signed_date_totals_after_restart()
    {
        var actor = Actor(); var company = actor.CompanyIds.Single(); var source = new Source(actor);
        SnapshotGenerationResult imported;
        await using (var db = database.Open()) imported = await Service(db).Bootstrap(actor, company, Guid.NewGuid(), new(source.Data.ExportId, "Bootstrap"), source, default);
        Assert.True(imported.SnapshotOnly);
        await using var reopened = database.Open(); var service = Service(reopened);
        var missing = await Assert.ThrowsAsync<DomainException>(() => service.ReadSnapshot(actor, company, new(2026, 10, 23), new(2026, 10, 25), default));
        Assert.Equal("snapshot_not_ready", missing.Code);
        var head = await service.ActivateSnapshot(actor, company, Guid.NewGuid(), new(imported.GenerationId, 0, "Verified snapshot only"), default);
        Assert.False(head.LiveCoverageVerified);
        var october23 = await service.ReadSnapshot(actor, company, new(2026, 10, 23), new(2026, 10, 24), default);
        Assert.Equal(250m, Assert.Single(october23.Totals).NetAmount);
        var all = await service.ReadSnapshot(actor, company, new(2026, 10, 23), new(2026, 10, 25), default);
        Assert.Equal(0m, Assert.Single(all.Totals).NetAmount); Assert.Equal(2, all.Totals.Single().EntryCount);
        var stored = await reopened.Set<SnapshotEntry>().Where(x => x.CompanyId == company).ToArrayAsync();
        Assert.Equal(source.Rows.Length, stored.Length); Assert.Equal(imported.FingerprintSha256, source.Data.FingerprintSha256);
        Assert.Equal(1, await reopened.Set<SnapshotHead>().CountAsync(x => x.CompanyId == company));
    }

    [Fact]
    public async Task Concurrent_bootstrap_retries_commit_once_and_replay_offline_but_reject_changed_actor_or_payload()
    {
        var actor = Actor(); var company = actor.CompanyIds.Single(); var source = new Source(actor); var key = Guid.NewGuid(); var request = new BootstrapSnapshotRequest(source.Data.ExportId, "Bootstrap");
        async Task<SnapshotGenerationResult> Run() { await using var db = database.Open(); return await Service(db).Bootstrap(actor, company, key, request, source, default); }
        var results = await Task.WhenAll(Run(), Run(), Run()); Assert.Single(results.Select(x => x.GenerationId).Distinct());
        source.Offline = true;
        await using var read = database.Open(); var service = Service(read);
        var replay = await service.Bootstrap(actor, company, key, request, source, default); Assert.Equal(results[0], replay);
        Assert.Equal("snapshot_command_conflict", (await Assert.ThrowsAsync<DomainException>(() => service.Bootstrap(actor with { Subject = "other" }, company, key, request, source, default))).Code);
        Assert.Equal("snapshot_command_conflict", (await Assert.ThrowsAsync<DomainException>(() => service.Bootstrap(actor, company, key, request with { Reason = "Changed" }, source, default))).Code);
        Assert.Equal(1, await read.Set<SnapshotGeneration>().CountAsync(x => x.CompanyId == company));
        Assert.Equal(1, await read.Set<SnapshotAudit>().CountAsync(x => x.CompanyId == company));
        Assert.Equal(1, await read.Set<SnapshotReceipt>().CountAsync(x => x.CompanyId == company));
    }

    [Theory]
    [InlineData("scope")]
    [InlineData("coverage")]
    [InlineData("page")]
    [InlineData("hash")]
    [InlineData("duplicate")]
    public async Task Invalid_or_incomplete_exports_never_persist_a_generation_or_receipt(string fault)
    {
        var actor = Actor(); var company = actor.CompanyIds.Single(); var source = new Source(actor);
        if (fault == "scope") source.Data = source.Data with { TenantId = Guid.NewGuid() };
        if (fault == "coverage") source.Data = source.Data with { CoverageComplete = false };
        if (fault == "page") source.BreakPage = true;
        if (fault == "hash") source.Data = source.Data with { FingerprintSha256 = new('a', 64) };
        if (fault == "duplicate") source.Rows[1] = source.Rows[0];
        await using var db = database.Open();
        Assert.Equal("invalid_source_snapshot", (await Assert.ThrowsAsync<DomainException>(() => Service(db).Bootstrap(actor, company, Guid.NewGuid(), new(source.Data.ExportId, "Bootstrap"), source, default))).Code);
        Assert.Equal(0, await db.Set<SnapshotGeneration>().CountAsync(x => x.CompanyId == company));
        Assert.Equal(0, await db.Set<SnapshotReceipt>().CountAsync(x => x.CompanyId == company));
    }

    [Fact]
    public async Task Rebuild_download_keeps_old_snapshot_readable_and_stale_activation_cannot_replace_head()
    {
        var actor = Actor(); var company = actor.CompanyIds.Single(); var old = new Source(actor, false);
        await using var original = database.Open(); var service = Service(original);
        var first = await service.Bootstrap(actor, company, Guid.NewGuid(), new(old.Data.ExportId, "First"), old, default);
        var activationKey = Guid.NewGuid(); var activation = new ActivateSnapshotRequest(first.GenerationId, 0, "First snapshot");
        await service.ActivateSnapshot(actor, company, activationKey, activation, default);
        var source = new Source(actor); var paused = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        source.BeforePage = async offset => { if (offset == 1) { paused.SetResult(); await resume.Task; } };
        await using var building = database.Open(); var bootstrap = Service(building).Bootstrap(actor, company, Guid.NewGuid(), new(source.Data.ExportId, "Shadow"), source, default);
        await paused.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await using (var reader = database.Open())
        {
            var current = await Service(reader).ReadSnapshot(actor, company, new(2026, 10, 1), new(2026, 11, 1), default);
            Assert.Equal(first.GenerationId, current.Snapshot.GenerationId); Assert.Equal(250m, current.Totals.Single().NetAmount);
        }
        resume.SetResult(); var candidate = await bootstrap;
        await using var promoting = database.Open(); var promote = Service(promoting);
        Assert.Equal("snapshot_version_conflict", (await Assert.ThrowsAsync<DomainException>(() => promote.ActivateSnapshot(actor, company, Guid.NewGuid(), new(candidate.GenerationId, 0, "Stale"), default))).Code);
        var head = await promote.ActivateSnapshot(actor, company, Guid.NewGuid(), new(candidate.GenerationId, 1, "Verified rebuild snapshot"), default);
        Assert.Equal(2, head.Version); Assert.False(head.LiveCoverageVerified);
        await using var replayer = database.Open(); var oldReceipt = await Service(replayer).ActivateSnapshot(actor, company, activationKey, activation, default);
        Assert.Equal(1, oldReceipt.Version); Assert.Equal(candidate.GenerationId, (await Service(replayer).ReadSnapshot(actor, company, new(2026, 10, 1), new(2026, 11, 1), default)).Snapshot.GenerationId);
        Assert.Equal(2, await replayer.Set<SnapshotGeneration>().CountAsync(x => x.CompanyId == company));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Audit_failure_rolls_back_import_or_head_switch_with_receipt_and_can_retry(bool activation)
    {
        var actor = Actor(); var company = actor.CompanyIds.Single(); var source = new Source(actor); var key = Guid.NewGuid(); SnapshotGenerationResult? imported = null;
        if (activation) { await using var setup = database.Open(); imported = await Service(setup).Bootstrap(actor, company, Guid.NewGuid(), new(source.Data.ExportId, "Prepare"), source, default); }
        await using (var failing = database.Open(new RejectAudit()))
        {
            if (activation) await Assert.ThrowsAsync<InvalidOperationException>(() => Service(failing).ActivateSnapshot(actor, company, key, new(imported!.GenerationId, 0, "Activate"), default));
            else await Assert.ThrowsAsync<InvalidOperationException>(() => Service(failing).Bootstrap(actor, company, key, new(source.Data.ExportId, "Bootstrap"), source, default));
        }
        await using var retry = database.Open();
        Assert.Equal(0, await retry.Set<SnapshotHead>().CountAsync(x => x.CompanyId == company));
        Assert.Equal(0, await retry.Set<SnapshotReceipt>().CountAsync(x => x.CompanyId == company && x.Key == key));
        if (activation) await Service(retry).ActivateSnapshot(actor, company, key, new(imported!.GenerationId, 0, "Activate"), default);
        else
        {
            Assert.Equal(0, await retry.Set<SnapshotGeneration>().CountAsync(x => x.CompanyId == company));
            await Service(retry).Bootstrap(actor, company, key, new(source.Data.ExportId, "Bootstrap"), source, default);
        }
    }

    [Fact]
    public async Task Tenant_permission_and_date_boundaries_are_enforced_without_financial_dependencies()
    {
        var actor = Actor(); var company = actor.CompanyIds.Single(); var source = new Source(actor);
        await using var db = database.Open(); var service = Service(db);
        Assert.Equal(403, (await Assert.ThrowsAsync<DomainException>(() => service.Bootstrap(actor with { Permissions = new HashSet<string>() }, company, Guid.NewGuid(), new(source.Data.ExportId, "Import"), source, default))).Status);
        Assert.Equal(403, (await Assert.ThrowsAsync<DomainException>(() => service.Bootstrap(actor, Guid.NewGuid(), Guid.NewGuid(), new(source.Data.ExportId, "Import"), source, default))).Status);
        Assert.Equal(0, source.Calls);
        var import = await service.Bootstrap(actor, company, Guid.NewGuid(), new(source.Data.ExportId, "Import"), source, default);
        Assert.Equal(404, (await Assert.ThrowsAsync<DomainException>(() => service.ActivateSnapshot(actor with { TenantId = Guid.NewGuid() }, company, Guid.NewGuid(), new(import.GenerationId, 0, "Other tenant"), default))).Status);
        Assert.Equal("validation_failed", (await Assert.ThrowsAsync<DomainException>(() => service.ReadSnapshot(actor, company, new(2026, 10, 1), new(2026, 10, 1), default))).Code);
        foreach (var assembly in new[] { typeof(VerifiedFinancialSnapshot).Assembly, typeof(ReportingGenerationService).Assembly, typeof(ReportingDbContext).Assembly })
            Assert.DoesNotContain(assembly.GetReferencedAssemblies(), x => new[] { "Vat.Hosting", "Vat.Application", "Vat.Domain", "Vat.Infrastructure", "Vat.Api" }.Contains(x.Name));
        Assert.False(db.Database.HasPendingModelChanges());
    }
    [Fact]
    public async Task Cancelled_partial_download_leaves_no_target_state()
    {
        var actor = Actor(); var company = actor.CompanyIds.Single(); var source = new Source(actor);
        using var cancellation = new CancellationTokenSource();
        source.BeforePage = offset => { if (offset == 1) cancellation.Cancel(); return Task.CompletedTask; };
        await using var db = database.Open();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service(db).Bootstrap(actor, company, Guid.NewGuid(), new(source.Data.ExportId, "Interrupted"), source, cancellation.Token));
        Assert.Equal(0, await db.Set<SnapshotGeneration>().CountAsync(x => x.CompanyId == company));
        Assert.Equal(0, await db.Set<SnapshotReceipt>().CountAsync(x => x.CompanyId == company));
    }

    [Fact]
    public async Task Changed_manifest_for_an_imported_export_is_refused_and_snapshot_evidence_is_append_only()
    {
        var actor = Actor(); var company = actor.CompanyIds.Single(); var source = new Source(actor);
        await using var db = database.Open(); var service = Service(db);
        var imported = await service.Bootstrap(actor, company, Guid.NewGuid(), new(source.Data.ExportId, "Import"), source, default);
        source.Data = source.Data with { CapturedAt = source.Data.CapturedAt.AddSeconds(1) };
        Assert.Equal("source_export_conflict", (await Assert.ThrowsAsync<DomainException>(() => service.Bootstrap(actor, company, Guid.NewGuid(), new(source.Data.ExportId, "Changed evidence"), source, default))).Code);
        Assert.Equal("P0001", (await Assert.ThrowsAsync<PostgresException>(() => db.Set<SnapshotEntry>().Where(x => x.CompanyId == company).ExecuteDeleteAsync())).SqlState);
        Assert.Equal("P0001", (await Assert.ThrowsAsync<PostgresException>(() => db.Set<SnapshotGeneration>().Where(x => x.CompanyId == company).ExecuteUpdateAsync(s => s.SetProperty(x => x.EventCount, 0)))).SqlState);
        Assert.Equal("P0001", (await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync("TRUNCATE reporting.snapshot_command_receipts"))).SqlState);
        Assert.Equal(source.Rows.Length, await db.Set<SnapshotEntry>().CountAsync(x => x.CompanyId == company && x.GenerationId == imported.GenerationId));
    }

    [Fact]
    public async Task Snapshot_totals_are_partitioned_by_currency()
    {
        var actor = Actor(); var company = actor.CompanyIds.Single(); var source = new Source(actor, false); var first = source.Rows[0];
        source.Rows = new[] { first, first with { EventId = Guid.NewGuid(), TransactionId = Guid.NewGuid(), PurchaseId = Guid.NewGuid(), Currency = "USD", NetAmount = 300m } }
            .OrderBy(x => x.TransactionId.ToString("D"), StringComparer.Ordinal).ToArray();
        source.Data = source.Data with { EventCount = 2, ReconciledPurchases = 2,
            FingerprintSha256 = ReportingSourceV1.Fingerprint(actor.TenantId, company, source.Rows) };
        await using var db = database.Open(); var service = Service(db);
        var imported = await service.Bootstrap(actor, company, Guid.NewGuid(), new(source.Data.ExportId, "Multiple currencies"), source, default);
        await service.ActivateSnapshot(actor, company, Guid.NewGuid(), new(imported.GenerationId, 0, "Snapshot only"), default);
        var read = await service.ReadSnapshot(actor, company, new(2026, 10, 1), new(2026, 11, 1), default);
        Assert.Equal(250m, Assert.Single(read.Totals, x => x.Currency == "BDT").NetAmount);
        Assert.Equal(300m, Assert.Single(read.Totals, x => x.Currency == "USD").NetAmount);
    }

    private sealed class RejectAudit : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (data.Context!.ChangeTracker.Entries<SnapshotAudit>().Any(x => x.State == EntityState.Added)) throw new InvalidOperationException("Injected snapshot audit failure.");
            return base.SavingChangesAsync(data, result, ct);
        }
    }
}
