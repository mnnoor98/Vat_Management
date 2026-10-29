using Vat.Application.Security;
using Vat.Domain;
using Vat.Domain.Sales;

using Vat.Services.Sales.Contracts;

namespace Vat.Services.Sales;

public sealed record RequestReceiptReversal(long ExpectedVersion, DateOnly CorrectionDate, string Reason, string EvidenceReference);
public sealed record RequestCustomerRefund(long ExpectedVersion, DateOnly CorrectionDate, decimal Amount, string Method,
    string Reason, string EvidenceReference, IReadOnlyList<PaymentReleaseInput>? Releases = null);
public sealed record PaymentCorrectionResponse(Guid Id, Guid PaymentId, Guid CustomerId, long SourceVersion, long Version,
    string Mode, string Status, DateOnly CorrectionDate, decimal Amount, string Method, string Reason, string EvidenceReference,
    string RequestedBy, string? ApprovedBy, string? DocumentNumber, string? CancellationReason, DateTimeOffset CreatedAt,
    DateTimeOffset? PostedAt, IReadOnlyList<FrozenPaymentRelease> Releases)
{
    public static PaymentCorrectionResponse From(PaymentCorrection n) => new(n.Id,n.PaymentId,n.CustomerId,n.SourceVersion,n.Version,
        n.Mode,n.Status,n.CorrectionDate,n.Amount,n.Method,n.Reason,n.EvidenceReference,n.RequestedBy,n.ApprovedBy,
        n.DocumentNumber,n.CancellationReason,n.CreatedAt,n.PostedAt,n.Releases);
}
public sealed record PaymentReleaseResponse(Guid Id, Guid CorrectionId, Guid AllocationId, Guid InvoiceId, DateOnly BusinessDate, decimal Amount)
{
    public static PaymentReleaseResponse From(PaymentAllocationRelease r) => new(r.Id,r.CorrectionId,r.AllocationId,r.InvoiceId,r.BusinessDate,r.Amount);
}
public sealed record PaymentCorrectionsRead(IReadOnlyList<PaymentCorrection> Notes, IReadOnlyList<PaymentCashAdjustment> Cash, IReadOnlyList<PaymentAllocationRelease> Releases);
public sealed record CustomerPaymentCorrectionPostedV1(Guid CorrectionId, Guid PaymentId, Guid CustomerId, string Mode,
    DateOnly CorrectionDate, string DocumentNumber, string Currency, decimal Amount, Guid CashEntryId, IReadOnlyList<FrozenPaymentRelease> Releases);
