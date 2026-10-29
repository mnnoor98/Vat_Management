using Vat.Domain.Purchasing;

namespace Vat.Domain.Posting;

public enum ReversalStatus { Pending, Posted, Cancelled }

public sealed class PurchaseReversal
{
    private PurchaseReversal() { }
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid PurchaseId { get; private set; }
    public Guid PlanId { get; private set; }
    public long SourceVersion { get; private set; }
    public long Version { get; private set; }
    public DateOnly CorrectionDate { get; private set; }
    public string Reason { get; private set; } = "";
    public string RequestedBy { get; private set; } = "";
    public string? ApprovedBy { get; private set; }
    public string? CancellationReason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? PostedAt { get; private set; }
    public ReversalStatus Status { get; private set; }

    public static PurchaseReversal Request(Purchase purchase, Guid planId, DateOnly correctionDate,
        string reason, string actor, DateTimeOffset now)
    {
        if (purchase.Status != PurchaseStatus.ReversalPending)
            throw new DomainException("invalid_state", "Purchase must be pending reversal.", 409);
        if (correctionDate < purchase.BusinessDate)
            throw new DomainException("validation_failed", "Correction date cannot precede the original business date.");
        return new()
        {
            Id = Guid.CreateVersion7(), TenantId = purchase.TenantId, CompanyId = purchase.CompanyId, PurchaseId = purchase.Id,
            PlanId = Guard.Id(planId, nameof(planId)), SourceVersion = purchase.Version, CorrectionDate = correctionDate,
            Reason = Guard.Text(reason, nameof(reason), 1000), RequestedBy = Guard.Text(actor, nameof(actor), 200), CreatedAt = now, Version = 1
        };
    }

    public void Authorize(string actor, string purchaseMaker, DateTimeOffset now)
    {
        ExpectPending();
        if (actor == RequestedBy || actor == purchaseMaker)
            throw new DomainException("self_approval_forbidden", "The reversal requester and purchase maker cannot authorize the reversal.", 403);
        ApprovedBy = Guard.Text(actor, nameof(actor), 200);
        Status = ReversalStatus.Posted;
        PostedAt = now;
        Version++;
    }

    public void Cancel(string reason)
    {
        ExpectPending();
        CancellationReason = Guard.Text(reason, nameof(reason), 1000);
        Status = ReversalStatus.Cancelled;
        Version++;
    }

    private void ExpectPending()
    {
        if (Status != ReversalStatus.Pending) throw new DomainException("invalid_state", "Only a pending reversal can be authorized or cancelled.", 409);
    }
}
