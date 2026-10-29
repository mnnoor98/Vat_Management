namespace Vat.Domain.Sales;

public enum SalesInvoiceStatus { Draft, Submitted, ApprovedPendingPosting, Rejected, Cancelled, Reserved, Posted, CreditPending, Credited, PartiallyCredited, DebitPending }

public sealed record SalesInvoiceLineInput(string ProductReference, string Description, decimal Quantity, decimal UnitPrice);

public sealed class SalesInvoiceLine
{
    private SalesInvoiceLine() { }
    public Guid Id { get; private set; }
    public Guid SalesInvoiceId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CompanyId { get; private set; }
    public string ProductReference { get; private set; } = "";
    public string Description { get; private set; } = "";
    public decimal Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal NetAmount { get; private set; }

    internal static SalesInvoiceLine Create(Guid invoiceId, Guid tenantId, Guid companyId, SalesInvoiceLineInput input)
    {
        if (input.Quantity <= 0 || input.Quantity > 1_000_000 || decimal.Round(input.Quantity, 6) != input.Quantity)
            throw new DomainException("validation_failed", "Quantity must be positive, at most 1000000 and have at most six decimal places.");
        if (input.UnitPrice < 0 || input.UnitPrice > 1_000_000_000 || decimal.Round(input.UnitPrice, 4) != input.UnitPrice)
            throw new DomainException("validation_failed", "Unit price must be between 0 and 1000000000 with at most four decimal places.");
        return new SalesInvoiceLine
        {
            Id = Guid.CreateVersion7(), SalesInvoiceId = invoiceId, TenantId = tenantId, CompanyId = companyId,
            ProductReference = Guard.Text(input.ProductReference, "productReference", 100),
            Description = Guard.Text(input.Description, "description", 500),
            Quantity = input.Quantity, UnitPrice = input.UnitPrice,
            NetAmount = decimal.Round(input.Quantity * input.UnitPrice, 4, MidpointRounding.AwayFromZero)
        };
    }
}

