using Vat.Application.Security;
using Vat.Domain;
using Vat.Domain.Sales;

using Vat.Services.Sales.Contracts;

namespace Vat.Services.Sales;

public interface ISalesCreditStore
{
    Task<SalesCreditNote?> Find(Guid tenantId, Guid companyId, Guid invoiceId, Guid id, CancellationToken ct);
    Task<IReadOnlyList<SalesCreditNote>> List(Guid tenantId, Guid companyId, Guid invoiceId, Guid? before, int take, CancellationToken ct);
    void Add(SalesCreditNote note);
    Task Return(SalesCreditNote note, SalesPostingRead original, DateTimeOffset now, CancellationToken ct);
}

public sealed record RequestSalesPriceCredit(long ExpectedVersion, DateOnly CorrectionDate, string Reason, string EvidenceReference, IReadOnlyList<SalesPriceLineInput> Lines);

public sealed record RequestSalesCredit(long ExpectedVersion, DateOnly CorrectionDate, string Reason, string ReturnReceiptReference, IReadOnlyList<SalesReturnLineInput>? Lines = null);
public sealed record SalesCreditResponse(Guid Id, Guid InvoiceId, long SourceVersion, long OriginalPostedVersion,
    DateOnly CorrectionDate, string Reason, string ReturnReceiptReference, string Status, string RequestedBy,
    string? ApprovedBy, string OriginalDocumentNumber, string? DocumentNumber, decimal NetAmount, decimal OutputTaxAmount,
    decimal GrossAmount, DateTimeOffset CreatedAt, DateTimeOffset? PostedAt, string? CancellationReason, string Mode, IReadOnlyList<SalesReturnAllocation> Lines, string? AdjustmentEvidenceReference = null)
{
    public static SalesCreditResponse From(SalesCreditNote n) => new(n.Id, n.InvoiceId, n.SourceVersion,
        n.OriginalPostedVersion, n.CorrectionDate, n.Reason, n.ReturnReceiptReference, n.Status, n.RequestedBy,
        n.ApprovedBy, n.OriginalDocumentNumber, n.DocumentNumber, n.NetAmount, n.OutputTaxAmount,
        n.NetAmount + n.OutputTaxAmount, n.CreatedAt, n.PostedAt, n.CancellationReason, n.Mode, n.Lines, n.AdjustmentEvidenceReference);
}
public sealed record SalesCreditPostedV1(Guid InvoiceId, Guid CreditNoteId, Guid WarehouseId,
    string OriginalDocumentNumber, string DocumentNumber, DateOnly CorrectionDate, string Currency,
    decimal NetAmount, decimal OutputTaxAmount, decimal GrossAmount);

public sealed record SalesPartialCreditPostedV1(Guid InvoiceId, Guid CreditNoteId, Guid WarehouseId,
    string OriginalDocumentNumber, string DocumentNumber, DateOnly CorrectionDate, string Currency,
    decimal NetAmount, decimal OutputTaxAmount, decimal GrossAmount, IReadOnlyList<SalesReturnAllocation> Lines);
public sealed record SalesPriceCreditPostedV1(Guid InvoiceId, Guid CreditNoteId, Guid WarehouseId,
    string OriginalDocumentNumber, string DocumentNumber, DateOnly CorrectionDate, string Currency,
    decimal NetAmount, decimal OutputTaxAmount, decimal GrossAmount, string EvidenceReference, IReadOnlyList<SalesReturnAllocation> Lines);
public sealed record SalesReturnableLine(Guid LineId, Guid ProductId, string ProductReference, decimal SoldQuantity,
    decimal ReturnedQuantity, decimal PendingQuantity, decimal RemainingQuantity, decimal RemainingNetAmount, decimal RemainingOutputTaxAmount, decimal PendingNetCreditAmount = 0, decimal PendingOutputTaxCreditAmount = 0, decimal PendingNetDebitAmount = 0, decimal PendingOutputTaxDebitAmount = 0);
