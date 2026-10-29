using Microsoft.EntityFrameworkCore;
using Vat.Application.Contracts;
using Vat.Application.Security;
using Vat.Domain.Catalog;
using Vat.Infrastructure.Persistence;

namespace Vat.Services.MasterData;

public sealed class MasterDataQueries(VatDbContext db)
{
    public async Task<IReadOnlyList<ProductResponse>> Products(Actor actor, Guid companyId, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        return (await db.Set<Product>().AsNoTracking().Where(x => x.TenantId == actor.TenantId && x.CompanyId == companyId)
            .OrderBy(x => x.Reference).ToListAsync(ct)).Select(ProductResponse.From).ToArray();
    }
    public async Task<IReadOnlyList<SupplierResponse>> Suppliers(Actor actor, Guid companyId, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        return (await db.Set<Supplier>().AsNoTracking().Where(x => x.TenantId == actor.TenantId && x.CompanyId == companyId)
            .OrderBy(x => x.Reference).ToListAsync(ct)).Select(SupplierResponse.From).ToArray();
    }
}
