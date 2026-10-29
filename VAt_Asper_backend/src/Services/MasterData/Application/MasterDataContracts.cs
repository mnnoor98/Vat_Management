using Vat.Application.Security;
using Vat.Domain.Catalog;
using Vat.Domain.Sales;

namespace Vat.Services.MasterData
{
    public sealed record CommandOutcome(int StatusCode, object Body, string? Location = null);
    public sealed record StoredResponse(int StatusCode, string Json, string? Location, bool Replayed);

    public interface ICommandExecutor
    {
        Task<StoredResponse> Execute(Actor actor, Guid companyId, string operation, string key,
            object request, Func<Task<CommandOutcome>> action, CancellationToken ct);
    }

    public interface IAuditWriter
    {
        void Append(Actor actor, Guid companyId, Guid resourceId, string action, string correlationId,
            long? version = null, string? reason = null);
    }

    public interface IEventWriter
    {
        void Append<T>(Guid tenantId, Guid companyId, Guid aggregateId, long version,
            string eventType, string correlationId, T payload);
    }

    public interface IMasterDataStore
    {
        Task<Product?> FindProduct(Guid tenantId, Guid companyId, Guid id, CancellationToken ct);
        Task<Product?> FindProductByReference(Guid tenantId, Guid companyId, string reference, CancellationToken ct);
        Task<IReadOnlyList<Product>> ListProducts(Guid tenantId, Guid companyId, CancellationToken ct);
        void AddProduct(Product product);

        Task<Supplier?> FindSupplier(Guid tenantId, Guid companyId, Guid id, CancellationToken ct);
        Task<Supplier?> FindSupplierByReference(Guid tenantId, Guid companyId, string reference, CancellationToken ct);
        Task<IReadOnlyList<Supplier>> ListSuppliers(Guid tenantId, Guid companyId, CancellationToken ct);
        void AddSupplier(Supplier supplier);
    }

    public interface IMasterDataCustomerStore
    {
        Task<Customer?> FindCustomer(Guid tenantId, Guid companyId, Guid id, CancellationToken ct);
        Task<Customer?> FindCustomerByCode(Guid tenantId, Guid companyId, string code, CancellationToken ct);
        Task<IReadOnlyList<Customer>> ListCustomers(Guid tenantId, Guid companyId, Guid? before, int take, CancellationToken ct);
        void AddCustomer(Customer customer);
    }
}

namespace Vat.Services.MasterData.Contracts
{
    public sealed record CreateProductRequest(string Reference, string Description, string UnitOfMeasure, string TaxCode);
    public sealed record ProductResponse(Guid Id, string Reference, string Description, string UnitOfMeasure, string TaxCode)
    {
        public static ProductResponse From(Product product) =>
            new(product.Id, product.Reference, product.Description, product.UnitOfMeasure, product.TaxCode);
    }

    public sealed record CreateSupplierRequest(string Reference, string Name);
    public sealed record SupplierResponse(Guid Id, string Reference, string Name)
    {
        public static SupplierResponse From(Supplier supplier) =>
            new(supplier.Id, supplier.Reference, supplier.Name);
    }

    public sealed record CreateCustomerRequest(string Code, string Name, string? Contact, string? Address, string? TaxRegistrationReference);
    public sealed record UpdateCustomerRequest(long ExpectedVersion, string Name, string? Contact, string? Address, string? TaxRegistrationReference, bool IsActive);
    public sealed record CustomerResponse(Guid Id, long Version, string Code, string Name, string Contact, string Address, string TaxRegistrationReference, bool IsActive, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)
    {
        public static CustomerResponse From(Customer c) =>
            new(c.Id, c.Version, c.Code, c.Name, c.Contact, c.Address, c.TaxRegistrationReference, c.IsActive, c.CreatedAt, c.UpdatedAt);
    }
}

