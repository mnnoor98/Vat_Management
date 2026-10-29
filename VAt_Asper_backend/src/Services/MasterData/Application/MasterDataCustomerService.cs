using Vat.Application.Security;
using Vat.Domain;
using Vat.Domain.Sales;
using Vat.Services.MasterData.Contracts;

namespace Vat.Services.MasterData;

public sealed class MasterDataCustomerService(IMasterDataCustomerStore store, IAuditWriter audit, TimeProvider clock)
{
    public async Task<CustomerResponse> Get(Actor actor, Guid company, Guid id, CancellationToken ct) =>
        CustomerResponse.From(await Find(actor, company, id, ct));

    public async Task<IReadOnlyList<CustomerResponse>> List(Actor actor, Guid company, Guid? before, int take, CancellationToken ct)
    {
        actor.RequireCompany(company);
        if (take is < 1 or > 100) throw new DomainException("validation_failed", "take must be between 1 and 100.");
        return (await store.ListCustomers(actor.TenantId, company, before, take, ct)).Select(CustomerResponse.From).ToArray();
    }

    public Task<CommandOutcome> Create(Actor actor, Guid company, CreateCustomerRequest request, string correlation, CancellationToken ct)
    {
        actor.RequireCompany(company);
        var customer = Customer.Create(actor.TenantId, company, request.Code, request.Name, request.Contact,
            request.Address, request.TaxRegistrationReference, clock.GetUtcNow());
        store.AddCustomer(customer);
        audit.Append(actor, company, customer.Id, "CustomerCreated", correlation, customer.Version);
        return Task.FromResult(new CommandOutcome(201, CustomerResponse.From(customer), $"/api/v1/companies/{company}/customers/{customer.Id}"));
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
        return await store.FindCustomer(actor.TenantId, company, id, ct)
            ?? throw new DomainException("not_found", "Customer not found.", 404);
    }
}

