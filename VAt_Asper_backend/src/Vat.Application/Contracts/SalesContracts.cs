using Vat.Domain.Sales;
using System.Text.Json.Serialization;

namespace Vat.Application.Contracts;

public sealed record CreateSalesInvoiceRequest(Guid BranchId, string ExternalReference, string CustomerReference,
    DateOnly BusinessDate, IReadOnlyList<SalesInvoiceLineInput> Lines, Guid? CustomerId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateOnly? DueDate = null);
public sealed record CancelSalesInvoiceRequest(long ExpectedVersion, string Reason);
public sealed record RejectSalesInvoiceRequest(long ExpectedVersion, string Reason);
public sealed record SalesInvoiceLineResponse(string ProductReference, string Description, decimal Quantity,
    decimal UnitPrice, decimal NetAmount);
public sealed record SalesInvoiceResponse(Guid Id, Guid CompanyId, Guid BranchId, string ExternalReference,
    string CustomerReference, DateOnly BusinessDate, string Currency, string Status, long Version,
    decimal NetAmount, string CreatedBy, string? ApprovedBy, string? RejectionReason,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, IReadOnlyList<SalesInvoiceLineResponse> Lines,
    string? CancellationReason, long? PostedVersion = null, Guid? CustomerId = null, CustomerSnapshot? CustomerSnapshot = null, DateOnly? DueDate = null)
{
    public static SalesInvoiceResponse From(SalesInvoice invoice) => new(invoice.Id, invoice.CompanyId,
        invoice.BranchId, invoice.ExternalReference, invoice.CustomerReference, invoice.BusinessDate,
        invoice.Currency, invoice.Status.ToString(), invoice.Version, invoice.NetAmount,
        invoice.CreatedBy, invoice.ApprovedBy, invoice.RejectionReason, invoice.CreatedAt, invoice.UpdatedAt,
        invoice.Lines.Select(x => new SalesInvoiceLineResponse(x.ProductReference, x.Description,
            x.Quantity, x.UnitPrice, x.NetAmount)).ToArray(), invoice.CancellationReason, invoice.PostedVersion, invoice.CustomerId, invoice.CustomerSnapshot, invoice.DueDate);
}

public sealed record SalesInvoiceApprovedV1(Guid SalesInvoiceId, Guid BranchId, long DocumentVersion,
    string ExternalReference, DateOnly BusinessDate, string Currency, decimal NetAmount,
    string MakerId, string ApproverId);

