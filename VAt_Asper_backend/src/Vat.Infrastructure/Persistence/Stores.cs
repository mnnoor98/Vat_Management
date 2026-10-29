using Microsoft.EntityFrameworkCore;
using Vat.Application;
using Vat.Domain.Company;
using Vat.Domain.Purchasing;

namespace Vat.Infrastructure.Persistence;

public sealed class CompanyStore(VatDbContext db) : ICompanyStore
{
    public Task<Company?> FindCompany(Guid tenantId, Guid companyId, CancellationToken ct) => db.Companies
        .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == companyId, ct);

    public Task<Branch?> FindBranch(Guid tenantId, Guid companyId, Guid branchId, CancellationToken ct) => db.Branches
        .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.CompanyId == companyId && x.Id == branchId, ct);

    public async Task<IReadOnlyList<Branch>> ListBranches(Guid tenantId, Guid companyId, CancellationToken ct) =>
        await db.Branches.AsNoTracking().Where(x => x.TenantId == tenantId && x.CompanyId == companyId)
            .OrderBy(x => x.Name).Take(100).ToArrayAsync(ct);

    public void Add(Company company) => db.Companies.Add(company);
    public void Add(Branch branch) => db.Branches.Add(branch);
}

public sealed class PurchaseStore(VatDbContext db) : IPurchaseStore
{
    public Task<Purchase?> Find(Guid tenantId, Guid companyId, Guid purchaseId, CancellationToken ct) =>
        db.Purchases.Include(x => x.Lines).SingleOrDefaultAsync(x =>
            x.TenantId == tenantId && x.CompanyId == companyId && x.Id == purchaseId, ct);

    public async Task<IReadOnlyList<Purchase>> List(Guid tenantId, Guid companyId, Guid? before, int take, CancellationToken ct)
    {
        var query = db.Purchases.AsNoTracking().Where(x => x.TenantId == tenantId && x.CompanyId == companyId);
        if (before is not null) query = query.Where(x => x.Id.CompareTo(before.Value) < 0);
        return await query.OrderByDescending(x => x.Id).Take(take).Include(x => x.Lines).ToArrayAsync(ct);
    }

    public void Add(Purchase purchase) => db.Purchases.Add(purchase);
}
