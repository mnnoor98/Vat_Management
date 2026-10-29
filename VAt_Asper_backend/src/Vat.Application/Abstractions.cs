using Vat.Application.Security;
using Vat.Domain.Company;
using Vat.Domain.Purchasing;

namespace Vat.Application;

public interface ICompanyStore
{
    Task<Company?> FindCompany(Guid tenantId, Guid companyId, CancellationToken ct);
    Task<Branch?> FindBranch(Guid tenantId, Guid companyId, Guid branchId, CancellationToken ct);
    Task<IReadOnlyList<Branch>> ListBranches(Guid tenantId, Guid companyId, CancellationToken ct);
    void Add(Company company);
    void Add(Branch branch);
}

public interface IPurchaseStore
{
    Task<Purchase?> Find(Guid tenantId, Guid companyId, Guid purchaseId, CancellationToken ct);
    Task<IReadOnlyList<Purchase>> List(Guid tenantId, Guid companyId, Guid? before, int take, CancellationToken ct);
    void Add(Purchase purchase);
}

public interface IEventWriter
{
    void Append<T>(Guid tenantId, Guid companyId, Guid aggregateId, long version,
        string eventType, string correlationId, T payload);
}

public interface IAuditWriter
{
    void Append(Actor actor, Guid companyId, Guid resourceId, string action,
        string correlationId, long? version = null, string? reason = null);
}

public sealed record CommandOutcome(int StatusCode, object Body, string? Location = null);
public sealed record StoredResponse(int StatusCode, string Json, string? Location, bool Replayed = false);

public interface ICommandExecutor
{
    Task<StoredResponse> Execute(Actor actor, Guid companyId, string operation, string key, object request,
        Func<Task<CommandOutcome>> action, CancellationToken ct);
}

public interface ISalesInvoiceStore
{
    Task<Vat.Domain.Sales.SalesInvoice?> Find(Guid tenantId, Guid companyId, Guid invoiceId, CancellationToken ct);
    Task<IReadOnlyList<Vat.Domain.Sales.SalesInvoice>> List(Guid tenantId, Guid companyId, Guid? before, int take, CancellationToken ct);
    void Add(Vat.Domain.Sales.SalesInvoice invoice);
}
