using Microsoft.EntityFrameworkCore;
using Vat.Application;
using Vat.Domain.Sales;

namespace Vat.Infrastructure.Persistence;

public sealed class CustomerStore(VatDbContext db) : ICustomerStore
{
    public Task<Customer?> Find(Guid tenant, Guid company, Guid id, CancellationToken ct) =>
        db.Set<Customer>().SingleOrDefaultAsync(x => x.TenantId == tenant && x.CompanyId == company && x.Id == id, ct);
    public async Task<IReadOnlyList<Customer>> List(Guid tenant, Guid company, Guid? before, int take, CancellationToken ct) =>
        await db.Set<Customer>().AsNoTracking().Where(x => x.TenantId == tenant && x.CompanyId == company &&
            (before == null || x.Id.CompareTo(before.Value) < 0)).OrderByDescending(x => x.Id).Take(take).ToArrayAsync(ct);
    public void Add(Customer customer) => db.Add(customer);
}
