namespace Vat.Domain.Sales;

public sealed record CustomerSnapshot(Guid Id, long Version, string Code, string Name, string Contact,
    string Address, string TaxRegistrationReference);

public sealed class Customer
{
    private Customer() { }
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CompanyId { get; private set; }
    public string Code { get; private set; } = "";
    public string Name { get; private set; } = "";
    public string Contact { get; private set; } = "";
    public string Address { get; private set; } = "";
    public string TaxRegistrationReference { get; private set; } = "";
    public bool IsActive { get; private set; }
    public long Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public static Customer Create(Guid tenant, Guid company, string code, string name, string? contact,
        string? address, string? taxReference, DateTimeOffset now)
    {
        var value = new Customer { Id = Guid.CreateVersion7(), TenantId = Guard.Id(tenant, nameof(tenant)),
            CompanyId = Guard.Id(company, nameof(company)), Code = Guard.Text(code, nameof(code), 100).ToUpperInvariant(),
            Version = 1, IsActive = true, CreatedAt = now, UpdatedAt = now };
        value.Details(name, contact, address, taxReference);
        return value;
    }
    public void Update(long expectedVersion, string name, string? contact, string? address,
        string? taxReference, bool isActive, DateTimeOffset now)
    {
        if (Version != expectedVersion) throw new DomainException("version_conflict", "Reload the customer before retrying.", 409);
        Details(name, contact, address, taxReference);
        IsActive = isActive; Version++; UpdatedAt = now;
    }
    public CustomerSnapshot Snapshot() => new(Id, Version, Code, Name, Contact, Address, TaxRegistrationReference);
    private void Details(string name, string? contact, string? address, string? taxReference)
    {
        Name = Guard.Text(name, nameof(name), 200);
        Contact = Optional(contact, 300); Address = Optional(address, 1000);
        TaxRegistrationReference = Optional(taxReference, 100);
    }
    private static string Optional(string? value, int limit)
    {
        value = value?.Trim() ?? "";
        if (value.Length > limit) throw new DomainException("validation_failed", $"Customer field exceeds {limit} characters.");
        return value;
    }
}
