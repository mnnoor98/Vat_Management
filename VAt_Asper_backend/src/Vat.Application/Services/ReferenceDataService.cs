using Vat.Application.Contracts;
using Vat.Application.Security;
using Vat.Domain;
using Vat.Domain.Catalog;
using Vat.Domain.Tax;

namespace Vat.Application;

public sealed class ReferenceDataService(IReferenceStore store, ICompanyStore companies, IAuditWriter audit, TimeProvider clock)
{
    public async Task<CommandOutcome> Product(Actor actor, Guid companyId, CreateProductRequest request, string correlationId, CancellationToken ct)
    {
        await RequireCompany(actor, companyId, ct);
        var value = Domain.Catalog.Product.Create(actor.TenantId, companyId, request.Reference, request.Description, request.UnitOfMeasure, request.TaxCode);
        store.Add(value);
        audit.Append(actor, companyId, value.Id, "ProductCreated", correlationId);
        return new(201, ProductResponse.From(value), $"/api/v1/companies/{companyId}/products/{value.Id}");
    }

    public async Task<CommandOutcome> Supplier(Actor actor, Guid companyId, CreateSupplierRequest request, string correlationId, CancellationToken ct)
    {
        await RequireCompany(actor, companyId, ct);
        var value = Domain.Catalog.Supplier.Create(actor.TenantId, companyId, request.Reference, request.Name);
        store.Add(value);
        audit.Append(actor, companyId, value.Id, "SupplierCreated", correlationId);
        return new(201, SupplierResponse.From(value), $"/api/v1/companies/{companyId}/suppliers/{value.Id}");
    }

    public async Task<CommandOutcome> Warehouse(Actor actor, Guid companyId, CreateWarehouseRequest request, string correlationId, CancellationToken ct)
    {
        await RequireCompany(actor, companyId, ct);
        if (await companies.FindBranch(actor.TenantId, companyId, request.BranchId, ct) is null)
            throw new DomainException("not_found", "Branch not found in this company.", 404);
        var value = Domain.Catalog.Warehouse.Create(actor.TenantId, companyId, request.BranchId, request.Code, request.Name);
        store.Add(value);
        audit.Append(actor, companyId, value.Id, "WarehouseCreated", correlationId);
        return new(201, WarehouseResponse.From(value), $"/api/v1/companies/{companyId}/warehouses/{value.Id}");
    }

    public async Task<CommandOutcome> Rule(Actor actor, Guid companyId, CreateTaxRuleRequest request, string correlationId, CancellationToken ct)
    {
        await RequireCompany(actor, companyId, ct);
        var value = TaxRule.Create(actor.TenantId, companyId, request.TaxCode, request.EffectiveFrom, request.EffectiveUntil,
            request.RatePercent, request.RecoverablePercent, request.LegalBasis, request.EvidenceReference, actor.Subject, request.Treatment);
        store.Add(value);
        audit.Append(actor, companyId, value.Id, "TaxRuleDrafted", correlationId, value.Version);
        return new(201, TaxRuleResponse.From(value), $"/api/v1/companies/{companyId}/tax/rules/{value.Id}");
    }

    public async Task<CommandOutcome> Publish(Actor actor, Guid companyId, Guid id, TransitionRequest request, string correlationId, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        var value = await store.Rule(actor.TenantId, companyId, id, ct)
            ?? throw new DomainException("not_found", "Tax rule not found.", 404);
        await store.LockTaxCode(actor.TenantId, companyId, value.TaxCode, ct);
        value.Publish(actor.Subject, request.ExpectedVersion, clock.GetUtcNow());
        if (await store.HasPublishedOverlap(value, ct))
            throw new DomainException("tax_rule_overlap", "A published rule already covers part of this interval for the tax code.", 409);
        audit.Append(actor, companyId, id, "TaxRulePublished", correlationId, value.Version);
        return new(200, TaxRuleResponse.From(value));
    }

    public async Task<ProductResponse> GetProduct(Actor actor, Guid companyId, Guid id, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        return ProductResponse.From(await store.ProductById(actor.TenantId, companyId, id, ct)
            ?? throw new DomainException("not_found", "Product not found.", 404));
    }

    public async Task<SupplierResponse> GetSupplier(Actor actor, Guid companyId, Guid id, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        return SupplierResponse.From(await store.SupplierById(actor.TenantId, companyId, id, ct)
            ?? throw new DomainException("not_found", "Supplier not found.", 404));
    }

    public async Task<WarehouseResponse> GetWarehouse(Actor actor, Guid companyId, Guid id, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        return WarehouseResponse.From(await store.Warehouse(actor.TenantId, companyId, id, ct)
            ?? throw new DomainException("not_found", "Warehouse not found.", 404));
    }

    public async Task<IReadOnlyList<TaxRuleResponse>> GetRules(Actor actor, Guid companyId, int skip, int take, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        if (skip < 0 || skip > 10000 || take < 1 || take > 100)
            throw new DomainException("validation_failed", "Use skip 0..10000 and take 1..100.");
        return (await store.Rules(actor.TenantId, companyId, skip, take, ct)).Select(TaxRuleResponse.From).ToArray();
    }

    public async Task<TaxRuleResponse> GetRule(Actor actor, Guid companyId, Guid id, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        return TaxRuleResponse.From(await store.Rule(actor.TenantId, companyId, id, ct)
            ?? throw new DomainException("not_found", "Tax rule not found.", 404));
    }

    private async Task RequireCompany(Actor actor, Guid companyId, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        if (await companies.FindCompany(actor.TenantId, companyId, ct) is null)
            throw new DomainException("not_found", "Company not found.", 404);
    }
}
