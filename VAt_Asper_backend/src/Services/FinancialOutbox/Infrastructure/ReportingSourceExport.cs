using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vat.Application;
using Vat.Application.Contracts;
using Vat.Application.Security;
using Vat.Domain;
using Vat.Domain.Posting;
using Vat.Domain.Purchasing;
using Vat.Messaging.Contracts.V1;

namespace Vat.Infrastructure.Persistence;

// Financial source ownership: this table must stay behind when Reporting moves databases.
public sealed class ReportingSourceExport
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid Id { get; set; }
    public string ManifestJson { get; set; } = "";
    public string RowsJson { get; set; } = "";
}

public sealed class ReportingSourceExportService(VatDbContext db, IPeriodStore periods,
    IPostingStore postings, IAuditWriter audit, TimeProvider clock)
{
    public async Task<CommandOutcome> Prepare(Actor actor, Guid companyId, string reason, string correlationId, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        if (reason is null || reason.Length > 1000) throw new DomainException("validation_failed", "Reason must contain at most 1000 characters.");
        reason = Guard.Text(reason, nameof(reason), 1000);
        // ICommandExecutor supplies the transaction. Every source financial writer takes this fence.
        await periods.LockCompany(actor.TenantId, companyId, true, ct);
        if (!await db.Companies.AnyAsync(x => x.TenantId == actor.TenantId && x.Id == companyId, ct))
            throw new DomainException("not_found", "Company not found.", 404);
        var plans = await db.Set<PurchasePostingPlan>().AsNoTracking()
            .Where(x => x.TenantId == actor.TenantId && x.CompanyId == companyId && x.Status == PostingPlanStatus.Posted)
            .Select(x => new { x.Id, x.PurchaseId })
            .Take(ReportingSourceV1.MaxRows + 1).ToArrayAsync(ct);
        Limit(plans.Length);
        var sourceQuery = db.Outbox.AsNoTracking().Where(x => x.TenantId == actor.TenantId && x.CompanyId == companyId
                && x.EventType == nameof(FinancialEntryRecordedV1));
        // Bound materialization before loading payloads (UTF-8 uses at most four bytes per scalar).
        // Explicit text cast is essential: Payload is jsonb, so EF member translation
        // must not interpret string.Length as a JSON member named Length.
        var sourceCharacters = await db.Database.SqlQuery<long>($"""
            SELECT COALESCE(SUM(length("Payload"::text)), 0)::bigint AS "Value"
            FROM platform.outbox_messages
            WHERE "TenantId" = {actor.TenantId} AND "CompanyId" = {companyId}
              AND "EventType" = {nameof(FinancialEntryRecordedV1)}
            """).SingleAsync(ct);
        if (sourceCharacters > ReportingSourceV1.MaxBytes / 4)
            throw new DomainException("export_limit", "Source exceeds the conservative V1 text budget.", 409);
        var source = await sourceQuery
            .OrderBy(x => x.Id).Take(ReportingSourceV1.MaxRows + 1).ToArrayAsync(ct);
        Limit(source.Length);
        if (source.Sum(x => (long)Encoding.UTF8.GetByteCount(x.Payload)) > ReportingSourceV1.MaxBytes)
            throw new DomainException("export_limit", "Source exceeds the V1 export byte limit.", 409);
        var expected = new Dictionary<(Guid, long), FinancialEntryRecordedV1>();
        var reversals = 0;
        foreach (var plan in plans)
        {
            var evidence = await postings.ReadReconciliation(actor.TenantId, companyId, plan.PurchaseId, ct);
            if (evidence is null || evidence.Plan?.Id != plan.Id || !PurchaseReconciler.Evaluate(evidence).IsReconciled)
                throw new DomainException("reconciliation_failed", "Reconcile purchase ledgers before preparing an export.", 409);
            var purchase = evidence.Purchase;
            var frozen = evidence.Plan!;
            void Add(Guid id, long version, string kind, DateOnly date, decimal sign, Guid? reversalOf)
            {
                if (!expected.TryAdd((purchase.Id, version), new(id, purchase.Id, purchase.BranchId, frozen.WarehouseId,
                        kind, date, purchase.ExternalReference, purchase.Currency, sign * frozen.Snapshot.NetAmount,
                        sign * frozen.Snapshot.TaxAmount, sign * frozen.Snapshot.RecoverableTaxAmount, reversalOf))) Conflict();
                Limit(expected.Count);
            }
            Add(plan.Id, purchase.PostedVersion!.Value, "Purchase", purchase.BusinessDate, 1, null);
            foreach (var reversal in evidence.Reversals.Where(x => x.Status == ReversalStatus.Posted))
            {
                Add(reversal.Id, reversal.SourceVersion + 1, "PurchaseReversal", reversal.CorrectionDate, -1, plan.Id);
                reversals++;
            }
            // LatestPlan is tracked by the shared reconciler store; retain only compact export data.
            db.Entry(frozen).State = EntityState.Detached;
        }
        // Do not silently omit an orphan posted reversal or a legacy purchase without a plan.
        var postedCount = await db.Purchases.CountAsync(x => x.TenantId == actor.TenantId && x.CompanyId == companyId &&
            (x.PostedVersion != null || x.Status == PurchaseStatus.Posted || x.Status == PurchaseStatus.ReversalPending || x.Status == PurchaseStatus.Reversed), ct);
        var reversalCount = await db.Set<PurchaseReversal>().CountAsync(x => x.TenantId == actor.TenantId && x.CompanyId == companyId && x.Status == ReversalStatus.Posted, ct);
        if (postedCount != plans.Length || reversalCount != reversals) Conflict();
        if (source.Length != expected.Count) Conflict();
        var rows = new List<ReportingSourceRowV1>(source.Length);
        foreach (var message in source)
        {
            IntegrationEvent<FinancialEntryRecordedV1>? envelope;
            try { envelope = JsonSerializer.Deserialize<IntegrationEvent<FinancialEntryRecordedV1>>(message.Payload, ReportingSourceV1.Json); }
            catch (JsonException) { Conflict(); throw; }
            if (envelope is null || envelope.SchemaVersion != 1 || envelope.EventId != message.Id
                || envelope.TenantId != actor.TenantId || envelope.CompanyId != companyId
                || envelope.EventType != message.EventType || envelope.AggregateId != message.AggregateId
                || envelope.AggregateVersion != message.AggregateVersion
                || !expected.Remove((message.AggregateId, message.AggregateVersion), out var value) || value != envelope.Payload)
                Conflict();
            var p = envelope!.Payload;
            rows.Add(new(message.Id, message.AggregateVersion, p.TransactionId, p.PurchaseId, p.BranchId, p.WarehouseId,
                p.Kind, p.BusinessDate, p.ExternalReference, p.Currency, p.NetAmount, p.TaxAmount, p.RecoverableTaxAmount, p.ReversalOfTransactionId));
        }
        var ordered = rows.OrderBy(x => x.TransactionId.ToString("D"), StringComparer.Ordinal).ToArray();
        var json = JsonSerializer.Serialize(ordered, ReportingSourceV1.Json);
        if (Encoding.UTF8.GetByteCount(json) > ReportingSourceV1.MaxBytes)
            throw new DomainException("export_limit", "Snapshot exceeds the V1 export byte limit.", 409);
        var manifest = new ReportingExportManifestV1(1, Guid.NewGuid(), actor.TenantId, companyId, ReportingSourceV1.Scope,
            clock.GetUtcNow(), plans.Length, reversals, ordered.Length, true, ReportingSourceV1.Fingerprint(actor.TenantId, companyId, ordered));
        db.Add(new ReportingSourceExport { TenantId = actor.TenantId, CompanyId = companyId, Id = manifest.ExportId,
            ManifestJson = JsonSerializer.Serialize(manifest, ReportingSourceV1.Json), RowsJson = json });
        audit.Append(actor, companyId, manifest.ExportId, "ReportingSourceExportPrepared", correlationId, reason: reason);
        return new(200, manifest);
    }

    public async Task<ReportingExportManifestV1> Manifest(Actor actor, Guid companyId, Guid id, CancellationToken ct)
        => JsonSerializer.Deserialize<ReportingExportManifestV1>((await Read(actor, companyId, id, ct)).ManifestJson, ReportingSourceV1.Json)!;

    public async Task<ReportingSourceBoundaryV1> Boundary(Actor actor, Guid companyId, Guid id, CancellationToken ct)
    {
        // Existing pre-sequence exports deliberately have no inferred checkpoint.
        await Read(actor, companyId, id, ct);
        return await db.Database.SqlQuery<ReportingSourceBoundaryV1>($"""
            SELECT "Epoch", "Sequence", "LegacyThroughSequence" FROM platform.reporting_export_boundaries
            WHERE "TenantId" = {actor.TenantId} AND "CompanyId" = {companyId} AND "ExportId" = {id}
            """).SingleOrDefaultAsync(ct) ?? throw new DomainException("checkpoint_unavailable", "Prepare a new export to capture a source checkpoint.", 409);
    }

    public async Task<ReportingSourceBoundaryV1> Checkpoint(Actor actor, Guid companyId, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        return await db.Database.SqlQuery<ReportingSourceBoundaryV1>($"""
            SELECT "Epoch", "Sequence", "LegacyThroughSequence" FROM platform.reporting_source_heads
            WHERE "TenantId" = {actor.TenantId} AND "CompanyId" = {companyId}
            """).SingleOrDefaultAsync(ct) ?? throw new DomainException("checkpoint_unavailable", "Prepare an export before reading its source checkpoint.", 409);
    }

    public async Task<ReportingExportPageV1> Page(Actor actor, Guid companyId, Guid id, int offset, int take, CancellationToken ct)
    {
        if (offset < 0 || offset > ReportingSourceV1.MaxRows || take is < 1 or > 100)
            throw new DomainException("validation_failed", "offset must be 0..10000 and take must be 1..100.");
        var export = await Read(actor, companyId, id, ct);
        var manifest = JsonSerializer.Deserialize<ReportingExportManifestV1>(export.ManifestJson, ReportingSourceV1.Json)!;
        var rows = JsonSerializer.Deserialize<ReportingSourceRowV1[]>(export.RowsJson, ReportingSourceV1.Json)!;
        return new(id, manifest.FingerprintSha256, offset, offset + take < rows.Length ? offset + take : null,
            rows.Skip(offset).Take(take).ToArray());
    }

    private async Task<ReportingSourceExport> Read(Actor actor, Guid companyId, Guid id, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        var export = await db.Set<ReportingSourceExport>().AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == actor.TenantId
            && x.CompanyId == companyId && x.Id == id, ct) ?? throw new DomainException("not_found", "Export not found.", 404);
        if (JsonSerializer.Deserialize<ReportingExportManifestV1>(export.ManifestJson, ReportingSourceV1.Json)?.Scope != ReportingSourceV1.Scope)
            throw new DomainException("not_found", "Export not found.", 404);
        return export;
    }
    private static void Limit(int count)
    {
        if (count > ReportingSourceV1.MaxRows) throw new DomainException("export_limit", "Source exceeds 10000 rows; a new contract is required.", 409);
    }
    private static void Conflict() => throw new DomainException("source_coverage_conflict", "Financial source events do not match reconciled ledgers. Review source/backfill before export.", 409);
}
