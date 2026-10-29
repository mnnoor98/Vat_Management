namespace Vat.Domain.Sales;

public sealed record SalesCalculatedLine(Guid LineId, Guid ProductId, string ProductReference, string Description,
    string UnitOfMeasure, decimal Quantity, decimal UnitPrice, decimal NetAmount, Guid TaxRuleId, long TaxRuleVersion,
    string TaxCode, decimal RatePercent, decimal OutputTaxAmount, string LegalBasis, string EvidenceReference);

public sealed record SalesCalculationSnapshot(string Algorithm, string Currency, string CustomerReference,
    DateOnly BusinessDate, IReadOnlyList<SalesCalculatedLine> Lines, decimal NetAmount, decimal OutputTaxAmount,
    decimal GrossAmount);

public sealed class SalesFulfillment
{
    private SalesFulfillment() { }
    public Guid TenantId { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid InvoiceId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public DateOnly BusinessDate { get; private set; }
    public long SourceVersion { get; private set; }
    public long Version { get; private set; }
    public string Status { get; private set; } = "Reserved";
    public string ReservedBy { get; private set; } = "";
    public string? PostedBy { get; private set; }
    public string? DocumentNumber { get; private set; }
    public DateTimeOffset ReservedAt { get; private set; }
    public DateTimeOffset? PostedAt { get; private set; }
    public SalesCalculationSnapshot Snapshot { get; private set; } = null!;

    public static SalesFulfillment Create(SalesInvoice invoice, Guid warehouseId, string actor,
        IReadOnlyList<SalesCalculatedLine> lines, DateTimeOffset now)
    {
        if (invoice.Status != SalesInvoiceStatus.Reserved || lines.Count != invoice.Lines.Count ||
            lines.Select(x => x.LineId).Distinct().Count() != lines.Count ||
            !lines.Select(x => x.LineId).ToHashSet().SetEquals(invoice.Lines.Select(x => x.Id)))
            throw new DomainException("snapshot_mismatch", "The reservation must cover every invoice line exactly once.", 409);
        var net = lines.Sum(x => x.NetAmount);
        var tax = lines.Sum(x => x.OutputTaxAmount);
        return new()
        {
            TenantId = invoice.TenantId, CompanyId = invoice.CompanyId, InvoiceId = invoice.Id,
            WarehouseId = Guard.Id(warehouseId, nameof(warehouseId)), BusinessDate = invoice.BusinessDate, SourceVersion = invoice.Version, Version = 1,
            ReservedBy = Guard.Text(actor, nameof(actor), 200), ReservedAt = now,
            Snapshot = new("domestic-sales-exclusive-v1", "BDT", invoice.CustomerReference,
                invoice.BusinessDate, Array.AsReadOnly(lines.ToArray()), net, tax, net + tax)
        };
    }

    public void Post(SalesInvoice invoice, string actor, DateTimeOffset now)
    {
        if (Status != "Reserved" || invoice.Version != SourceVersion || invoice.Status != SalesInvoiceStatus.Reserved)
            throw new DomainException("invalid_state", "An active matching reservation is required.", 409);
        if (actor == invoice.CreatedBy || actor == ReservedBy)
            throw new DomainException("self_approval_forbidden", "The maker or reservation preparer cannot post this invoice.", 403);
        Status = "Posted";
        PostedBy = Guard.Text(actor, nameof(actor), 200);
        PostedAt = now;
        DocumentNumber = $"S-{InvoiceId:N}";
        Version++;
    }

    public void Cancel()
    {
        if (Status != "Reserved") throw new DomainException("invalid_state", "Only reserved stock can be released.", 409);
        Status = "Cancelled";
        Version++;
    }
}

// Immutable sales journal: goods rows carry stock quantities; price credits carry zero quantity. No input tax is stored here.
public sealed class SalesPostingEntry
{
    private SalesPostingEntry() { }
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid InvoiceId { get; private set; }
    public Guid LineId { get; private set; }
    public string EntryType { get; private set; } = "Goods";
    public Guid? ReversesEntryId { get; private set; }
    public Guid? CreditNoteId { get; private set; }
    public Guid? DebitNoteId { get; private set; }
    public Guid? AdjustsEntryId { get; private set; }
    public bool IsOriginal => CreditNoteId is null && DebitNoteId is null;
    public Guid? SourceEntryId => ReversesEntryId ?? AdjustsEntryId;
    public Guid? AdjustmentId => CreditNoteId ?? DebitNoteId;
    public Guid WarehouseId { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid TaxRuleId { get; private set; }
    public DateOnly BusinessDate { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal NetAmount { get; private set; }
    public decimal OutputTaxAmount { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static SalesPostingEntry Return(SalesPostingEntry original, SalesCreditNote note, SalesReturnAllocation allocation, DateTimeOffset now)
    {
        if (!original.IsOriginal || note.Status != "Posted" || original.InvoiceId != note.InvoiceId ||
            original.TenantId != note.TenantId || original.CompanyId != note.CompanyId || allocation.OriginalEntryId != original.Id || allocation.LineId != original.LineId ||
            (note.Mode == "PriceOnly" ? allocation.Quantity != 0 || allocation.NetAmount >= 0 : allocation.Quantity <= 0) || allocation.Quantity > -original.Quantity || allocation.NetAmount > 0 || allocation.OutputTaxAmount > 0)
            throw new DomainException("invalid_state", "Only original entries of the credited invoice can be returned.", 409);
        return new()
        {
            Id = Guid.CreateVersion7(), TenantId = original.TenantId, CompanyId = original.CompanyId,
            InvoiceId = original.InvoiceId, LineId = original.LineId, WarehouseId = original.WarehouseId,
            ProductId = original.ProductId, TaxRuleId = original.TaxRuleId, BusinessDate = note.CorrectionDate,
            EntryType = note.Mode == "PriceOnly" ? "PriceCredit" : "Goods",
            Quantity = allocation.Quantity, NetAmount = allocation.NetAmount, OutputTaxAmount = allocation.OutputTaxAmount,
            ReversesEntryId = original.Id, CreditNoteId = note.Id, CreatedAt = now
        };
    }

    public static SalesPostingEntry Debit(SalesPostingEntry original, SalesDebitNote note, SalesDebitAllocation line, DateTimeOffset now)
    {
        if (!original.IsOriginal || note.Status != "Posted" || original.InvoiceId != note.InvoiceId || original.TenantId != note.TenantId ||
            original.CompanyId != note.CompanyId || line.OriginalEntryId != original.Id || line.LineId != original.LineId || line.NetAmount <= 0 || line.OutputTaxAmount < 0)
            throw new DomainException("invalid_state", "Debit must reference a matching original invoice entry.", 409);
        return new()
        {
            Id = Guid.CreateVersion7(), TenantId = original.TenantId, CompanyId = original.CompanyId, InvoiceId = original.InvoiceId,
            LineId = original.LineId, WarehouseId = original.WarehouseId, ProductId = original.ProductId, TaxRuleId = original.TaxRuleId,
            BusinessDate = note.CorrectionDate, Quantity = 0, NetAmount = line.NetAmount, OutputTaxAmount = line.OutputTaxAmount,
            EntryType = "PriceDebit", DebitNoteId = note.Id, AdjustsEntryId = original.Id, CreatedAt = now
        };
    }

    public static SalesPostingEntry Issue(SalesFulfillment plan, SalesCalculatedLine line, DateTimeOffset now)
    {
        if (plan.Status != "Posted") throw new DomainException("invalid_state", "Posting authorization is required.", 409);
        return new()
        {
            Id = Guid.CreateVersion7(), TenantId = plan.TenantId, CompanyId = plan.CompanyId,
            InvoiceId = plan.InvoiceId, LineId = line.LineId, WarehouseId = plan.WarehouseId,
            ProductId = line.ProductId, TaxRuleId = line.TaxRuleId, BusinessDate = plan.Snapshot.BusinessDate,
            Quantity = -line.Quantity, NetAmount = line.NetAmount, OutputTaxAmount = line.OutputTaxAmount, CreatedAt = now
        };
    }
}
