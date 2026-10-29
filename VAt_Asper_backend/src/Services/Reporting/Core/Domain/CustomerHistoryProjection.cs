using Vat.Domain;

namespace Vat.Services.Reporting.Domain;

public sealed record CustomerSnapshotStatementLine(Guid DocumentId, Guid? InvoiceId, DateOnly BusinessDate, string Type,
    string DocumentNumber, decimal NetAmount, decimal OutputTaxAmount, decimal BalanceChange, decimal RunningBalance);
public sealed record CustomerSnapshotStatement(Guid CustomerId, string Currency, DateOnly From, DateOnly Until,
    DateTimeOffset CapturedAt, decimal OpeningBalance, decimal TotalDebit, decimal TotalCredit, decimal ClosingBalance,
    IReadOnlyList<CustomerSnapshotStatementLine> Lines);
public sealed record CustomerSnapshotAgeingInvoice(Guid InvoiceId, string Reference, DateOnly BusinessDate, DateOnly? DueDate,
    int? DaysOverdue, string Bucket, decimal GrossAmount, decimal AllocatedAmount, decimal OutstandingAmount);
public sealed record CustomerSnapshotAgeingBuckets(decimal NotDue, decimal Overdue1To30, decimal Overdue31To60,
    decimal Overdue61To90, decimal OverdueOver90, decimal UnknownDueDate);
public sealed record CustomerSnapshotAgeing(Guid CustomerId, string Currency, DateOnly AsOf, DateTimeOffset CapturedAt,
    CustomerSnapshotAgeingBuckets Buckets, decimal OutstandingDebitAmount, decimal InvoiceCreditAmount, decimal CollectedAmount,
    decimal AllocatedAmount, decimal UnallocatedAmount, decimal Balance, IReadOnlyList<CustomerSnapshotAgeingInvoice> Invoices,
    decimal ReturnedAmount, decimal NetCollectedAmount);

// Pure, independent projections over verified signed journals. Never recalculates tax.
public static class CustomerHistoryProjection
{
    public static CustomerSnapshotStatement Statement(VerifiedCustomerHistory history, DateOnly from, DateOnly until)
    {
        if (from == default || until <= from || until.DayNumber - from.DayNumber > 366)
            throw new DomainException("validation_failed", "Statement requires a [from, until) interval of at most 366 days.");
        var d = history.Data;
        var movements = d.Documents.Select(x => new CustomerSnapshotStatementLine(x.Id, x.InvoiceId, x.BusinessDate, x.Kind,
                x.Number, x.NetAmount, x.TaxAmount, x.NetAmount + x.TaxAmount, 0))
            .Concat(d.Payments.Select(x => new CustomerSnapshotStatementLine(x.Id, null, x.BusinessDate, "Payment", x.Reference, 0, 0, -x.Amount, 0)))
            .Concat(d.Corrections.Select(x => new CustomerSnapshotStatementLine(x.Id, null, x.BusinessDate, x.Kind, x.Number, 0, 0, -x.Amount, 0))).ToArray();
        var opening = movements.Where(x => x.BusinessDate < from).Sum(x => x.BalanceChange);
        var window = movements.Where(x => from <= x.BusinessDate && x.BusinessDate < until)
            .OrderBy(x => x.BusinessDate).ThenBy(x => x.Type, StringComparer.Ordinal).ThenBy(x => x.DocumentId).ToArray();
        if (window.Length > 1000) throw new DomainException("report_too_large", "Statement exceeds 1000 documents. Use a smaller date interval.");
        var running = opening;
        var lines = window.Select(x => { running += x.BalanceChange; return x with { RunningBalance = running }; }).ToArray();
        return new(history.Manifest.CustomerId, d.Currency, from, until, history.Manifest.CapturedAt, opening,
            lines.Sum(x => Math.Max(x.BalanceChange, 0)), lines.Sum(x => Math.Max(-x.BalanceChange, 0)), running, lines);
    }
    public static CustomerSnapshotAgeing Ageing(VerifiedCustomerHistory history, DateOnly asOf)
    {
        if (asOf == default) throw new DomainException("validation_failed", "An asOf business date is required.");
        var d = history.Data;
        var allocations = d.Allocations.Where(x => x.BusinessDate <= asOf).GroupBy(x => x.InvoiceId).ToDictionary(x => x.Key, x => x.Sum(a => a.Amount));
        var originals = d.Allocations.ToDictionary(x => x.Id);
        foreach (var release in d.Releases.Where(x => x.BusinessDate <= asOf))
        {
            var invoice = originals[release.AllocationId].InvoiceId;
            allocations[invoice] = allocations.GetValueOrDefault(invoice) - release.Amount;
        }
        var invoices = new List<CustomerSnapshotAgeingInvoice>();
        foreach (var i in d.Invoices.Where(x => x.BusinessDate <= asOf))
        {
            var gross = d.Documents.Where(x => x.InvoiceId == i.Id && x.BusinessDate <= asOf).Sum(x => x.NetAmount + x.TaxAmount);
            var allocated = allocations.GetValueOrDefault(i.Id); var outstanding = gross - allocated;
            if (outstanding == 0) continue;
            int? days = i.DueDate is DateOnly due ? Math.Max(0, asOf.DayNumber - due.DayNumber) : null;
            var bucket = outstanding < 0 ? "Credit" : days is null ? "UnknownDueDate" : days == 0 ? "NotDue" :
                days <= 30 ? "Overdue1To30" : days <= 60 ? "Overdue31To60" : days <= 90 ? "Overdue61To90" : "OverdueOver90";
            invoices.Add(new(i.Id, i.Reference, i.BusinessDate, i.DueDate, days, bucket, gross, allocated, outstanding));
        }
        decimal Bucket(string name) => invoices.Where(x => x.Bucket == name).Sum(x => x.OutstandingAmount);
        var buckets = new CustomerSnapshotAgeingBuckets(Bucket("NotDue"), Bucket("Overdue1To30"), Bucket("Overdue31To60"),
            Bucket("Overdue61To90"), Bucket("OverdueOver90"), Bucket("UnknownDueDate"));
        var collected = d.Payments.Where(x => x.BusinessDate <= asOf).Sum(x => x.Amount);
        var returned = -d.Corrections.Where(x => x.BusinessDate <= asOf).Sum(x => x.Amount);
        var applied = allocations.Values.Sum(); var unallocated = collected - returned - applied;
        var debit = invoices.Sum(x => Math.Max(x.OutstandingAmount, 0)); var credit = invoices.Sum(x => Math.Max(-x.OutstandingAmount, 0));
        return new(history.Manifest.CustomerId, d.Currency, asOf, history.Manifest.CapturedAt, buckets, debit, credit, collected,
            applied, unallocated, debit - credit - unallocated,
            invoices.OrderBy(x => x.DueDate is null).ThenBy(x => x.DueDate).ThenBy(x => x.InvoiceId).ToArray(), returned, collected - returned);
    }
}
