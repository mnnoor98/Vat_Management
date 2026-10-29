namespace Vat.Domain.Tax;

public sealed class TaxRule
{
    private TaxRule() { }
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CompanyId { get; private set; }
    public string Treatment { get; private set; } = "Purchase";
    public string TaxCode { get; private set; } = "";
    public DateOnly EffectiveFrom { get; private set; }
    public DateOnly EffectiveUntil { get; private set; }
    public decimal RatePercent { get; private set; }
    public decimal RecoverablePercent { get; private set; }
    public string LegalBasis { get; private set; } = "";
    public string EvidenceReference { get; private set; } = "";
    public string CreatedBy { get; private set; } = "";
    public string? PublishedBy { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }
    public long Version { get; private set; }

    public static TaxRule Create(Guid tenantId, Guid companyId, string taxCode, DateOnly from, DateOnly until,
        decimal rate, decimal recoverablePercent, string legalBasis, string evidenceReference, string actor, string treatment = "Purchase") 
    {
        if (from == default || until <= from)
            throw new DomainException("validation_failed", "Rule dates must define a non-empty [from, until) interval.");
        if (treatment is not ("Purchase" or "Sale") || (treatment == "Sale" && recoverablePercent != 0))
            throw new DomainException("validation_failed", "Treatment must be Purchase or Sale; sales rules require zero recoverability.");
        ValidatePercent(rate, "ratePercent");
        ValidatePercent(recoverablePercent, "recoverablePercent");
        return new()
        {
            Id = Guid.CreateVersion7(), TenantId = Guard.Id(tenantId, nameof(tenantId)), CompanyId = Guard.Id(companyId, nameof(companyId)),
            TaxCode = Guard.Text(taxCode, nameof(taxCode), 100), EffectiveFrom = from, EffectiveUntil = until,
            Treatment = treatment, RatePercent = rate, RecoverablePercent = recoverablePercent,
            LegalBasis = Guard.Text(legalBasis, nameof(legalBasis), 2000), EvidenceReference = Guard.Text(evidenceReference, nameof(evidenceReference), 1000),
            CreatedBy = Guard.Text(actor, nameof(actor), 200), Version = 1
        };
    }

    public void Publish(string actor, long expectedVersion, DateTimeOffset now)
    {
        if (Version != expectedVersion || PublishedAt is not null)
            throw new DomainException("version_conflict", "The rule changed or was already published.", 409);
        if (actor == CreatedBy)
            throw new DomainException("self_approval_forbidden", "A rule maker cannot publish their own rule.", 403);
        PublishedBy = Guard.Text(actor, nameof(actor), 200);
        PublishedAt = now;
        Version++;
    }

    private static void ValidatePercent(decimal value, string field)
    {
        if (value is < 0 or > 100 || decimal.Round(value, 4) != value)
            throw new DomainException("validation_failed", $"{field} must be between 0 and 100 with at most four decimal places.");
    }
}