public interface IPaymentCorrectionStore
{
    Task<PaymentCorrection?> Find(Guid tenant, Guid company, Guid payment, Guid id, CancellationToken ct);
    Task<PaymentCorrectionsRead> Read(Guid tenant, Guid company, Guid customer, CancellationToken ct);
    void Add(PaymentCorrection note);
    Guid Post(PaymentCorrection note, DateTimeOffset now);
}
public sealed class PaymentCorrectionService(IPaymentStore payments, IPaymentCorrectionStore corrections, ISalesPostingStore sales,
    IPeriodGuard periods, PostingPolicy policy, IAuditWriter audit, IEventWriter events, TimeProvider clock)
{
    public async Task<CommandOutcome> Request(Actor actor, Guid company, Guid paymentId, long expected, DateOnly date, string mode,
        decimal? requestedAmount, string? method, string reason, string evidence, IReadOnlyList<PaymentReleaseInput>? selected, string correlation, CancellationToken ct)
    {
        var p = await Payment(actor, company, paymentId, ct);
        await periods.RequireOpen(actor.TenantId, company, date, ct);
        await payments.LockCustomer(actor.TenantId, company, p.CustomerId, ct);
        if (p.Version != expected) throw new DomainException("version_conflict", "Reload the receipt before requesting a correction.",409);
        if (p.Status != "Posted") throw new DomainException("invalid_state", "Only an active posted receipt can be corrected.",409);
        await Reconcile(actor, company, p.CustomerId, ct);
        var state = await State(actor, company, p, ct);
        var amount = requestedAmount ?? p.Amount - state.Cash.Sum(x => -x.Amount);
        if (mode == "Refund" && method is not ("Cash" or "Bank")) throw new DomainException("validation_failed", "Refund method must be Cash or Bank.");
        if (mode == "Refund" && (selected?.Count ?? 0) > 200) throw new DomainException("validation_failed", "Select at most 200 original allocations per refund.");
        var lines = PaymentCorrectionRules.Freeze(p, await payments.Allocations(actor.TenantId, company, paymentId, ct), state.Notes, state.Releases, mode, amount, date, selected);
        if (mode == "Refund") await RefundBudget(actor, company, p, amount, lines, date, ct);
        p.RequestCorrection(expected);
        var note = PaymentCorrection.Create(p, mode, date, amount, method ?? p.Method, reason, evidence, actor.Subject, lines, clock.GetUtcNow());
        corrections.Add(note);
        audit.Append(actor, company, paymentId, $"Payment{mode}Requested", correlation, p.Version, note.Reason);
        return new(201, PaymentCorrectionResponse.From(note), $"/api/v1/companies/{company}/payments/{paymentId}/corrections/{note.Id}");
    }
    public async Task<CommandOutcome> Approve(Actor actor, Guid company, Guid paymentId, Guid id, string mode, TransitionRequest request, string correlation, CancellationToken ct)
    {
        var p = await Payment(actor, company, paymentId, ct);
        var n = await Note(actor, company, paymentId, id, mode, ct);
        if (!policy.Enabled) throw new DomainException("posting_disabled", "Financial posting is disabled.", 409);
        await periods.RequireOpen(actor.TenantId, company, n.CorrectionDate, ct);
        await payments.LockCustomer(actor.TenantId, company, p.CustomerId, ct);
        if (request.ExpectedVersion != n.SourceVersion) throw new DomainException("version_conflict", "Use the pending correction sourceVersion.", 409);
        await Reconcile(actor, company, p.CustomerId, ct);
        if (mode == "Refund") await RefundBudget(actor, company, p, n.Amount, n.Releases, n.CorrectionDate, ct);
        var state = await State(actor, company, p, ct);
        n.Approve(actor.Subject, p.RequestedBy, clock.GetUtcNow());
        var terminal = mode == "Reversal" ? "Reversed" : state.Cash.Sum(x => -x.Amount) + n.Amount == p.Amount ? "Refunded" : null;
        p.DecideCorrection(request.ExpectedVersion, terminal);
        var cashId = corrections.Post(n, clock.GetUtcNow());
        audit.Append(actor, company, paymentId, $"Payment{mode}Posted", correlation, p.Version, n.Reason);
        events.Append(actor.TenantId, company, paymentId, p.Version, nameof(CustomerPaymentCorrectionPostedV1), correlation,
            new CustomerPaymentCorrectionPostedV1(n.Id,paymentId,p.CustomerId,mode,n.CorrectionDate,n.DocumentNumber!,p.Currency,n.Amount,cashId,n.Releases));
        return new(200, PaymentCorrectionResponse.From(n));
    }
    public async Task<CommandOutcome> Cancel(Actor actor, Guid company, Guid paymentId, Guid id, string mode, CancelSalesInvoiceRequest request, string correlation, CancellationToken ct)
    {
        var p = await Payment(actor, company, paymentId, ct); var n = await Note(actor, company, paymentId, id, mode, ct);
        await periods.LockShared(actor.TenantId, company, ct);
        await payments.LockCustomer(actor.TenantId, company, p.CustomerId, ct);
        if (request.ExpectedVersion != n.SourceVersion) throw new DomainException("version_conflict", "Use the pending correction sourceVersion.", 409);
        n.Cancel(request.Reason); p.DecideCorrection(request.ExpectedVersion, null);
        audit.Append(actor, company, paymentId, $"Payment{mode}Cancelled", correlation, p.Version, request.Reason);
        return new(200, PaymentCorrectionResponse.From(n));
    }
    public async Task<PaymentCorrectionResponse> Get(Actor actor, Guid company, Guid paymentId, Guid id, CancellationToken ct)
        => PaymentCorrectionResponse.From(await Note(actor,company,paymentId,id,null,ct));
    public async Task<IReadOnlyList<PaymentCorrectionResponse>> List(Actor actor, Guid company, Guid paymentId, CancellationToken ct)
    {
        var p = await Payment(actor,company,paymentId,ct);
        return (await State(actor,company,p,ct)).Notes.OrderByDescending(x=>x.SourceVersion).Select(PaymentCorrectionResponse.From).ToArray();
    }
    private async Task RefundBudget(Actor actor, Guid company, CustomerPayment p, decimal amount, IReadOnlyList<FrozenPaymentRelease> lines, DateOnly date, CancellationToken ct)
    {
        var report = await payments.Receivables(actor.TenantId,company,p.CustomerId,ct);
        var dated = await payments.Receivables(actor.TenantId,company,p.CustomerId,ct,date);
        if (amount > -report.Balance || amount > -dated.Balance)
            throw new DomainException("refund_credit_exceeded", "Refund must fit customer credit both now and on its correction date.", 409);
        foreach (var group in lines.GroupBy(x=>x.InvoiceId))
        {
            var read = await sales.Read(actor.TenantId,company,group.Key,ct);
            if (read is null || read.Invoice.CustomerId != p.CustomerId) throw new DomainException("not_found", "Refund invoice not found.",404);
            if (read.Invoice.Status is SalesInvoiceStatus.CreditPending or SalesInvoiceStatus.DebitPending)
                throw new DomainException("invoice_not_refundable", "Resolve pending invoice adjustments before refunding invoice credit.",409);
            var row = report.Invoices.SingleOrDefault(x=>x.InvoiceId==group.Key);
            var datedRow = dated.Invoices.SingleOrDefault(x=>x.InvoiceId==group.Key);
            if (row is null || datedRow is null || group.Sum(x=>x.Amount) > -row.OutstandingAmount || group.Sum(x=>x.Amount) > -datedRow.OutstandingAmount)
                throw new DomainException("refund_invoice_credit_exceeded", "Release exceeds invoice overpayment credit.",409);
        }
    }
    private async Task Reconcile(Actor actor,Guid company,Guid customer,CancellationToken ct)
    {
        if (!await payments.Reconciles(actor.TenantId,company,ct,customer)) throw new DomainException("payment_reconciliation_failed","Receipt history must reconcile before correction.",409);
    }
    private async Task<PaymentCorrectionsRead> State(Actor actor,Guid company,CustomerPayment p,CancellationToken ct)
    {
        var state = await corrections.Read(actor.TenantId,company,p.CustomerId,ct);
        return new(state.Notes.Where(x=>x.PaymentId==p.Id).ToArray(),state.Cash.Where(x=>x.PaymentId==p.Id).ToArray(),state.Releases.Where(x=>x.PaymentId==p.Id).ToArray());
    }
    private async Task<CustomerPayment> Payment(Actor actor,Guid company,Guid id,CancellationToken ct)
    {
        actor.RequireCompany(company); return await payments.Find(actor.TenantId,company,id,ct) ?? throw new DomainException("not_found","Payment not found.",404);
    }
    private async Task<PaymentCorrection> Note(Actor actor,Guid company,Guid payment,Guid id,string? mode,CancellationToken ct)
    {
        actor.RequireCompany(company); var n=await corrections.Find(actor.TenantId,company,payment,id,ct);
        if (n is null || (mode is not null && n.Mode!=mode)) throw new DomainException("not_found","Correction not found.",404);
        return n;
    }
}
