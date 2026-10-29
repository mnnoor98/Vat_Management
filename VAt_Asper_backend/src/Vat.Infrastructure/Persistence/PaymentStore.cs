using System.Buffers.Binary;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Vat.Application;
using Vat.Domain.Sales;
using Vat.Domain;

namespace Vat.Infrastructure.Persistence;

public sealed class PaymentStore(VatDbContext db) : IPaymentStore
{
    public async Task LockCustomer(Guid tenant, Guid company, Guid customer, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Payment locking requires a transaction.");
        var key = BinaryPrimitives.ReadInt64BigEndian(SHA256.HashData(Encoding.UTF8.GetBytes($"customer-payments:{tenant}:{company}:{customer}")));
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})", ct);
    }
    public Task<CustomerPayment?> Find(Guid tenant, Guid company, Guid id, CancellationToken ct) =>
        db.Set<CustomerPayment>().SingleOrDefaultAsync(x => x.TenantId == tenant && x.CompanyId == company && x.Id == id, ct);
    public async Task<IReadOnlyList<CustomerPayment>> List(Guid tenant, Guid company, Guid? customer, Guid? before, int take, CancellationToken ct) =>
        await db.Set<CustomerPayment>().AsNoTracking().Where(x => x.TenantId == tenant && x.CompanyId == company &&
            (customer == null || x.CustomerId == customer) && (before == null || x.Id.CompareTo(before.Value) < 0)).OrderByDescending(x => x.Id).Take(take).ToArrayAsync(ct);
    public async Task<IReadOnlyList<PaymentAllocation>> Allocations(Guid tenant, Guid company, Guid payment, CancellationToken ct) =>
        await db.Set<PaymentAllocation>().AsNoTracking().Where(x => x.TenantId == tenant && x.CompanyId == company && x.PaymentId == payment).OrderBy(x => x.Id).ToArrayAsync(ct);
    public async Task<decimal> InvoiceAllocated(Guid tenant, Guid company, Guid invoice, CancellationToken ct) =>
        await db.Set<PaymentAllocation>().Where(x => x.TenantId == tenant && x.CompanyId == company && x.InvoiceId == invoice).SumAsync(x => x.Amount, ct) -
        await db.Set<PaymentAllocationRelease>().Where(x => x.TenantId == tenant && x.CompanyId == company && x.InvoiceId == invoice).SumAsync(x => x.Amount, ct);
    public async Task<PaymentResponse?> Read(Guid tenant, Guid company, Guid id, CancellationToken ct)
    {
        await using var transaction = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct) : null;
        var p = await db.Set<CustomerPayment>().AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == tenant && x.CompanyId == company && x.Id == id, ct);
        if (p is null) return null;
        var lines = await Allocations(tenant, company, id, ct);
        var returned = -await db.Set<PaymentCashAdjustment>().Where(x=>x.TenantId==tenant && x.CompanyId==company && x.PaymentId==id).SumAsync(x=>x.Amount,ct);
        var released = await db.Set<PaymentAllocationRelease>().AsNoTracking().Where(x=>x.TenantId==tenant && x.CompanyId==company && x.PaymentId==id).ToArrayAsync(ct);
        var lastDate = await db.Set<PaymentCorrection>().Where(x=>x.TenantId==tenant && x.CompanyId==company && x.PaymentId==id && x.Status=="Posted").MaxAsync(x=>(DateOnly?)x.CorrectionDate,ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return PaymentResponse.From(p, lines, returned, released, lastDate);
    }
    public async Task<CustomerReceivables> Receivables(Guid tenant, Guid company, Guid customer, CancellationToken ct, DateOnly? asOf = null)
    {
        await using var transaction = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct) : null;
        var invoices = await db.SalesInvoices.AsNoTracking().Where(x => x.TenantId == tenant && x.CompanyId == company && x.CustomerId == customer && x.PostedVersion != null && (asOf == null || x.BusinessDate <= asOf)).OrderBy(x => x.Id).ToArrayAsync(ct);
        if (!await Reconciles(tenant, company, ct, customer)) throw new DomainException("payment_reconciliation_failed", "Payment records must reconcile.", 409);
        var postingStore = new SalesPostingStore(db);
        foreach (var invoice in invoices)
        {
            var read = await postingStore.Read(tenant, company, invoice.Id, ct);
            if (read is null || !SalesReconciler.Evaluate(read).IsReconciled)
                throw new DomainException("reconciliation_failed", "Customer invoices must reconcile.", 409);
        }
        var gross = await (from e in db.Set<SalesPostingEntry>() join i in db.SalesInvoices on new { e.TenantId, e.CompanyId, Id = e.InvoiceId } equals new { i.TenantId, i.CompanyId, i.Id }
            where i.TenantId == tenant && i.CompanyId == company && i.CustomerId == customer && (asOf == null || e.BusinessDate <= asOf)
            group e by e.InvoiceId into g select new { Id = g.Key, Amount = g.Sum(x => x.NetAmount + x.OutputTaxAmount) }).ToDictionaryAsync(x => x.Id, x => x.Amount, ct);
        var allocated = await db.Set<PaymentAllocation>().Where(x => x.TenantId == tenant && x.CompanyId == company && x.CustomerId == customer && (asOf == null || x.AllocationDate <= asOf))
            .GroupBy(x => x.InvoiceId).Select(g => new { Id = g.Key, Amount = g.Sum(x => x.Amount) }).ToDictionaryAsync(x => x.Id, x => x.Amount, ct);
        var released = await db.Set<PaymentAllocationRelease>().Where(x=>x.TenantId==tenant && x.CompanyId==company && x.CustomerId==customer && (asOf==null || x.BusinessDate<=asOf))
            .GroupBy(x=>x.InvoiceId).Select(g=>new { Id=g.Key, Amount=g.Sum(x=>x.Amount) }).ToArrayAsync(ct);
        foreach(var row in released) allocated[row.Id] = allocated.GetValueOrDefault(row.Id) - row.Amount;
        var returned = -await db.Set<PaymentCashAdjustment>().Where(x=>x.TenantId==tenant && x.CompanyId==company && x.CustomerId==customer && (asOf==null || x.BusinessDate<=asOf)).SumAsync(x=>x.Amount,ct);
        var collected = await db.Set<CustomerPayment>().Where(x => x.TenantId == tenant && x.CompanyId == company && x.CustomerId == customer && x.PostedAt != null && (asOf == null || x.PaymentDate <= asOf)).SumAsync(x => x.Amount, ct);
        var lines = invoices.Select(x => new InvoiceReceivable(x.Id, x.ExternalReference, gross.GetValueOrDefault(x.Id), allocated.GetValueOrDefault(x.Id),
            gross.GetValueOrDefault(x.Id) - allocated.GetValueOrDefault(x.Id))).ToArray();
        if (transaction is not null) await transaction.CommitAsync(ct);
        return new(customer, "BDT", lines.Sum(x => x.GrossAmount), collected, allocated.Values.Sum(), collected - returned - allocated.Values.Sum(), lines.Sum(x => x.GrossAmount) - collected + returned, lines, returned, collected - returned);
    }
    public async Task<bool> Reconciles(Guid tenant, Guid company, CancellationToken ct, Guid? customer = null)
    {
        var payments = await db.Set<CustomerPayment>().AsNoTracking().Where(x => x.TenantId == tenant && x.CompanyId == company && (customer == null || x.CustomerId == customer)).ToArrayAsync(ct);
        var lines = await db.Set<PaymentAllocation>().AsNoTracking().Where(x => x.TenantId == tenant && x.CompanyId == company && (customer == null || x.CustomerId == customer)).ToArrayAsync(ct);
        var invoices = await db.SalesInvoices.AsNoTracking().Where(x => x.TenantId == tenant && x.CompanyId == company && (customer == null || x.CustomerId == customer)).Select(x => new { x.Id, x.CustomerId, x.BusinessDate, x.PostedVersion }).ToDictionaryAsync(x => x.Id, ct);
        var notes = await db.Set<PaymentCorrection>().AsNoTracking().Where(x=>x.TenantId==tenant && x.CompanyId==company && (customer==null || x.CustomerId==customer)).ToArrayAsync(ct);
        var cash = await db.Set<PaymentCashAdjustment>().AsNoTracking().Where(x=>x.TenantId==tenant && x.CompanyId==company && (customer==null || x.CustomerId==customer)).ToArrayAsync(ct);
        var releases = await db.Set<PaymentAllocationRelease>().AsNoTracking().Where(x=>x.TenantId==tenant && x.CompanyId==company && (customer==null || x.CustomerId==customer)).ToArrayAsync(ct);
        foreach (var p in payments)
            if (!PaymentCorrectionRules.Reconciles(p,lines.Where(x=>x.PaymentId==p.Id).ToArray(),notes.Where(x=>x.PaymentId==p.Id).ToArray(),
                cash.Where(x=>x.PaymentId==p.Id).ToArray(),releases.Where(x=>x.PaymentId==p.Id).ToArray())) return false;
        return lines.All(x=>payments.Any(p=>p.Id==x.PaymentId && p.CustomerId==x.CustomerId && p.PostedAt!=null && p.PaymentDate<=x.AllocationDate && x.PaymentVersion>=2 && x.PaymentVersion<=p.Version) && x.Amount>0 &&
            invoices.TryGetValue(x.InvoiceId,out var i) && i.CustomerId==x.CustomerId && i.PostedVersion!=null && i.BusinessDate<=x.AllocationDate) &&
            lines.GroupBy(x=>new{x.PaymentId,x.PaymentVersion,x.InvoiceId}).All(g=>g.Count()==1) &&
            notes.All(n=>payments.Any(p=>p.Id==n.PaymentId && p.CustomerId==n.CustomerId)) &&
            releases.All(r=>lines.Any(a=>a.Id==r.AllocationId && a.InvoiceId==r.InvoiceId && a.PaymentId==r.PaymentId && a.CustomerId==r.CustomerId)) &&
            cash.All(c=>notes.Any(n=>n.Id==c.CorrectionId && n.PaymentId==c.PaymentId && n.CustomerId==c.CustomerId));
    }
    public void Add(CustomerPayment payment) => db.Add(payment);
    public void Add(PaymentAllocation allocation) => db.Add(allocation);
}
