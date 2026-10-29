using Vat.Application.Contracts;
using Vat.Application.Security;
using Vat.Domain;
using Vat.Domain.Sales;

namespace Vat.Application;

public interface ICustomerStore
{
    Task<Customer?> Find(Guid tenant, Guid company, Guid id, CancellationToken ct);
    Task<IReadOnlyList<Customer>> List(Guid tenant, Guid company, Guid? before, int take, CancellationToken ct);
    void Add(Customer customer);
}
public sealed record CreateCustomerRequest(string Code, string Name, string? Contact = null, string? Address = null,
    string? TaxRegistrationReference = null);
public sealed record UpdateCustomerRequest(long ExpectedVersion, string Name, string? Contact, string? Address,
    string? TaxRegistrationReference, bool IsActive);
public sealed record CustomerResponse(Guid Id, string Code, string Name, string Contact, string Address,
    string TaxRegistrationReference, bool IsActive, long Version, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)
{
    public static CustomerResponse From(Customer c) => new(c.Id, c.Code, c.Name, c.Contact, c.Address,
        c.TaxRegistrationReference, c.IsActive, c.Version, c.CreatedAt, c.UpdatedAt);
}
public sealed class CustomerService(ICustomerStore store, ICompanyStore companies, IAuditWriter audit, TimeProvider clock)
{
    public async Task<CustomerResponse> Get(Actor actor, Guid company, Guid id, CancellationToken ct) => CustomerResponse.From(await Find(actor, company, id, ct));
    public async Task<IReadOnlyList<CustomerResponse>> List(Actor actor, Guid company, Guid? before, int take, CancellationToken ct)
    {
        actor.RequireCompany(company);
        if (take is < 1 or > 100) throw new DomainException("validation_failed", "take must be between 1 and 100.");
        return (await store.List(actor.TenantId, company, before, take, ct)).Select(CustomerResponse.From).ToArray();
    }
    public async Task<CommandOutcome> Create(Actor actor, Guid company, CreateCustomerRequest request, string correlation, CancellationToken ct)
    {
        actor.RequireCompany(company);
        if (await companies.FindCompany(actor.TenantId, company, ct) is null) throw new DomainException("not_found", "Company not found.", 404);
        var customer = Customer.Create(actor.TenantId, company, request.Code, request.Name, request.Contact,
            request.Address, request.TaxRegistrationReference, clock.GetUtcNow());
        store.Add(customer);
        audit.Append(actor, company, customer.Id, "CustomerCreated", correlation, customer.Version);
        return new(201, CustomerResponse.From(customer), $"/api/v1/companies/{company}/customers/{customer.Id}");
    }
    public async Task<CommandOutcome> Update(Actor actor, Guid company, Guid id, UpdateCustomerRequest request, string correlation, CancellationToken ct)
    {
        var customer = await Find(actor, company, id, ct);
        customer.Update(request.ExpectedVersion, request.Name, request.Contact, request.Address, request.TaxRegistrationReference,
            request.IsActive, clock.GetUtcNow());
        audit.Append(actor, company, id, "CustomerUpdated", correlation, customer.Version);
        return new(200, CustomerResponse.From(customer));
    }
    private async Task<Customer> Find(Actor actor, Guid company, Guid id, CancellationToken ct)
    {
        actor.RequireCompany(company);
        return await store.Find(actor.TenantId, company, id, ct) ?? throw new DomainException("not_found", "Customer not found.", 404);
    }
}
