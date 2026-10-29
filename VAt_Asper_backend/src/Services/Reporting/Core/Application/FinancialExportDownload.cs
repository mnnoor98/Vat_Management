using System.Text;
using System.Text.Json;
using Vat.Domain;
using Vat.Messaging.Contracts.V1;
using Vat.Services.Reporting.Domain;

namespace Vat.Services.Reporting.Application;

// Shared download boundary for bootstrap and export-coverage verification.
public static class FinancialExportDownload
{
    public static async Task<VerifiedFinancialSnapshot> Read(Guid tenant, Guid company, Guid exportId,
        IReportingExportSource source, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(TimeSpan.FromSeconds(20));
        var manifest = await Call(source.Manifest(company, exportId, deadline.Token), deadline.Token, ct);
        VerifiedFinancialSnapshot.RequireManifest(manifest, tenant, company, exportId);
        var rows = new List<ReportingSourceRowV1>(manifest.EventCount); long bytes = 1;
        while (rows.Count < manifest.EventCount)
        {
            var offset = rows.Count;
            var page = await Call(source.Page(company, exportId, offset, 100, deadline.Token), deadline.Token, ct);
            if (page.ExportId != exportId || page.FingerprintSha256 != manifest.FingerprintSha256 || page.Offset != offset
                || page.Rows is null || page.Rows.Count is < 1 or > 100 || page.Rows.Count > manifest.EventCount - offset
                || page.NextOffset != (offset + page.Rows.Count < manifest.EventCount ? offset + page.Rows.Count : (int?)null))
                throw new DomainException("invalid_source_snapshot", "Source export pages are incomplete or inconsistent.", 409);
            foreach (var row in page.Rows)
            {
                bytes += Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(row, ReportingSourceV1.Json)) + 1;
                if (bytes > ReportingSourceV1.MaxBytes) throw new DomainException("invalid_source_snapshot", "Source snapshot exceeds the byte budget.", 409);
                rows.Add(row);
            }
        }
        var verified = VerifiedFinancialSnapshot.Verify(manifest, rows);
        if (deadline.IsCancellationRequested)
        { ct.ThrowIfCancellationRequested(); throw new DomainException("reporting_source_unavailable", "Source export verification exceeded its deadline.", 503); }
        return verified;
    }
    private static async Task<T> Call<T>(Task<T> call, CancellationToken deadline, CancellationToken caller)
    {
        try { return await call.WaitAsync(deadline); }
        catch (OperationCanceledException) when (!caller.IsCancellationRequested)
        { throw new DomainException("reporting_source_unavailable", "Source export download did not complete.", 503); }
    }
}
