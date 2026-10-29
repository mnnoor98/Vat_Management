using Microsoft.EntityFrameworkCore;
using Vat.Application;
using Vat.Domain.Sales;

namespace Vat.Infrastructure.Persistence;

public sealed class SalesDebitStore(VatDbContext db) : ISalesDebitStore
{
    public Task<SalesDebitNote?> Find(Guid tenantId, Guid companyId, Guid invoiceId, Guid id, CancellationToken ct) =>
        db.Set<SalesDebitNote>().SingleOrDefaultAsync(x => x.TenantId == tenantId && x.CompanyId == companyId && x.InvoiceId == invoiceId && x.Id == id, ct);
    public async Task<IReadOnlyList<SalesDebitNote>> List(Guid tenantId, Guid companyId, Guid invoiceId, Guid? before, int take, CancellationToken ct)
    {
        var query = db.Set<SalesDebitNote>().AsNoTracking().Where(x => x.TenantId == tenantId && x.CompanyId == companyId && x.InvoiceId == invoiceId);
        if (before is not null) query = query.Where(x => x.Id.CompareTo(before.Value) < 0);
        return await query.OrderByDescending(x => x.Id).Take(take).ToArrayAsync(ct);
    }
    public void Add(SalesDebitNote note) => db.Add(note);
    public void Post(SalesDebitNote note, SalesPostingRead original, DateTimeOffset now)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Debit posting requires a transaction.");
        foreach (var line in note.Lines)
            db.Add(SalesPostingEntry.Debit(original.Entries.Single(x => x.IsOriginal && x.Id == line.OriginalEntryId), note, line, now));
    }
}
