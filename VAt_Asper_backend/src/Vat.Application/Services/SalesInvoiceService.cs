using Vat.Application.Contracts;
using Vat.Application.Security;
using Vat.Domain;
using Vat.Domain.Sales;

namespace Vat.Application;

public sealed class SalesInvoiceService(ISalesInvoiceStore invoices, ICompanyStore companies, IReferenceStore references,
    IEventWriter events, IAuditWriter audit, TimeProvider clock, IPeriodGuard periods, IPeriodStore periodStore, ISalesPostingStore postings, ICustomerStore customers)
{
    public async Task<SalesInvoiceResponse> Get(Actor actor, Guid companyId, Guid id, CancellationToken ct)
        => SalesInvoiceResponse.From(await RequireSalesInvoice(actor, companyId, id, ct));

    public async Task<IReadOnlyList<SalesInvoiceResponse>> List(Actor actor, Guid companyId, Guid? before,
        int take, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        if (take is < 1 or > 100)
            throw new DomainException("validation_failed", "take must be between 1 and 100.");
        return (await invoices.List(actor.TenantId, companyId, before, take, ct)).Select(SalesInvoiceResponse.From).ToArray();
    }

    public async Task<CommandOutcome> Create(Actor actor, Guid companyId, CreateSalesInvoiceRequest request,
        string correlationId, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        if (await companies.FindBranch(actor.TenantId, companyId, request.BranchId, ct) is null)
            throw new DomainException("not_found", "Branch not found in this company.", 404);
        CustomerSnapshot? customer = null;
        if (request.CustomerId is Guid customerId)
        {
            var master = await customers.Find(actor.TenantId, companyId, customerId, ct)
                ?? throw new DomainException("not_found", "Customer not found in this company.", 404);
            if (!master.IsActive) throw new DomainException("customer_inactive", "Choose an active customer.", 409);
            if (!string.Equals(request.CustomerReference?.Trim(), master.Code, StringComparison.OrdinalIgnoreCase))
                throw new DomainException("validation_failed", "Customer reference must match the selected customer's code.");
            customer = master.Snapshot();
        }
        var invoice = SalesInvoice.Create(actor.TenantId, companyId, request.BranchId, request.ExternalReference,
            customer?.Code ?? request.CustomerReference ?? "", request.BusinessDate, actor.Subject, request.Lines, clock.GetUtcNow(), customer, request.DueDate);
        invoices.Add(invoice);
        audit.Append(actor, companyId, invoice.Id, "SalesInvoiceCreated", correlationId, invoice.Version);
        return new(201, SalesInvoiceResponse.From(invoice), $"/api/v1/companies/{companyId}/sales/{invoice.Id}");
    }

    public async Task<CommandOutcome> Submit(Actor actor, Guid companyId, Guid id, TransitionRequest request,
        string correlationId, CancellationToken ct)
    {
        var invoice = await RequireSalesInvoice(actor, companyId, id, ct);
        await periods.RequireOpen(actor.TenantId, companyId, invoice.BusinessDate, ct);
        foreach (var reference in invoice.Lines.Select(x => x.ProductReference).Distinct())
            if (await references.ProductByReference(actor.TenantId, companyId, reference, ct) is null)
                throw new DomainException("product_not_found", "Every invoice product must be registered in this company.", 422);
        invoice.Submit(request.ExpectedVersion, clock.GetUtcNow());
        audit.Append(actor, companyId, id, "SalesInvoiceSubmitted", correlationId, invoice.Version);
        return new(200, SalesInvoiceResponse.From(invoice));
    }

    public async Task<CommandOutcome> Approve(Actor actor, Guid companyId, Guid id, TransitionRequest request,
        string correlationId, CancellationToken ct)
    {
        var invoice = await RequireSalesInvoice(actor, companyId, id, ct);
        await periods.RequireOpen(actor.TenantId, companyId, invoice.BusinessDate, ct);
        invoice.Approve(actor.Subject, request.ExpectedVersion, clock.GetUtcNow());
        events.Append(actor.TenantId, companyId, id, invoice.Version, nameof(SalesInvoiceApprovedV1), correlationId,
            new SalesInvoiceApprovedV1(id, invoice.BranchId, invoice.Version, invoice.ExternalReference,
                invoice.BusinessDate, invoice.Currency, invoice.NetAmount, invoice.CreatedBy, actor.Subject));
        audit.Append(actor, companyId, id, "SalesInvoiceApproved", correlationId, invoice.Version);
        return new(200, SalesInvoiceResponse.From(invoice));
    }

    public async Task<CommandOutcome> Reject(Actor actor, Guid companyId, Guid id, RejectSalesInvoiceRequest request,
        string correlationId, CancellationToken ct)
    {
        var invoice = await RequireSalesInvoice(actor, companyId, id, ct);
        invoice.Reject(actor.Subject, request.ExpectedVersion, request.Reason, clock.GetUtcNow());
        audit.Append(actor, companyId, id, "SalesInvoiceRejected", correlationId, invoice.Version);
        return new(200, SalesInvoiceResponse.From(invoice));
    }

    public async Task<CommandOutcome> Cancel(Actor actor, Guid companyId, Guid id, CancelSalesInvoiceRequest request,
        string correlationId, CancellationToken ct)
    {
        var invoice = await RequireSalesInvoice(actor, companyId, id, ct);
        // Nonfinancial cancellation remains available after close, under the company fence.
        await periodStore.LockCompany(actor.TenantId, companyId, false, ct);
        invoice.Cancel(request.ExpectedVersion, request.Reason, clock.GetUtcNow());
        var reservation = await postings.Find(actor.TenantId, companyId, id, ct);
        if (reservation is not null) await postings.Release(reservation, ct);
        audit.Append(actor, companyId, id, "SalesInvoiceCancelled", correlationId, invoice.Version, request.Reason);
        return new(200, SalesInvoiceResponse.From(invoice));
    }

    private async Task<SalesInvoice> RequireSalesInvoice(Actor actor, Guid companyId, Guid id, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        return await invoices.Find(actor.TenantId, companyId, id, ct)
            ?? throw new DomainException("not_found", "SalesInvoice not found.", 404);
    }
}
