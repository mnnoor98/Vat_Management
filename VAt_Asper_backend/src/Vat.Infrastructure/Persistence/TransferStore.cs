using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vat.Application;
using Vat.Application.Security;
using Vat.Domain;
using Vat.Domain.Catalog;
using Vat.Domain.Inventory;

namespace Vat.Infrastructure.Persistence;

public sealed class TransferStore(VatDbContext db, IPeriodGuard periods, IPeriodStore periodStore, TimeProvider clock) : ITransferStore
{
    private IQueryable<Dictionary<string, object>> Headers(Guid tenant, Guid company) =>
        db.Set<Dictionary<string, object>>("inventory.transfers").Where(x => EF.Property<Guid>(x, "TenantId") == tenant && EF.Property<Guid>(x, "CompanyId") == company);

    public async Task<TransferResult> Create(Actor actor, Guid company, TransferCreateRequest request, CancellationToken ct)
    {
        RequireTransaction();
        await periods.RequireOpen(actor.TenantId, company, request.DispatchDate, ct);
        var warehouseIds = new[] { request.FromWarehouseId, request.ToWarehouseId };
        if (await db.Set<Warehouse>().CountAsync(x => x.TenantId == actor.TenantId && x.CompanyId == company && warehouseIds.Contains(x.Id), ct) != 2)
            throw new DomainException("warehouse_not_found", "A warehouse is outside the company or unavailable.", 404);
        var products = request.Lines.Select(x => x.ProductId).ToArray();
        if (await db.Set<Product>().CountAsync(x => x.TenantId == actor.TenantId && x.CompanyId == company && products.Contains(x.Id), ct) != products.Length)
            throw new DomainException("product_not_found", "A product is outside the company or unavailable.", 404);
        var id = Guid.CreateVersion7(); var now = clock.GetUtcNow();
        var reference = string.IsNullOrWhiteSpace(request.TransferNumber) ? "TR-" + id.ToString("N") : request.TransferNumber.Trim();
        if (await Headers(actor.TenantId, company).AnyAsync(x => EF.Property<string>(x, "Reference") == reference, ct))
            throw new DomainException("duplicate_reference", "Transfer reference already exists.", 409);
        var header = Bag(actor, company, id, now);
        header["FromWarehouseId"] = request.FromWarehouseId; header["ToWarehouseId"] = request.ToWarehouseId;
        header["Reference"] = reference; header["BusinessDate"] = request.DispatchDate; header["State"] = "Draft";
        header["VehicleNumber"] = request.VehicleNumber!; header["DriverName"] = request.DriverName!;
        header["Remarks"] = request.Remarks!; header["DispatchTime"] = request.DispatchTime!;
        db.Set<Dictionary<string, object>>("inventory.transfers").Add(header);
        foreach (var line in request.Lines)
        {
            var row = Bag(actor, company, Guid.CreateVersion7(), now);
            row["TransferId"] = id; row["ProductId"] = line.ProductId; row["Quantity"] = line.Quantity; row["UnitPrice"] = line.UnitPrice;
            db.Set<Dictionary<string, object>>("inventory.transfer_lines").Add(row);
        }
        return new(id, reference, "Draft", 1);
    }

