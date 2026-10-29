using Microsoft.EntityFrameworkCore;
using Vat.Application;
using Vat.Domain.Sales;

namespace Vat.Infrastructure.Persistence;

public sealed class SalesInvoiceStore(VatDbContext db) : ISalesInvoiceStore
{
    public Task<SalesInvoice?> Find(Guid tenantId, Guid companyId, Guid invoiceId, CancellationToken ct) =>
        db.SalesInvoices.Include(x => x.Lines).SingleOrDefaultAsync(x =>
            x.TenantId == tenantId && x.CompanyId == companyId && x.Id == invoiceId, ct);

    public async Task<IReadOnlyList<SalesInvoice>> List(Guid tenantId, Guid companyId, Guid? before, int take, CancellationToken ct)
    {
        var query = db.SalesInvoices.AsNoTracking().Where(x => x.TenantId == tenantId && x.CompanyId == companyId);
        if (before is not null) query = query.Where(x => x.Id.CompareTo(before.Value) < 0);
        return await query.OrderByDescending(x => x.Id).Take(take).Include(x => x.Lines).ToArrayAsync(ct);
    }

    public void Add(SalesInvoice invoice) => db.SalesInvoices.Add(invoice);
}
