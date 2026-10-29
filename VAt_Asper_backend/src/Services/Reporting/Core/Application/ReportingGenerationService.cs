using Vat.Application.Security;
using Vat.Domain;
using Vat.Messaging.Contracts.V1;
using Vat.Services.Reporting.Domain;

namespace Vat.Services.Reporting.Application;

public sealed record BootstrapSnapshotRequest(Guid ExportId, string Reason);
public sealed record ActivateSnapshotRequest(Guid GenerationId, long ExpectedHeadVersion, string Reason);
public sealed record SnapshotGenerationResult(Guid GenerationId, Guid ExportId, string FingerprintSha256, int EventCount, bool SnapshotOnly = true);
public sealed record SnapshotHeadResult(Guid GenerationId, long Version, Guid ExportId, DateTimeOffset CapturedAt, bool LiveCoverageVerified = false);
public sealed record SnapshotCurrencyTotals(string Currency, int EntryCount, decimal NetAmount, decimal TaxAmount, decimal RecoverableTaxAmount);
public sealed record SnapshotSummary(SnapshotHeadResult Snapshot, DateOnly From, DateOnly Until, IReadOnlyList<SnapshotCurrencyTotals> Totals);

public interface IReportingExportSource
{
    Task<ReportingExportManifestV1> Manifest(Guid companyId, Guid exportId, CancellationToken ct);
    Task<ReportingExportPageV1> Page(Guid companyId, Guid exportId, int offset, int take, CancellationToken ct);
}

public interface IReportingGenerationStore
{
    Task<SnapshotGenerationResult?> PreviousBootstrap(Actor actor, Guid company, Guid key, BootstrapSnapshotRequest request, CancellationToken ct);
    Task<SnapshotGenerationResult> Import(Actor actor, Guid company, Guid key, BootstrapSnapshotRequest request, VerifiedFinancialSnapshot snapshot, CancellationToken ct);
    Task<SnapshotHeadResult> Activate(Actor actor, Guid company, Guid key, ActivateSnapshotRequest request, CancellationToken ct);
    Task<SnapshotSummary> Summary(Actor actor, Guid company, DateOnly from, DateOnly until, CancellationToken ct);
}

public sealed class ReportingGenerationService(IReportingGenerationStore store)
{
    public async Task<SnapshotGenerationResult> Bootstrap(Actor actor, Guid company, Guid key, BootstrapSnapshotRequest request,
        IReportingExportSource source, CancellationToken ct)
    {
        Require(actor, company, key, request.Reason, Permissions.ReportManage);
        if (request.ExportId == Guid.Empty) throw new DomainException("validation_failed", "Export ID is required.");
        var previous = await store.PreviousBootstrap(actor, company, key, request, ct);
        if (previous is not null) return previous; // committed retries work even if the source is offline
        var verified = await FinancialExportDownload.Read(actor.TenantId, company, request.ExportId, source, ct);
        return await store.Import(actor, company, key, request, verified, ct);
    }
    public Task<SnapshotHeadResult> ActivateSnapshot(Actor actor, Guid company, Guid key, ActivateSnapshotRequest request, CancellationToken ct)
    {
        Require(actor, company, key, request.Reason, Permissions.ReportManage);
        if (request.GenerationId == Guid.Empty || request.ExpectedHeadVersion < 0) throw new DomainException("validation_failed", "Generation and expected head version are required.");
        return store.Activate(actor, company, key, request, ct);
    }
    public Task<SnapshotSummary> ReadSnapshot(Actor actor, Guid company, DateOnly from, DateOnly until, CancellationToken ct)
    {
        actor.RequireCompany(company);
        if (!actor.Permissions.Contains(Permissions.ReportRead)) throw new DomainException("permission_denied", "Report read permission is required.", 403);
        if (from == default || until <= from) throw new DomainException("validation_failed", "A non-empty [from, until) interval is required.");
        return store.Summary(actor, company, from, until, ct);
    }
    private static void Require(Actor actor, Guid company, Guid key, string reason, string permission)
    {
        actor.RequireCompany(company);
        if (actor.TenantId == Guid.Empty || string.IsNullOrWhiteSpace(actor.Subject) || actor.Subject.Length > 200)
            throw new DomainException("validation_failed", "A verified tenant and operator identity are required.");
        if (!actor.Permissions.Contains(permission)) throw new DomainException("permission_denied", "Report management permission is required.", 403);
        if (key == Guid.Empty || reason is null || reason.Length > 1000 || string.IsNullOrWhiteSpace(reason))
            throw new DomainException("validation_failed", "A command UUID and reason of at most 1000 characters are required.");
    }
}
