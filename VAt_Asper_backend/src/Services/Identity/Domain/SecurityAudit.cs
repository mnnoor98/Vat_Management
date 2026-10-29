namespace Vat.Services.Identity.Domain;

public sealed class SecurityAudit
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = "";
    public int Version { get; set; } = 1;
    public Guid? UserId { get; set; }
    public string Action { get; set; } = "";
    public string Resource { get; set; } = "";
    public DateTimeOffset OccurredAt { get; set; }
    public string CorrelationId { get; set; } = "";
    public string Outcome { get; set; } = "";
}
