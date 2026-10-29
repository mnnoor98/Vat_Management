using Microsoft.EntityFrameworkCore;
using Vat.Domain.Company;
using Vat.Services.Company;

namespace Vat.Services.Company.Infrastructure;

public sealed class CompanyStore(CompanyDbContext db) : ICompanyStore
{
    public Task<Vat.Domain.Company.Company?> FindCompany(Guid tenantId, Guid companyId, CancellationToken ct) => db.Companies
        .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == companyId, ct);

    public Task<Branch?> FindBranch(Guid tenantId, Guid companyId, Guid branchId, CancellationToken ct) => db.Branches
        .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.CompanyId == companyId && x.Id == branchId, ct);

    public async Task<IReadOnlyList<Branch>> ListBranches(Guid tenantId, Guid companyId, CancellationToken ct) =>
        await db.Branches.Where(x => x.TenantId == tenantId && x.CompanyId == companyId)
            .OrderBy(x => x.Name).ToArrayAsync(ct);

    public void Add(Vat.Domain.Company.Company company) => db.Companies.Add(company);

    public void Add(Branch branch) => db.Branches.Add(branch);
}
