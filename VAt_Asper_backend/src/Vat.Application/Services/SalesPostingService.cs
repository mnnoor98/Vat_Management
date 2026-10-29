using Vat.Application.Contracts;
using Vat.Application.Security;
using Vat.Domain;
using Vat.Domain.Sales;

namespace Vat.Application;

public interface ISalesPostingStore
{
    Task<SalesFulfillment?> Find(Guid tenantId, Guid companyId, Guid invoiceId, CancellationToken ct);
    Task Reserve(SalesFulfillment plan, CancellationToken ct);
    Task Issue(SalesFulfillment plan, DateTimeOffset now, CancellationToken ct);
    Task Release(SalesFulfillment plan, CancellationToken ct);
    Task<SalesPostingRead?> Read(Guid tenantId, Guid companyId, Guid invoiceId, CancellationToken ct);
    Task<SalesLedgerPage> Ledger(Guid tenantId, Guid companyId, Guid? before, int take, CancellationToken ct);
    Task<SalesSummary> Summary(Guid tenantId, Guid companyId, DateOnly from, DateOnly until, CancellationToken ct);
}

public sealed record SalesPostingRead(SalesInvoice Invoice, SalesFulfillment? Fulfillment, IReadOnlyList<SalesPostingEntry> Entries, IReadOnlyList<SalesCreditNote>? CreditNotes = null,
    IReadOnlyList<SalesDebitNote>? DebitNotes = null)
{
    public IReadOnlyList<SalesAdjustmentHistory> History => (CreditNotes ?? []).Select(x => new SalesAdjustmentHistory(x.Id, x.SourceVersion, x.Status, x.Mode, x.CorrectionDate))
        .Concat((DebitNotes ?? []).Select(x => new SalesAdjustmentHistory(x.Id, x.SourceVersion, x.Status, "PriceDebit", x.CorrectionDate)))
        .OrderBy(x => x.SourceVersion).ToArray();
}
public sealed record ReserveSalesRequest(long ExpectedVersion, Guid WarehouseId);
public sealed record SalesPostingResponse(Guid InvoiceId, string Status, long SourceVersion, Guid WarehouseId,
    string ReservedBy, string? PostedBy, string? DocumentNumber, SalesCalculationSnapshot Snapshot)
{
    public static SalesPostingResponse From(SalesFulfillment p) => new(p.InvoiceId, p.Status, p.SourceVersion,
        p.WarehouseId, p.ReservedBy, p.PostedBy, p.DocumentNumber, p.Snapshot);
}
public sealed record SalesReconciliation(Guid InvoiceId, bool IsReconciled, IReadOnlyList<string> Differences);
public sealed record SalesLedgerEntryResponse(Guid Id, Guid InvoiceId, Guid LineId, Guid WarehouseId, Guid ProductId,
    Guid TaxRuleId, DateOnly BusinessDate, decimal Quantity, decimal NetAmount, decimal OutputTaxAmount, Guid? ReversesEntryId = null, Guid? CreditNoteId = null, string EntryType = "Goods", Guid? DebitNoteId = null, Guid? AdjustsEntryId = null)
{
    public static SalesLedgerEntryResponse From(SalesPostingEntry e) => new(e.Id, e.InvoiceId, e.LineId,
        e.WarehouseId, e.ProductId, e.TaxRuleId, e.BusinessDate, e.Quantity, e.NetAmount, e.OutputTaxAmount, e.ReversesEntryId, e.CreditNoteId, e.EntryType, e.DebitNoteId, e.AdjustsEntryId);
}
public sealed record SalesLedgerPage(IReadOnlyList<SalesLedgerEntryResponse> Entries, Guid? NextBefore);
public sealed record SalesSummary(DateOnly From, DateOnly Until, int InvoiceCount, decimal NetAmount, decimal OutputTaxAmount, decimal GrossAmount, int CreditNoteCount = 0, int PriceCreditCount = 0, int DebitNoteCount = 0);
public sealed record SalesPostedV1(Guid InvoiceId, Guid WarehouseId, string DocumentNumber, DateOnly BusinessDate,
    string Currency, decimal NetAmount, decimal OutputTaxAmount, decimal GrossAmount);

