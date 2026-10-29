using Vat.Application.Security;
using Vat.Domain;
using Vat.Domain.Catalog;
using Vat.Domain.Sales;
using Vat.Services.Sales.Contracts;

namespace Vat.Services.Sales;

public sealed class SalesInvoiceService(ISalesInvoiceStore invoices, IAuditWriter audit,
    IEventWriter events, TimeProvider clock)
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

    public Task<CommandOutcome> Create(Actor actor, Guid companyId, CreateSalesInvoiceRequest request,
        string correlationId, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        var now = clock.GetUtcNow();
        var invoice = SalesInvoice.Create(actor.TenantId, companyId, request.BranchId, request.ExternalReference,
            request.CustomerReference ?? "", request.BusinessDate, actor.Subject, request.Lines, now, null, request.DueDate);
        invoices.Add(invoice);
        audit.Append(actor, companyId, invoice.Id, "SalesInvoiceCreated", correlationId, invoice.Version);
        return Task.FromResult(new CommandOutcome(201, SalesInvoiceResponse.From(invoice), $"/api/v1/companies/{companyId}/sales/{invoice.Id}"));
    }

    public async Task<CommandOutcome> Submit(Actor actor, Guid companyId, Guid id, TransitionRequest request,
        string correlationId, CancellationToken ct)
    {
        var invoice = await RequireSalesInvoice(actor, companyId, id, ct);
        invoice.Submit(request.ExpectedVersion, clock.GetUtcNow());
        audit.Append(actor, companyId, id, "SalesInvoiceSubmitted", correlationId, invoice.Version);
        return new(200, SalesInvoiceResponse.From(invoice));
    }

    public async Task<CommandOutcome> Approve(Actor actor, Guid companyId, Guid id, TransitionRequest request,
        string correlationId, CancellationToken ct)
    {
        var invoice = await RequireSalesInvoice(actor, companyId, id, ct);
        invoice.Approve(actor.Subject, request.ExpectedVersion, clock.GetUtcNow());
        events.Append(actor.TenantId, companyId, id, invoice.Version, "SalesInvoiceApprovedV1", correlationId,
            new { SalesInvoiceId = id, invoice.BranchId, DocumentVersion = invoice.Version, invoice.ExternalReference,
                invoice.BusinessDate, invoice.Currency, invoice.NetAmount, MakerId = invoice.CreatedBy, ApproverId = actor.Subject });
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
        invoice.Cancel(request.ExpectedVersion, request.Reason, clock.GetUtcNow());
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
