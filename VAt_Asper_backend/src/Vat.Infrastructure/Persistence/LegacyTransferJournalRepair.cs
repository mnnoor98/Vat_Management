using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vat.Domain;
using Vat.Domain.Company;
using Vat.Domain.Inventory;
using Vat.Domain.Posting;
using Vat.Domain.Sales;

namespace Vat.Infrastructure.Persistence;

public sealed record TransferJournalRepairResult(int Transfers, int ExistingEntries, int MissingEntries, bool Applied, string EvidenceSha256);

// Operator-only reconstruction of already-applied legacy transfers. No balance updates,
// no new transfer approval, and no HTTP registration. A preview must reconcile exactly.
public sealed class LegacyTransferJournalRepair(VatDbContext db, TimeProvider clock)
{
    public async Task<TransferJournalRepairResult> Run(Guid tenant, Guid company, string actor, string reason, bool apply, CancellationToken ct)
    {
        Guard.Id(tenant, nameof(tenant)); Guard.Id(company, nameof(company));
        actor = Guard.Text(actor, nameof(actor), 200); reason = Guard.Text(reason, nameof(reason), 1000);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new PeriodStore(db).LockCompany(tenant, company, true, ct);
        // Freeze source documents and the quantity basis while checking the proof.
        await db.Database.ExecuteSqlRawAsync("LOCK TABLE inventory.transfers, inventory.transfer_lines, inventory.transfer_journal, inventory.stock_balances, inventory.stock_movements, sales.posting_entries IN SHARE ROW EXCLUSIVE MODE", ct);
        var headers = await db.Set<Dictionary<string, object>>("inventory.transfers").AsNoTracking()
            .Where(x => EF.Property<Guid>(x, "TenantId") == tenant && EF.Property<Guid>(x, "CompanyId") == company
                && EF.Property<string>(x, "State") == "Posted").ToArrayAsync(ct);
        var ids = headers.Select(x => (Guid)x["Id"]).ToArray();
        var lines = await db.Set<Dictionary<string, object>>("inventory.transfer_lines").AsNoTracking()
            .Where(x => EF.Property<Guid>(x, "TenantId") == tenant && EF.Property<Guid>(x, "CompanyId") == company
                && ids.Contains(EF.Property<Guid>(x, "TransferId"))).OrderBy(x => EF.Property<Guid>(x, "Id")).ToArrayAsync(ct);
        if (headers.Any(h => !lines.Any(l => (Guid)l["TransferId"] == (Guid)h["Id"])))
            throw new DomainException("repair_evidence_incomplete", "A posted transfer has no lines.", 409);
        var existing = await db.Set<TransferJournalEntry>().AsNoTracking().Where(x => x.TenantId == tenant && x.CompanyId == company).ToArrayAsync(ct);
        var proposed = lines.Select(line =>
        {
            var header = headers.Single(h => (Guid)h["Id"] == (Guid)line["TransferId"]);
            var from = (Guid)header["FromWarehouseId"]; var to = (Guid)header["ToWarehouseId"]; var quantity = (decimal)line["Quantity"];
            if (quantity <= 0 || from == to) throw new DomainException("repair_evidence_invalid", "Transfer line evidence is invalid.", 409);
            var proof = new { TenantId = tenant, CompanyId = company, TransferId = (Guid)header["Id"], TransferLineId = (Guid)line["Id"],
                Version = (int)header["Version"], FromWarehouseId = from, ToWarehouseId = to, ProductId = (Guid)line["ProductId"],
                Quantity = quantity, BusinessDate = (DateOnly)header["BusinessDate"] };
            return new TransferJournalEntry { TenantId = tenant, CompanyId = company, Id = Guid.NewGuid(), TransferId = proof.TransferId,
                TransferLineId = proof.TransferLineId, FromWarehouseId = from, ToWarehouseId = to, ProductId = proof.ProductId,
                BusinessDate = proof.BusinessDate, Quantity = quantity, EvidenceSha256 = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(proof))),
                RecordedBy = actor, RecordedAt = clock.GetUtcNow() };
        }).ToArray();
        if (existing.Any(e => !proposed.Any(p => p.TransferLineId == e.TransferLineId && p.EvidenceSha256 == e.EvidenceSha256
                && p.TransferId == e.TransferId && p.FromWarehouseId == e.FromWarehouseId && p.ToWarehouseId == e.ToWarehouseId
                && p.ProductId == e.ProductId && p.Quantity == e.Quantity && p.BusinessDate == e.BusinessDate)))
            throw new DomainException("repair_evidence_conflict", "Existing transfer journal differs from the source documents.", 409);
        var missing = proposed.Where(x => !existing.Any(e => e.TransferLineId == x.TransferLineId)).ToArray();
        var periods = await db.Set<FiscalPeriod>().AsNoTracking().Where(x => x.TenantId == tenant && x.CompanyId == company).ToArrayAsync(ct);
        if (missing.Any(x => !periods.Any(p => p.From <= x.BusinessDate && x.BusinessDate < p.Until && p.Status == FiscalPeriodStatus.Open)))
            throw new DomainException("repair_period_unavailable", "Missing journal dates must belong to an open fiscal period; closed history requires separate recovery approval.", 409);
        var receipts = await db.Set<StockMovement>().Where(x => x.TenantId == tenant && x.CompanyId == company)
            .Select(x => new { x.WarehouseId, x.ProductId, x.Quantity }).ToArrayAsync(ct);
        var sales = await db.Set<SalesPostingEntry>().Where(x => x.TenantId == tenant && x.CompanyId == company && x.EntryType == "Goods")
            .Select(x => new { x.WarehouseId, x.ProductId, x.Quantity }).ToArrayAsync(ct);
        var expected = receipts.Concat(sales).GroupBy(x => (x.WarehouseId, x.ProductId)).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
        foreach (var entry in proposed)
        {
            var from = (entry.FromWarehouseId, entry.ProductId); var to = (entry.ToWarehouseId, entry.ProductId);
            expected[from] = expected.GetValueOrDefault(from) - entry.Quantity;
            expected[to] = expected.GetValueOrDefault(to) + entry.Quantity;
        }
        var balances = await db.Set<StockBalance>().AsNoTracking().Where(x => x.TenantId == tenant && x.CompanyId == company).ToArrayAsync(ct);
        if (balances.Length != expected.Count || balances.Any(x => !expected.TryGetValue((x.WarehouseId, x.ProductId), out var quantity) || quantity != x.Quantity))
            throw new DomainException("repair_does_not_reconcile", "Documented transfers do not fully explain current balances; no entries were written.", 409);
        var digest = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(proposed.Select(x => x.EvidenceSha256).ToArray())));
        if (apply && missing.Length > 0)
        {
            db.AddRange(missing);
            foreach (var transfer in missing.GroupBy(x => x.TransferId))
                db.Audit.Add(new AuditEntry { Id = Guid.CreateVersion7(), TenantId = tenant, CompanyId = company, ResourceId = transfer.Key,
                    ActorId = actor, Action = "LegacyTransferJournalReconstructed", CorrelationId = digest, Reason = reason,
                    OccurredAt = clock.GetUtcNow() });
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return new(headers.Length, existing.Length, missing.Length, apply && missing.Length > 0, digest);
    }
}
