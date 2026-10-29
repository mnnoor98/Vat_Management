using System.Buffers.Binary;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vat.Application.Security;
using Vat.Domain;
using Vat.Messaging.Contracts.V1;
using Vat.Services.Reporting.Application;
using Vat.Services.Reporting.Domain;

namespace Vat.Services.Reporting.Infrastructure;

public sealed class ReportingGenerationStore(ReportingDbContext db, TimeProvider clock) : IReportingGenerationStore
{
    private const string BootstrapOperation = "snapshot.bootstrap-v1";
    private const string ActivateOperation = "snapshot.activate-v1";
    public Task<SnapshotGenerationResult?> PreviousBootstrap(Actor actor, Guid company, Guid key, BootstrapSnapshotRequest request, CancellationToken ct)
        => Receipt<SnapshotGenerationResult>(actor, company, BootstrapOperation, key, request, ct);

    public async Task<SnapshotGenerationResult> Import(Actor actor, Guid company, Guid key, BootstrapSnapshotRequest request, VerifiedFinancialSnapshot snapshot, CancellationToken ct)
    {
        actor.RequireCompany(company);
        VerifiedFinancialSnapshot.RequireManifest(snapshot.Manifest, actor.TenantId, company, request.ExportId);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await Lock(actor.TenantId, company, ct);
        var previous = await Receipt<SnapshotGenerationResult>(actor, company, BootstrapOperation, key, request, ct);
        if (previous is not null) { await transaction.CommitAsync(ct); return previous; }
        var manifest = snapshot.Manifest;
        var generation = await db.Set<SnapshotGeneration>().SingleOrDefaultAsync(x => x.TenantId == actor.TenantId && x.CompanyId == company && x.ExportId == request.ExportId, ct);
        if (generation is not null && (generation.Fingerprint != manifest.FingerprintSha256
            || generation.ManifestJson != JsonSerializer.Serialize(manifest, ReportingSourceV1.Json)))
            throw new DomainException("source_export_conflict", "An immutable export changed its fingerprint.", 409);
        if (generation is null)
        {
            generation = new() { TenantId = actor.TenantId, CompanyId = company, Id = Guid.NewGuid(), ExportId = request.ExportId,
                ManifestJson = JsonSerializer.Serialize(manifest, ReportingSourceV1.Json), Fingerprint = manifest.FingerprintSha256,
                CapturedAt = manifest.CapturedAt.ToUniversalTime(), ImportedAt = clock.GetUtcNow(), EventCount = manifest.EventCount };
            db.Add(generation);
            foreach (var row in snapshot.Rows)
                db.Add(new SnapshotEntry { TenantId = actor.TenantId, CompanyId = company, GenerationId = generation.Id,
                    TransactionId = row.TransactionId, EventId = row.EventId, BusinessDate = row.BusinessDate, Currency = row.Currency,
                    NetAmount = row.NetAmount, TaxAmount = row.TaxAmount, RecoverableTaxAmount = row.RecoverableTaxAmount,
                    RowJson = JsonSerializer.Serialize(row, ReportingSourceV1.Json) });
        }
        var result = new SnapshotGenerationResult(generation.Id, generation.ExportId, generation.Fingerprint, generation.EventCount);
        Record(actor, company, generation.Id, "SnapshotBootstrapped", request.Reason, BootstrapOperation, key, request, result);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return result;
    }

