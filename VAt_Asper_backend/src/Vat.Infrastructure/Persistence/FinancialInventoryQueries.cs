using Microsoft.EntityFrameworkCore;
using Vat.Application.Security;
using Vat.Domain;
using Vat.Domain.Catalog;
using Vat.Domain.Posting;
using System.Data;
using System.Globalization;

namespace Vat.Infrastructure.Persistence;

public sealed record FinancialWarehouseItem(Guid Id, string Code, string Name, Guid BranchId, string BranchName);

public sealed record FinancialStockItem(
    Guid WarehouseId,
    string WarehouseCode,
    string WarehouseName,
    Guid BranchId,
    string BranchName,
    Guid ProductId,
    string ProductReference,
    string ProductDescription,
    string UnitOfMeasure,
    string TaxCode,
    decimal Quantity,
    decimal ReservedQuantity,
    decimal AvailableQuantity,
    decimal? StandardCost,
    decimal? TotalValuation,
    string? HsCode,
    string CostingMethod,
    string ValuationStatus,
    string? ValuationReason,
    string? ValuationBasis = null,
    string? TotalValuationExact = null,
    string? UnitCostExact = null,
    string? QuantityExact = null,
    string? ReservedQuantityExact = null,
    string? AvailableQuantityExact = null
);

public sealed record WarehouseValuationSummary(
    Guid WarehouseId,
    string WarehouseCode,
    string WarehouseName,
    Guid BranchId,
    string BranchName,
    int ItemsCount,
    decimal TotalQuantity,
    decimal? TotalValuation,
    string? TotalValuationExact = null
);

public sealed record InventoryValuationSummary(
    Guid CompanyId,
    string CostingMethod,
    decimal TotalStockQuantity,
    decimal? TotalStockValuation,
    int TotalWarehousesCount,
    int TotalUniqueProductsCount,
    IReadOnlyList<WarehouseValuationSummary> Warehouses,
    IReadOnlyList<FinancialStockItem> TopValuedItems,
    string ValuationStatus,
    string? ValuationReason,
    DateTimeOffset CapturedAt,
    string? ValuationBasis = null,
    string? TotalStockValuationExact = null
);

public sealed class FinancialInventoryQueries(VatDbContext db)
{
    private const string MissingCostJournal = "cost_journal_unavailable";

    public async Task<IReadOnlyList<FinancialWarehouseItem>> Warehouses(Actor actor, Guid companyId, int skip, int take, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        Page(skip, take);

        return await (from warehouse in db.Set<Warehouse>().AsNoTracking()
            join branch in db.Branches.AsNoTracking() on new { warehouse.TenantId, warehouse.CompanyId, Id = warehouse.BranchId }
                equals new { branch.TenantId, branch.CompanyId, branch.Id }
            where warehouse.TenantId == actor.TenantId && warehouse.CompanyId == companyId
            orderby warehouse.Id
            select new FinancialWarehouseItem(warehouse.Id, warehouse.Code, warehouse.Name, branch.Id, branch.Name))
            .Skip(skip).Take(take).ToArrayAsync(ct);
    }

    public async Task<IReadOnlyList<FinancialStockItem>> AllBalances(
        Actor actor, Guid companyId, Guid? warehouseId, string method, int skip, int take, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        Page(skip, take);
        var requestedMethod = Method(method);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var availability = await CostAvailability(actor, companyId, ct);
        var basis = await db.Set<StockCostHead>().Where(x => x.TenantId == actor.TenantId && x.CompanyId == companyId).Select(x => x.Basis).SingleOrDefaultAsync(ct);
        var rows = await Balances(actor, companyId, warehouseId)
            .OrderBy(x => x.WarehouseId).ThenBy(x => x.ProductId)
            .Skip(skip).Take(take)
            .Select(x => new FinancialStockItem(x.WarehouseId, x.WarehouseCode, x.WarehouseName, x.BranchId, x.BranchName,
                x.ProductId, x.ProductReference, x.ProductDescription, x.UnitOfMeasure, x.TaxCode,
                x.Quantity, x.ReservedQuantity, x.Quantity - x.ReservedQuantity,
                availability == null && x.Quantity > 0 ? x.CostValue / x.Quantity : null,
                availability == null ? x.CostValue ?? 0 : x.Quantity == 0 ? (decimal?)0 : null, null, x.CostMethod ?? requestedMethod,
                x.Quantity == 0 ? "NoStock" : availability == null ? "Available" : "Unavailable", x.Quantity == 0 ? null : availability, basis,null,null,null,null,null))
            .ToArrayAsync(ct);
        await tx.CommitAsync(ct); return rows.Select(Exact).ToArray();
    }

