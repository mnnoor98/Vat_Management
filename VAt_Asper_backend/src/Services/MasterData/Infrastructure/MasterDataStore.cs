using Microsoft.EntityFrameworkCore;
using Vat.Domain.Catalog;
using Vat.Domain.Sales;
using Vat.Services.MasterData;

namespace Vat.Services.MasterData.Infrastructure;

public sealed class MasterDataStore(MasterDataDbContext db) : IMasterDataStore, IMasterDataCustomerStore
{
    public Task<Product?> FindProduct(Guid tenantId, Guid companyId, Guid id, CancellationToken ct) => db.Products
        .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.CompanyId == companyId && x.Id == id, ct);

    public Task<Product?> FindProductByReference(Guid tenantId, Guid companyId, string reference, CancellationToken ct) => db.Products
        .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.CompanyId == companyId && x.Reference == reference, ct);

    public async Task<IReadOnlyList<Product>> ListProducts(Guid tenantId, Guid companyId, CancellationToken ct) =>
        await db.Products.Where(x => x.TenantId == tenantId && x.CompanyId == companyId)
            .OrderBy(x => x.Reference).ToArrayAsync(ct);

    public void AddProduct(Product product) => db.Products.Add(product);

    public Task<Supplier?> FindSupplier(Guid tenantId, Guid companyId, Guid id, CancellationToken ct) => db.Suppliers
        .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.CompanyId == companyId && x.Id == id, ct);

    public Task<Supplier?> FindSupplierByReference(Guid tenantId, Guid companyId, string reference, CancellationToken ct) => db.Suppliers
        .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.CompanyId == companyId && x.Reference == reference, ct);

    public async Task<IReadOnlyList<Supplier>> ListSuppliers(Guid tenantId, Guid companyId, CancellationToken ct) =>
        await db.Suppliers.Where(x => x.TenantId == tenantId && x.CompanyId == companyId)
            .OrderBy(x => x.Reference).ToArrayAsync(ct);

    public void AddSupplier(Supplier supplier) => db.Suppliers.Add(supplier);

    public Task<Customer?> FindCustomer(Guid tenantId, Guid companyId, Guid id, CancellationToken ct) => db.Customers
        .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.CompanyId == companyId && x.Id == id, ct);

    public Task<Customer?> FindCustomerByCode(Guid tenantId, Guid companyId, string code, CancellationToken ct) => db.Customers
        .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.CompanyId == companyId && x.Code == code, ct);

    public async Task<IReadOnlyList<Customer>> ListCustomers(Guid tenantId, Guid companyId, Guid? before, int take, CancellationToken ct)
    {
        var query = db.Customers.Where(x => x.TenantId == tenantId && x.CompanyId == companyId);
        if (before is not null) query = query.Where(x => x.Id.CompareTo(before.Value) < 0);
        return await query.OrderByDescending(x => x.Id).Take(take).ToArrayAsync(ct);
    }

    public void AddCustomer(Customer customer) => db.Customers.Add(customer);
}

