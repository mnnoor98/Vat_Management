using System.Data;
using Microsoft.EntityFrameworkCore;
using Vat.Application;
using Vat.Domain;
using Vat.Domain.Sales;

namespace Vat.Infrastructure.Persistence;

public sealed class CustomerReportingStore(VatDbContext db, TimeProvider clock) : ICustomerReportingStore
{
    private sealed record History(SalesPostingRead[] Sales, CustomerPayment[] Payments, PaymentAllocation[] Allocations, PaymentCorrectionsRead Corrections);
    private async Task<History> Read(Guid tenant, Guid company, Guid customer, CancellationToken ct)
    {
        var paymentStore = new PaymentStore(db);
        if (!await paymentStore.Reconciles(tenant, company, ct, customer))
            throw new DomainException("payment_reconciliation_failed", "Customer payment records must reconcile before reporting.", 409);
        var ids = await db.SalesInvoices.AsNoTracking().Where(x => x.TenantId == tenant && x.CompanyId == company &&
            x.CustomerId == customer && x.PostedVersion != null).OrderBy(x => x.Id).Select(x => x.Id).ToArrayAsync(ct);
        var postingStore = new SalesPostingStore(db);
        var sales = new List<SalesPostingRead>();
        foreach (var id in ids)
        {
            var read = await postingStore.Read(tenant, company, id, ct);
            if (read is null || !SalesReconciler.Evaluate(read).IsReconciled)
                throw new DomainException("reconciliation_failed", "Customer sales history must reconcile before reporting.", 409);
            sales.Add(read);
        }
        var payments = await db.Set<CustomerPayment>().AsNoTracking().Where(x => x.TenantId == tenant && x.CompanyId == company &&
            x.CustomerId == customer && x.PostedAt != null).ToArrayAsync(ct);
        var allocations = await db.Set<PaymentAllocation>().AsNoTracking().Where(x => x.TenantId == tenant && x.CompanyId == company &&
            x.CustomerId == customer).ToArrayAsync(ct);
        return new(sales.ToArray(), payments, allocations, await new PaymentCorrectionStore(db).Read(tenant,company,customer,ct));
    }
    public async Task<CustomerStatement> Statement(Guid tenant, Guid company, Guid customer, DateOnly from, DateOnly until, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var history = await Read(tenant, company, customer, ct);
        var rows = new List<CustomerStatementLine>();
        foreach (var sale in history.Sales)
        {
            foreach (var group in sale.Entries.GroupBy(x => x.AdjustmentId ?? x.InvoiceId))
            {
                var first = group.First();
                var credit = (sale.CreditNotes ?? []).SingleOrDefault(x => x.Id == group.Key);
                var debit = (sale.DebitNotes ?? []).SingleOrDefault(x => x.Id == group.Key);
                var kind = credit is not null ? "CreditNote" : debit is not null ? "DebitNote" : "Invoice";
                var number = credit?.DocumentNumber ?? debit?.DocumentNumber ?? sale.Fulfillment!.DocumentNumber!;
                rows.Add(new(group.Key, sale.Invoice.Id, first.BusinessDate, kind, number,
                    group.Sum(x => x.NetAmount), group.Sum(x => x.OutputTaxAmount), group.Sum(x => x.NetAmount + x.OutputTaxAmount), 0));
            }
        }
        rows.AddRange(history.Payments.Select(p => new CustomerStatementLine(p.Id, null, p.PaymentDate, "Payment", p.Reference, 0, 0, -p.Amount, 0)));
        rows.AddRange(history.Corrections.Cash.Select(c =>
        {
            var note=history.Corrections.Notes.Single(x=>x.Id==c.CorrectionId);
            return new CustomerStatementLine(note.Id,null,c.BusinessDate,note.Mode=="Refund"?"PaymentRefund":"ReceiptReversal",note.DocumentNumber!,0,0,-c.Amount,0);
        }));
        var opening = rows.Where(x => x.BusinessDate < from).Sum(x => x.BalanceChange);
        var window = rows.Where(x => from <= x.BusinessDate && x.BusinessDate < until)
            .OrderBy(x => x.BusinessDate).ThenBy(x => x.Type, StringComparer.Ordinal).ThenBy(x => x.DocumentId).ToArray();
        if (window.Length > 1000) throw new DomainException("report_too_large", "Statement exceeds 1000 documents. Use a smaller date interval.");
        var running = opening;
        var lines = window.Select(x => { running += x.BalanceChange; return x with { RunningBalance = running }; }).ToArray();
        await transaction.CommitAsync(ct);
        return new(customer, "BDT", from, until, clock.GetUtcNow(), opening,
            lines.Sum(x => Math.Max(x.BalanceChange, 0)), lines.Sum(x => Math.Max(-x.BalanceChange, 0)), running, lines);
    }
    public async Task<CustomerAgeing> Ageing(Guid tenant, Guid company, Guid customer, DateOnly asOf, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var history = await Read(tenant, company, customer, ct);
        var allocations = history.Allocations.Where(x => x.AllocationDate <= asOf).GroupBy(x => x.InvoiceId).ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));
        foreach(var release in history.Corrections.Releases.Where(x=>x.BusinessDate<=asOf).GroupBy(x=>x.InvoiceId))
            allocations[release.Key] = allocations.GetValueOrDefault(release.Key) - release.Sum(x=>x.Amount);
        var invoices = new List<AgeingInvoice>();
        foreach (var sale in history.Sales.Where(x => x.Invoice.BusinessDate <= asOf))
        {
            var invoice = sale.Invoice;
            var gross = sale.Entries.Where(x => x.BusinessDate <= asOf).Sum(x => x.NetAmount + x.OutputTaxAmount);
            var allocated = allocations.GetValueOrDefault(invoice.Id);
            var outstanding = gross - allocated;
            if (outstanding == 0) continue;
            int? days = invoice.DueDate is DateOnly due ? Math.Max(0, asOf.DayNumber - due.DayNumber) : null;
            var bucket = outstanding < 0 ? "Credit" : days is null ? "UnknownDueDate" : days == 0 ? "NotDue" :
                days <= 30 ? "Overdue1To30" : days <= 60 ? "Overdue31To60" : days <= 90 ? "Overdue61To90" : "OverdueOver90";
            invoices.Add(new(invoice.Id, invoice.ExternalReference, invoice.BusinessDate, invoice.DueDate, days, bucket, gross, allocated, outstanding));
        }
        decimal Bucket(string name) => invoices.Where(x => x.Bucket == name).Sum(x => x.OutstandingAmount);
        var buckets = new AgeingBuckets(Bucket("NotDue"), Bucket("Overdue1To30"), Bucket("Overdue31To60"), Bucket("Overdue61To90"), Bucket("OverdueOver90"), Bucket("UnknownDueDate"));
        var collected = history.Payments.Where(x => x.PaymentDate <= asOf).Sum(x => x.Amount);
        var totalAllocated = allocations.Values.Sum();
        var returned = -history.Corrections.Cash.Where(x=>x.BusinessDate<=asOf).Sum(x=>x.Amount);
        var unallocated = collected - returned - totalAllocated;
        var debit = invoices.Sum(x => Math.Max(x.OutstandingAmount, 0));
        var credit = invoices.Sum(x => Math.Max(-x.OutstandingAmount, 0));
        await transaction.CommitAsync(ct);
        return new(customer, "BDT", asOf, clock.GetUtcNow(), buckets, debit, credit, collected, totalAllocated, unallocated,
            debit - credit - unallocated, invoices.OrderBy(x => x.DueDate is null).ThenBy(x => x.DueDate).ThenBy(x => x.InvoiceId).ToArray(), returned, collected - returned);
    }
}
