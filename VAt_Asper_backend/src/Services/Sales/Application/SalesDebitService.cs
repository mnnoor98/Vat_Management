using Vat.Application.Security;
using Vat.Domain;
using Vat.Domain.Sales;

using Vat.Services.Sales.Contracts;

namespace Vat.Services.Sales;

public interface ISalesDebitStore
{
    Task<SalesDebitNote?> Find(Guid tenantId, Guid companyId, Guid invoiceId, Guid id, CancellationToken ct);
    Task<IReadOnlyList<SalesDebitNote>> List(Guid tenantId, Guid companyId, Guid invoiceId, Guid? before, int take, CancellationToken ct);
    void Add(SalesDebitNote note);
    void Post(SalesDebitNote note, SalesPostingRead original, DateTimeOffset now);
}
public sealed record RequestSalesDebit(long ExpectedVersion, DateOnly CorrectionDate, string Reason,
    string EvidenceReference, IReadOnlyList<SalesDebitLineInput> Lines);
public sealed record SalesDebitResponse(Guid Id, Guid InvoiceId, long SourceVersion, long OriginalPostedVersion,
    DateOnly CorrectionDate, string Reason, string EvidenceReference, string Status, string RequestedBy, string? ApprovedBy,
    string OriginalDocumentNumber, string? DocumentNumber, decimal NetAmount, decimal OutputTaxAmount, decimal GrossAmount,
    IReadOnlyList<SalesDebitAllocation> Lines, DateTimeOffset CreatedAt, DateTimeOffset? PostedAt, string? CancellationReason)
{
    public static SalesDebitResponse From(SalesDebitNote n) => new(n.Id, n.InvoiceId, n.SourceVersion, n.OriginalPostedVersion,
        n.CorrectionDate, n.Reason, n.EvidenceReference, n.Status, n.RequestedBy, n.ApprovedBy, n.OriginalDocumentNumber,
        n.DocumentNumber, n.NetAmount, n.OutputTaxAmount, n.NetAmount + n.OutputTaxAmount, n.Lines, n.CreatedAt, n.PostedAt, n.CancellationReason);
}
public sealed record SalesDebitPostedV1(Guid InvoiceId, Guid DebitNoteId, string OriginalDocumentNumber,
    string DocumentNumber, DateOnly CorrectionDate, string Currency, decimal NetAmount, decimal OutputTaxAmount,
    decimal GrossAmount, string EvidenceReference, IReadOnlyList<SalesDebitAllocation> Lines);

