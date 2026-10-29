namespace Vat.Domain.Sales;

public sealed record SalesAdjustmentHistory(Guid Id, long SourceVersion, string Status, string Mode, DateOnly CorrectionDate);
public sealed record SalesDebitLineInput(Guid LineId, decimal NetAmount);
public sealed record SalesDebitAllocation(Guid OriginalEntryId, Guid LineId, decimal NetAmount, decimal OutputTaxAmount);

public sealed class SalesDebitNote
{
    private SalesDebitNote() { }
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid InvoiceId { get; private set; }
    public long SourceVersion { get; private set; }
    public long OriginalPostedVersion { get; private set; }
    public long Version { get; private set; }
    public DateOnly CorrectionDate { get; private set; }
    public string Reason { get; private set; } = "";
    public string EvidenceReference { get; private set; } = "";
    public string RequestedBy { get; private set; } = "";
    public string? ApprovedBy { get; private set; }
    public string Status { get; private set; } = "Pending";
    public string OriginalDocumentNumber { get; private set; } = "";
    public string? DocumentNumber { get; private set; }
    public IReadOnlyList<SalesDebitAllocation> Lines { get; private set; } = [];
    public decimal NetAmount { get; private set; }
    public decimal OutputTaxAmount { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? PostedAt { get; private set; }
    public string? CancellationReason { get; private set; }

    public static SalesDebitNote Request(SalesInvoice invoice, SalesFulfillment plan, DateOnly date,
        string reason, string evidence, string actor, IReadOnlyList<SalesDebitAllocation> lines, DateTimeOffset now)
    {
        if (invoice.Status != SalesInvoiceStatus.DebitPending || plan.Status != "Posted")
            throw new DomainException("invalid_state", "A posted invoice pending debit approval is required.", 409);
        if (date == default || date < invoice.BusinessDate || lines.Count is < 1 or > 200 ||
            lines.Select(x => x.LineId).Distinct().Count() != lines.Count || lines.Any(x => x.NetAmount <= 0 || x.OutputTaxAmount < 0))
            throw new DomainException("validation_failed", "Debit date and positive line allocations must be valid.");
        return new()
        {
            Id = Guid.CreateVersion7(), TenantId = invoice.TenantId, CompanyId = invoice.CompanyId, InvoiceId = invoice.Id,
            SourceVersion = invoice.Version, OriginalPostedVersion = invoice.PostedVersion ?? plan.SourceVersion + 1,
            Version = 1, CorrectionDate = date, Reason = Guard.Text(reason, nameof(reason), 1000),
            EvidenceReference = Guard.Text(evidence, nameof(evidence), 1000), RequestedBy = Guard.Text(actor, nameof(actor), 200),
            OriginalDocumentNumber = plan.DocumentNumber!, Lines = Array.AsReadOnly(lines.ToArray()),
            NetAmount = lines.Sum(x => x.NetAmount), OutputTaxAmount = lines.Sum(x => x.OutputTaxAmount), CreatedAt = now
        };
    }

    public void Approve(string actor, string invoiceMaker, DateTimeOffset now)
    {
        RequirePending();
        if (actor == RequestedBy || actor == invoiceMaker)
            throw new DomainException("self_approval_forbidden", "Requester and invoice maker cannot approve this debit note.", 403);
        ApprovedBy = Guard.Text(actor, nameof(actor), 200);
        Status = "Posted"; PostedAt = now; DocumentNumber = $"DN-{Id:N}"; Version++;
    }

    public void Cancel(string reason)
    {
        RequirePending();
        CancellationReason = Guard.Text(reason, nameof(reason), 1000);
        Status = "Cancelled"; Version++;
    }

    private void RequirePending()
    {
        if (Status != "Pending") throw new DomainException("invalid_state", "Only pending debit notes can be decided.", 409);
    }
}

public static class SalesDebitAllocator
{
    public const decimal MaxLineValue = 1_000_000_000_000_000m;
    public static IReadOnlyList<SalesDebitAllocation> Allocate(IReadOnlyList<SalesPostingEntry> originals,
        IReadOnlyList<SalesPostingEntry> previous, IReadOnlyList<SalesDebitLineInput>? requested,
        IReadOnlyList<SalesAdjustmentHistory> history, IReadOnlyList<SalesCalculatedLine> frozenLines)
    {
        if (requested is null || requested.Count is < 1 or > 200 || requested.Select(x => x?.LineId).Distinct().Count() != requested.Count)
            throw new DomainException("validation_failed", "Select 1-200 distinct original lines for a debit.");
        var versions = history.Where(x => x.Status == "Posted").ToDictionary(x => x.Id, x => x.SourceVersion);
        var result = new List<SalesDebitAllocation>();
        foreach (var input in requested)
        {
            if (input is null || input.LineId == Guid.Empty || input.NetAmount <= 0 || input.NetAmount > MaxLineValue || decimal.Round(input.NetAmount, 4) != input.NetAmount)
                throw new DomainException("validation_failed", "Debit net must be positive, at most 1000000000000000 and have at most four decimal places.");
            var original = originals.SingleOrDefault(x => x.LineId == input.LineId && x.IsOriginal)
                ?? throw new DomainException("debit_line_not_found", "Debit line must belong to this posted invoice.");
            var prior = previous.Where(x => x.SourceEntryId == original.Id).ToArray();
            if (prior.Any(x => x.AdjustmentId is null || !versions.ContainsKey(x.AdjustmentId.Value)))
                throw new DomainException("reconciliation_failed", "Adjustment history is incomplete.", 409);
            if (-original.Quantity <= prior.Sum(x => x.Quantity))
                throw new DomainException("no_retained_goods", "Returned goods cannot receive a price increase.", 409);
            if (original.NetAmount + prior.Sum(x => x.NetAmount) + input.NetAmount > MaxLineValue)
                throw new DomainException("debit_value_exceeded", "Remaining line net value would exceed the supported limit.", 409);
            var boundary = prior.Where(x => x.EntryType != "PriceDebit").Select(x => versions[x.AdjustmentId!.Value]).DefaultIfEmpty(0).Max();
            var consecutive = prior.Where(x => versions[x.AdjustmentId!.Value] > boundary).ToArray();
            var rate = frozenLines.Single(x => x.LineId == input.LineId).RatePercent;
            var cumulative = input.NetAmount + consecutive.Sum(x => x.NetAmount);
            var tax = SalesReturnAllocator.Target(rate, cumulative, 100m) - consecutive.Sum(x => x.OutputTaxAmount);
            if (tax < 0) throw new DomainException("reconciliation_failed", "Prior debit VAT allocation is inconsistent.", 409);
            result.Add(new(original.Id, original.LineId, input.NetAmount, tax));
        }
        return Array.AsReadOnly(result.OrderBy(x => x.LineId).ToArray());
    }
}
