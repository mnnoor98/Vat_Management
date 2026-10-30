using Microsoft.EntityFrameworkCore;
using Vat.Application.Security;
using Vat.Domain;
using Vat.Domain.Catalog;
using Vat.Domain.Posting;

namespace Vat.Infrastructure.Persistence;

public sealed record FinancialWarehouseItem(Guid Id, string Code, string Name, Guid BranchId, string BranchName);
public sealed record FinancialStockItem(Guid WarehouseId, string WarehouseCode, string WarehouseName, Guid BranchId,
    string BranchName, Guid ProductId, string ProductReference, string ProductDescription, string UnitOfMeasure,
    string TaxCode, decimal Quantity, decimal ReservedQuantity, decimal AvailableQuantity);

public sealed class FinancialInventoryQueries(VatDbContext db)
{
    public async Task<IReadOnlyList<FinancialWarehouseItem>> Warehouses(Actor actor, Guid companyId, int skip, int take, CancellationToken ct)
    {
        actor.RequireCompany(companyId); Page(skip, take);
        return await (from warehouse in db.Set<Warehouse>().AsNoTracking()
            join branch in db.Branches.AsNoTracking() on new { warehouse.TenantId, warehouse.CompanyId, Id = warehouse.BranchId }
                equals new { branch.TenantId, branch.CompanyId, branch.Id }
            where warehouse.TenantId == actor.TenantId && warehouse.CompanyId == companyId
            orderby warehouse.Id
            select new FinancialWarehouseItem(warehouse.Id, warehouse.Code, warehouse.Name, branch.Id, branch.Name))
            .Skip(skip).Take(take).ToArrayAsync(ct);
    }

    public async Task<IReadOnlyList<FinancialStockItem>> AllBalances(Actor actor, Guid companyId, Guid? warehouseId, int skip, int take, CancellationToken ct)
    {
        actor.RequireCompany(companyId); Page(skip, take);
        return await (from balance in db.Set<StockBalance>().AsNoTracking()
            join warehouse in db.Set<Warehouse>().AsNoTracking() on new { balance.TenantId, balance.CompanyId, Id = balance.WarehouseId }
                equals new { warehouse.TenantId, warehouse.CompanyId, warehouse.Id }
            join product in db.Set<Product>().AsNoTracking() on new { balance.TenantId, balance.CompanyId, Id = balance.ProductId }
                equals new { product.TenantId, product.CompanyId, product.Id }
            join branch in db.Branches.AsNoTracking() on new { warehouse.TenantId, warehouse.CompanyId, Id = warehouse.BranchId }
                equals new { branch.TenantId, branch.CompanyId, branch.Id }
            where balance.TenantId == actor.TenantId && balance.CompanyId == companyId && (warehouseId == null || balance.WarehouseId == warehouseId)
            orderby balance.WarehouseId, balance.ProductId
            select new FinancialStockItem(warehouse.Id, warehouse.Code, warehouse.Name, branch.Id, branch.Name,
                product.Id, product.Reference, product.Description, product.UnitOfMeasure, product.TaxCode,
                balance.Quantity, balance.ReservedQuantity, balance.Quantity - balance.ReservedQuantity))
            .Skip(skip).Take(take).ToArrayAsync(ct);
    }

    private static void Page(int skip, int take)
    {
        if (skip is < 0 or > 10000 || take is < 1 or > 100)
            throw new DomainException("validation_failed", "Require skip 0-10000 and take 1-100.");
    }
}