public sealed class SalesDebitService(ISalesInvoiceStore invoices, ISalesPostingStore postings, ISalesDebitStore notes,
    IPeriodGuard periods, PostingPolicy policy, IAuditWriter audit, IEventWriter events, TimeProvider clock)
{
    public async Task<CommandOutcome> Request(Actor actor, Guid companyId, Guid invoiceId, RequestSalesDebit request, string correlation, CancellationToken ct)
    {
        var invoice = await Invoice(actor, companyId, invoiceId, ct);
        await periods.RequireOpen(actor.TenantId, companyId, request.CorrectionDate, ct);
        var original = await Reconciled(actor, companyId, invoiceId, ct);
        invoice.RequestDebit(request.ExpectedVersion, clock.GetUtcNow());
        var dates = original.History.Where(x => x.Status == "Posted").Select(x => x.CorrectionDate).ToArray();
        if (dates.Length > 0 && request.CorrectionDate < dates.Max())
            throw new DomainException("validation_failed", "Correction date cannot precede an earlier posted adjustment.");
        var lines = SalesDebitAllocator.Allocate(original.Entries.Where(x => x.IsOriginal).ToArray(), original.Entries.Where(x => !x.IsOriginal).ToArray(),
            request.Lines, original.History, original.Fulfillment!.Snapshot.Lines);
        var note = SalesDebitNote.Request(invoice, original.Fulfillment, request.CorrectionDate, request.Reason,
            request.EvidenceReference, actor.Subject, lines, clock.GetUtcNow());
        notes.Add(note);
        audit.Append(actor, companyId, invoiceId, "SalesDebitRequested", correlation, invoice.Version, note.Reason);
        return new(201, SalesDebitResponse.From(note), $"/api/v1/companies/{companyId}/sales/{invoiceId}/debit-notes/{note.Id}");
    }

    public async Task<CommandOutcome> Approve(Actor actor, Guid companyId, Guid invoiceId, Guid id, TransitionRequest request, string correlation, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        var note = await Note(actor, companyId, invoiceId, id, ct);
        if (!policy.Enabled) throw new DomainException("posting_disabled", "Financial posting is disabled.", 409);
        await periods.RequireOpen(actor.TenantId, companyId, note.CorrectionDate, ct);
        var invoice = await Invoice(actor, companyId, invoiceId, ct);
        if (note.SourceVersion != request.ExpectedVersion) throw new DomainException("version_conflict", "Use the pending debit's sourceVersion.", 409);
        var original = await Reconciled(actor, companyId, invoiceId, ct);
        var now = clock.GetUtcNow();
        note.Approve(actor.Subject, invoice.CreatedBy, now);
        invoice.DecideDebit(request.ExpectedVersion, now, (original.CreditNotes ?? []).Any(x => x.Status == "Posted"));
        notes.Post(note, original, now);
        audit.Append(actor, companyId, invoiceId, "SalesDebitPosted", correlation, invoice.Version, note.Reason);
        events.Append(actor.TenantId, companyId, invoiceId, invoice.Version, nameof(SalesDebitPostedV1), correlation,
            new SalesDebitPostedV1(invoiceId, note.Id, note.OriginalDocumentNumber, note.DocumentNumber!, note.CorrectionDate,
                "BDT", note.NetAmount, note.OutputTaxAmount, note.NetAmount + note.OutputTaxAmount, note.EvidenceReference, note.Lines));
        return new(200, SalesDebitResponse.From(note));
    }

    public async Task<CommandOutcome> Cancel(Actor actor, Guid companyId, Guid invoiceId, Guid id, CancelSalesInvoiceRequest request, string correlation, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        await periods.LockShared(actor.TenantId, companyId, ct);
        var invoice = await Invoice(actor, companyId, invoiceId, ct);
        var note = await Note(actor, companyId, invoiceId, id, ct);
        if (note.SourceVersion != request.ExpectedVersion) throw new DomainException("version_conflict", "Use the pending debit's sourceVersion.", 409);
        var original = await postings.Read(actor.TenantId, companyId, invoiceId, ct) ?? throw new DomainException("not_found", "Invoice not found.", 404);
        note.Cancel(request.Reason);
        invoice.DecideDebit(request.ExpectedVersion, clock.GetUtcNow(), (original.CreditNotes ?? []).Any(x => x.Status == "Posted"));
        audit.Append(actor, companyId, invoiceId, "SalesDebitCancelled", correlation, invoice.Version, note.CancellationReason);
        return new(200, SalesDebitResponse.From(note));
    }

    public async Task<SalesDebitResponse> Get(Actor actor, Guid companyId, Guid invoiceId, Guid id, CancellationToken ct) =>
        SalesDebitResponse.From(await Note(actor, companyId, invoiceId, id, ct));
    public async Task<IReadOnlyList<SalesDebitResponse>> List(Actor actor, Guid companyId, Guid invoiceId, Guid? before, int take, CancellationToken ct)
    {
        await Invoice(actor, companyId, invoiceId, ct);
        if (take is < 1 or > 100) throw new DomainException("validation_failed", "take must be 1-100.");
        return (await notes.List(actor.TenantId, companyId, invoiceId, before, take, ct)).Select(SalesDebitResponse.From).ToArray();
    }
    private async Task<SalesInvoice> Invoice(Actor actor, Guid companyId, Guid id, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        return await invoices.Find(actor.TenantId, companyId, id, ct) ?? throw new DomainException("not_found", "Invoice not found.", 404);
    }
    private async Task<SalesDebitNote> Note(Actor actor, Guid companyId, Guid invoiceId, Guid id, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        return await notes.Find(actor.TenantId, companyId, invoiceId, id, ct) ?? throw new DomainException("not_found", "Debit note not found.", 404);
    }
    private async Task<SalesPostingRead> Reconciled(Actor actor, Guid companyId, Guid invoiceId, CancellationToken ct)
    {
        var read = await postings.Read(actor.TenantId, companyId, invoiceId, ct) ?? throw new DomainException("not_found", "Invoice not found.", 404);
        if (!SalesReconciler.Evaluate(read).IsReconciled) throw new DomainException("reconciliation_failed", "Invoice and adjustment history must reconcile.", 409);
        return read;
    }
}