    public async Task<SnapshotHeadResult> Activate(Actor actor, Guid company, Guid key, ActivateSnapshotRequest request, CancellationToken ct)
    {
        actor.RequireCompany(company);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await Lock(actor.TenantId, company, ct);
        var previous = await Receipt<SnapshotHeadResult>(actor, company, ActivateOperation, key, request, ct);
        if (previous is not null) { await transaction.CommitAsync(ct); return previous; }
        var generation = await db.Set<SnapshotGeneration>().AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == actor.TenantId && x.CompanyId == company && x.Id == request.GenerationId, ct)
            ?? throw new DomainException("not_found", "Snapshot generation not found.", 404);
        var head = await db.Set<SnapshotHead>().SingleOrDefaultAsync(x => x.TenantId == actor.TenantId && x.CompanyId == company, ct);
        if ((head?.Version ?? 0) != request.ExpectedHeadVersion)
            throw new DomainException("snapshot_version_conflict", "The current snapshot changed; reload its head version.", 409);
        if (head is null) { head = new() { TenantId = actor.TenantId, CompanyId = company }; db.Add(head); }
        head.GenerationId = generation.Id; head.Version = checked(head.Version + 1);
        var result = new SnapshotHeadResult(generation.Id, head.Version, generation.ExportId, generation.CapturedAt);
        Record(actor, company, generation.Id, "SnapshotActivated", request.Reason, ActivateOperation, key, request, result);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return result;
    }

    public async Task<SnapshotSummary> Summary(Actor actor, Guid company, DateOnly from, DateOnly until, CancellationToken ct)
    {
        actor.RequireCompany(company);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var head = await db.Set<SnapshotHead>().AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == actor.TenantId && x.CompanyId == company, ct)
            ?? throw new DomainException("snapshot_not_ready", "No verified snapshot is active.", 409);
        var generation = await db.Set<SnapshotGeneration>().AsNoTracking().SingleAsync(x => x.TenantId == actor.TenantId && x.CompanyId == company && x.Id == head.GenerationId, ct);
        var totals = await db.Set<SnapshotEntry>().AsNoTracking().Where(x => x.TenantId == actor.TenantId && x.CompanyId == company
                && x.GenerationId == generation.Id && from <= x.BusinessDate && x.BusinessDate < until)
            .GroupBy(x => x.Currency).OrderBy(x => x.Key)
            .Select(x => new SnapshotCurrencyTotals(x.Key, x.Count(), x.Sum(r => r.NetAmount), x.Sum(r => r.TaxAmount), x.Sum(r => r.RecoverableTaxAmount))).ToArrayAsync(ct);
        await transaction.CommitAsync(ct);
        return new(new(generation.Id, head.Version, generation.ExportId, generation.CapturedAt), from, until, totals);
    }

    private async Task<T?> Receipt<T>(Actor actor, Guid company, string operation, Guid key, object request, CancellationToken ct) where T : class
    {
        actor.RequireCompany(company);
        var receipt = await db.Set<SnapshotReceipt>().AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == actor.TenantId
            && x.CompanyId == company && x.Operation == operation && x.Key == key, ct);
        if (receipt is null) return null;
        if (receipt.ActorId != actor.Subject || receipt.RequestHash != Hash(request))
            throw new DomainException("snapshot_command_conflict", "This command key belongs to another actor or payload.", 409);
        return JsonSerializer.Deserialize<T>(receipt.ResponseJson, ReportingSourceV1.Json)!;
    }
    private async Task Lock(Guid tenant, Guid company, CancellationToken ct)
    {
        var key = BinaryPrimitives.ReadInt64BigEndian(SHA256.HashData(Encoding.UTF8.GetBytes($"reporting-snapshot:{tenant}:{company}")));
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})", ct);
    }
    private void Record(Actor actor, Guid company, Guid generation, string action, string reason, string operation, Guid key, object request, object result)
    {
        db.Add(new SnapshotAudit { TenantId = actor.TenantId, CompanyId = company, Id = Guid.NewGuid(), GenerationId = generation,
            ActorId = actor.Subject, CommandKey = key, Action = action, Reason = reason.Trim(), OccurredAt = clock.GetUtcNow() });
        db.Add(new SnapshotReceipt { TenantId = actor.TenantId, CompanyId = company, Operation = operation, Key = key, ActorId = actor.Subject,
            RequestHash = Hash(request), ResponseJson = JsonSerializer.Serialize(result, result.GetType(), ReportingSourceV1.Json) });
    }
    private static string Hash(object request) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request, request.GetType(), ReportingSourceV1.Json)));
}