    private IQueryable<StockRow> Balances(Actor actor, Guid companyId, Guid? warehouseId) =>
        from balance in db.Set<StockBalance>().AsNoTracking()
        join warehouse in db.Set<Warehouse>().AsNoTracking() on new { balance.TenantId, balance.CompanyId, Id = balance.WarehouseId }
            equals new { warehouse.TenantId, warehouse.CompanyId, warehouse.Id }
        join product in db.Set<Product>().AsNoTracking() on new { balance.TenantId, balance.CompanyId, Id = balance.ProductId }
            equals new { product.TenantId, product.CompanyId, product.Id }
        join branch in db.Branches.AsNoTracking() on new { warehouse.TenantId, warehouse.CompanyId, Id = warehouse.BranchId }
            equals new { branch.TenantId, branch.CompanyId, branch.Id }
        join cost in db.Set<StockCostPosition>().AsNoTracking() on new { balance.TenantId, balance.CompanyId, balance.WarehouseId, balance.ProductId }
            equals new { cost.TenantId, cost.CompanyId, cost.WarehouseId, cost.ProductId } into costs
        from cost in costs.DefaultIfEmpty()
        where balance.TenantId == actor.TenantId && balance.CompanyId == companyId && (warehouseId == null || balance.WarehouseId == warehouseId)
        select new StockRow
        {
            WarehouseId = warehouse.Id, WarehouseCode = warehouse.Code, WarehouseName = warehouse.Name,
            BranchId = branch.Id, BranchName = branch.Name, ProductId = product.Id, ProductReference = product.Reference,
            ProductDescription = product.Description, UnitOfMeasure = product.UnitOfMeasure, TaxCode = product.TaxCode,
            Quantity = balance.Quantity, ReservedQuantity = balance.ReservedQuantity,
            CostValue = cost == null ? null : cost.Value, CostMethod = cost == null ? null : cost.Method
        };

    private sealed class StockRow
    {
        public Guid WarehouseId { get; init; }
        public string WarehouseCode { get; init; } = "";
        public string WarehouseName { get; init; } = "";
        public Guid BranchId { get; init; }
        public string BranchName { get; init; } = "";
        public Guid ProductId { get; init; }
        public string ProductReference { get; init; } = "";
        public string ProductDescription { get; init; } = "";
        public string UnitOfMeasure { get; init; } = "";
        public string TaxCode { get; init; } = "";
        public decimal Quantity { get; init; }
        public decimal ReservedQuantity { get; init; }
        public decimal? CostValue { get; init; }
        public string? CostMethod { get; init; }
    }

    public async Task<InventoryValuationSummary> ValuationSummary(Actor actor, Guid companyId, string method, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        var requestedMethod = Method(method);
        // Multiple aggregates must describe one database snapshot, including concurrent posting.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var availability = await CostAvailability(actor, companyId, ct);
        var basis = await db.Set<StockCostHead>().Where(x => x.TenantId == actor.TenantId && x.CompanyId == companyId).Select(x => x.Basis).SingleOrDefaultAsync(ct);
        var query = Balances(actor, companyId, null);
        var warehouses = await query.GroupBy(x => new { x.WarehouseId, x.WarehouseCode, x.WarehouseName, x.BranchId, x.BranchName })
            .OrderBy(g => g.Key.WarehouseCode).ThenBy(g => g.Key.WarehouseId)
            .Select(g => new WarehouseValuationSummary(g.Key.WarehouseId, g.Key.WarehouseCode, g.Key.WarehouseName,
                g.Key.BranchId, g.Key.BranchName, g.Count(), g.Sum(x => x.Quantity),
                availability == null ? g.Sum(x => x.CostValue ?? 0) : g.All(x => x.Quantity == 0) ? (decimal?)0 : null,null))
            .ToArrayAsync(ct);
        var products = await query.Select(x => x.ProductId).Distinct().CountAsync(ct);
        var unavailable = warehouses.Any(x => x.TotalValuation is null);
        var methods = await query.Where(x => x.CostMethod != null).Select(x => x.CostMethod!).Distinct().ToArrayAsync(ct);
        var top = availability == null ? await query.Where(x => x.Quantity > 0).OrderByDescending(x => x.CostValue).ThenBy(x => x.WarehouseId).ThenBy(x => x.ProductId).Take(10)
            .Select(x => new FinancialStockItem(x.WarehouseId,x.WarehouseCode,x.WarehouseName,x.BranchId,x.BranchName,x.ProductId,x.ProductReference,x.ProductDescription,
                x.UnitOfMeasure,x.TaxCode,x.Quantity,x.ReservedQuantity,x.Quantity-x.ReservedQuantity,x.CostValue/x.Quantity,x.CostValue,null,x.CostMethod!,"Available",null,basis,null,null,null,null,null)).ToArrayAsync(ct)
            : Array.Empty<FinancialStockItem>();
        var result = new InventoryValuationSummary(companyId, methods.Length == 0 ? requestedMethod : methods.Length == 1 ? methods[0] : "MIXED", warehouses.Sum(x => x.TotalQuantity),
            unavailable ? null : warehouses.Sum(x => x.TotalValuation ?? 0), warehouses.Length, products, warehouses.Select(x=>x with {TotalValuationExact=x.TotalValuation?.ToString("0.0000",CultureInfo.InvariantCulture)}).ToArray(), top.Select(Exact).ToArray(),
            unavailable ? "Unavailable" : availability == null ? "Available" : "NoStock", unavailable ? availability : null, DateTimeOffset.UtcNow, basis,
            unavailable ? null : warehouses.Sum(x=>x.TotalValuation??0).ToString("0.0000",CultureInfo.InvariantCulture));
        await transaction.CommitAsync(ct);
        return result;
    }

