using Vat.Application.Security;
using Vat.Domain;
using Vat.Messaging.Contracts.V1;
using Vat.Services.Reporting.Domain;

namespace Vat.Services.Reporting.Application;

public sealed record VerifyFinancialCoverageRequest(Guid GenerationId, Guid ExportId, long ExpectedVerificationVersion, string Reason);
public sealed record FinancialCoverageProof(Guid VerificationId, Guid GenerationId, Guid ExportId, long VerificationVersion,
    string FingerprintSha256, int RowCount, DateTimeOffset SourceCapturedAt, DateTimeOffset VerifiedAt,
    string SourceScope = ReportingSourceV1.Scope, bool ExportCoverageVerified = true, bool LiveCoverageVerified = false);
public sealed record FinancialCoverageStatus(Guid GenerationId, FinancialCoverageProof? LatestVerification,
    bool ProjectionMatchesVerifiedExport, int PendingOriginals, int QuarantinedMessages, int RejectedMessages, bool LiveCoverageVerified = false);
public interface IFinancialCoverageStore
{
    Task<FinancialCoverageProof?> Previous(Actor actor, Guid company, Guid key, VerifyFinancialCoverageRequest request, CancellationToken ct);
    Task<FinancialCoverageProof> Verify(Actor actor, Guid company, Guid key, VerifyFinancialCoverageRequest request, VerifiedFinancialSnapshot source, CancellationToken ct);
    Task<FinancialCoverageStatus> Status(Actor actor, Guid company, Guid generation, CancellationToken ct);
}
public sealed class FinancialCoverageService(IFinancialCoverageStore store)
{
    public async Task<FinancialCoverageProof> Verify(Actor actor, Guid company, Guid key, VerifyFinancialCoverageRequest request,
        IReportingExportSource source, CancellationToken ct)
    {
        Require(actor, company, Permissions.ReportManage);
        if (key == Guid.Empty || request.GenerationId == Guid.Empty || request.ExportId == Guid.Empty
            || request.ExpectedVerificationVersion is < 0 or long.MaxValue || string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 1000)
            throw new DomainException("validation_failed", "Supply command/generation/export UUIDs, a verification version and bounded reason.");
        var previous = await store.Previous(actor, company, key, request, ct);
        if (previous is not null) return previous; // An original committed proof remains replayable offline.
        var snapshot = await FinancialExportDownload.Read(actor.TenantId, company, request.ExportId, source, ct);
        return await store.Verify(actor, company, key, request, snapshot, ct);
    }
    public Task<FinancialCoverageStatus> Status(Actor actor, Guid company, Guid generation, CancellationToken ct)
    {
        Require(actor, company, Permissions.ReportRead);
        if (generation == Guid.Empty) throw new DomainException("validation_failed", "Generation UUID is required.");
        return store.Status(actor, company, generation, ct);
    }
    private static void Require(Actor actor, Guid company, string permission)
    {
        actor.RequireCompany(company);
        if (!actor.Permissions.Contains(permission)) throw new DomainException("permission_denied", "Report permission is required.", 403);
        if (actor.TenantId == Guid.Empty || string.IsNullOrWhiteSpace(actor.Subject) || actor.Subject.Length > 200)
            throw new DomainException("validation_failed", "A verified tenant and operator identity are required.");
    }
}
