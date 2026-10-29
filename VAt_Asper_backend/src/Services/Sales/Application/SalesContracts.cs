using Vat.Application.Security;
using Vat.Domain.Catalog;
using Vat.Domain.Sales;
using System.Text.Json.Serialization;

namespace Vat.Services.Sales
{
    public sealed record CommandOutcome(int StatusCode, object Body, string? Location = null);
    public sealed record StoredResponse(int StatusCode, string Json, string? Location, bool Replayed);

    public interface ICommandExecutor
    {
        Task<StoredResponse> Execute(Actor actor, Guid companyId, string operation, string key,
            object request, Func<Task<CommandOutcome>> action, CancellationToken ct);
    }

    public interface IAuditWriter
    {
        void Append(Actor actor, Guid companyId, Guid resourceId, string action, string correlationId,
            long? version = null, string? reason = null);
    }

    public interface IEventWriter
    {
        void Append<T>(Guid tenantId, Guid companyId, Guid aggregateId, long version,
            string eventType, string correlationId, T payload);
    }

    public interface IPeriodGuard
    {
        Task RequireOpen(Guid tenantId, Guid companyId, DateOnly businessDate, CancellationToken ct);
        Task LockShared(Guid tenantId, Guid companyId, CancellationToken ct);
    }

    public sealed record PostingPolicy(bool Enabled);
    public sealed record WarehouseReference(Guid Id, Guid BranchId, string Code, string Name);

    public interface ISalesInvoiceStore
    {
        Task<SalesInvoice?> Find(Guid tenantId, Guid companyId, Guid invoiceId, CancellationToken ct);
        Task<IReadOnlyList<SalesInvoice>> List(Guid tenantId, Guid companyId, Guid? before, int take, CancellationToken ct);
        void Add(SalesInvoice invoice);
    }

    public interface ICustomerStore
    {
        Task<Vat.Domain.Sales.Customer?> Find(Guid tenant, Guid company, Guid id, CancellationToken ct);
        Task<IReadOnlyList<Vat.Domain.Sales.Customer>> List(Guid tenant, Guid company, Guid? before, int take, CancellationToken ct);
        void Add(Vat.Domain.Sales.Customer customer);
    }

    public interface IReferenceStore
    {
        Task<Product?> ProductByReference(Guid tenantId, Guid companyId, string reference, CancellationToken ct);
        Task<Vat.Domain.Tax.TaxRule?> EffectiveSalesRule(Guid tenantId, Guid companyId, string taxCode, DateOnly date, CancellationToken ct);
        Task<WarehouseReference?> Warehouse(Guid tenantId, Guid companyId, Guid id, CancellationToken ct);
    }
}

namespace Vat.Services.Sales.Contracts
{
    public sealed record CreateSalesInvoiceRequest(Guid BranchId, string ExternalReference, string CustomerReference,
        DateOnly BusinessDate, IReadOnlyList<SalesInvoiceLineInput> Lines, Guid? CustomerId = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateOnly? DueDate = null);

    public sealed record CancelSalesInvoiceRequest(long ExpectedVersion, string Reason);
    public sealed record RejectSalesInvoiceRequest(long ExpectedVersion, string Reason);
    public sealed record TransitionRequest(long ExpectedVersion);

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
}

