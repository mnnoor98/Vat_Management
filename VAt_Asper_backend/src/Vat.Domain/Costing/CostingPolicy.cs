namespace Vat.Domain.Costing;

public sealed class CostingPolicy
{
    private CostingPolicy() { }
    public Guid TenantId { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid Id { get; private set; }
    public Guid? ProductId { get; private set; }
    public string Method { get; private set; } = "";
    public DateOnly EffectiveFrom { get; private set; }
    public DateOnly EffectiveUntil { get; private set; }
    public string State { get; private set; } = "Draft";
    public int Version { get; private set; } = 1;
    public string CreatedBy { get; private set; } = "";
    public DateTimeOffset CreatedAt { get; private set; }
    public string? ApprovedBy { get; private set; }
    public DateTimeOffset? ApprovedAt { get; private set; }

    public static CostingPolicy Create(Guid tenant, Guid company, Guid? product, string method, DateOnly from,
        DateOnly until, string actor, DateTimeOffset now)
    {
        var normalized = method?.Trim().ToUpperInvariant();
        if (normalized is not ("FIFO" or "WEIGHTED_AVERAGE"))
            throw new DomainException("validation_failed", "Require FIFO or WEIGHTED_AVERAGE.");
        if (from >= until) throw new DomainException("validation_failed", "EffectiveUntil must be after EffectiveFrom (exclusive).");
        if (product == Guid.Empty) throw new DomainException("validation_failed", "ProductId must be a non-empty UUID or null for a company default.");
        return new() { TenantId = Guard.Id(tenant, nameof(tenant)), CompanyId = Guard.Id(company, nameof(company)),
            Id = Guid.CreateVersion7(), ProductId = product, Method = normalized, EffectiveFrom = from,
            EffectiveUntil = until, CreatedBy = Guard.Text(actor, nameof(actor), 200), CreatedAt = now };
    }

    public void Approve(string actor, int expectedVersion, DateTimeOffset now)
    {
        if (Version != expectedVersion) throw new DomainException("version_conflict", "Reload the policy before deciding.", 409);
        if (State != "Draft") throw new DomainException("invalid_state", "Only draft policies can be approved.", 409);
        if (actor == CreatedBy) throw new DomainException("self_approval_forbidden", "A different actor must approve this policy.", 403);
        ApprovedBy = Guard.Text(actor, nameof(actor), 200); ApprovedAt = now; State = "Approved"; Version++;
    }
}
