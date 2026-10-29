using System.Text.Json;
using System.Diagnostics.CodeAnalysis;
using Vat.Domain;
using Vat.Messaging.Contracts.V1;

namespace Vat.Services.Reporting.Domain;

public sealed class VerifiedFinancialSnapshot
{
    public ReportingExportManifestV1 Manifest { get; }
    public IReadOnlyList<ReportingSourceRowV1> Rows { get; }
    private VerifiedFinancialSnapshot(ReportingExportManifestV1 manifest, IReadOnlyList<ReportingSourceRowV1> rows)
    { Manifest = manifest; Rows = Array.AsReadOnly(rows.ToArray()); }
    public static void RequireManifest(ReportingExportManifestV1 manifest, Guid tenant, Guid company, Guid exportId)
    {
        if (manifest.Version != 1 || manifest.Scope != ReportingSourceV1.Scope || !manifest.CoverageComplete
            || manifest.TenantId != tenant || manifest.CompanyId != company || manifest.ExportId != exportId || exportId == Guid.Empty
            || manifest.CapturedAt == default || manifest.EventCount is < 0 or > ReportingSourceV1.MaxRows
            || manifest.ReconciledPurchases < 0 || manifest.ReconciledReversals < 0
            || (long)manifest.ReconciledPurchases + manifest.ReconciledReversals != manifest.EventCount
            || manifest.FingerprintSha256 is not { Length: 64 } || manifest.FingerprintSha256.Any(c => !char.IsAsciiHexDigitLower(c)))
            Invalid();
    }

    public static VerifiedFinancialSnapshot Verify(ReportingExportManifestV1 manifest, IReadOnlyList<ReportingSourceRowV1> rows)
    {
        RequireManifest(manifest, manifest.TenantId, manifest.CompanyId, manifest.ExportId);
        if (manifest.TenantId == Guid.Empty || manifest.CompanyId == Guid.Empty || rows.Count != manifest.EventCount) Invalid();
        var transactions = new HashSet<Guid>(); var events = new HashSet<Guid>(); string? previous = null;
        foreach (var row in rows)
        {
            if (row is null) Invalid();
            var id = row.TransactionId.ToString("D");
            if (row.TransactionId == Guid.Empty || row.EventId == Guid.Empty || row.PurchaseId == Guid.Empty
                || row.BranchId == Guid.Empty || row.WarehouseId == Guid.Empty || row.AggregateVersion <= 0 || row.BusinessDate == default
                || row.ExternalReference is not { Length: > 0 and <= 100 } || row.Currency is not { Length: 3 }
                || row.Currency.Any(c => c is < 'A' or > 'Z') || !transactions.Add(row.TransactionId) || !events.Add(row.EventId)
                || (previous is not null && StringComparer.Ordinal.Compare(previous, id) >= 0)
                || !Money(row.NetAmount) || !Money(row.TaxAmount) || !Money(row.RecoverableTaxAmount)) Invalid();
            previous = id;
        }
        var originals = rows.Where(x => x.Kind == "Purchase").ToDictionary(x => x.TransactionId);
        var reversals = rows.Where(x => x.Kind == "PurchaseReversal").ToArray();
        if (originals.Count != manifest.ReconciledPurchases || reversals.Length != manifest.ReconciledReversals
            || originals.Count + reversals.Length != rows.Count || originals.Values.Select(x => x.PurchaseId).Distinct().Count() != originals.Count
            || reversals.Select(x => x.ReversalOfTransactionId).Distinct().Count() != reversals.Length) Invalid();
        foreach (var row in originals.Values)
            if (row.ReversalOfTransactionId is not null || row.NetAmount < 0 || row.TaxAmount < 0
                || row.RecoverableTaxAmount < 0 || row.RecoverableTaxAmount > row.TaxAmount) Invalid();
        foreach (var row in reversals)
        {
            if (row.ReversalOfTransactionId is null || !originals.TryGetValue(row.ReversalOfTransactionId.Value, out var original)) { Invalid(); continue; }
            if (row.PurchaseId != original.PurchaseId || row.BranchId != original.BranchId || row.WarehouseId != original.WarehouseId
                || row.ExternalReference != original.ExternalReference || row.Currency != original.Currency || row.BusinessDate < original.BusinessDate
                || row.AggregateVersion <= original.AggregateVersion || row.NetAmount != -original.NetAmount
                || row.TaxAmount != -original.TaxAmount || row.RecoverableTaxAmount != -original.RecoverableTaxAmount) Invalid();
        }
        if (JsonSerializer.SerializeToUtf8Bytes(rows, ReportingSourceV1.Json).Length > ReportingSourceV1.MaxBytes
            || ReportingSourceV1.Fingerprint(manifest.TenantId, manifest.CompanyId, rows) != manifest.FingerprintSha256) Invalid();
        return new(manifest, rows);
    }
    private static bool Money(decimal amount) => amount is > -100000000000000000000m and < 100000000000000000000m && decimal.Round(amount, 4) == amount;
    [DoesNotReturn]
    private static void Invalid() => throw new DomainException("invalid_source_snapshot", "Source snapshot scope, coverage, pages or fingerprint could not be verified.", 409);
}
