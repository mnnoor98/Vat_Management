namespace Vat.Domain.Purchasing;

public enum PurchaseStatus { Draft, Submitted, ApprovedPendingPosting, Rejected, PostingPrepared, Posted, ReversalPending, Reversed, Cancelled }

public sealed record PurchaseLineInput(string ProductReference, string Description, decimal Quantity, decimal UnitPrice);

public sealed class PurchaseLine
{
    private PurchaseLine() { }
    public Guid Id { get; private set; }
    public Guid PurchaseId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CompanyId { get; private set; }
    public string ProductReference { get; private set; } = "";
    public string Description { get; private set; } = "";
    public decimal Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal NetAmount { get; private set; }

    internal static PurchaseLine Create(Guid purchaseId, Guid tenantId, Guid companyId, PurchaseLineInput input)
    {
        if (input.Quantity <= 0 || input.Quantity > 1_000_000 || decimal.Round(input.Quantity, 6) != input.Quantity)
            throw new DomainException("validation_failed", "Quantity must be positive, at most 1000000 and have at most six decimal places.");
        if (input.UnitPrice < 0 || input.UnitPrice > 1_000_000_000 || decimal.Round(input.UnitPrice, 4) != input.UnitPrice)
            throw new DomainException("validation_failed", "Unit price must be between 0 and 1000000000 with at most four decimal places.");
        return new PurchaseLine
        {
            Id = Guid.CreateVersion7(), PurchaseId = purchaseId, TenantId = tenantId, CompanyId = companyId,
            ProductReference = Guard.Text(input.ProductReference, "productReference", 100),
            Description = Guard.Text(input.Description, "description", 500),
            Quantity = input.Quantity, UnitPrice = input.UnitPrice,
            NetAmount = decimal.Round(input.Quantity * input.UnitPrice, 4, MidpointRounding.AwayFromZero)
        };
    }
}

public sealed class Purchase
{
    private readonly List<PurchaseLine> _lines = [];
    private Purchase() { }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid BranchId { get; private set; }
    public string ExternalReference { get; private set; } = "";
    public string SupplierReference { get; private set; } = "";
    public DateOnly BusinessDate { get; private set; }
    public string Currency { get; private set; } = "BDT";
    public string CreatedBy { get; private set; } = "";
    public string? ApprovedBy { get; private set; }
    public string? RejectionReason { get; private set; }
    public PurchaseStatus Status { get; private set; }
    public long Version { get; private set; }
    public long? PostedVersion { get; private set; }
    public string? CancellationReason { get; private set; }
    public decimal NetAmount { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public IReadOnlyCollection<PurchaseLine> Lines => _lines.AsReadOnly();

    public static Purchase Create(Guid tenantId, Guid companyId, Guid branchId, string externalReference,
        string supplierReference, DateOnly businessDate, string actorId,
        IReadOnlyList<PurchaseLineInput>? lines, DateTimeOffset now)
    {
        if (businessDate == default)
            throw new DomainException("validation_failed", "Business date is required.");
        if (lines is null || lines.Count is < 1 or > 200)
            throw new DomainException("validation_failed", "A purchase must contain 1-200 lines.");
        var purchase = new Purchase
        {
            Id = Guid.CreateVersion7(), TenantId = Guard.Id(tenantId, nameof(tenantId)),
            CompanyId = Guard.Id(companyId, nameof(companyId)), BranchId = Guard.Id(branchId, nameof(branchId)),
            ExternalReference = Guard.Text(externalReference, nameof(externalReference), 100),
            SupplierReference = Guard.Text(supplierReference, nameof(supplierReference), 100),
            BusinessDate = businessDate, CreatedBy = Guard.Text(actorId, nameof(actorId), 200),
            Status = PurchaseStatus.Draft, Version = 1, CreatedAt = now, UpdatedAt = now
        };
        foreach (var input in lines)
        {
            if (input is null) throw new DomainException("validation_failed", "Purchase lines cannot be null.");
            purchase._lines.Add(PurchaseLine.Create(purchase.Id, tenantId, companyId, input));
        }
        purchase.NetAmount = purchase._lines.Sum(x => x.NetAmount);
        return purchase;
    }

    public void Submit(long expectedVersion, DateTimeOffset now)
    {
        Expect(expectedVersion, PurchaseStatus.Draft);
        Status = PurchaseStatus.Submitted;
        Advance(now);
    }

    public void Approve(string actorId, long expectedVersion, DateTimeOffset now)
    {
        Expect(expectedVersion, PurchaseStatus.Submitted);
        if (actorId == CreatedBy)
            throw new DomainException("self_approval_forbidden", "The maker cannot approve their own purchase.", 403);
        ApprovedBy = Guard.Text(actorId, nameof(actorId), 200);
        Status = PurchaseStatus.ApprovedPendingPosting;
        Advance(now);
    }

    public void Reject(string actorId, long expectedVersion, string reason, DateTimeOffset now)
    {
        Expect(expectedVersion, PurchaseStatus.Submitted);
        if (actorId == CreatedBy)
            throw new DomainException("self_approval_forbidden", "The maker cannot decide their own purchase.", 403);
        RejectionReason = Guard.Text(reason, nameof(reason), 1000);
        Status = PurchaseStatus.Rejected;
        Advance(now);
    }

    public void PreparePosting(long expectedVersion, DateTimeOffset now)
    {
        Expect(expectedVersion, PurchaseStatus.ApprovedPendingPosting);
        Status = PurchaseStatus.PostingPrepared;
        Advance(now);
    }

    public void Post(long expectedVersion, DateTimeOffset now)
    {
        Expect(expectedVersion, PurchaseStatus.PostingPrepared);
        Status = PurchaseStatus.Posted;
        Advance(now);
        PostedVersion = Version;
    }

    public void RequestReversal(long expectedVersion, DateTimeOffset now)
    {
        Expect(expectedVersion, PurchaseStatus.Posted);
        Status = PurchaseStatus.ReversalPending;
        Advance(now);
    }

    public void Reverse(long expectedVersion, DateTimeOffset now)
    {
        Expect(expectedVersion, PurchaseStatus.ReversalPending);
        Status = PurchaseStatus.Reversed;
        Advance(now);
    }

    public void CancelReversal(long expectedVersion, DateTimeOffset now)
    {
        Expect(expectedVersion, PurchaseStatus.ReversalPending);
        Status = PurchaseStatus.Posted;
        Advance(now);
    }

    public void CancelUnposted(long expectedVersion, string reason, DateTimeOffset now)
    {
        Expect(expectedVersion, PurchaseStatus.ApprovedPendingPosting);
        CancellationReason = Guard.Text(reason, nameof(reason), 1000);
        Status = PurchaseStatus.Cancelled;
        Advance(now);
    }

    public void CancelPosting(long expectedVersion, DateTimeOffset now)
    {
        Expect(expectedVersion, PurchaseStatus.PostingPrepared);
        Status = PurchaseStatus.ApprovedPendingPosting;
        Advance(now);
    }

    private void Expect(long version, PurchaseStatus status)
    {
        if (Version != version)
            throw new DomainException("version_conflict", "The purchase changed. Reload it before retrying.", 409);
        if (Status != status)
            throw new DomainException("invalid_state", $"This operation requires state {status}.", 409);
    }

    private void Advance(DateTimeOffset now) { Version++; UpdatedAt = now; }
}