public static class SalesReconciler
{
    public static SalesReconciliation Evaluate(SalesPostingRead read)
    {
        var errors = new List<string>();
        var invoice = read.Invoice;
        var plan = read.Fulfillment;
        var notes = (read.CreditNotes ?? []).Select(x => NoteAudit.From(x))
            .Concat((read.DebitNotes ?? []).Select(x => NoteAudit.From(x))).OrderBy(x => x.SourceVersion).ToArray();
        var originals = read.Entries.Where(x => x.IsOriginal).ToArray();
        var returns = read.Entries.Except(originals).ToArray();
        var pending = notes.Where(x => x.Status == "Pending").ToArray();
        var latest = notes.LastOrDefault();
        var expectedVersion = latest is null ? plan?.SourceVersion + 1 : latest.SourceVersion + (latest.Status == "Pending" ? 0 : 1);
        var hasPosted = notes.Any(x => x.Status == "Posted" && x.Mode != "PriceDebit");
        var fullyReturned = originals.Length > 0 && SalesReturnAllocator.FullyReturned(originals, returns);
        var expectedState = pending.Length > 0 ? pending[0].Mode == "PriceDebit" ? SalesInvoiceStatus.DebitPending : SalesInvoiceStatus.CreditPending : !hasPosted ? SalesInvoiceStatus.Posted :
            fullyReturned ? SalesInvoiceStatus.Credited : SalesInvoiceStatus.PartiallyCredited;
        if (plan?.Status != "Posted" || invoice.Status != expectedState || invoice.Version != expectedVersion ||
            (invoice.PostedVersion ?? plan.SourceVersion + 1) != plan.SourceVersion + 1 ||
            plan.DocumentNumber != $"S-{invoice.Id:N}" || plan.PostedBy is null || plan.PostedAt is null ||
            plan.BusinessDate != invoice.BusinessDate || pending.Length > 1)
            errors.Add("Source and fulfillment posting state/version differ.");
        var lines = plan?.Snapshot.Lines;
        if (plan is null || lines is null || originals.Length != lines.Count || lines.Count != invoice.Lines.Count ||
            plan.Snapshot.NetAmount != invoice.NetAmount || plan.Snapshot.BusinessDate != invoice.BusinessDate ||
            plan.Snapshot.CustomerReference != invoice.CustomerReference || plan.Snapshot.Currency != "BDT" ||
            lines.Select(x => x.LineId).Distinct().Count() != lines.Count ||
            lines.Sum(x => x.NetAmount) != plan.Snapshot.NetAmount || lines.Sum(x => x.OutputTaxAmount) != plan.Snapshot.OutputTaxAmount ||
            plan.Snapshot.NetAmount + plan.Snapshot.OutputTaxAmount != plan.Snapshot.GrossAmount)
            errors.Add("Frozen invoice totals or line coverage differ.");
        if (plan is not null && lines is not null)
            foreach (var line in lines)
            {
                var source = invoice.Lines.SingleOrDefault(x => x.Id == line.LineId);
                var entries = originals.Where(x => x.LineId == line.LineId).ToArray();
                if (source is null || source.Quantity != line.Quantity || source.UnitPrice != line.UnitPrice || source.NetAmount != line.NetAmount ||
                    source.ProductReference != line.ProductReference || entries.Length != 1 || entries[0].Quantity != -line.Quantity ||
                    entries[0].NetAmount != line.NetAmount || entries[0].OutputTaxAmount != line.OutputTaxAmount ||
                    entries[0].WarehouseId != plan.WarehouseId || entries[0].ProductId != line.ProductId || entries[0].TaxRuleId != line.TaxRuleId ||
                    entries[0].BusinessDate != invoice.BusinessDate || entries[0].EntryType != "Goods" || line.OutputTaxAmount !=
                    decimal.Round(line.NetAmount * line.RatePercent / 100, 4, MidpointRounding.AwayFromZero))
                    errors.Add($"Line {line.LineId} differs from its stock/output-tax entry.");
            }
        var prior = new List<SalesPostingEntry>();
        var expectedRequestVersion = (plan?.SourceVersion ?? 0) + 2;
        var expectedEntryCount = 0;
        var lastPostedDate = invoice.BusinessDate;
        foreach (var note in notes)
        {
            if (note.SourceVersion != expectedRequestVersion || (note != latest && note.Status == "Pending"))
                errors.Add("Credit-note history has inconsistent source versions or decisions.");
            expectedRequestVersion = note.SourceVersion + 2;
            if (note.OriginalPostedVersion != plan?.SourceVersion + 1 || note.OriginalDocumentNumber != plan?.DocumentNumber ||
                note.CorrectionDate < lastPostedDate || note.Status is not ("Pending" or "Posted" or "Cancelled") ||
                note.Lines.Count is < 1 or > 200 || note.NetAmount != note.Lines.Sum(x => x.NetAmount) ||
                note.OutputTaxAmount != note.Lines.Sum(x => x.OutputTaxAmount) ||
                (note.Status == "Posted" && (note.DocumentNumber != $"{(note.Mode == "PriceDebit" ? "DN" : "CN")}-{note.Id:N}" || note.PostedAt is null || note.ApprovedBy is null)))
                errors.Add($"Credit note {note.Id} differs from its frozen allocation.");
            try
            {
                var postedHistory = read.History.Where(x => x.Status == "Posted" && x.SourceVersion < note.SourceVersion).ToArray();
                IReadOnlyList<SalesReturnAllocation> expected = note.Mode == "PriceDebit"
                    ? SalesDebitAllocator.Allocate(originals, prior, note.Lines.Select(x => new SalesDebitLineInput(x.LineId, x.NetAmount)).ToArray(),
                        postedHistory, plan?.Snapshot.Lines ?? []).Select(x => new SalesReturnAllocation(x.OriginalEntryId, x.LineId, 0, x.NetAmount, x.OutputTaxAmount)).ToArray()
                    : note.Mode == "PriceOnly"
                    ? SalesPriceAllocator.Allocate(originals, prior, note.Lines.Select(x => new SalesPriceLineInput(x.LineId, -x.NetAmount)).ToArray(), postedHistory)
                    : SalesReturnAllocator.Allocate(originals, prior, note.Lines.Select(x => new SalesReturnLineInput(x.LineId, x.Quantity)).ToArray(), postedHistory);
                if (!expected.SequenceEqual(note.Lines.OrderBy(x => x.LineId))) errors.Add($"Credit note {note.Id} allocation differs from remaining original amounts.");
                var full = expected.Count == originals.Length && expected.All(x => originals.Any(e => e.Id == x.OriginalEntryId && -e.Quantity == x.Quantity && -e.NetAmount == x.NetAmount && -e.OutputTaxAmount == x.OutputTaxAmount));
                if (note.Mode != (note.Mode == "PriceDebit" ? "PriceDebit" : note.Mode == "PriceOnly" ? "PriceOnly" : full ? "FullInvoice" : "PartialReturn") ||
                    ((note.Mode == "PriceOnly" || note.Mode == "PriceDebit") && (string.IsNullOrWhiteSpace(note.AdjustmentEvidenceReference) || note.ReturnReceiptReference != ""))) errors.Add($"Credit note {note.Id} mode is inconsistent.");
            }
            catch (DomainException) { errors.Add($"Credit note {note.Id} exceeds or mismatches its original invoice lines."); }
            var corrections = returns.Where(x => x.AdjustmentId == note.Id).ToArray();
            if (note.Status != "Posted")
            {
                if (corrections.Length > 0) errors.Add("Unposted credit note has financial entries.");
                continue;
            }
            expectedEntryCount += note.Lines.Count;
            if (corrections.Length != note.Lines.Count) errors.Add("Credit-note return entry coverage differs.");
            foreach (var allocation in note.Lines)
            {
                var original = originals.SingleOrDefault(x => x.Id == allocation.OriginalEntryId);
                var rows = corrections.Where(x => x.SourceEntryId == allocation.OriginalEntryId).ToArray();
                if (original is null || rows.Length != 1 || rows[0].LineId != allocation.LineId ||
                    rows[0].WarehouseId != original.WarehouseId || rows[0].ProductId != original.ProductId || rows[0].TaxRuleId != original.TaxRuleId ||
                    rows[0].Quantity != allocation.Quantity || rows[0].NetAmount != allocation.NetAmount || rows[0].OutputTaxAmount != allocation.OutputTaxAmount ||
                    rows[0].BusinessDate != note.CorrectionDate || rows[0].EntryType != (note.Mode == "PriceDebit" ? "PriceDebit" : note.Mode == "PriceOnly" ? "PriceCredit" : "Goods")) errors.Add($"Return for {allocation.OriginalEntryId} differs from the credit note.");
            }
            prior.AddRange(corrections);
            lastPostedDate = note.CorrectionDate;
        }
        if (returns.Length != expectedEntryCount) errors.Add("Orphan or missing return entries exist.");
        return new(invoice.Id, errors.Count == 0, errors);
    }
    private sealed record NoteAudit(Guid Id, long SourceVersion, long OriginalPostedVersion, DateOnly CorrectionDate,
        string Status, string Mode, string OriginalDocumentNumber, string? DocumentNumber, string? ApprovedBy, DateTimeOffset? PostedAt,
        decimal NetAmount, decimal OutputTaxAmount, string ReturnReceiptReference, string? AdjustmentEvidenceReference,
        IReadOnlyList<SalesReturnAllocation> Lines)
    {
        public static NoteAudit From(SalesCreditNote n) => new(n.Id, n.SourceVersion, n.OriginalPostedVersion, n.CorrectionDate,
            n.Status, n.Mode, n.OriginalDocumentNumber, n.DocumentNumber, n.ApprovedBy, n.PostedAt, n.NetAmount, n.OutputTaxAmount,
            n.ReturnReceiptReference, n.AdjustmentEvidenceReference, n.Lines);
        public static NoteAudit From(SalesDebitNote n) => new(n.Id, n.SourceVersion, n.OriginalPostedVersion, n.CorrectionDate,
            n.Status, "PriceDebit", n.OriginalDocumentNumber, n.DocumentNumber, n.ApprovedBy, n.PostedAt, n.NetAmount, n.OutputTaxAmount,
            "", n.EvidenceReference, n.Lines.Select(x => new SalesReturnAllocation(x.OriginalEntryId, x.LineId, 0, x.NetAmount, x.OutputTaxAmount)).ToArray());
    }

}

