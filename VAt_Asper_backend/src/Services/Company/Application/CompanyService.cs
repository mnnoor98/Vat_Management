using Vat.Application.Security;
using Vat.Domain;
using Vat.Domain.Company;
using Vat.Services.Company.Contracts;

namespace Vat.Services.Company;

public sealed class CompanyService(ICompanyStore store, IAuditWriter audit, TimeProvider clock)
{
    public async Task<CompanyResponse> Get(Actor actor, Guid companyId, CancellationToken ct)
    {
        var company = await RequireCompany(actor, companyId, ct);
        return new(company.Id, company.Name, company.RegistrationReference);
    }

    public Task<CommandOutcome> Create(Actor actor, CreateCompanyRequest request, string correlationId)
    {
        // An issuer must pre-grant the company UUID. A client-supplied ID is never a grant.
        actor.RequireCompany(request.Id);
        var company = Vat.Domain.Company.Company.Create(actor.TenantId, request.Id, request.Name, request.RegistrationReference, clock.GetUtcNow());
        store.Add(company);
        audit.Append(actor, company.Id, company.Id, "CompanyCreated", correlationId);
        return Task.FromResult(new CommandOutcome(201,
            new CompanyResponse(company.Id, company.Name, company.RegistrationReference), $"/api/v1/companies/{company.Id}"));
    }

    public async Task<CommandOutcome> CreateBranch(Actor actor, Guid companyId, CreateBranchRequest request,
        string correlationId, CancellationToken ct)
    {
        await RequireCompany(actor, companyId, ct);
        var branch = Branch.Create(actor.TenantId, companyId, request.Name, clock.GetUtcNow());
        store.Add(branch);
        audit.Append(actor, companyId, branch.Id, "BranchCreated", correlationId);
        return new(201, new BranchResponse(branch.Id, companyId, branch.Name),
            $"/api/v1/companies/{companyId}/branches/{branch.Id}");
    }

    public async Task<BranchResponse> GetBranch(Actor actor, Guid companyId, Guid branchId, CancellationToken ct)
    {
        await RequireCompany(actor, companyId, ct);
        var branch = await store.FindBranch(actor.TenantId, companyId, branchId, ct)
            ?? throw new DomainException("not_found", "Branch not found.", 404);
        return new(branch.Id, companyId, branch.Name);
    }

    public async Task<IReadOnlyList<BranchResponse>> Branches(Actor actor, Guid companyId, CancellationToken ct)
    {
        await RequireCompany(actor, companyId, ct);
        return (await store.ListBranches(actor.TenantId, companyId, ct))
            .Select(x => new BranchResponse(x.Id, x.CompanyId, x.Name)).ToArray();
    }

    private async Task<Vat.Domain.Company.Company> RequireCompany(Actor actor, Guid companyId, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        return await store.FindCompany(actor.TenantId, companyId, ct)
            ?? throw new DomainException("not_found", "Company not found.", 404);
    }
}
