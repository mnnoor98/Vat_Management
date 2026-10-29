namespace Vat.Services.Identity.Domain;

public sealed class IdentityPermission
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = "";
    public int Version { get; set; } = 1;
    public string Code { get; set; } = "";
    public string Description { get; set; } = "";
}
