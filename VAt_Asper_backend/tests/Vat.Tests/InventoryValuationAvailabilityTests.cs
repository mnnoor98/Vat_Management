using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Vat.Application.Security;
using Vat.Domain.Catalog;
using Vat.Domain.Company;
using Vat.Domain.Posting;
using Vat.Infrastructure.Persistence;
using Xunit;

namespace Vat.Tests;

[Collection("PostgreSQL integration")]
public sealed class InventoryValuationAvailabilityTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    [Fact]
    public async Task Summary_covers_more_than_1000_positions_without_inventing_costs_or_losing_reserved_stock()
    {
        var tenant = Guid.NewGuid(); var company = Guid.NewGuid();
        var warehouseId = await factory.Inspect(async db =>
        {
            db.Add(Company.Create(tenant, company, "Valuation test", "REG", DateTimeOffset.UtcNow));
            var branch = Branch.Create(tenant, company, "Branch", DateTimeOffset.UtcNow); db.Add(branch);
            var warehouse = Warehouse.Create(tenant, company, branch.Id, "MAIN", "Warehouse"); db.Add(warehouse);
            for (var i = 0; i < 1001; i++)
            {
                var product = Product.Create(tenant, company, i == 0 ? "PROD-01" : $"ITEM-{i}", "Materials", "KG", "SYNTHETIC"); db.Add(product);
                db.Entry(product).Property(x => x.Id).CurrentValue = Guid.Parse($"00000000-0000-0000-0000-{i + 1:000000000000}");
                var balance = new StockBalance(); db.Add(balance);
                db.Entry(balance).Property(x => x.TenantId).CurrentValue = tenant;
                db.Entry(balance).Property(x => x.CompanyId).CurrentValue = company;
                db.Entry(balance).Property(x => x.WarehouseId).CurrentValue = warehouse.Id;
                db.Entry(balance).Property(x => x.ProductId).CurrentValue = product.Id;
                db.Entry(balance).Property(x => x.Quantity).CurrentValue = i == 0 ? 10 : i == 1000 ? 20 : 0;
                db.Entry(balance).Property(x => x.ReservedQuantity).CurrentValue = i == 0 ? 9 : 0;
            }
            await db.SaveChangesAsync(); return warehouse.Id;
        });
        using var client = factory.Client(tenant, company, permissions: [Permissions.InventoryRead]);
        var root = $"/api/v1/companies/{company}/inventory";
        foreach (var method in new[] { "FIFO", "WEIGHTED_AVERAGE" })
        {
            var summary = (await client.GetFromJsonAsync<InventoryValuationSummary>($"{root}/valuation-summary?method={method}"))!;
            Assert.Equal(1001, summary.TotalUniqueProductsCount);
            Assert.Equal(30, summary.TotalStockQuantity);
            Assert.Null(summary.TotalStockValuation);
            Assert.Equal("Unavailable", summary.ValuationStatus);
            Assert.Equal("cost_journal_unavailable", summary.ValuationReason);
            Assert.Empty(summary.TopValuedItems);
            var warehouse = Assert.Single(summary.Warehouses);
            Assert.Equal(1001, warehouse.ItemsCount); Assert.Null(warehouse.TotalValuation);
            var rows = (await client.GetFromJsonAsync<FinancialStockItem[]>($"{root}/all-balances?warehouseId={warehouseId}&take=100&method={method}"))!;
            Assert.Equal(100, rows.Length);
            var reserved = Assert.Single(rows, row => row.ProductReference == "PROD-01");
            Assert.Equal(10m, reserved.Quantity); Assert.Equal(9m, reserved.ReservedQuantity); Assert.Equal(1m, reserved.AvailableQuantity);
            Assert.All(rows, row => { Assert.Null(row.StandardCost); Assert.Null(row.HsCode); Assert.Equal(method, row.CostingMethod); });
            Assert.All(rows.Where(row => row.Quantity != 0), row => Assert.Null(row.TotalValuation));
            Assert.All(rows.Where(row => row.Quantity == 0), row => Assert.Equal(0m, row.TotalValuation));
        }
        using var foreign = factory.Client(Guid.NewGuid(), company, permissions: [Permissions.InventoryRead]);
        var empty = (await foreign.GetFromJsonAsync<InventoryValuationSummary>($"{root}/valuation-summary"))!;
        Assert.Equal(0, empty.TotalUniqueProductsCount); Assert.Equal(0m, empty.TotalStockValuation);
        Assert.Equal("NoStock", empty.ValuationStatus);
        using var denied = factory.Client(tenant, company, permissions: []);
        Assert.Equal(HttpStatusCode.Forbidden, (await denied.GetAsync($"{root}/valuation-summary")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/companies/{Guid.NewGuid()}/inventory/valuation-summary")).StatusCode);
    }

    [Theory]
    [InlineData("all-balances")]
    [InlineData("valuation-summary")]
    public async Task Unknown_costing_method_is_rejected(string route)
    {
        var company = Guid.NewGuid();
        using var client = factory.Client(Guid.NewGuid(), company, permissions: [Permissions.InventoryRead]);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await client.GetAsync($"/api/v1/companies/{company}/inventory/{route}?method=made-up")).StatusCode);
    }
}
