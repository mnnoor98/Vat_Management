using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Vat.Application;
using Vat.Application.Security;
using Vat.Domain.Catalog;
using Vat.Domain.Company;
using Vat.Domain.Posting;
using Xunit;

namespace Vat.Tests;

[Collection("PostgreSQL integration")]
public sealed class InventoryScopeRegressionTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    [Fact]
    public async Task Reused_ids_in_another_tenant_do_not_duplicate_or_leak_inventory_reads()
    {
        var tenant = Guid.NewGuid(); var other = Guid.NewGuid(); var company = Guid.NewGuid();
        var branchId = Guid.NewGuid(); var fromId = Guid.NewGuid(); var toId = Guid.NewGuid(); var productId = Guid.NewGuid();
        var transferId = Guid.NewGuid(); var lineId = Guid.NewGuid();
        await factory.Inspect(async db =>
        {
            foreach (var scope in new[] { tenant, other })
            {
                var label = scope == tenant ? "OWNED" : "FOREIGN";
                db.Companies.Add(Company.Create(scope, company, label, "REG", DateTimeOffset.UtcNow));
                var branch = Branch.Create(scope, company, label, DateTimeOffset.UtcNow); db.Add(branch); db.Entry(branch).Property(x => x.Id).CurrentValue = branchId;
                var from = Warehouse.Create(scope, company, branchId, "FROM", label); db.Add(from); db.Entry(from).Property(x => x.Id).CurrentValue = fromId;
                var to = Warehouse.Create(scope, company, branchId, "TO", label); db.Add(to); db.Entry(to).Property(x => x.Id).CurrentValue = toId;
                var product = Product.Create(scope, company, "ITEM", label, "KG", "TEST"); db.Add(product); db.Entry(product).Property(x => x.Id).CurrentValue = productId;
                var balance = new StockBalance(); db.Add(balance);
                db.Entry(balance).Property(x => x.TenantId).CurrentValue = scope; db.Entry(balance).Property(x => x.CompanyId).CurrentValue = company;
                db.Entry(balance).Property(x => x.WarehouseId).CurrentValue = fromId; db.Entry(balance).Property(x => x.ProductId).CurrentValue = productId;
                db.Entry(balance).Property(x => x.Quantity).CurrentValue = scope == tenant ? 10m : 999m;
                db.Set<Dictionary<string, object>>("inventory.transfers").Add(new() { ["TenantId"] = scope, ["CompanyId"] = company,
                    ["Id"] = transferId, ["FromWarehouseId"] = fromId, ["ToWarehouseId"] = toId, ["Reference"] = label,
                    ["BusinessDate"] = new DateOnly(2026, 10, 23), ["State"] = "Posted", ["CreatedBy"] = "maker", ["ApprovedBy"] = "checker", ["VehicleNumber"] = label });
                db.Set<Dictionary<string, object>>("inventory.transfer_lines").Add(new() { ["TenantId"] = scope, ["CompanyId"] = company,
                    ["Id"] = lineId, ["TransferId"] = transferId, ["ProductId"] = productId, ["Quantity"] = scope == tenant ? 1m : 999m,
                    ["UnitPrice"] = 2m, ["CreatedBy"] = "maker" });
            }
            await db.SaveChangesAsync(); return 0;
        });
        var client = factory.Client(tenant, company, permissions: [Permissions.InventoryRead]);
        var root = $"/api/v1/companies/{company}";
        var warehouses = await client.GetStringAsync(root + "/warehouses");
        Assert.DoesNotContain("FOREIGN", warehouses); Assert.Equal(2, System.Text.Json.JsonDocument.Parse(warehouses).RootElement.GetArrayLength());
        var balances = await client.GetStringAsync(root + "/inventory/all-balances");
        Assert.DoesNotContain("FOREIGN", balances); Assert.Equal(1, System.Text.Json.JsonDocument.Parse(balances).RootElement.GetArrayLength());
        var transfers = (await client.GetFromJsonAsync<TransferHistoryItem[]>(root + "/transfers"))!;
        var item = Assert.Single(transfers); Assert.Equal("OWNED", item.TransferNumber);
        Assert.Equal("OWNED", item.FromBranchName); Assert.Equal("OWNED", item.ToWarehouseName);
        var line = Assert.Single(item.Lines); Assert.Equal("OWNED", line.ProductName); Assert.Equal(1m, line.Quantity);
    }
}