    private static FinancialStockItem Exact(FinancialStockItem x) => x with {
        TotalValuationExact=x.TotalValuation?.ToString("0.0000",CultureInfo.InvariantCulture),
        UnitCostExact=x.StandardCost?.ToString("0.000000",CultureInfo.InvariantCulture),
        QuantityExact=x.Quantity.ToString("0.000000",CultureInfo.InvariantCulture),
        ReservedQuantityExact=x.ReservedQuantity.ToString("0.000000",CultureInfo.InvariantCulture),
        AvailableQuantityExact=x.AvailableQuantity.ToString("0.000000",CultureInfo.InvariantCulture)
    };

    internal async Task<string?> CostAvailability(Actor actor, Guid company, CancellationToken ct)
    {
        if (!await db.Set<StockCostHead>().AnyAsync(x => x.TenantId == actor.TenantId && x.CompanyId == company, ct)) return MissingCostJournal;
        var stocks = db.Set<StockBalance>().Where(x => x.TenantId == actor.TenantId && x.CompanyId == company);
        var costs = db.Set<StockCostPosition>().Where(x => x.TenantId == actor.TenantId && x.CompanyId == company);
        var entries = db.Set<StockCostEntry>().Where(x => x.TenantId == actor.TenantId && x.CompanyId == company);
        var mismatch = await stocks.AnyAsync(s => s.Quantity != (costs.Where(p => p.WarehouseId == s.WarehouseId && p.ProductId == s.ProductId).Select(p => (decimal?)p.Quantity).FirstOrDefault() ?? 0), ct) ||
            await costs.AnyAsync(p => p.Quantity != (stocks.Where(s => s.WarehouseId == p.WarehouseId && s.ProductId == p.ProductId).Select(s => (decimal?)s.Quantity).FirstOrDefault() ?? 0) ||
                p.Quantity != (entries.Where(e => e.WarehouseId == p.WarehouseId && e.ProductId == p.ProductId).Sum(e => (decimal?)e.Quantity) ?? 0) ||
                p.Value != (entries.Where(e => e.WarehouseId == p.WarehouseId && e.ProductId == p.ProductId).Sum(e => (decimal?)e.Value) ?? 0), ct);
        return mismatch ? "cost_stock_reconciliation_failed" : null;
    }

    private static string Method(string method) => method?.Trim().ToUpperInvariant() switch
    {
        "FIFO" => "FIFO",
        "WEIGHTED_AVERAGE" => "WEIGHTED_AVERAGE",
        _ => throw new DomainException("validation_failed", "Require FIFO or WEIGHTED_AVERAGE costing method.")
    };

    private static void Page(int skip, int take)
    {
        if (skip is < 0 or > 10000 || take is < 1 or > 100)
            throw new DomainException("validation_failed", "Require skip 0-10000 and take 1-100.");
    }
}
