namespace Vat.Services.Identity.Domain;

public sealed class IdentityUser
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = "";
    public int Version { get; set; } = 1;
    public string Issuer { get; set; } = "";
    public string Subject { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool Disabled { get; set; }
}