public sealed class SalesInvoice
{
    private readonly List<SalesInvoiceLine> _lines = [];
    private SalesInvoice() { }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid BranchId { get; private set; }
    public string ExternalReference { get; private set; } = "";
    public string CustomerReference { get; private set; } = "";
    public Guid? CustomerId { get; private set; }
    public CustomerSnapshot? CustomerSnapshot { get; private set; }
    public DateOnly BusinessDate { get; private set; }
    public DateOnly? DueDate { get; private set; }
    public string Currency { get; private set; } = "BDT";
    public string CreatedBy { get; private set; } = "";
    public string? ApprovedBy { get; private set; }
    public string? RejectionReason { get; private set; }
    public SalesInvoiceStatus Status { get; private set; }
    public long Version { get; private set; }
    public long? PostedVersion { get; private set; }
    public string? CancellationReason { get; private set; }
    public decimal NetAmount { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public IReadOnlyCollection<SalesInvoiceLine> Lines => _lines.AsReadOnly();

    public static SalesInvoice Create(Guid tenantId, Guid companyId, Guid branchId, string externalReference,
        string customerReference, DateOnly businessDate, string actorId,
        IReadOnlyList<SalesInvoiceLineInput>? lines, DateTimeOffset now, CustomerSnapshot? customer = null, DateOnly? dueDate = null)
    {
        if (businessDate == default)
            throw new DomainException("validation_failed", "Business date is required.");
        if (dueDate is not null && dueDate < businessDate)
            throw new DomainException("validation_failed", "Due date cannot precede the invoice business date.");
        if (lines is null || lines.Count is < 1 or > 200)
            throw new DomainException("validation_failed", "An invoice must contain 1-200 lines.");
        var invoice = new SalesInvoice
        {
            Id = Guid.CreateVersion7(), TenantId = Guard.Id(tenantId, nameof(tenantId)),
            CompanyId = Guard.Id(companyId, nameof(companyId)), BranchId = Guard.Id(branchId, nameof(branchId)),
            ExternalReference = Guard.Text(externalReference, nameof(externalReference), 100),
            CustomerReference = Guard.Text(customerReference, nameof(customerReference), 100),
            CustomerId = customer?.Id, CustomerSnapshot = customer,
            BusinessDate = businessDate, DueDate = dueDate, CreatedBy = Guard.Text(actorId, nameof(actorId), 200),
            Status = SalesInvoiceStatus.Draft, Version = 1, CreatedAt = now, UpdatedAt = now
        };
        foreach (var input in lines)
        {
            if (input is null) throw new DomainException("validation_failed", "Invoice lines cannot be null.");
            invoice._lines.Add(SalesInvoiceLine.Create(invoice.Id, tenantId, companyId, input));
        }
        invoice.NetAmount = invoice._lines.Sum(x => x.NetAmount);
        return invoice;
    }

    public void Submit(long expectedVersion, DateTimeOffset now)
    {
        Expect(expectedVersion, SalesInvoiceStatus.Draft);
        Status = SalesInvoiceStatus.Submitted;
        Advance(now);
    }

    public void Approve(string actorId, long expectedVersion, DateTimeOffset now)
    {
        Expect(expectedVersion, SalesInvoiceStatus.Submitted);
        if (actorId == CreatedBy)
            throw new DomainException("self_approval_forbidden", "The maker cannot approve their own invoice.", 403);
        ApprovedBy = Guard.Text(actorId, nameof(actorId), 200);
        Status = SalesInvoiceStatus.ApprovedPendingPosting;
        Advance(now);
    }

    public void Reject(string actorId, long expectedVersion, string reason, DateTimeOffset now)
    {
        Expect(expectedVersion, SalesInvoiceStatus.Submitted);
        if (actorId == CreatedBy)
            throw new DomainException("self_approval_forbidden", "The maker cannot decide their own invoice.", 403);
        RejectionReason = Guard.Text(reason, nameof(reason), 1000);
        Status = SalesInvoiceStatus.Rejected;
        Advance(now);
    }

    public void Reserve(long expectedVersion, DateTimeOffset now)
    {
        Expect(expectedVersion, SalesInvoiceStatus.ApprovedPendingPosting);
        Status = SalesInvoiceStatus.Reserved;
        Advance(now);
    }

    public void Post(long expectedVersion, DateTimeOffset now)
    {
        Expect(expectedVersion, SalesInvoiceStatus.Reserved);
        Status = SalesInvoiceStatus.Posted;
        Advance(now);
        PostedVersion = Version;
    }

    public void RequestDebit(long expectedVersion, DateTimeOffset now)
    {
        if (Status is not (SalesInvoiceStatus.Posted or SalesInvoiceStatus.PartiallyCredited))
            throw new DomainException("invalid_state", "Only posted invoices with retained goods can receive a debit.", 409);
        if (Version != expectedVersion) throw new DomainException("version_conflict", "The invoice changed. Reload it before retrying.", 409);
        Status = SalesInvoiceStatus.DebitPending;
        Advance(now);
    }

    public void DecideDebit(long expectedVersion, DateTimeOffset now, bool hasPostedCredits)
    {
        Expect(expectedVersion, SalesInvoiceStatus.DebitPending);
        Status = hasPostedCredits ? SalesInvoiceStatus.PartiallyCredited : SalesInvoiceStatus.Posted;
        Advance(now);
    }

    public void RequestCredit(long expectedVersion, DateTimeOffset now)
    {
        if (Status is not (SalesInvoiceStatus.Posted or SalesInvoiceStatus.PartiallyCredited))
            throw new DomainException("invalid_state", "Only a posted invoice with remaining quantities can be credited.", 409);
        if (Version != expectedVersion) throw new DomainException("version_conflict", "The invoice changed. Reload it before retrying.", 409);
        Status = SalesInvoiceStatus.CreditPending;
        Advance(now);
    }

    public void Credit(long expectedVersion, DateTimeOffset now, bool fullyReturned = true)
    {
        Expect(expectedVersion, SalesInvoiceStatus.CreditPending);
        Status = fullyReturned ? SalesInvoiceStatus.Credited : SalesInvoiceStatus.PartiallyCredited;
        Advance(now);
    }

    public void CancelCredit(long expectedVersion, DateTimeOffset now, bool hasPostedCredits = false)
    {
        Expect(expectedVersion, SalesInvoiceStatus.CreditPending);
        Status = hasPostedCredits ? SalesInvoiceStatus.PartiallyCredited : SalesInvoiceStatus.Posted;
        Advance(now);
    }

    public void Cancel(long expectedVersion, string reason, DateTimeOffset now)
    {
        if (Version != expectedVersion)
            throw new DomainException("version_conflict", "The invoice changed. Reload it before retrying.", 409);
        if (Status is not (SalesInvoiceStatus.Draft or SalesInvoiceStatus.Submitted or SalesInvoiceStatus.ApprovedPendingPosting or SalesInvoiceStatus.Reserved))
            throw new DomainException("invalid_state", "Only an unposted, active invoice can be cancelled.", 409);
        CancellationReason = Guard.Text(reason, nameof(reason), 1000);
        Status = SalesInvoiceStatus.Cancelled;
        Advance(now);
    }

    private void Expect(long version, SalesInvoiceStatus status)
    {
        if (Version != version)
            throw new DomainException("version_conflict", "The invoice changed. Reload it before retrying.", 409);
        if (Status != status)
            throw new DomainException("invalid_state", $"This operation requires state {status}.", 409);
    }

    private void Advance(DateTimeOffset now) { Version++; UpdatedAt = now; }
}
