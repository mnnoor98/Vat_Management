using Vat.Application.Security;
using Vat.Domain;

namespace Vat.Application;

public sealed record CustomerStatementLine(Guid DocumentId, Guid? InvoiceId, DateOnly BusinessDate, string Type,
    string DocumentNumber, decimal NetAmount, decimal OutputTaxAmount, decimal BalanceChange, decimal RunningBalance);
public sealed record CustomerStatement(Guid CustomerId, string Currency, DateOnly From, DateOnly Until,
    DateTimeOffset CapturedAt, decimal OpeningBalance, decimal TotalDebit, decimal TotalCredit, decimal ClosingBalance,
    IReadOnlyList<CustomerStatementLine> Lines);
public sealed record AgeingInvoice(Guid InvoiceId, string Reference, DateOnly BusinessDate, DateOnly? DueDate,
    int? DaysOverdue, string Bucket, decimal GrossAmount, decimal AllocatedAmount, decimal OutstandingAmount);
public sealed record AgeingBuckets(decimal NotDue, decimal Overdue1To30, decimal Overdue31To60,
    decimal Overdue61To90, decimal OverdueOver90, decimal UnknownDueDate);
public sealed record CustomerAgeing(Guid CustomerId, string Currency, DateOnly AsOf, DateTimeOffset CapturedAt,
    AgeingBuckets Buckets, decimal OutstandingDebitAmount, decimal InvoiceCreditAmount, decimal CollectedAmount,
    decimal AllocatedAmount, decimal UnallocatedAmount, decimal Balance, IReadOnlyList<AgeingInvoice> Invoices, decimal ReturnedAmount = 0, decimal NetCollectedAmount = 0);
public interface ICustomerReportingStore
{
    Task<CustomerStatement> Statement(Guid tenant, Guid company, Guid customer, DateOnly from, DateOnly until, CancellationToken ct);
    Task<CustomerAgeing> Ageing(Guid tenant, Guid company, Guid customer, DateOnly asOf, CancellationToken ct);
}
public sealed class CustomerReportingService(ICustomerReportingStore store, ICustomerStore customers)
{
    public async Task<CustomerStatement> Statement(Actor actor, Guid company, Guid customer, DateOnly from, DateOnly until, CancellationToken ct)
    {
        await RequireCustomer(actor, company, customer, ct);
        if (from == default || until <= from || until.DayNumber - from.DayNumber > 366)
            throw new DomainException("validation_failed", "Statement requires a non-empty [from, until) interval of at most 366 days.");
        return await store.Statement(actor.TenantId, company, customer, from, until, ct);
    }
    public async Task<CustomerAgeing> Ageing(Actor actor, Guid company, Guid customer, DateOnly asOf, CancellationToken ct)
    {
        await RequireCustomer(actor, company, customer, ct);
        if (asOf == default) throw new DomainException("validation_failed", "An asOf business date is required.");
        return await store.Ageing(actor.TenantId, company, customer, asOf, ct);
    }
    private async Task RequireCustomer(Actor actor, Guid company, Guid customer, CancellationToken ct)
    {
        actor.RequireCompany(company);
        if (await customers.Find(actor.TenantId, company, customer, ct) is null)
            throw new DomainException("not_found", "Customer not found.", 404);
    }
}
