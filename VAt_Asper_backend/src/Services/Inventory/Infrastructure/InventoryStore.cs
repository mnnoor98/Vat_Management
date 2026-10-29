using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vat.Application.Security;
using Vat.Domain;

namespace Vat.Services.Inventory.Infrastructure;

public sealed class InventoryStore(InventoryDbContext db, IPeriodGuard periods, TimeProvider clock)
    : IWarehouseStore, IStockStore, ITransferStore, IInventoryReadStore
{
    // Warehouse implementation
    public async Task<WarehouseResponse> Create(Actor actor, Guid companyId, CreateWarehouseRequest request, string correlationId, CancellationToken ct)
    {
        if (await db.Warehouses.AnyAsync(x => x.TenantId == actor.TenantId && x.CompanyId == companyId && x.Code == request.Code, ct))
            throw new DomainException("duplicate_code", "A warehouse with this code already exists.", 409);

        var warehouse = Warehouse.Create(actor.TenantId, companyId, request.BranchId, request.Code, request.Name);
        db.Warehouses.Add(warehouse);
        await db.SaveChangesAsync(ct);
        return WarehouseResponse.From(warehouse);
    }

    public async Task<WarehouseResponse?> Get(Actor actor, Guid companyId, Guid id, CancellationToken ct)
    {
        var warehouse = await db.Warehouses.AsNoTracking()
            .SingleOrDefaultAsync(x => x.TenantId == actor.TenantId && x.CompanyId == companyId && x.Id == id, ct);
        return warehouse is null ? null : WarehouseResponse.From(warehouse);
    }

    public async Task<IReadOnlyList<WarehouseListItem>> List(Actor actor, Guid companyId, int skip, int take, CancellationToken ct)
    {
        return await db.Warehouses.AsNoTracking()
            .Where(x => x.TenantId == actor.TenantId && x.CompanyId == companyId)
            .OrderBy(x => x.Code)
            .Skip(skip).Take(take)
            .Select(w => new WarehouseListItem(w.Id, w.Code, w.Name, w.BranchId, ""))
            .ToListAsync(ct);
    }

    // Stock balances implementation
    public async Task<IReadOnlyList<StockBalanceResponse>> Balances(Actor actor, Guid companyId, Guid warehouseId, Guid? beforeProduct, int take, CancellationToken ct)
    {
        var q = db.StockBalances.AsNoTracking()
            .Where(x => x.TenantId == actor.TenantId && x.CompanyId == companyId && x.WarehouseId == warehouseId);
        if (beforeProduct.HasValue)
            q = q.Where(x => x.ProductId.CompareTo(beforeProduct.Value) < 0);

        return await q.OrderByDescending(x => x.ProductId)
            .Take(take)
            .Select(x => new StockBalanceResponse(x.WarehouseId, x.ProductId, x.Quantity, x.ReservedQuantity, x.Quantity - x.ReservedQuantity))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AllStockBalanceItem>> AllBalances(Actor actor, Guid companyId, Guid? warehouseId, int skip, int take, CancellationToken ct)
    {
        var q = from sb in db.StockBalances.AsNoTracking()
                join w in db.Warehouses.AsNoTracking() on new { sb.TenantId, sb.CompanyId, Id = sb.WarehouseId } equals new { w.TenantId, w.CompanyId, w.Id }
                where sb.TenantId == actor.TenantId && sb.CompanyId == companyId
                select new { sb, w };

        if (warehouseId.HasValue)
            q = q.Where(x => x.sb.WarehouseId == warehouseId.Value);

        return await q.OrderBy(x => x.sb.WarehouseId).ThenBy(x => x.sb.ProductId)
            .Skip(skip).Take(take)
            .Select(x => new AllStockBalanceItem(
                x.sb.WarehouseId,
                x.w.Code,
                x.w.Name,
                x.w.BranchId,
                "",
                x.sb.ProductId,
                "",
                "",
                "",
                "",
                x.sb.Quantity,
                x.sb.ReservedQuantity,
                x.sb.Quantity - x.sb.ReservedQuantity
            )).ToListAsync(ct);
    }

    // Transfer implementation
    public async Task<TransferResult> Create(Actor actor, Guid company, TransferCreateRequest request, CancellationToken ct)
    {
        await periods.RequireOpen(actor.TenantId, company, request.DispatchDate, ct);
        var warehouseIds = new[] { request.FromWarehouseId, request.ToWarehouseId };
        if (await db.Warehouses.CountAsync(x => x.TenantId == actor.TenantId && x.CompanyId == company && warehouseIds.Contains(x.Id), ct) != 2)
            throw new DomainException("warehouse_not_found", "A warehouse is outside the company or unavailable.", 404);

        var id = Guid.CreateVersion7();
        var now = clock.GetUtcNow();
        var reference = string.IsNullOrWhiteSpace(request.TransferNumber) ? "TR-" + id.ToString("N") : request.TransferNumber.Trim();
        if (await db.Transfers.AnyAsync(x => x.TenantId == actor.TenantId && x.CompanyId == company && x.Reference == reference, ct))
            throw new DomainException("duplicate_reference", "Transfer reference already exists.", 409);

        var header = new TransferHeader
        {
            Id = id,
            TenantId = actor.TenantId,
            CompanyId = company,
            FromWarehouseId = request.FromWarehouseId,
            ToWarehouseId = request.ToWarehouseId,
            Reference = reference,
            BusinessDate = request.DispatchDate,
            State = "Draft",
            VehicleNumber = request.VehicleNumber,
            DriverName = request.DriverName,
            Remarks = request.Remarks,
            DispatchTime = request.DispatchTime,
            CreatedBy = actor.Subject,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 1
        };
        db.Transfers.Add(header);

        foreach (var line in request.Lines)
        {
            var row = new TransferLine
            {
                Id = Guid.CreateVersion7(),
                TenantId = actor.TenantId,
                CompanyId = company,
                TransferId = id,
                ProductId = line.ProductId,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                Remarks = line.Remarks,
                CreatedBy = actor.Subject,
                CreatedAt = now,
                UpdatedAt = now,
                Version = 1
            };
            db.TransferLines.Add(row);
        }

        await db.SaveChangesAsync(ct);
        return new(id, reference, "Draft", 1);
    }

    public async Task<TransferResult> Transition(Actor actor, Guid company, Guid id, string action, TransferActionRequest request, CancellationToken ct)
    {
        var key = System.Buffers.Binary.BinaryPrimitives.ReadInt64BigEndian(SHA256.HashData(id.ToByteArray()));
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})", ct);

        var header = await db.Transfers.SingleOrDefaultAsync(x => x.TenantId == actor.TenantId && x.CompanyId == company && x.Id == id, ct)
            ?? throw new DomainException("transfer_not_found", "Transfer was not found.", 404);

        if (header.Version != request.ExpectedVersion)
            throw new DomainException("version_conflict", "Reload the current transfer version.", 409);

        var next = TransferWorkflow.Next(header.State, action, header.CreatedBy, actor.Subject, header.ApprovedBy);
        if (action != "cancel")
            await periods.RequireOpen(actor.TenantId, company, header.BusinessDate, ct);

        header.State = next;
        header.Version += 1;
        header.UpdatedAt = clock.GetUtcNow();
        if (action == "approve") header.ApprovedBy = actor.Subject;
        if (action == "post") await Post(actor, company, header, ct);

        await db.SaveChangesAsync(ct);
        return new(id, header.Reference, next, header.Version);
    }

    private async Task Post(Actor actor, Guid company, TransferHeader header, CancellationToken ct)
    {
        var tenant = actor.TenantId;
        var id = header.Id;
        var from = header.FromWarehouseId;
        var to = header.ToWarehouseId;

        var lines = await db.TransferLines.AsNoTracking()
            .Where(x => x.TenantId == tenant && x.CompanyId == company && x.TransferId == id)
            .OrderBy(x => x.ProductId).ToArrayAsync(ct);

        if (lines.Length is < 1 or > 100)
            throw new DomainException("invalid_transfer_lines", "Transfer must have 1 to 100 lines.", 409);

        foreach (var line in lines)
        {
            var product = line.ProductId;
            var quantity = line.Quantity;

            var changed = await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE inventory.stock_balances SET "Quantity" = "Quantity" - {quantity}
                WHERE "TenantId" = {tenant} AND "CompanyId" = {company} AND "WarehouseId" = {from} AND "ProductId" = {product}
                  AND "Quantity" - "ReservedQuantity" >= {quantity}
                """, ct);
            if (changed != 1)
                throw new DomainException("insufficient_stock", "Available source stock is insufficient; reserved stock cannot be transferred.", 409);

            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO inventory.stock_balances ("TenantId", "CompanyId", "WarehouseId", "ProductId", "Quantity", "ReservedQuantity")
                VALUES ({tenant}, {company}, {to}, {product}, {quantity}, 0)
                ON CONFLICT ("TenantId", "CompanyId", "WarehouseId", "ProductId")
                DO UPDATE SET "Quantity" = inventory.stock_balances."Quantity" + EXCLUDED."Quantity"
                """, ct);

            var proof = new
            {
                TenantId = tenant,
                CompanyId = company,
                TransferId = id,
                TransferLineId = line.Id,
                Version = header.Version,
                FromWarehouseId = from,
                ToWarehouseId = to,
                ProductId = product,
                Quantity = quantity,
                BusinessDate = header.BusinessDate
            };

            db.TransferJournal.Add(new TransferJournalEntry
            {
                TenantId = tenant,
                CompanyId = company,
                Id = Guid.CreateVersion7(),
                TransferId = id,
                TransferLineId = proof.TransferLineId,
                FromWarehouseId = from,
                ToWarehouseId = to,
                ProductId = product,
                BusinessDate = proof.BusinessDate,
                Quantity = quantity,
                EvidenceSha256 = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(proof))),
                RecordedBy = actor.Subject,
                RecordedAt = clock.GetUtcNow()
            });
        }
    }

    // Read store implementation
    public async Task<IReadOnlyList<TransferHistoryItem>> Transfers(Guid tenant, Guid company, int skip, int take, CancellationToken ct)
    {
        var headers = await db.Transfers.AsNoTracking()
            .Where(x => x.TenantId == tenant && x.CompanyId == company)
            .OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            .Skip(skip).Take(take).ToArrayAsync(ct);

        if (headers.Length == 0) return [];
        var ids = headers.Select(x => x.Id).ToArray();

        var lines = await db.TransferLines.AsNoTracking()
            .Where(x => x.TenantId == tenant && x.CompanyId == company && ids.Contains(x.TransferId))
            .OrderBy(x => x.Id)
            .Select(line => new
            {
                line.TransferId,
                Line = new TransferHistoryLine(
                    line.Id,
                    line.ProductId,
                    "",
                    "",
                    "",
                    line.Quantity,
                    line.UnitPrice ?? 0m,
                    line.Quantity * (line.UnitPrice ?? 0m))
            }).Take(10001).ToArrayAsync(ct);

        if (lines.Length > 10000)
            throw new DomainException("transfer_read_limit", "Reduce the page size; too many transfer lines.", 422);

        var warehouseIds = headers.SelectMany(x => new[] { x.FromWarehouseId, x.ToWarehouseId }).Distinct().ToArray();
        var warehouses = await db.Warehouses.AsNoTracking()
            .Where(x => x.TenantId == tenant && x.CompanyId == company && warehouseIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, ct);

        return headers.Select(x =>
        {
            var from = warehouses.GetValueOrDefault(x.FromWarehouseId);
            var to = warehouses.GetValueOrDefault(x.ToWarehouseId);
            var fromId = from?.Id ?? x.FromWarehouseId;
            var fromCode = from?.Code ?? "";
            var fromName = from?.Name ?? "";
            var fromBranchId = from?.BranchId ?? Guid.Empty;
            var toId = to?.Id ?? x.ToWarehouseId;
            var toCode = to?.Code ?? "";
            var toName = to?.Name ?? "";
            var toBranchId = to?.BranchId ?? Guid.Empty;

            return new TransferHistoryItem(
                x.Id,
                x.Reference,
                x.BusinessDate,
                x.DispatchTime ?? "",
                x.State,
                x.VehicleNumber ?? "",
                x.DriverName ?? "",
                x.Remarks ?? "",
                x.CreatedAt,
                x.CreatedBy,
                fromId,
                fromCode,
                fromName,
                fromBranchId,
                "",
                toId,
                toCode,
                toName,
                toBranchId,
                "",
                lines.Where(l => l.TransferId == x.Id).Select(l => l.Line).ToArray(),
                x.Version,
                x.ApprovedBy);
        }).ToArray();
    }
}