    public async Task<TransferResult> Transition(Actor actor, Guid company, Guid id, string action, TransferActionRequest request, CancellationToken ct)
    {
        RequireTransaction();
        // All financial admission uses the same company fence; close cannot pass this command.
        await periodStore.LockCompany(actor.TenantId, company, true, ct);
        // Serialize document transitions before reading their expected version.
        var key = System.Buffers.Binary.BinaryPrimitives.ReadInt64BigEndian(SHA256.HashData(id.ToByteArray()));
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})", ct);
        var header = await Headers(actor.TenantId, company).SingleOrDefaultAsync(x => EF.Property<Guid>(x, "Id") == id, ct)
            ?? throw new DomainException("transfer_not_found", "Transfer was not found.", 404);
        var version = (int)header["Version"];
        if (version != request.ExpectedVersion) throw new DomainException("version_conflict", "Reload the current transfer version.", 409);
        var next = TransferWorkflow.Next((string)header["State"], action, (string)header["CreatedBy"], actor.Subject,
            header.GetValueOrDefault("ApprovedBy") as string);
        if (action != "cancel") await periods.RequireOpen(actor.TenantId, company, (DateOnly)header["BusinessDate"], ct);
        header["State"] = next; header["Version"] = version + 1; header["UpdatedAt"] = clock.GetUtcNow();
        if (action == "approve") header["ApprovedBy"] = actor.Subject;
        if (action == "post") await Post(actor, company, header, ct);
        return new(id, (string)header["Reference"], next, version + 1);
    }

    private async Task Post(Actor actor, Guid company, Dictionary<string, object> header, CancellationToken ct)
    {
        var tenant = actor.TenantId; var id = (Guid)header["Id"];
        var from = (Guid)header["FromWarehouseId"]; var to = (Guid)header["ToWarehouseId"];
        var lines = await db.Set<Dictionary<string, object>>("inventory.transfer_lines").AsNoTracking()
            .Where(x => EF.Property<Guid>(x, "TenantId") == tenant && EF.Property<Guid>(x, "CompanyId") == company && EF.Property<Guid>(x, "TransferId") == id)
            .OrderBy(x => EF.Property<Guid>(x, "ProductId")).ToArrayAsync(ct);
        if (lines.Length is < 1 or > 100) throw new DomainException("invalid_transfer_lines", "Transfer must have 1 to 100 lines.", 409);
        // The exclusive company fence was acquired before any stock row. All sides,
        // journal entries and state changes commit in the command executor transaction.
        foreach (var line in lines)
        {
            var product = (Guid)line["ProductId"]; var quantity = (decimal)line["Quantity"];
            var changed = await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE inventory.stock_balances SET "Quantity" = "Quantity" - {quantity}
                WHERE "TenantId" = {tenant} AND "CompanyId" = {company} AND "WarehouseId" = {from} AND "ProductId" = {product}
                  AND "Quantity" - "ReservedQuantity" >= {quantity}
                """, ct);
            if (changed != 1) throw new DomainException("insufficient_stock", "Available source stock is insufficient; reserved stock cannot be transferred.", 409);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO inventory.stock_balances ("TenantId", "CompanyId", "WarehouseId", "ProductId", "Quantity")
                VALUES ({tenant}, {company}, {to}, {product}, {quantity})
                ON CONFLICT ("TenantId", "CompanyId", "WarehouseId", "ProductId")
                DO UPDATE SET "Quantity" = inventory.stock_balances."Quantity" + EXCLUDED."Quantity"
                """, ct);
            var proof = new { TenantId = tenant, CompanyId = company, TransferId = id, TransferLineId = (Guid)line["Id"],
                Version = (int)header["Version"], FromWarehouseId = from, ToWarehouseId = to, ProductId = product,
                Quantity = quantity, BusinessDate = (DateOnly)header["BusinessDate"] };
            db.Add(new TransferJournalEntry { TenantId = tenant, CompanyId = company, Id = Guid.CreateVersion7(), TransferId = id,
                TransferLineId = proof.TransferLineId, FromWarehouseId = from, ToWarehouseId = to, ProductId = product,
                BusinessDate = proof.BusinessDate, Quantity = quantity, EvidenceSha256 = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(proof))),
                RecordedBy = actor.Subject, RecordedAt = clock.GetUtcNow() });
        }
    }

    private static Dictionary<string, object> Bag(Actor actor, Guid company, Guid id, DateTimeOffset now) => new()
    { ["TenantId"] = actor.TenantId, ["CompanyId"] = company, ["Id"] = id, ["CreatedBy"] = actor.Subject, ["CreatedAt"] = now, ["UpdatedAt"] = now, ["Version"] = 1 };
    private void RequireTransaction()
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Transfer commands require the idempotent command transaction.");
    }
}
