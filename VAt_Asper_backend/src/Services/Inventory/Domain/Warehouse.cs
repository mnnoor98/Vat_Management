using Vat.Domain;

namespace Vat.Services.Inventory;

public sealed class Warehouse
{
    public Warehouse() { }
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid BranchId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";

    public static Warehouse Create(Guid tenantId, Guid companyId, Guid branchId, string code, string name)
    {
        Guard.Id(tenantId, nameof(tenantId));
        Guard.Id(companyId, nameof(companyId));
        Guard.Id(branchId, nameof(branchId));
        code = Guard.Text(code, nameof(code), 100);
        name = Guard.Text(name, nameof(name), 200);

        return new Warehouse
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            CompanyId = companyId,
            BranchId = branchId,
            Code = code,
            Name = name
        };
    }
}

