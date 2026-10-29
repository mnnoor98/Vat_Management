using Microsoft.EntityFrameworkCore;
using Vat.Application;
using Vat.Domain;
using Vat.Domain.Catalog;
using Vat.Domain.Company;

namespace Vat.Infrastructure.Persistence;

public sealed class InventoryReadStore(VatDbContext db) : IInventoryReadStore
{
    public async Task<IReadOnlyList<TransferHistoryItem>> Transfers(Guid tenant, Guid company, int skip, int take, CancellationToken ct)
    {
        var headers = await db.Set<Dictionary<string, object>>("inventory.transfers").AsNoTracking()
            .Where(x => EF.Property<Guid>(x, "TenantId") == tenant && EF.Property<Guid>(x, "CompanyId") == company)
            .OrderByDescending(x => EF.Property<DateTimeOffset>(x, "CreatedAt")).ThenBy(x => EF.Property<Guid>(x, "Id"))
            .Skip(skip).Take(take).ToArrayAsync(ct);
        if (headers.Length == 0) return [];
        var ids = headers.Select(x => (Guid)x["Id"]).ToArray();
        var lines = await (from line in db.Set<Dictionary<string, object>>("inventory.transfer_lines").AsNoTracking()
            join product in db.Set<Product>().AsNoTracking() on
                new { TenantId = EF.Property<Guid>(line, "TenantId"), CompanyId = EF.Property<Guid>(line, "CompanyId"), Id = EF.Property<Guid>(line, "ProductId") }
                equals new { product.TenantId, product.CompanyId, product.Id }
            where EF.Property<Guid>(line, "TenantId") == tenant && EF.Property<Guid>(line, "CompanyId") == company
                && ids.Contains(EF.Property<Guid>(line, "TransferId"))
            orderby EF.Property<Guid>(line, "Id")
            select new { TransferId = EF.Property<Guid>(line, "TransferId"), Line = new TransferHistoryLine(
                EF.Property<Guid>(line, "Id"), product.Id, product.Reference, product.Description, product.UnitOfMeasure,
                EF.Property<decimal>(line, "Quantity"), EF.Property<decimal?>(line, "UnitPrice") ?? 0m,
                EF.Property<decimal>(line, "Quantity") * (EF.Property<decimal?>(line, "UnitPrice") ?? 0m)) }).Take(10001).ToArrayAsync(ct);
        if (lines.Length > 10000) throw new DomainException("transfer_read_limit", "Reduce the page size; too many transfer lines.", 422);
        var warehouseIds = headers.SelectMany(x => new[] { (Guid)x["FromWarehouseId"], (Guid)x["ToWarehouseId"] }).Distinct().ToArray();
        var warehouses = await db.Set<Warehouse>().AsNoTracking().Where(x => x.TenantId == tenant && x.CompanyId == company && warehouseIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var branchIds = warehouses.Values.Select(x => x.BranchId).Distinct().ToArray();
        var branches = await db.Set<Branch>().AsNoTracking().Where(x => x.TenantId == tenant && x.CompanyId == company && branchIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        return headers.Select(x =>
        {
            var from = warehouses[(Guid)x["FromWarehouseId"]]; var to = warehouses[(Guid)x["ToWarehouseId"]];
            string Text(string key) => x.TryGetValue(key, out var value) ? value as string ?? "" : "";
            var id = (Guid)x["Id"];
            return new TransferHistoryItem(id, (string)x["Reference"], (DateOnly)x["BusinessDate"], Text("DispatchTime"),
                (string)x["State"], Text("VehicleNumber"), Text("DriverName"), Text("Remarks"), (DateTimeOffset)x["CreatedAt"], (string)x["CreatedBy"],
                from.Id, from.Code, from.Name, from.BranchId, branches.GetValueOrDefault(from.BranchId)?.Name ?? "",
                to.Id, to.Code, to.Name, to.BranchId, branches.GetValueOrDefault(to.BranchId)?.Name ?? "",
                lines.Where(l => l.TransferId == id).Select(l => l.Line).ToArray(), (int)x["Version"], Text("ApprovedBy"));
        }).ToArray();
    }
}
