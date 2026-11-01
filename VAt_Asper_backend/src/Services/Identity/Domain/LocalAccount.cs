namespace Vat.Services.Identity.Domain;

public sealed class LocalAccount
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid BranchId { get; set; }
    public string Username { get; set; } = "";
    public string FullName { get; set; } = "";
    public string FullNameBn { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string[] Roles { get; set; } = [];
    public string[] AssignedModuleCodes { get; set; } = [];
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public string CreatedBy { get; set; } = "";
}
