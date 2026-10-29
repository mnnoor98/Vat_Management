using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Vat.Domain;
using Vat.Messaging.Contracts.V1;

namespace Vat.Services.Reporting.Domain;

public sealed class VerifiedCustomerHistory
{
    public CustomerHistoryManifestV1 Manifest { get; }
    public CustomerHistoryDataV1 Data { get; }
    private VerifiedCustomerHistory(CustomerHistoryManifestV1 manifest, CustomerHistoryDataV1 data)
    {
        Manifest = manifest;
        Data = data with { Invoices = Array.AsReadOnly(data.Invoices.ToArray()), Documents = Array.AsReadOnly(data.Documents.ToArray()),
            Payments = Array.AsReadOnly(data.Payments.ToArray()), Corrections = Array.AsReadOnly(data.Corrections.ToArray()),
            Allocations = Array.AsReadOnly(data.Allocations.ToArray()), Releases = Array.AsReadOnly(data.Releases.ToArray()) };
    }
    public static VerifiedCustomerHistory Verify(CustomerHistoryExportV1 export, Guid tenant, Guid company, Guid customer)
    {
        if (export?.Manifest is not { } m || export.Data is not { } d) Invalid();
        m = export.Manifest; d = export.Data;
        if (tenant == Guid.Empty || company == Guid.Empty || customer == Guid.Empty || m.TenantId != tenant || m.CompanyId != company
            || m.CustomerId != customer || m.ExportId == Guid.Empty || m.Version != 1 || m.Scope != CustomerHistoryV1.Scope
            || !m.FullHistory || m.CapturedAt == default || m.RowCount is < 0 or > CustomerHistoryV1.MaxRows
            || m.FingerprintSha256 is not { Length: 64 } || m.FingerprintSha256.Any(c => !char.IsAsciiHexDigitLower(c))
            || d.CustomerCode is not { Length: > 0 and <= 100 } || d.CustomerName is not { Length: > 0 and <= 200 }
            || d.CustomerVersion < 1 || d.Currency != "BDT" || d.Invoices is null || d.Documents is null || d.Payments is null
            || d.Corrections is null || d.Allocations is null || d.Releases is null) Invalid();
        // Avoid checked count overflow or serializing a deliberately unbounded collection.
        long count = (long)d.Invoices.Count + d.Documents.Count + d.Payments.Count + d.Corrections.Count + d.Allocations.Count + d.Releases.Count;
        if (count != m.RowCount || count > CustomerHistoryV1.MaxRows) Invalid();
        Ordered(d.Invoices, x => x.Id); Ordered(d.Documents, x => x.Id); Ordered(d.Payments, x => x.Id);
        Ordered(d.Corrections, x => x.Id); Ordered(d.Allocations, x => x.Id); Ordered(d.Releases, x => x.Id);
        var invoices = d.Invoices.ToDictionary(x => x.Id); var payments = d.Payments.ToDictionary(x => x.Id);
        var allocations = d.Allocations.ToDictionary(x => x.Id); var corrections = d.Corrections.ToDictionary(x => x.Id);
        foreach (var i in d.Invoices)
            if (!Text(i.Reference, 100) || i.BusinessDate == default || i.DueDate < i.BusinessDate) Invalid();
        foreach (var doc in d.Documents)
        {
            if (!invoices.TryGetValue(doc.InvoiceId, out var invoice) || doc.BusinessDate < invoice.BusinessDate
                || !Text(doc.Number, 100) || !Money(doc.NetAmount) || !Money(doc.TaxAmount)
                || doc.Kind is not ("Invoice" or "CreditNote" or "DebitNote")) Invalid();
            if (doc.Kind == "Invoice" && (doc.Id != doc.InvoiceId || doc.BusinessDate != invoice.BusinessDate)) Invalid();
            if (doc.Kind != "Invoice" && invoices.ContainsKey(doc.Id)) Invalid();
            if (doc.Kind == "CreditNote" ? doc.NetAmount > 0 || doc.TaxAmount > 0 : doc.NetAmount < 0 || doc.TaxAmount < 0) Invalid();
        }
        if (d.Documents.Count(x => x.Kind == "Invoice") != invoices.Count) Invalid();
        foreach (var p in d.Payments)
            if (!Text(p.Reference, 100) || p.BusinessDate == default || !Positive(p.Amount)) Invalid();
        foreach (var c in d.Corrections)
            if (!payments.TryGetValue(c.PaymentId, out var p) || c.BusinessDate < p.BusinessDate
                || !Text(c.Number, 100) || c.Kind is not ("PaymentRefund" or "ReceiptReversal") || !Money(c.Amount) || c.Amount >= 0) Invalid();
        foreach (var a in d.Allocations)
            if (!payments.TryGetValue(a.PaymentId, out var p) || !invoices.TryGetValue(a.InvoiceId, out var i)
                || a.BusinessDate < p.BusinessDate || a.BusinessDate < i.BusinessDate || !Positive(a.Amount)) Invalid();
        foreach (var r in d.Releases)
            if (!corrections.TryGetValue(r.CorrectionId, out var c) || !allocations.TryGetValue(r.AllocationId, out var a)
                || c.PaymentId != a.PaymentId || r.BusinessDate != c.BusinessDate || r.BusinessDate < a.BusinessDate || !Positive(r.Amount)) Invalid();
        foreach (var a in d.Allocations)
            if (d.Releases.Where(x => x.AllocationId == a.Id).Sum(x => x.Amount) > a.Amount) Invalid();
        foreach (var c in d.Corrections)
            if (d.Releases.Where(x => x.CorrectionId == c.Id).Sum(x => x.Amount) > -c.Amount) Invalid();
        // Check dated funding, not only a final balance: future releases must not fund old cash.
        foreach (var p in d.Payments)
        {
            var paid = d.Allocations.Where(x => x.PaymentId == p.Id).ToArray();
            var returned = d.Corrections.Where(x => x.PaymentId == p.Id).ToArray();
            var released = d.Releases.Where(x => corrections[x.CorrectionId].PaymentId == p.Id).ToArray();
            foreach (var day in paid.Select(x => x.BusinessDate).Concat(returned.Select(x => x.BusinessDate)).Distinct())
            {
                var cash = p.Amount + returned.Where(x => x.BusinessDate <= day).Sum(x => x.Amount);
                var applied = paid.Where(x => x.BusinessDate <= day).Sum(x => x.Amount) - released.Where(x => x.BusinessDate <= day).Sum(x => x.Amount);
                if (cash < 0 || applied < 0 || applied > cash) Invalid();
            }
        }
        if (JsonSerializer.SerializeToUtf8Bytes(d, ReportingSourceV1.Json).Length > CustomerHistoryV1.MaxBytes
            || CustomerHistoryV1.Fingerprint(tenant, company, customer, d) != m.FingerprintSha256) Invalid();
        return new(m, d);
    }
    private static void Ordered<T>(IReadOnlyList<T> rows, Func<T, Guid> key)
    {
        string? previous = null;
        foreach (var row in rows)
        {
            if (row is null || key(row) == Guid.Empty) Invalid();
            var current = key(row).ToString("D");
            if (previous is not null && StringComparer.Ordinal.Compare(previous, current) >= 0) Invalid();
            previous = current;
        }
    }
    private static bool Text(string value, int max) => !string.IsNullOrWhiteSpace(value) && value.Length <= max;
    private static bool Money(decimal value) => value is > -100000000000000000000m and < 100000000000000000000m && decimal.Round(value, 4) == value;
    private static bool Positive(decimal value) => value > 0 && Money(value);
    [DoesNotReturn] private static void Invalid() => throw new DomainException("invalid_customer_history", "Complete customer history scope, references, dates, amounts or fingerprint could not be verified.", 409);
}
