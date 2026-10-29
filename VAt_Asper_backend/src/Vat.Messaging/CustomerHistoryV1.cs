using System.Security.Cryptography;
using System.Text.Json;

namespace Vat.Messaging.Contracts.V1;

// Complete dated history, not a current balance or a live event checkpoint.
public static class CustomerHistoryV1
{
    public const string Scope = "customer-history-v1";
    public const string Prefix = "/internal/v1/reporting-source/companies/{companyId:guid}/customers/{customerId:guid}/exports";
    public const int MaxRows = 10_000;
    public const int MaxBytes = 8 * 1024 * 1024;
    public static string Fingerprint(Guid tenant, Guid company, Guid customer, CustomerHistoryDataV1 data)
        => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
            new { version = 1, tenant, company, customer, scope = Scope, data }, ReportingSourceV1.Json))).ToLowerInvariant();
    public static int Count(CustomerHistoryDataV1 data) => checked(data.Invoices.Count + data.Documents.Count
        + data.Payments.Count + data.Corrections.Count + data.Allocations.Count + data.Releases.Count);
}

public sealed record CustomerHistoryInvoiceV1(Guid Id, string Reference, DateOnly BusinessDate, DateOnly? DueDate);
public sealed record CustomerHistoryDocumentV1(Guid Id, Guid InvoiceId, string Kind, DateOnly BusinessDate,
    string Number, decimal NetAmount, decimal TaxAmount);
public sealed record CustomerHistoryPaymentV1(Guid Id, string Reference, DateOnly BusinessDate, decimal Amount);
public sealed record CustomerHistoryCorrectionV1(Guid Id, Guid PaymentId, string Kind, DateOnly BusinessDate,
    string Number, decimal Amount);
public sealed record CustomerHistoryAllocationV1(Guid Id, Guid PaymentId, Guid InvoiceId, DateOnly BusinessDate, decimal Amount);
public sealed record CustomerHistoryReleaseV1(Guid Id, Guid CorrectionId, Guid AllocationId, DateOnly BusinessDate, decimal Amount);
public sealed record CustomerHistoryDataV1(string CustomerCode, string CustomerName, long CustomerVersion, string Currency,
    IReadOnlyList<CustomerHistoryInvoiceV1> Invoices, IReadOnlyList<CustomerHistoryDocumentV1> Documents,
    IReadOnlyList<CustomerHistoryPaymentV1> Payments, IReadOnlyList<CustomerHistoryCorrectionV1> Corrections,
    IReadOnlyList<CustomerHistoryAllocationV1> Allocations, IReadOnlyList<CustomerHistoryReleaseV1> Releases);
public sealed record CustomerHistoryManifestV1(int Version, Guid ExportId, Guid TenantId, Guid CompanyId, Guid CustomerId,
    string Scope, DateTimeOffset CapturedAt, bool FullHistory, int RowCount, string FingerprintSha256);
public sealed record CustomerHistoryExportV1(CustomerHistoryManifestV1 Manifest, CustomerHistoryDataV1 Data);
