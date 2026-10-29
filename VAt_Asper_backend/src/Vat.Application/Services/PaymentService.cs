using Vat.Application.Contracts;
using Vat.Application.Security;
using Vat.Domain;
using Vat.Domain.Sales;

namespace Vat.Application;

public sealed record CreatePaymentRequest(Guid CustomerId, string Reference, string Method, string EvidenceReference,
    DateOnly PaymentDate, decimal Amount, IReadOnlyList<PaymentAllocationInput>? Allocations = null);
public sealed record AllocatePaymentRequest(long ExpectedVersion, DateOnly AllocationDate, IReadOnlyList<PaymentAllocationInput> Allocations);
public sealed record PaymentDocumentResponse(Guid Id, Guid TenantId, Guid CompanyId, Guid CustomerId, string Reference, string Method,
    string EvidenceReference, DateOnly PaymentDate, decimal Amount, string Currency, string Status, long Version,
    string RequestedBy, string? ApprovedBy, string? CancellationReason, DateTimeOffset CreatedAt, DateTimeOffset? PostedAt,
    IReadOnlyList<PaymentAllocationInput> RequestedAllocations)
{
    public static PaymentDocumentResponse From(CustomerPayment p) => new(p.Id, p.TenantId, p.CompanyId, p.CustomerId, p.Reference, p.Method,
        p.EvidenceReference, p.PaymentDate, p.Amount, p.Currency, p.Status, p.Version, p.RequestedBy, p.ApprovedBy,
        p.CancellationReason, p.CreatedAt, p.PostedAt, p.RequestedAllocations);
}
public sealed record PaymentAllocationResponse(Guid Id, Guid TenantId, Guid CompanyId, Guid CustomerId, Guid PaymentId, Guid InvoiceId,
    long PaymentVersion, DateOnly AllocationDate, decimal Amount, string AllocatedBy, DateTimeOffset CreatedAt)
{
    public static PaymentAllocationResponse From(PaymentAllocation a) => new(a.Id, a.TenantId, a.CompanyId, a.CustomerId, a.PaymentId,
        a.InvoiceId, a.PaymentVersion, a.AllocationDate, a.Amount, a.AllocatedBy, a.CreatedAt);
}
public sealed record PaymentResponse(PaymentDocumentResponse Payment, IReadOnlyList<PaymentAllocationResponse> Allocations, decimal AllocatedAmount, decimal UnallocatedAmount,
    decimal ReturnedAmount = 0, decimal NetCollectedAmount = 0, IReadOnlyList<PaymentReleaseResponse>? AllocationReleases = null, DateOnly? LastCorrectionDate = null)
{
    public static PaymentResponse From(CustomerPayment p, IReadOnlyList<PaymentAllocation> lines, decimal returned = 0,
        IReadOnlyList<PaymentAllocationRelease>? releases = null, DateOnly? lastDate = null)
    {
        releases ??= []; var allocated = lines.Sum(x=>x.Amount) - releases.Sum(x=>x.Amount);
        var netCash = p.PostedAt is not null ? p.Amount - returned : 0;
        return new(PaymentDocumentResponse.From(p),lines.Select(PaymentAllocationResponse.From).ToArray(),allocated,
            p.PostedAt is not null ? netCash - allocated : 0,returned,netCash,releases.Select(PaymentReleaseResponse.From).ToArray(),lastDate);
    }
}
public sealed record InvoiceReceivable(Guid InvoiceId, string Reference, decimal GrossAmount, decimal AllocatedAmount, decimal OutstandingAmount);
public sealed record CustomerReceivables(Guid CustomerId, string Currency, decimal SalesGrossAmount, decimal CollectedAmount,
    decimal AllocatedAmount, decimal UnallocatedAmount, decimal Balance, IReadOnlyList<InvoiceReceivable> Invoices, decimal ReturnedAmount = 0, decimal NetCollectedAmount = 0);
public sealed record CustomerPaymentPostedV1(Guid PaymentId, Guid CustomerId, DateOnly PaymentDate, string Currency, decimal Amount, string Method,
    string Reference, IReadOnlyList<PaymentAllocationResponse> Allocations);
