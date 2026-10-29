namespace Vat.Domain.Catalog;

public sealed class Product
{
    private Product() { }
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CompanyId { get; private set; }
    public string Reference { get; private set; } = "";
    public string Description { get; private set; } = "";
    public string UnitOfMeasure { get; private set; } = "";
    public string TaxCode { get; private set; } = "";

    public static Product Create(Guid tenantId, Guid companyId, string reference, string description, string uom, string taxCode) => new()
    {
        Id = Guid.CreateVersion7(), TenantId = Guard.Id(tenantId, nameof(tenantId)), CompanyId = Guard.Id(companyId, nameof(companyId)),
        Reference = Guard.Text(reference, nameof(reference), 100), Description = Guard.Text(description, nameof(description), 500),
        UnitOfMeasure = Guard.Text(uom, nameof(uom), 30), TaxCode = Guard.Text(taxCode, nameof(taxCode), 100)
    };
}

public sealed class Supplier
{
    private Supplier() { }
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CompanyId { get; private set; }
    public string Reference { get; private set; } = "";
    public string Name { get; private set; } = "";

    public static Supplier Create(Guid tenantId, Guid companyId, string reference, string name) => new()
    {
        Id = Guid.CreateVersion7(), TenantId = Guard.Id(tenantId, nameof(tenantId)), CompanyId = Guard.Id(companyId, nameof(companyId)),
        Reference = Guard.Text(reference, nameof(reference), 100), Name = Guard.Text(name, nameof(name), 200)
    };
}

public sealed class Warehouse
{
    private Warehouse() { }
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid BranchId { get; private set; }
    public string Code { get; private set; } = "";
    public string Name { get; private set; } = "";

    public static Warehouse Create(Guid tenantId, Guid companyId, Guid branchId, string code, string name) => new()
    {
        Id = Guid.CreateVersion7(), TenantId = Guard.Id(tenantId, nameof(tenantId)), CompanyId = Guard.Id(companyId, nameof(companyId)),
        BranchId = Guard.Id(branchId, nameof(branchId)), Code = Guard.Text(code, nameof(code), 100), Name = Guard.Text(name, nameof(name), 200)
    };
}