public sealed record SalesReturnableResponse(Guid InvoiceId, long Version, string Status, bool CanRequestCredit,
    IReadOnlyList<SalesReturnableLine> Lines, bool CanRequestPriceCredit = false, bool CanRequestDebit = false);

public sealed class SalesCreditService(ISalesInvoiceStore invoices, ISalesPostingStore postings, ISalesCreditStore notes,
    IPeriodGuard periods, PostingPolicy policy, IAuditWriter audit, IEventWriter events, TimeProvider clock)
{
    public async Task<CommandOutcome> Request(Actor actor, Guid companyId, Guid invoiceId, RequestSalesCredit request, string correlation, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        await periods.RequireOpen(actor.TenantId, companyId, request.CorrectionDate, ct);
        var invoice = await Invoice(actor, companyId, invoiceId, ct);
        var original = await Reconciled(actor, companyId, invoiceId, ct);
        invoice.RequestCredit(request.ExpectedVersion, clock.GetUtcNow());
        var originals = original.Entries.Where(x => x.IsOriginal).ToArray();
        var returns = original.Entries.Where(x => !x.IsOriginal).ToArray();
        RequireCorrectionOrder(original, request.CorrectionDate);
        var allocations = SalesReturnAllocator.Allocate(originals, returns, request.Lines, original.History);
        var note = SalesCreditNote.Request(invoice, original.Fulfillment!, request.CorrectionDate,
            request.Reason, request.ReturnReceiptReference, actor.Subject, clock.GetUtcNow(), allocations);
        notes.Add(note);
        audit.Append(actor, companyId, invoiceId, "SalesCreditRequested", correlation, invoice.Version, note.Reason);
        return new(201, SalesCreditResponse.From(note), $"/api/v1/companies/{companyId}/sales/{invoiceId}/credit-notes/{note.Id}");
    }

    public async Task<CommandOutcome> RequestPrice(Actor actor, Guid companyId, Guid invoiceId, RequestSalesPriceCredit request, string correlation, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        var invoice = await Invoice(actor, companyId, invoiceId, ct);
        await periods.RequireOpen(actor.TenantId, companyId, request.CorrectionDate, ct);
        var original = await Reconciled(actor, companyId, invoiceId, ct);
        invoice.RequestCredit(request.ExpectedVersion, clock.GetUtcNow());
        RequireCorrectionOrder(original, request.CorrectionDate);
        var allocations = SalesPriceAllocator.Allocate(original.Entries.Where(x => x.IsOriginal).ToArray(),
            original.Entries.Where(x => !x.IsOriginal).ToArray(), request.Lines, original.History);
        var note = SalesCreditNote.Request(invoice, original.Fulfillment!, request.CorrectionDate, request.Reason,
            "", actor.Subject, clock.GetUtcNow(), allocations, priceOnly: true, adjustmentEvidence: request.EvidenceReference);
        notes.Add(note);
        audit.Append(actor, companyId, invoiceId, "SalesPriceCreditRequested", correlation, invoice.Version, note.Reason);
        return new(201, SalesCreditResponse.From(note), $"/api/v1/companies/{companyId}/sales/{invoiceId}/credit-notes/{note.Id}");
    }

    public async Task<CommandOutcome> Approve(Actor actor, Guid companyId, Guid invoiceId, Guid id,
        TransitionRequest request, string correlation, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        Vat.Domain.FinancialBoundary.RequireAvailable();
        actor.RequireCompany(companyId);
        if (!policy.Enabled) throw new DomainException("posting_disabled", "Financial posting is disabled.", 409);
        var note = await Note(actor, companyId, invoiceId, id, ct);
        await periods.RequireOpen(actor.TenantId, companyId, note.CorrectionDate, ct);
        var invoice = await Invoice(actor, companyId, invoiceId, ct);
        if (note.SourceVersion != request.ExpectedVersion)
            throw new DomainException("version_conflict", "Use the pending credit note's source version.", 409);
        var original = await Reconciled(actor, companyId, invoiceId, ct);
        var now = clock.GetUtcNow();
        note.Approve(actor.Subject, invoice.CreatedBy, now);
        var originals = original.Entries.Where(x => x.IsOriginal).ToArray();
        var previousReturns = original.Entries.Where(x => !x.IsOriginal).ToArray();
        invoice.Credit(request.ExpectedVersion, now, SalesReturnAllocator.FullyReturned(originals, previousReturns, note.Lines));
        await notes.Return(note, original, now, ct);
        audit.Append(actor, companyId, invoiceId, "SalesCreditPosted", correlation, invoice.Version, note.Reason);
        if (note.Mode == "PriceOnly")
            events.Append(actor.TenantId, companyId, invoiceId, invoice.Version, nameof(SalesPriceCreditPostedV1), correlation,
                new SalesPriceCreditPostedV1(invoiceId, note.Id, original.Fulfillment!.WarehouseId, note.OriginalDocumentNumber,
                    note.DocumentNumber!, note.CorrectionDate, "BDT", note.NetAmount, note.OutputTaxAmount,
                    note.NetAmount + note.OutputTaxAmount, note.AdjustmentEvidenceReference!, note.Lines));
        else if (note.Mode == "FullInvoice")
            events.Append(actor.TenantId, companyId, invoiceId, invoice.Version, nameof(SalesCreditPostedV1), correlation,
                new SalesCreditPostedV1(invoiceId, note.Id, original.Fulfillment!.WarehouseId, note.OriginalDocumentNumber,
                    note.DocumentNumber!, note.CorrectionDate, "BDT", note.NetAmount, note.OutputTaxAmount, note.NetAmount + note.OutputTaxAmount));
        else
            events.Append(actor.TenantId, companyId, invoiceId, invoice.Version, nameof(SalesPartialCreditPostedV1), correlation,
                new SalesPartialCreditPostedV1(invoiceId, note.Id, original.Fulfillment!.WarehouseId, note.OriginalDocumentNumber,
                    note.DocumentNumber!, note.CorrectionDate, "BDT", note.NetAmount, note.OutputTaxAmount,
                    note.NetAmount + note.OutputTaxAmount, note.Lines));
        return new(200, SalesCreditResponse.From(note));
    }

    public async Task<CommandOutcome> Cancel(Actor actor, Guid companyId, Guid invoiceId, Guid id,
        CancelSalesInvoiceRequest request, string correlation, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        await periods.LockShared(actor.TenantId, companyId, ct);
        var invoice = await Invoice(actor, companyId, invoiceId, ct);
        var note = await Note(actor, companyId, invoiceId, id, ct);
        if (note.SourceVersion != request.ExpectedVersion)
            throw new DomainException("version_conflict", "Use the pending credit note's source version.", 409);
        var original = await postings.Read(actor.TenantId, companyId, invoiceId, ct)
            ?? throw new DomainException("not_found", "Invoice not found.", 404);
        note.Cancel(request.Reason);
        invoice.CancelCredit(request.ExpectedVersion, clock.GetUtcNow(), (original.CreditNotes ?? []).Any(x => x.Status == "Posted"));
        audit.Append(actor, companyId, invoiceId, "SalesCreditCancelled", correlation, invoice.Version, note.CancellationReason);
        return new(200, SalesCreditResponse.From(note));
    }

    public async Task<SalesCreditResponse> Get(Actor actor, Guid companyId, Guid invoiceId, Guid id, CancellationToken ct) =>
        SalesCreditResponse.From(await Note(actor, companyId, invoiceId, id, ct));

    public async Task<SalesReturnableResponse> Returnable(Actor actor, Guid companyId, Guid invoiceId, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        var read = await Reconciled(actor, companyId, invoiceId, ct);
        var pending = (read.CreditNotes ?? []).Where(x => x.Status == "Pending").SelectMany(x => x.Lines).ToArray();
        var pendingDebits = (read.DebitNotes ?? []).Where(x => x.Status == "Pending").SelectMany(x => x.Lines).ToArray();
        var lines = read.Entries.Where(x => x.IsOriginal).Select(original =>
        {
            var returned = read.Entries.Where(x => x.SourceEntryId == original.Id).ToArray();
            return new SalesReturnableLine(original.LineId, original.ProductId,
                read.Fulfillment!.Snapshot.Lines.Single(x => x.LineId == original.LineId).ProductReference, -original.Quantity,
                returned.Sum(x => x.Quantity), pending.Where(x => x.OriginalEntryId == original.Id).Sum(x => x.Quantity),
                -original.Quantity - returned.Sum(x => x.Quantity), original.NetAmount + returned.Sum(x => x.NetAmount),
                original.OutputTaxAmount + returned.Sum(x => x.OutputTaxAmount),
                -pending.Where(x => x.OriginalEntryId == original.Id).Sum(x => x.NetAmount),
                -pending.Where(x => x.OriginalEntryId == original.Id).Sum(x => x.OutputTaxAmount),
                pendingDebits.Where(x => x.OriginalEntryId == original.Id).Sum(x => x.NetAmount),
                pendingDebits.Where(x => x.OriginalEntryId == original.Id).Sum(x => x.OutputTaxAmount));
        }).OrderBy(x => x.LineId).ToArray();
        return new(invoiceId, read.Invoice.Version, read.Invoice.Status.ToString(),
            read.Invoice.Status is SalesInvoiceStatus.Posted or SalesInvoiceStatus.PartiallyCredited, lines,
            (read.Invoice.Status is SalesInvoiceStatus.Posted or SalesInvoiceStatus.PartiallyCredited) && lines.Any(x => x.RemainingNetAmount > 0),
            (read.Invoice.Status is SalesInvoiceStatus.Posted or SalesInvoiceStatus.PartiallyCredited) &&
                lines.Any(x => x.RemainingQuantity > 0 && x.RemainingNetAmount < SalesDebitAllocator.MaxLineValue));
    }

    public async Task<IReadOnlyList<SalesCreditResponse>> List(Actor actor, Guid companyId, Guid invoiceId, Guid? before, int take, CancellationToken ct)
    {
        await Invoice(actor, companyId, invoiceId, ct);
        if (take is < 1 or > 100) throw new DomainException("validation_failed", "take must be 1-100.");
        return (await notes.List(actor.TenantId, companyId, invoiceId, before, take, ct)).Select(SalesCreditResponse.From).ToArray();
    }

    private static void RequireCorrectionOrder(SalesPostingRead read, DateOnly date)
    {
        var dates = read.History.Where(x => x.Status == "Posted").Select(x => x.CorrectionDate).ToArray();
        if (dates.Length > 0 && date < dates.Max())
            throw new DomainException("validation_failed", "Correction date cannot precede an earlier posted credit on this invoice.");
    }

    private async Task<SalesPostingRead> Reconciled(Actor actor, Guid companyId, Guid invoiceId, CancellationToken ct)
    {
        var original = await postings.Read(actor.TenantId, companyId, invoiceId, ct);
        if (original is null) throw new DomainException("not_found", "Invoice not found.", 404);
        if (!SalesReconciler.Evaluate(original).IsReconciled)
            throw new DomainException("reconciliation_failed", "The original invoice and its ledger must reconcile before crediting.", 409);
        return original;
    }
    private async Task<SalesInvoice> Invoice(Actor actor, Guid companyId, Guid id, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        return await invoices.Find(actor.TenantId, companyId, id, ct) ?? throw new DomainException("not_found", "Invoice not found.", 404);
    }
    private async Task<SalesCreditNote> Note(Actor actor, Guid companyId, Guid invoiceId, Guid id, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        return await notes.Find(actor.TenantId, companyId, invoiceId, id, ct) ?? throw new DomainException("not_found", "Credit note not found.", 404);
    }
}
