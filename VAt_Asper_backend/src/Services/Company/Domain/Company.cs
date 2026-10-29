namespace Vat.Domain.Company;

public sealed class Company
{
    private Company() { }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = "";
    public string RegistrationReference { get; private set; } = "";
    public DateTimeOffset CreatedAt { get; private set; }

    public static Company Create(Guid tenantId, Guid companyId, string name, string registrationReference,
        DateTimeOffset now) => new()
    {
        Id = Guard.Id(companyId, nameof(companyId)),
        TenantId = Guard.Id(tenantId, nameof(tenantId)),
        Name = Guard.Text(name, nameof(name), 200),
        RegistrationReference = Guard.Text(registrationReference, nameof(registrationReference), 100),
        CreatedAt = now
    };
}

public sealed class Branch
{
    private Branch() { }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CompanyId { get; private set; }
    public string Name { get; private set; } = "";
    public DateTimeOffset CreatedAt { get; private set; }

    public static Branch Create(Guid tenantId, Guid companyId, string name, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(),
        TenantId = Guard.Id(tenantId, nameof(tenantId)),
        CompanyId = Guard.Id(companyId, nameof(companyId)),
        Name = Guard.Text(name, nameof(name), 200),
        CreatedAt = now
    };
}