public sealed class SalesPostingService(ISalesInvoiceStore invoices, ISalesPostingStore store, IReferenceStore references,
    IPeriodGuard periods, PostingPolicy policy, IAuditWriter audit, IEventWriter events, TimeProvider clock)
{
    public async Task<CommandOutcome> Reserve(Actor actor, Guid companyId, Guid id, ReserveSalesRequest request, string correlation, CancellationToken ct)
    {
        var invoice = await Invoice(actor, companyId, id, ct);
        await periods.RequireOpen(actor.TenantId, companyId, invoice.BusinessDate, ct);
        var warehouse = await references.Warehouse(actor.TenantId, companyId, request.WarehouseId, ct);
        if (warehouse is null || warehouse.BranchId != invoice.BranchId)
            throw new DomainException("warehouse_mismatch", "Warehouse must belong to the invoice branch.", 422);
        var lines = new List<SalesCalculatedLine>();
        foreach (var source in invoice.Lines)
        {
            var product = await references.ProductByReference(actor.TenantId, companyId, source.ProductReference, ct)
                ?? throw new DomainException("product_not_found", "Product is not registered.");
            var rule = await references.EffectiveSalesRule(actor.TenantId, companyId, product.TaxCode, invoice.BusinessDate, ct)
                ?? throw new DomainException("sales_tax_rule_missing", "A separately published Sale tax rule is required for every product/date.");
            lines.Add(new(source.Id, product.Id, product.Reference, source.Description, product.UnitOfMeasure,
                source.Quantity, source.UnitPrice, source.NetAmount, rule.Id, rule.Version, rule.TaxCode, rule.RatePercent,
                decimal.Round(source.NetAmount * rule.RatePercent / 100, 4, MidpointRounding.AwayFromZero), rule.LegalBasis, rule.EvidenceReference));
        }
        invoice.Reserve(request.ExpectedVersion, clock.GetUtcNow());
        var plan = SalesFulfillment.Create(invoice, warehouse.Id, actor.Subject, lines, clock.GetUtcNow());
        await store.Reserve(plan, ct);
        audit.Append(actor, companyId, id, "SalesStockReserved", correlation, invoice.Version);
        return new(201, SalesPostingResponse.From(plan), $"/api/v1/companies/{companyId}/sales/{id}/fulfillment");
    }

    public async Task<CommandOutcome> Post(Actor actor, Guid companyId, Guid id, TransitionRequest request, string correlation, CancellationToken ct)
    {
        var invoice = await Invoice(actor, companyId, id, ct);
        await periods.RequireOpen(actor.TenantId, companyId, invoice.BusinessDate, ct);
        if (!policy.Enabled) throw new DomainException("posting_disabled", "Financial posting is disabled.", 409);
        var plan = await store.Find(actor.TenantId, companyId, id, ct)
            ?? throw new DomainException("not_found", "Reservation not found.", 404);
        var now = clock.GetUtcNow();
        plan.Post(invoice, actor.Subject, now);
        invoice.Post(request.ExpectedVersion, now);
        await store.Issue(plan, now, ct);
        audit.Append(actor, companyId, id, "SalesPosted", correlation, invoice.Version);
        events.Append(actor.TenantId, companyId, id, invoice.Version, nameof(SalesPostedV1), correlation,
            new SalesPostedV1(id, plan.WarehouseId, plan.DocumentNumber!, invoice.BusinessDate, "BDT",
                plan.Snapshot.NetAmount, plan.Snapshot.OutputTaxAmount, plan.Snapshot.GrossAmount));
        return new(200, SalesInvoiceResponse.From(invoice));
    }

    public async Task<SalesPostingResponse> Get(Actor actor, Guid companyId, Guid id, CancellationToken ct)
    {
        await Invoice(actor, companyId, id, ct);
        return SalesPostingResponse.From(await store.Find(actor.TenantId, companyId, id, ct)
            ?? throw new DomainException("not_found", "Reservation not found.", 404));
    }

    public async Task<SalesReconciliation> Reconcile(Actor actor, Guid companyId, Guid id, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        return SalesReconciler.Evaluate(await store.Read(actor.TenantId, companyId, id, ct)
            ?? throw new DomainException("not_found", "Invoice not found.", 404));
    }

    public Task<SalesLedgerPage> Ledger(Actor actor, Guid companyId, Guid? before, int take, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        if (take is < 1 or > 100) throw new DomainException("validation_failed", "take must be 1–100.");
        return store.Ledger(actor.TenantId, companyId, before, take, ct);
    }

    public Task<SalesSummary> Summary(Actor actor, Guid companyId, DateOnly from, DateOnly until, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        if (from == default || until <= from) throw new DomainException("validation_failed", "Provide a nonempty [from, until) range.");
        return store.Summary(actor.TenantId, companyId, from, until, ct);
    }

    private async Task<SalesInvoice> Invoice(Actor actor, Guid companyId, Guid id, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        return await invoices.Find(actor.TenantId, companyId, id, ct) ?? throw new DomainException("not_found", "Invoice not found.", 404);
    }
}
