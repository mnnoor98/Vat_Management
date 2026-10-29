namespace Vat.Services.Identity.Domain;

public sealed class UserRole
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = "";
    public int Version { get; set; } = 1;
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
    public Guid? BranchId { get; set; }
}
