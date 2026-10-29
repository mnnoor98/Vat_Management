using System.Security.Cryptography;
using System.Text.Json;

namespace Vat.Messaging.Contracts.V1;

// A frozen, bounded bootstrap snapshot. CapturedAt is evidence, never a commit watermark.
public static class ReportingSourceV1
{
    public const string Prefix = "/internal/v1/reporting-source/companies/{companyId:guid}/exports";
    public const string Scope = "purchase-financial-v1";
    public const int MaxRows = 10_000;
    public const int MaxBytes = 8 * 1024 * 1024;
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // V1 canonical bytes: System.Text.Json web defaults, declared property order, ordinal
    // transaction UUID ordering; amounts use the authoritative source decimal representation.
    public static string Fingerprint(Guid tenantId, Guid companyId, IReadOnlyList<ReportingSourceRowV1> rows)
        => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
            new { version = 1, tenantId, companyId, scope = Scope, rows }, Json))).ToLowerInvariant();
}

public sealed record PrepareReportingExportV1(string Reason);
public sealed record ReportingSourceRowV1(Guid EventId, long AggregateVersion, Guid TransactionId,
    Guid PurchaseId, Guid BranchId, Guid WarehouseId, string Kind, DateOnly BusinessDate,
    string ExternalReference, string Currency, decimal NetAmount, decimal TaxAmount,
    decimal RecoverableTaxAmount, Guid? ReversalOfTransactionId);
public sealed record ReportingExportManifestV1(int Version, Guid ExportId, Guid TenantId, Guid CompanyId,
    string Scope, DateTimeOffset CapturedAt, int ReconciledPurchases, int ReconciledReversals,
    int EventCount, bool CoverageComplete, string FingerprintSha256);
public sealed record ReportingExportPageV1(Guid ExportId, string FingerprintSha256, int Offset,
    int? NextOffset, IReadOnlyList<ReportingSourceRowV1> Rows);
