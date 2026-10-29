using Vat.Application.Security;
using Vat.Domain;
using Vat.Domain.Catalog;
using Vat.Services.MasterData.Contracts;

namespace Vat.Services.MasterData;

public sealed class MasterDataProductService(IMasterDataStore store, IAuditWriter audit)
{
    public Task<CommandOutcome> CreateProduct(Actor actor, Guid companyId, CreateProductRequest request, string correlationId, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        var product = Product.Create(actor.TenantId, companyId, request.Reference, request.Description, request.UnitOfMeasure, request.TaxCode);
        store.AddProduct(product);
        audit.Append(actor, companyId, product.Id, "ProductCreated", correlationId);
        return Task.FromResult(new CommandOutcome(201, ProductResponse.From(product), $"/api/v1/companies/{companyId}/products/{product.Id}"));
    }

    public async Task<ProductResponse> GetProduct(Actor actor, Guid companyId, Guid id, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        var product = await store.FindProduct(actor.TenantId, companyId, id, ct)
            ?? throw new DomainException("not_found", "Product not found.", 404);
        return ProductResponse.From(product);
    }

    public async Task<IReadOnlyList<ProductResponse>> ListProducts(Actor actor, Guid companyId, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        return (await store.ListProducts(actor.TenantId, companyId, ct)).Select(ProductResponse.From).ToArray();
    }

    public Task<CommandOutcome> CreateSupplier(Actor actor, Guid companyId, CreateSupplierRequest request, string correlationId, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        var supplier = Supplier.Create(actor.TenantId, companyId, request.Reference, request.Name);
        store.AddSupplier(supplier);
        audit.Append(actor, companyId, supplier.Id, "SupplierCreated", correlationId);
        return Task.FromResult(new CommandOutcome(201, SupplierResponse.From(supplier), $"/api/v1/companies/{companyId}/suppliers/{supplier.Id}"));
    }

    public async Task<SupplierResponse> GetSupplier(Actor actor, Guid companyId, Guid id, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        var supplier = await store.FindSupplier(actor.TenantId, companyId, id, ct)
            ?? throw new DomainException("not_found", "Supplier not found.", 404);
        return SupplierResponse.From(supplier);
    }

    public async Task<IReadOnlyList<SupplierResponse>> ListSuppliers(Actor actor, Guid companyId, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        return (await store.ListSuppliers(actor.TenantId, companyId, ct)).Select(SupplierResponse.From).ToArray();
    }
}
