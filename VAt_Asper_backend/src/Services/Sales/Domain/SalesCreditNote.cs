namespace Vat.Domain.Sales;

public sealed class SalesCreditNote
{
    private SalesCreditNote() { }
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid InvoiceId { get; private set; }
    public long SourceVersion { get; private set; }
    public long OriginalPostedVersion { get; private set; }
    public long Version { get; private set; }
    public DateOnly CorrectionDate { get; private set; }
    public string Reason { get; private set; } = "";
    public string ReturnReceiptReference { get; private set; } = "";
    public string? AdjustmentEvidenceReference { get; private set; }
    public string RequestedBy { get; private set; } = "";
    public string? ApprovedBy { get; private set; }
    public string Status { get; private set; } = "Pending";
    public string OriginalDocumentNumber { get; private set; } = "";
    public string? DocumentNumber { get; private set; }
    public string Mode { get; private set; } = "FullInvoice";
    public IReadOnlyList<SalesReturnAllocation> Lines { get; private set; } = [];
    public decimal NetAmount { get; private set; }
    public decimal OutputTaxAmount { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? PostedAt { get; private set; }
    public string? CancellationReason { get; private set; }

    public static SalesCreditNote Request(SalesInvoice invoice, SalesFulfillment plan, DateOnly correctionDate,
        string reason, string returnReceiptReference, string actor, DateTimeOffset now, IReadOnlyList<SalesReturnAllocation> lines, bool priceOnly = false, string? adjustmentEvidence = null)
    {
        if (invoice.Status != SalesInvoiceStatus.CreditPending || plan.Status != "Posted")
            throw new DomainException("invalid_state", "A posted invoice with a pending credit request is required.", 409);
        if (correctionDate == default || correctionDate < invoice.BusinessDate)
            throw new DomainException("validation_failed", "Correction date cannot precede the original invoice date.");
        if (lines.Count is < 1 or > 200 || lines.Select(x => x.LineId).Distinct().Count() != lines.Count ||
            lines.Any(x => (priceOnly ? x.Quantity != 0 || x.NetAmount >= 0 : x.Quantity <= 0) || x.NetAmount > 0 || x.OutputTaxAmount > 0))
            throw new DomainException("validation_failed", "A credit note requires valid frozen return allocations.");
        var full = lines.Count == plan.Snapshot.Lines.Count && lines.All(x => plan.Snapshot.Lines.Any(l => l.LineId == x.LineId && l.Quantity == x.Quantity && x.NetAmount == -l.NetAmount && x.OutputTaxAmount == -l.OutputTaxAmount));
        return new()
        {
            Id = Guid.CreateVersion7(), TenantId = invoice.TenantId, CompanyId = invoice.CompanyId, InvoiceId = invoice.Id,
            SourceVersion = invoice.Version, OriginalPostedVersion = invoice.PostedVersion ?? plan.SourceVersion + 1,
            Version = 1, CorrectionDate = correctionDate, Reason = Guard.Text(reason, nameof(reason), 1000),
            ReturnReceiptReference = priceOnly ? "" : Guard.Text(returnReceiptReference, nameof(returnReceiptReference), 200),
            AdjustmentEvidenceReference = priceOnly ? Guard.Text(adjustmentEvidence, nameof(adjustmentEvidence), 1000) : null,
            RequestedBy = Guard.Text(actor, nameof(actor), 200), OriginalDocumentNumber = plan.DocumentNumber!,
            Mode = priceOnly ? "PriceOnly" : full ? "FullInvoice" : "PartialReturn", Lines = Array.AsReadOnly(lines.ToArray()),
            NetAmount = lines.Sum(x => x.NetAmount), OutputTaxAmount = lines.Sum(x => x.OutputTaxAmount), CreatedAt = now
        };
    }

    public void Approve(string actor, string invoiceMaker, DateTimeOffset now)
    {
        RequirePending();
        if (actor == RequestedBy || actor == invoiceMaker)
            throw new DomainException("self_approval_forbidden", "The requester and invoice maker cannot approve this credit note.", 403);
        ApprovedBy = Guard.Text(actor, nameof(actor), 200);
        Status = "Posted";
        PostedAt = now;
        DocumentNumber = $"CN-{Id:N}";
        Version++;
    }

    public void Cancel(string reason)
    {
        RequirePending();
        CancellationReason = Guard.Text(reason, nameof(reason), 1000);
        Status = "Cancelled";
        Version++;
    }

    private void RequirePending()
    {
        if (Status != "Pending") throw new DomainException("invalid_state", "Only pending credit notes can be decided.", 409);
    }
}
