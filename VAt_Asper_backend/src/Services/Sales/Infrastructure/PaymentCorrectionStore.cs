using Microsoft.EntityFrameworkCore;
using Vat.Services.Sales;
using Vat.Services.Sales.Contracts;
using Vat.Domain.Sales;

namespace Vat.Services.Sales.Infrastructure;

public sealed class PaymentCorrectionStore(SalesDbContext db) : IPaymentCorrectionStore
{
    public Task<PaymentCorrection?> Find(Guid tenant,Guid company,Guid payment,Guid id,CancellationToken ct) => db.Set<PaymentCorrection>().SingleOrDefaultAsync(x=>x.TenantId==tenant && x.CompanyId==company && x.PaymentId==payment && x.Id==id,ct);
    public async Task<PaymentCorrectionsRead> Read(Guid tenant,Guid company,Guid customer,CancellationToken ct) => new(
        await db.Set<PaymentCorrection>().AsNoTracking().Where(x=>x.TenantId==tenant && x.CompanyId==company && x.CustomerId==customer).ToArrayAsync(ct),
        await db.Set<PaymentCashAdjustment>().AsNoTracking().Where(x=>x.TenantId==tenant && x.CompanyId==company && x.CustomerId==customer).ToArrayAsync(ct),
        await db.Set<PaymentAllocationRelease>().AsNoTracking().Where(x=>x.TenantId==tenant && x.CompanyId==company && x.CustomerId==customer).ToArrayAsync(ct));
    public void Add(PaymentCorrection note) => db.Add(note);
    public Guid Post(PaymentCorrection note,DateTimeOffset now)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Correction posting requires a transaction.");
        var cash=PaymentCashAdjustment.Create(note,now); db.Add(cash);
        foreach(var line in note.Releases) db.Add(PaymentAllocationRelease.Create(note,line));
        return cash.Id;
    }
}