public sealed record CustomerPaymentAllocatedV1(Guid PaymentId, Guid CustomerId, DateOnly AllocationDate, string Currency, IReadOnlyList<PaymentAllocationResponse> Allocations);
public interface IPaymentStore
{
    Task LockCustomer(Guid tenant, Guid company, Guid customer, CancellationToken ct);
    Task<PaymentResponse?> Read(Guid tenant, Guid company, Guid id, CancellationToken ct);
    Task<CustomerPayment?> Find(Guid tenant, Guid company, Guid id, CancellationToken ct);
    Task<IReadOnlyList<CustomerPayment>> List(Guid tenant, Guid company, Guid? customer, Guid? before, int take, CancellationToken ct);
    Task<IReadOnlyList<PaymentAllocation>> Allocations(Guid tenant, Guid company, Guid payment, CancellationToken ct);
    Task<decimal> InvoiceAllocated(Guid tenant, Guid company, Guid invoice, CancellationToken ct);
    Task<CustomerReceivables> Receivables(Guid tenant, Guid company, Guid customer, CancellationToken ct, DateOnly? asOf = null);
    Task<bool> Reconciles(Guid tenant, Guid company, CancellationToken ct, Guid? customer = null);
    void Add(CustomerPayment payment);
    void Add(PaymentAllocation allocation);
}
public sealed class PaymentService(IPaymentStore store, ICustomerStore customers, ISalesPostingStore sales, IPeriodGuard periods,
    PostingPolicy policy, IAuditWriter audit, IEventWriter events, TimeProvider clock)
{
    public async Task<CommandOutcome> Create(Actor actor, Guid company, CreatePaymentRequest request, string correlation, CancellationToken ct)
    {
        await Customer(actor, company, request.CustomerId, ct);
        await periods.RequireOpen(actor.TenantId, company, request.PaymentDate, ct);
        await store.LockCustomer(actor.TenantId, company, request.CustomerId, ct);
        var payment = CustomerPayment.Create(actor.TenantId, company, request.CustomerId, request.Reference, request.Method,
            request.EvidenceReference, request.PaymentDate, request.Amount, request.Allocations, actor.Subject, clock.GetUtcNow());
        await Validate(actor, company, payment, payment.RequestedAllocations, payment.PaymentDate, ct);
        store.Add(payment);
        audit.Append(actor, company, payment.Id, "CustomerPaymentRequested", correlation, payment.Version);
        return new(201, Response(payment, []), $"/api/v1/companies/{company}/payments/{payment.Id}");
    }
    public async Task<CommandOutcome> Approve(Actor actor, Guid company, Guid id, TransitionRequest request, string correlation, CancellationToken ct)
    {
        var payment = await Find(actor, company, id, ct);
        if (!policy.Enabled) throw new DomainException("posting_disabled", "Financial posting is disabled.", 409);
        await periods.RequireOpen(actor.TenantId, company, payment.PaymentDate, ct);
        await store.LockCustomer(actor.TenantId, company, payment.CustomerId, ct);
        await Validate(actor, company, payment, payment.RequestedAllocations, payment.PaymentDate, ct);
        payment.Approve(request.ExpectedVersion, actor.Subject, clock.GetUtcNow());
        var allocations = AddAllocations(payment, payment.RequestedAllocations, payment.PaymentDate, actor.Subject);
        audit.Append(actor, company, id, "CustomerPaymentPosted", correlation, payment.Version);
        events.Append(actor.TenantId, company, id, payment.Version, nameof(CustomerPaymentPostedV1), correlation,
            new CustomerPaymentPostedV1(id, payment.CustomerId, payment.PaymentDate, payment.Currency, payment.Amount, payment.Method, payment.Reference, allocations.Select(PaymentAllocationResponse.From).ToArray()));
        return new(200, Response(payment, allocations));
    }
    public async Task<CommandOutcome> Allocate(Actor actor, Guid company, Guid id, AllocatePaymentRequest request, string correlation, CancellationToken ct)
    {
        var payment = await Find(actor, company, id, ct);
        if (!policy.Enabled) throw new DomainException("posting_disabled", "Financial posting is disabled.", 409);
        await periods.RequireOpen(actor.TenantId, company, request.AllocationDate, ct);
        await store.LockCustomer(actor.TenantId, company, payment.CustomerId, ct);
        var lines = CustomerPayment.Lines(request.Allocations);
        var existing = await store.Allocations(actor.TenantId, company, id, ct);
        var state = await store.Read(actor.TenantId, company, id, ct) ?? throw new DomainException("not_found", "Payment not found.", 404);
        if (state.LastCorrectionDate is DateOnly lastDate && request.AllocationDate < lastDate)
            throw new DomainException("validation_failed", "Allocation date cannot precede a posted receipt correction.");
        if (lines.Sum(x => x.Amount) > state.UnallocatedAmount)
            throw new DomainException("payment_allocation_exceeded", "Allocations exceed remaining receipt amount.", 409);
        await Validate(actor, company, payment, lines, request.AllocationDate, ct);
        payment.Allocate(request.ExpectedVersion);
        var added = AddAllocations(payment, lines, request.AllocationDate, actor.Subject);
        audit.Append(actor, company, id, "CustomerPaymentAllocated", correlation, payment.Version);
        events.Append(actor.TenantId, company, id, payment.Version, nameof(CustomerPaymentAllocatedV1), correlation,
            new CustomerPaymentAllocatedV1(id, payment.CustomerId, request.AllocationDate, payment.Currency, added.Select(PaymentAllocationResponse.From).ToArray()));
        var response = PaymentResponse.From(payment, existing.Concat(added).ToArray());
        var netAllocated = existing.Sum(x=>x.Amount) + added.Sum(x=>x.Amount) - (state.AllocationReleases ?? []).Sum(x=>x.Amount);
        return new(200, response with { AllocatedAmount = netAllocated, UnallocatedAmount = state.NetCollectedAmount - netAllocated,
            ReturnedAmount = state.ReturnedAmount, NetCollectedAmount = state.NetCollectedAmount, AllocationReleases = state.AllocationReleases, LastCorrectionDate = state.LastCorrectionDate });
    }
    public async Task<CommandOutcome> Cancel(Actor actor, Guid company, Guid id, CancelSalesInvoiceRequest request, string correlation, CancellationToken ct)
    {
        var payment = await Find(actor, company, id, ct);
        await periods.LockShared(actor.TenantId, company, ct);
        payment.Cancel(request.ExpectedVersion, request.Reason);
        audit.Append(actor, company, id, "CustomerPaymentCancelled", correlation, payment.Version, request.Reason);
        return new(200, Response(payment, []));
    }
    public async Task<PaymentResponse> Get(Actor actor, Guid company, Guid id, CancellationToken ct)
    {
        actor.RequireCompany(company);
        return await store.Read(actor.TenantId, company, id, ct) ?? throw new DomainException("not_found", "Payment not found.", 404);
    }
    public async Task<IReadOnlyList<PaymentDocumentResponse>> List(Actor actor, Guid company, Guid? customer, Guid? before, int take, CancellationToken ct)
    {
        actor.RequireCompany(company);
        if (take is < 1 or > 100) throw new DomainException("validation_failed", "take must be between 1 and 100.");
        return (await store.List(actor.TenantId, company, customer, before, take, ct)).Select(PaymentDocumentResponse.From).ToArray();
    }
    public async Task<CustomerReceivables> Receivables(Actor actor, Guid company, Guid customer, CancellationToken ct)
    {
        await Customer(actor, company, customer, ct);
        return await store.Receivables(actor.TenantId, company, customer, ct);
    }
    private async Task Validate(Actor actor, Guid company, CustomerPayment payment, IReadOnlyList<PaymentAllocationInput> lines, DateOnly date, CancellationToken ct)
    {
        if (date == default || date < payment.PaymentDate) throw new DomainException("validation_failed", "Allocation date cannot precede receipt date.");
        if (!await store.Reconciles(actor.TenantId, company, ct, payment.CustomerId)) throw new DomainException("payment_reconciliation_failed", "Payment records must reconcile before allocation or posting.", 409);
        foreach (var line in lines)
        {
            var read = await sales.Read(actor.TenantId, company, line.InvoiceId, ct);
            if (read is null || read.Invoice.CustomerId != payment.CustomerId) throw new DomainException("not_found", "Invoice not found for this customer.", 404);
            if (!SalesReconciler.Evaluate(read).IsReconciled || read.Invoice.Status is not (SalesInvoiceStatus.Posted or SalesInvoiceStatus.PartiallyCredited or SalesInvoiceStatus.Credited))
                throw new DomainException("invoice_not_allocatable", "A reconciled posted invoice without a pending adjustment is required.", 409);
            if (date < read.Invoice.BusinessDate) throw new DomainException("validation_failed", "Allocation date cannot precede invoice date.");
            var gross = read.Entries.Sum(x => x.NetAmount + x.OutputTaxAmount);
            if (line.Amount > gross - await store.InvoiceAllocated(actor.TenantId, company, line.InvoiceId, ct))
                throw new DomainException("invoice_allocation_exceeded", "Allocation exceeds current invoice outstanding.", 409);
        }
    }
    private IReadOnlyList<PaymentAllocation> AddAllocations(CustomerPayment payment, IReadOnlyList<PaymentAllocationInput> lines, DateOnly date, string actor)
    {
        var result = lines.Select(x => PaymentAllocation.Create(payment, x, date, actor, clock.GetUtcNow())).ToArray();
        foreach (var allocation in result) store.Add(allocation);
        return result;
    }
    private static PaymentResponse Response(CustomerPayment p, IReadOnlyList<PaymentAllocation> lines) => PaymentResponse.From(p,lines);
    private async Task<CustomerPayment> Find(Actor actor, Guid company, Guid id, CancellationToken ct)
    {
        actor.RequireCompany(company);
        return await store.Find(actor.TenantId, company, id, ct) ?? throw new DomainException("not_found", "Payment not found.", 404);
    }
    private async Task Customer(Actor actor, Guid company, Guid id, CancellationToken ct)
    {
        actor.RequireCompany(company);
        if (await customers.Find(actor.TenantId, company, id, ct) is null) throw new DomainException("not_found", "Customer not found.", 404);
    }
}
