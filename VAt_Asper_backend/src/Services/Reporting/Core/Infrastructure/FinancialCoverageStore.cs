using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vat.Application.Security;
using Vat.Domain;
using Vat.Messaging.Contracts.V1;
using Vat.Services.Reporting.Application;
using Vat.Services.Reporting.Domain;

namespace Vat.Services.Reporting.Infrastructure;

public sealed class FinancialCoverageVerification
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid GenerationId { get; set; }
    public Guid Id { get; set; }
    public Guid ExportId { get; set; }
    public long Version { get; set; }
    public string SourceManifestJson { get; set; } = "";
    public string Fingerprint { get; set; } = "";
    public int RowCount { get; set; }
    public DateTimeOffset VerifiedAt { get; set; }
}

public sealed class FinancialCoverageStore(ReportingDbContext db, TimeProvider clock) : IFinancialCoverageStore
{
    private const string Operation = "financial.coverage.verify-v1";
    private static readonly JsonSerializerOptions Json = ReportingSourceV1.Json;
    public async Task<FinancialCoverageProof?> Previous(Actor actor, Guid company, Guid key, VerifyFinancialCoverageRequest request, CancellationToken ct)
    {
        actor.RequireCompany(company);
        var receipt = await db.Set<SnapshotReceipt>().AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == actor.TenantId
            && x.CompanyId == company && x.Operation == Operation && x.Key == key, ct);
        if (receipt is null) return null;
        if (receipt.ActorId != actor.Subject || receipt.RequestHash != Hash(request))
            throw new DomainException("snapshot_command_conflict", "This command key belongs to another actor or request.", 409);
        return JsonSerializer.Deserialize<FinancialCoverageProof>(receipt.ResponseJson, Json)!;
    }
    public async Task<FinancialCoverageProof> Verify(Actor actor, Guid company, Guid key, VerifyFinancialCoverageRequest request,
        VerifiedFinancialSnapshot source, CancellationToken ct)
    {
        actor.RequireCompany(company);
        VerifiedFinancialSnapshot.RequireManifest(source.Manifest, actor.TenantId, company, request.ExportId);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await FinancialGenerationLock.Acquire(db, actor.TenantId, company, ct);
        var previous = await Previous(actor, company, key, request, ct);
        if (previous is not null) { await transaction.CommitAsync(ct); return previous; }
        var state = await State(actor.TenantId, company, request.GenerationId, ct);
        if (state.Status != "Open") throw new DomainException("generation_retired", "A retired generation cannot accept new coverage proofs.", 409);
        var latest = await Latest(actor.TenantId, company, request.GenerationId, ct);
        if ((latest?.Version ?? 0) != request.ExpectedVerificationVersion)
            throw new DomainException("coverage_version_conflict", "Reload the generation's verification version.", 409);
        var (pending, quarantine, rejected) = await Unsettled(actor.TenantId, company, request.GenerationId, ct);
        if (pending != 0 || quarantine != 0 || rejected != 0)
            throw new DomainException("coverage_unsettled", "Pending originals or unresolved quarantine prevent coverage verification.", 409);
        var rows = await Projection(actor.TenantId, company, request.GenerationId, ct);
        var fingerprint = ReportingSourceV1.Fingerprint(actor.TenantId, company, rows);
        if (rows.Length != source.Rows.Count || !rows.SequenceEqual(source.Rows) || fingerprint != source.Manifest.FingerprintSha256)
            throw new DomainException("coverage_mismatch", "The complete target event set does not match the frozen source export.", 409);
        var manifestJson = JsonSerializer.Serialize(source.Manifest, Json);
        var proof = await db.Set<FinancialCoverageVerification>().AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == actor.TenantId
            && x.CompanyId == company && x.GenerationId == request.GenerationId && x.ExportId == request.ExportId, ct);
        if (proof is not null && (proof.SourceManifestJson != manifestJson || proof.Fingerprint != fingerprint))
            throw new DomainException("source_export_conflict", "An immutable source export changed.", 409);
        if (proof is null)
        {
            var now = clock.GetUtcNow();
            proof = new() { TenantId = actor.TenantId, CompanyId = company, GenerationId = request.GenerationId, Id = Guid.NewGuid(),
                ExportId = request.ExportId, Version = checked((latest?.Version ?? 0) + 1), SourceManifestJson = manifestJson,
                Fingerprint = fingerprint, RowCount = rows.Length, VerifiedAt = new DateTimeOffset(now.UtcTicks - now.UtcTicks % 10, TimeSpan.Zero) };
            db.Add(proof);
        }
        var result = Result(proof);
        db.Add(new SnapshotAudit { TenantId = actor.TenantId, CompanyId = company, GenerationId = request.GenerationId,
            Id = Guid.NewGuid(), CommandKey = key, ActorId = actor.Subject, Action = Operation, Reason = request.Reason.Trim(), OccurredAt = clock.GetUtcNow() });
        db.Add(new SnapshotReceipt { TenantId = actor.TenantId, CompanyId = company, Operation = Operation, Key = key, ActorId = actor.Subject,
            RequestHash = Hash(request), ResponseJson = JsonSerializer.Serialize(result, Json) });
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return result;
    }
    public async Task<FinancialCoverageStatus> Status(Actor actor, Guid company, Guid generation, CancellationToken ct)
    {
        actor.RequireCompany(company);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        await State(actor.TenantId, company, generation, ct);
        var proof = await Latest(actor.TenantId, company, generation, ct);
        var (pending, quarantine, rejected) = await Unsettled(actor.TenantId, company, generation, ct);
        var matches = false;
        if (proof is not null && pending == 0 && quarantine == 0 && rejected == 0)
        {
            var rows = await Projection(actor.TenantId, company, generation, ct);
            matches = rows.Length == proof.RowCount && ReportingSourceV1.Fingerprint(actor.TenantId, company, rows) == proof.Fingerprint;
        }
        await transaction.CommitAsync(ct);
        return new(generation, proof is null ? null : Result(proof), matches, pending, quarantine, rejected);
    }
    private async Task<FinancialInboxGeneration> State(Guid tenant, Guid company, Guid generation, CancellationToken ct)
        => await db.Set<FinancialInboxGeneration>().AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == tenant && x.CompanyId == company
            && x.GenerationId == generation, ct) ?? throw new DomainException("not_found", "Inbox generation not found.", 404);
    private Task<FinancialCoverageVerification?> Latest(Guid tenant, Guid company, Guid generation, CancellationToken ct)
        => db.Set<FinancialCoverageVerification>().AsNoTracking().Where(x => x.TenantId == tenant && x.CompanyId == company && x.GenerationId == generation)
            .OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct);
    private async Task<(int Pending, int Quarantine, int Rejected)> Unsettled(Guid tenant, Guid company, Guid generation, CancellationToken ct)
    {
        var pending = await db.Set<FinancialInboxMessage>().CountAsync(x => x.TenantId == tenant && x.CompanyId == company && x.GenerationId == generation && x.Status == "PendingOriginal", ct);
        var quarantine = await db.Set<FinancialInboxDeadLetter>().CountAsync(x => x.TenantId == tenant && x.CompanyId == company && x.GenerationId == generation && x.ResolvedAt == null, ct);
        // A disposition cannot be hidden from verification merely by resolving its dead-letter row.
        var rejected = await db.Set<FinancialInboxMessage>().CountAsync(x => x.TenantId == tenant && x.CompanyId == company && x.GenerationId == generation && x.Status == "Quarantined", ct);
        return (pending, quarantine, rejected);
    }
    private async Task<ReportingSourceRowV1[]> Projection(Guid tenant, Guid company, Guid generation, CancellationToken ct)
    {
        var baseline = db.Set<SnapshotEntry>().AsNoTracking().Where(x => x.TenantId == tenant && x.CompanyId == company && x.GenerationId == generation);
        var live = db.Set<FinancialLiveEntry>().AsNoTracking().Where(x => x.TenantId == tenant && x.CompanyId == company && x.GenerationId == generation);
        var count = (long)await baseline.CountAsync(ct) + await live.CountAsync(ct);
        if (count > ReportingSourceV1.MaxRows) Limit();
        var bytes = await db.Database.SqlQuery<long>($"""
            SELECT COALESCE(SUM(octet_length(rows."RowJson")),0)::bigint AS "Value"
            FROM (
                SELECT "RowJson" FROM reporting.snapshot_entries
                WHERE "TenantId" = {tenant} AND "CompanyId" = {company} AND "GenerationId" = {generation}
                UNION ALL
                SELECT "RowJson" FROM reporting.financial_live_entries
                WHERE "TenantId" = {tenant} AND "CompanyId" = {company} AND "GenerationId" = {generation}
            ) rows
            """).SingleAsync(ct);
        if (bytes + count + 2 > ReportingSourceV1.MaxBytes) Limit();
        var json = (await baseline.Select(x => x.RowJson).ToArrayAsync(ct)).Concat(await live.Select(x => x.RowJson).ToArrayAsync(ct));
        return json.Select(x => JsonSerializer.Deserialize<ReportingSourceRowV1>(x, Json)!)
            .OrderBy(x => x.TransactionId.ToString("D"), StringComparer.Ordinal).ToArray();
    }
    private static FinancialCoverageProof Result(FinancialCoverageVerification proof)
    {
        var manifest = JsonSerializer.Deserialize<ReportingExportManifestV1>(proof.SourceManifestJson, Json)!;
        return new(proof.Id, proof.GenerationId, proof.ExportId, proof.Version, proof.Fingerprint, proof.RowCount, manifest.CapturedAt, proof.VerifiedAt);
    }
    private static string Hash(VerifyFinancialCoverageRequest request) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request, Json)));
    private static void Limit() => throw new DomainException("coverage_limit", "Target exceeds the complete V1 export verification budget.", 409);
}
