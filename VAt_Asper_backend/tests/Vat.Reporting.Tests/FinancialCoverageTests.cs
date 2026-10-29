using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Vat.Application.Security;
using Vat.Domain;
using Vat.Messaging.Contracts.V1;
using Vat.Services.Reporting.Application;
using Vat.Services.Reporting.Infrastructure;
using Xunit;

namespace Vat.Reporting.Tests;

public sealed class FinancialCoverageTests(ReportingDatabase database) : IClassFixture<ReportingDatabase>
{
    private static Actor Operator() => new(Guid.NewGuid(), "operator", new HashSet<Guid> { Guid.NewGuid() }, new HashSet<string> { Permissions.ReportRead, Permissions.ReportManage });
    private static FinancialCoverageService Service(ReportingDbContext db) => new(new FinancialCoverageStore(db, TimeProvider.System));
    private sealed class Source : IReportingExportSource
    {
        public ReportingSourceRowV1[] Rows { get; }
        public ReportingExportManifestV1 Data { get; set; }
        public bool Offline { get; set; }
        public Func<Task>? BeforePage { get; set; }
        public Source(Actor actor, params ReportingSourceRowV1[] rows)
        {
            Rows = rows.OrderBy(x => x.TransactionId.ToString("D"), StringComparer.Ordinal).ToArray();
            Data = new(1, Guid.NewGuid(), actor.TenantId, actor.CompanyIds.Single(), ReportingSourceV1.Scope, DateTimeOffset.UtcNow,
                rows.Count(x => x.Kind == "Purchase"), rows.Count(x => x.Kind == "PurchaseReversal"), rows.Length, true,
                ReportingSourceV1.Fingerprint(actor.TenantId, actor.CompanyIds.Single(), Rows));
        }
        public Task<ReportingExportManifestV1> Manifest(Guid companyId, Guid exportId, CancellationToken ct)
            => Offline ? throw new DomainException("source_offline", "Offline", 503) : Task.FromResult(Data);
        public async Task<ReportingExportPageV1> Page(Guid companyId, Guid exportId, int offset, int take, CancellationToken ct)
        {
            if (BeforePage is not null) await BeforePage();
            return new(exportId, Data.FingerprintSha256, offset, offset + take < Rows.Length ? offset + take : null, Rows.Skip(offset).Take(take).ToArray());
        }
    }
    private static ReportingSourceRowV1 Row() => new(Guid.NewGuid(), 5, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
        "Purchase", new(2026, 10, 24), "SOURCE", "BDT", 100, 15, 6, null);
    private async Task<Guid> Open(Actor actor)
    {
        await using var db = database.Open(); var company = actor.CompanyIds.Single(); var source = new Source(actor);
        var snapshot = await new ReportingGenerationService(new ReportingGenerationStore(db, TimeProvider.System)).Bootstrap(actor, company,
            Guid.NewGuid(), new(source.Data.ExportId, "Empty source baseline"), source, default);
        await new FinancialGenerationInbox(db, TimeProvider.System).Open(actor, company, Guid.NewGuid(), new(snapshot.GenerationId, "Receive"), default);
        return snapshot.GenerationId;
    }
    private async Task Deliver(Actor actor, Guid generation, ReportingSourceRowV1 row)
    {
        var envelope = new ReportingFinancialEnvelopeV1(row.EventId, "FinancialEntryRecordedV1", 1, DateTimeOffset.UtcNow, actor.TenantId,
            actor.CompanyIds.Single(), row.PurchaseId, row.AggregateVersion, Guid.NewGuid().ToString(),
            new(row.TransactionId, row.PurchaseId, row.BranchId, row.WarehouseId, row.Kind, row.BusinessDate, row.ExternalReference,
                row.Currency, row.NetAmount, row.TaxAmount, row.RecoverableTaxAmount, row.ReversalOfTransactionId));
        var delivery = InboxDeliveryV1.Create(new(JsonSerializer.SerializeToUtf8Bytes(envelope, ReportingSourceV1.Json), row.EventId.ToString(), envelope.EventType, "application/json"));
        await using var db = database.Open(); await new FinancialGenerationInbox(db, TimeProvider.System).Deliver(actor.TenantId, actor.CompanyIds.Single(), generation, delivery, default);
    }
    [Fact]
    public async Task Exact_export_set_proof_survives_restart_offline_retry_and_remains_historical_after_later_delivery()
    {
        var actor = Operator(); var company = actor.CompanyIds.Single(); var generation = await Open(actor); var row = Row(); await Deliver(actor, generation, row);
        var source = new Source(actor, row); var key = Guid.NewGuid(); var request = new VerifyFinancialCoverageRequest(generation, source.Data.ExportId, 0, "Complete frozen export");
        FinancialCoverageProof proof;
        await using (var db = database.Open()) proof = await Service(db).Verify(actor, company, key, request, source, default);
        Assert.True(proof.ExportCoverageVerified); Assert.False(proof.LiveCoverageVerified); Assert.Equal(1, proof.RowCount);
        Assert.Equal(ReportingSourceV1.Scope, proof.SourceScope);
        source.Offline = true;
        await using var reopened = database.Open(); var service = Service(reopened);
        Assert.Equal(proof, await service.Verify(actor, company, key, request, source, default));
        var status = await service.Status(actor, company, generation, default);
        Assert.True(status.ProjectionMatchesVerifiedExport); Assert.Equal(proof, status.LatestVerification); Assert.False(status.LiveCoverageVerified);
        var second = Row(); await Deliver(actor, generation, second);
        status = await service.Status(actor, company, generation, default);
        Assert.False(status.ProjectionMatchesVerifiedExport); Assert.Equal(proof, status.LatestVerification);
        Assert.Equal(proof, await service.Verify(actor, company, key, request, source, default));
        var nextSource = new Source(actor, row, second);
        var next = await service.Verify(actor, company, Guid.NewGuid(), new(generation, nextSource.Data.ExportId, 1, "Next complete export"), nextSource, default);
        Assert.Equal(2, next.VerificationVersion); Assert.Equal(2, next.RowCount);
        Assert.Equal(proof, await service.Verify(actor, company, key, request, source, default));
        Assert.Equal(next, (await service.Status(actor, company, generation, default)).LatestVerification);
    }
    [Theory]
    [InlineData("missing")][InlineData("extra")][InlineData("identity")][InlineData("reference")]
    public async Task Counts_or_signed_totals_alone_cannot_certify_coverage(string fault)
    {
        var actor = Operator(); var company = actor.CompanyIds.Single(); var generation = await Open(actor); var row = Row(); await Deliver(actor, generation, row);
        var source = fault switch
        {
            "missing" => new Source(actor, row, Row()), "extra" => new Source(actor),
            "identity" => new Source(actor, row with { EventId = Guid.NewGuid(), TransactionId = Guid.NewGuid() }),
            _ => new Source(actor, row with { ExternalReference = "DIFFERENT" })
        };
        await using var db = database.Open();
        Assert.Equal("coverage_mismatch", (await Assert.ThrowsAsync<DomainException>(() => Service(db).Verify(actor, company, Guid.NewGuid(),
            new(generation, source.Data.ExportId, 0, "Compare every row"), source, default))).Code);
        Assert.Equal(0, await db.Set<FinancialCoverageVerification>().CountAsync(x => x.GenerationId == generation));
    }
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task Invalid_source_coverage_or_fingerprint_cannot_write_a_proof(bool hash)
    {
        var actor = Operator(); var company = actor.CompanyIds.Single(); var generation = await Open(actor); var source = new Source(actor);
        source.Data = hash ? source.Data with { FingerprintSha256 = new string('0', 64) } : source.Data with { CoverageComplete = false };
        await using var db = database.Open();
        Assert.Equal("invalid_source_snapshot", (await Assert.ThrowsAsync<DomainException>(() => Service(db).Verify(actor, company, Guid.NewGuid(),
            new(generation, source.Data.ExportId, 0, "Invalid source"), source, default))).Code);
        Assert.Equal(0, await db.Set<FinancialCoverageVerification>().CountAsync(x => x.GenerationId == generation));
    }
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task Unsettled_pending_or_rejected_history_blocks_even_matching_projection_totals(bool rejected)
    {
        var actor = Operator(); var company = actor.CompanyIds.Single(); var generation = await Open(actor); var row = Row(); var source = new Source(actor);
        if (rejected)
        {
            await Deliver(actor, generation, row); source = new Source(actor, row);
            await Deliver(actor, generation, row with { EventId = Guid.NewGuid(), TransactionId = Guid.NewGuid() });
            await using var resolve = database.Open();
            var dead = await resolve.Set<FinancialInboxDeadLetter>().SingleAsync(x => x.GenerationId == generation);
            dead.ResolvedAt = DateTimeOffset.UtcNow; await resolve.SaveChangesAsync();
        }
        else await Deliver(actor, generation, row with { Kind = "PurchaseReversal", NetAmount = -100, TaxAmount = -15, RecoverableTaxAmount = -6,
            ReversalOfTransactionId = Guid.NewGuid() });
        await using var db = database.Open(); var service = Service(db);
        Assert.Equal("coverage_unsettled", (await Assert.ThrowsAsync<DomainException>(() => service.Verify(actor, company, Guid.NewGuid(),
            new(generation, source.Data.ExportId, 0, "No hidden pending work"), source, default))).Code);
        var status = await service.Status(actor, company, generation, default); Assert.False(status.ProjectionMatchesVerifiedExport);
        Assert.True(rejected ? status.RejectedMessages > 0 : status.PendingOriginals > 0);
    }
    [Fact]
    public async Task Audit_failure_rolls_back_proof_and_receipt_and_concurrent_retries_commit_once()
    {
        var actor = Operator(); var company = actor.CompanyIds.Single(); var generation = await Open(actor); var row = Row(); await Deliver(actor, generation, row);
        var source = new Source(actor, row); var key = Guid.NewGuid(); var request = new VerifyFinancialCoverageRequest(generation, source.Data.ExportId, 0, "Atomic proof");
        await using (var failing = database.Open(new RejectCoverageAudit()))
            await Assert.ThrowsAsync<InvalidOperationException>(() => Service(failing).Verify(actor, company, key, request, source, default));
        await using (var check = database.Open())
        {
            Assert.Equal(0, await check.Set<FinancialCoverageVerification>().CountAsync(x => x.GenerationId == generation));
            Assert.Equal(0, await check.Set<SnapshotReceipt>().CountAsync(x => x.Key == key));
        }
        async Task<FinancialCoverageProof> Verify()
        { await using var db = database.Open(); return await Service(db).Verify(actor, company, key, request, source, default); }
        var results = await Task.WhenAll(Verify(), Verify()); Assert.Equal(results[0], results[1]);
        await using var after = database.Open(); Assert.Equal(1, await after.Set<FinancialCoverageVerification>().CountAsync(x => x.GenerationId == generation));
    }
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task Delivery_or_retirement_during_source_download_cannot_produce_a_stale_success(bool retire)
    {
        var actor = Operator(); var company = actor.CompanyIds.Single(); var generation = await Open(actor); var row = Row(); await Deliver(actor, generation, row);
        var source = new Source(actor, row) { BeforePage = async () =>
        {
            if (!retire) await Deliver(actor, generation, Row());
            else { await using var other = database.Open(); await new FinancialGenerationInbox(other, TimeProvider.System).Retire(actor, company,
                Guid.NewGuid(), new(generation, 1, "Retire during download"), default); }
        } };
        await using var db = database.Open();
        Assert.Equal(retire ? "generation_retired" : "coverage_mismatch", (await Assert.ThrowsAsync<DomainException>(() => Service(db).Verify(actor, company, Guid.NewGuid(),
            new(generation, source.Data.ExportId, 0, "Race boundary"), source, default))).Code);
        Assert.Equal(0, await db.Set<FinancialCoverageVerification>().CountAsync(x => x.GenerationId == generation));
    }
    [Fact]
    public async Task Proof_versions_identity_scope_permissions_and_append_only_evidence_are_enforced()
    {
        var actor = Operator(); var company = actor.CompanyIds.Single(); var generation = await Open(actor); var source = new Source(actor);
        await using var db = database.Open(); var service = Service(db); var key = Guid.NewGuid();
        var request = new VerifyFinancialCoverageRequest(generation, source.Data.ExportId, 0, "Empty complete export");
        var proof = await service.Verify(actor, company, key, request, source, default); Assert.Equal(0, proof.RowCount);
        Assert.Equal("coverage_version_conflict", (await Assert.ThrowsAsync<DomainException>(() => service.Verify(actor, company, Guid.NewGuid(), request, source, default))).Code);
        Assert.Equal("snapshot_command_conflict", (await Assert.ThrowsAsync<DomainException>(() => service.Verify(actor, company, key, request with { Reason = "Changed" }, source, default))).Code);
        var other = actor with { Subject = "other" };
        Assert.Equal("snapshot_command_conflict", (await Assert.ThrowsAsync<DomainException>(() => service.Verify(other, company, key, request, source, default))).Code);
        Assert.Equal("not_found", (await Assert.ThrowsAsync<DomainException>(() => service.Status(actor with { TenantId = Guid.NewGuid() }, company, generation, default))).Code);
        var denied = actor with { Permissions = new HashSet<string>() };
        Assert.Equal("permission_denied", (await Assert.ThrowsAsync<DomainException>(() => service.Status(denied, company, generation, default))).Code);
        Assert.Equal("permission_denied", (await Assert.ThrowsAsync<DomainException>(() => service.Verify(denied, company, Guid.NewGuid(), request, source, default))).Code);
        source.Data = source.Data with { CapturedAt = source.Data.CapturedAt.AddSeconds(1) };
        Assert.Equal("source_export_conflict", (await Assert.ThrowsAsync<DomainException>(() => service.Verify(actor, company, Guid.NewGuid(), request with { ExpectedVerificationVersion = 1 }, source, default))).Code);
        await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE reporting.financial_coverage_verifications SET \"RowCount\" = 1 WHERE \"GenerationId\" = {generation}"));
        await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync("TRUNCATE reporting.financial_coverage_verifications"));
    }
    [Fact]
    public async Task Source_unavailable_creates_no_proof_and_never_implies_caught_up()
    {
        var actor = Operator(); var company = actor.CompanyIds.Single(); var generation = await Open(actor); var source = new Source(actor) { Offline = true };
        await using var db = database.Open(); var service = Service(db);
        await Assert.ThrowsAsync<DomainException>(() => service.Verify(actor, company, Guid.NewGuid(), new(generation, source.Data.ExportId, 0, "Offline source"), source, default));
        var status = await service.Status(actor, company, generation, default);
        Assert.Null(status.LatestVerification); Assert.False(status.ProjectionMatchesVerifiedExport); Assert.False(status.LiveCoverageVerified);
    }
    private sealed class RejectCoverageAudit : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (data.Context!.ChangeTracker.Entries<SnapshotAudit>().Any(x => x.Entity.Action == "financial.coverage.verify-v1")) throw new InvalidOperationException("Injected coverage audit failure");
            return base.SavingChangesAsync(data, result, ct);
        }
    }
}
