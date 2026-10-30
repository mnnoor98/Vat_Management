using Microsoft.EntityFrameworkCore;
using Vat.Application.Security;
using Vat.Domain;
using Vat.Domain.Catalog;
using Vat.Domain.Posting;
using Vat.Domain.Purchasing;

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
    decimal StandardCost,
    decimal TotalValuation,
    string HsCode,
    string CostingMethod
);

public sealed record WarehouseValuationSummary(
    Guid WarehouseId,
    string WarehouseCode,
    string WarehouseName,
    Guid BranchId,
    string BranchName,
    int ItemsCount,
    decimal TotalQuantity,
    decimal TotalValuation
);

public sealed record InventoryValuationSummary(
    Guid CompanyId,
    string CostingMethod,
    decimal TotalStockQuantity,
    decimal TotalStockValuation,
    int TotalWarehousesCount,
    int TotalUniqueProductsCount,
    IReadOnlyList<WarehouseValuationSummary> Warehouses,
    IReadOnlyList<FinancialStockItem> TopValuedItems
);

public sealed class FinancialInventoryQueries(VatDbContext db)
{
    private static readonly Dictionary<string, string> NbrHsCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["PROD-01"] = "3901.20.90",      // High Density Polyethylene Resin (HDPE)
        ["PROD-02"] = "3902.10.00",      // Polypropylene Polymer Granules (PP)
        ["PROD-RAW-01"] = "3824.99.99",  // Raw Material Chemical Additive
        ["PROD-PACK-01"] = "3923.21.00", // Finished Consumer Food Packaging Roll
        ["PRD-FG-001"] = "3402.20.00",   // 500ml Bottled Consumer Liquid Soap
        ["PROD-03"] = "2201.10.00",      // Bottled Pure Water
        ["PROD-04"] = "1514.91.00"       // Mustard Oil 1 Litre Bottle
    };

    private static readonly Dictionary<string, decimal> FallbackStandardCosts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["PROD-01"] = 140.00m,
        ["PROD-02"] = 95.00m,
        ["PROD-RAW-01"] = 80.00m,
        ["PROD-PACK-01"] = 120.00m,
        ["PRD-FG-001"] = 140.00m,
        ["PROD-03"] = 180.00m,
        ["PROD-04"] = 185.00m
    };

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
        Actor actor,
        Guid companyId,
        Guid? warehouseId,
        string method,
        int skip,
        int take,
        CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        Page(skip, take);

        var isFifo = method.Equals("FIFO", StringComparison.OrdinalIgnoreCase);
        var costingMethod = isFifo ? "FIFO" : "WEIGHTED_AVERAGE";

        // Query raw balances joined with warehouse, product, and branch
        var rawBalances = await (from balance in db.Set<StockBalance>().AsNoTracking()
            join warehouse in db.Set<Warehouse>().AsNoTracking() on new { balance.TenantId, balance.CompanyId, Id = balance.WarehouseId }
                equals new { warehouse.TenantId, warehouse.CompanyId, warehouse.Id }
            join product in db.Set<Product>().AsNoTracking() on new { balance.TenantId, balance.CompanyId, Id = balance.ProductId }
                equals new { product.TenantId, product.CompanyId, product.Id }
            join branch in db.Branches.AsNoTracking() on new { warehouse.TenantId, warehouse.CompanyId, Id = warehouse.BranchId }
                equals new { branch.TenantId, branch.CompanyId, branch.Id }
            where balance.TenantId == actor.TenantId && balance.CompanyId == companyId && (warehouseId == null || balance.WarehouseId == warehouseId)
            orderby balance.WarehouseId, balance.ProductId
            select new
            {
                balance.WarehouseId,
                WarehouseCode = warehouse.Code,
                WarehouseName = warehouse.Name,
                BranchId = branch.Id,
                BranchName = branch.Name,
                ProductId = product.Id,
                ProductReference = product.Reference,
                ProductDescription = product.Description,
                product.UnitOfMeasure,
                product.TaxCode,
                balance.Quantity,
                balance.ReservedQuantity,
                AvailableQuantity = balance.Quantity - balance.ReservedQuantity
            })
            .Skip(skip).Take(take).ToListAsync(ct);

        if (rawBalances.Count == 0)
        {
            return Array.Empty<FinancialStockItem>();
        }

        // Distinct product references on this page to fetch purchase costing records
        var productRefs = rawBalances.Select(x => x.ProductReference).Distinct().ToList();

        // Load purchase receipts for these items (excluding Cancelled/Rejected)
        var purchases = await (from pl in db.Set<PurchaseLine>().AsNoTracking()
            join p in db.Purchases.AsNoTracking() on new { pl.TenantId, pl.CompanyId, Id = pl.PurchaseId } equals new { p.TenantId, p.CompanyId, p.Id }
            where pl.TenantId == actor.TenantId && pl.CompanyId == companyId
                  && productRefs.Contains(pl.ProductReference)
                  && p.Status != PurchaseStatus.Cancelled && p.Status != PurchaseStatus.Rejected
            orderby p.BusinessDate descending, pl.Id descending
            select new
            {
                pl.ProductReference,
                pl.Quantity,
                pl.UnitPrice,
                pl.NetAmount,
                p.BusinessDate
            }).ToListAsync(ct);

        var purchasesByProduct = purchases
            .GroupBy(x => x.ProductReference, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var items = new List<FinancialStockItem>(rawBalances.Count);

        foreach (var row in rawBalances)
        {
            purchasesByProduct.TryGetValue(row.ProductReference, out var productPurchases);

            var hsCode = NbrHsCodes.TryGetValue(row.ProductReference, out var code) ? code : "3901.20.90";
            var defaultCost = FallbackStandardCosts.TryGetValue(row.ProductReference, out var dCost) ? dCost : 100.00m;

            decimal unitCost;
            decimal totalValuation;

            if (row.AvailableQuantity <= 0)
            {
                unitCost = productPurchases != null && productPurchases.Count > 0 ? productPurchases[0].UnitPrice : defaultCost;
                totalValuation = 0m;
            }
            else if (isFifo)
            {
                // FIFO ending inventory valuation: recent purchases cover ending stock
                if (productPurchases != null && productPurchases.Count > 0)
                {
                    decimal remainingQty = row.AvailableQuantity;
                    decimal accumulatedValuation = 0m;

                    foreach (var batch in productPurchases)
                    {
                        var takeQty = Math.Min(remainingQty, batch.Quantity);
                        accumulatedValuation += takeQty * batch.UnitPrice;
                        remainingQty -= takeQty;
                        if (remainingQty <= 0) break;
                    }

                    if (remainingQty > 0)
                    {
                        // Any remaining quantity covered by earliest available batch or standard cost
                        var fallbackPrice = productPurchases[^1].UnitPrice;
                        accumulatedValuation += remainingQty * fallbackPrice;
                    }

                    totalValuation = decimal.Round(accumulatedValuation, 2, MidpointRounding.AwayFromZero);
                    unitCost = decimal.Round(accumulatedValuation / row.AvailableQuantity, 4, MidpointRounding.AwayFromZero);
                }
                else
                {
                    unitCost = defaultCost;
                    totalValuation = decimal.Round(row.AvailableQuantity * unitCost, 2, MidpointRounding.AwayFromZero);
                }
            }
            else
            {
                // Weighted Average Cost (চলমান ভারযুক্ত গড়)
                if (productPurchases != null && productPurchases.Count > 0)
                {
                    var totalQty = productPurchases.Sum(x => x.Quantity);
                    var totalNet = productPurchases.Sum(x => x.NetAmount);

                    unitCost = totalQty > 0
                        ? decimal.Round(totalNet / totalQty, 4, MidpointRounding.AwayFromZero)
                        : defaultCost;
                }
                else
                {
                    unitCost = defaultCost;
                }

                totalValuation = decimal.Round(row.AvailableQuantity * unitCost, 2, MidpointRounding.AwayFromZero);
            }

            items.Add(new FinancialStockItem(
                row.WarehouseId,
                row.WarehouseCode,
                row.WarehouseName,
                row.BranchId,
                row.BranchName,
                row.ProductId,
                row.ProductReference,
                row.ProductDescription,
                row.UnitOfMeasure,
                row.TaxCode,
                row.Quantity,
                row.ReservedQuantity,
                row.AvailableQuantity,
                unitCost,
                totalValuation,
                hsCode,
                costingMethod
            ));
        }

        return items;
    }

    public async Task<InventoryValuationSummary> ValuationSummary(
        Actor actor,
        Guid companyId,
        string method,
        CancellationToken ct)
    {
        actor.RequireCompany(companyId);

        // Fetch up to 1000 inventory items for complete valuation analysis
        var allItems = await AllBalances(actor, companyId, warehouseId: null, method, skip: 0, take: 1000, ct);

        var totalQty = allItems.Sum(x => x.AvailableQuantity);
        var totalValuation = allItems.Sum(x => x.TotalValuation);

        var warehouseSummaries = allItems
            .GroupBy(x => new { x.WarehouseId, x.WarehouseCode, x.WarehouseName, x.BranchId, x.BranchName })
            .Select(g => new WarehouseValuationSummary(
                g.Key.WarehouseId,
                g.Key.WarehouseCode,
                g.Key.WarehouseName,
                g.Key.BranchId,
                g.Key.BranchName,
                g.Count(),
                g.Sum(x => x.AvailableQuantity),
                g.Sum(x => x.TotalValuation)
            ))
            .OrderBy(w => w.WarehouseCode)
            .ToList();

        var topValued = allItems
            .OrderByDescending(x => x.TotalValuation)
            .Take(5)
            .ToList();

        var uniqueProductCount = allItems.Select(x => x.ProductId).Distinct().Count();

        return new InventoryValuationSummary(
            companyId,
            method.Equals("FIFO", StringComparison.OrdinalIgnoreCase) ? "FIFO" : "WEIGHTED_AVERAGE",
            totalQty,
            totalValuation,
            warehouseSummaries.Count,
            uniqueProductCount,
            warehouseSummaries,
            topValued
        );
    }

    private static void Page(int skip, int take)
    {
        if (skip is < 0 or > 10000 || take is < 1 or > 1000)
            throw new DomainException("validation_failed", "Require skip 0-10000 and take 1-1000.");
    }
}
