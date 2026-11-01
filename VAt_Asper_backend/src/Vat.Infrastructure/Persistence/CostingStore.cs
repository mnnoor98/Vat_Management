using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Vat.Application;
using Vat.Domain.Costing;
using Vat.Domain.Sales;

namespace Vat.Infrastructure.Persistence;

public sealed class CostingStore(VatDbContext db) : ICostingStore
{
    public Task Lock(Guid tenant, Guid company, CancellationToken ct) => LockScope(db, tenant, company, false, ct);
    internal static async Task LockScope(VatDbContext db, Guid tenant, Guid company, bool shared, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Costing fencing requires a transaction.");
        var key = BinaryPrimitives.ReadInt64BigEndian(SHA256.HashData(Encoding.UTF8.GetBytes($"costing-policy:{tenant}:{company}")));
        if (shared) await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock_shared({key})", ct);
        else await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})", ct);
    }
    public void Add(CostingPolicy policy) => db.Add(policy);
    public Task<CostingPolicy?> Find(Guid tenant, Guid company, Guid id, CancellationToken ct) => db.Set<CostingPolicy>()
        .SingleOrDefaultAsync(x => x.TenantId == tenant && x.CompanyId == company && x.Id == id, ct);
    public async Task<IReadOnlyList<CostingPolicy>> List(Guid tenant, Guid company, Guid? before, int take, CancellationToken ct) =>
        await db.Set<CostingPolicy>().AsNoTracking().Where(x => x.TenantId == tenant && x.CompanyId == company &&
            (before == null || x.Id.CompareTo(before.Value) < 0)).OrderByDescending(x => x.Id).Take(take).ToArrayAsync(ct);
    public Task<bool> Overlaps(CostingPolicy policy, CancellationToken ct) => db.Set<CostingPolicy>().AnyAsync(x =>
        x.TenantId == policy.TenantId && x.CompanyId == policy.CompanyId && x.Id != policy.Id && x.ProductId == policy.ProductId &&
        x.State == "Approved" && x.EffectiveFrom < policy.EffectiveUntil && policy.EffectiveFrom < x.EffectiveUntil, ct);
    public async Task<bool> HasFrozenReceipts(CostingPolicy policy, CancellationToken ct) => await db.Set<PurchaseCostReceipt>().AnyAsync(x =>
        x.TenantId == policy.TenantId && x.CompanyId == policy.CompanyId && (policy.ProductId == null || x.ProductId == policy.ProductId) &&
        x.BusinessDate >= policy.EffectiveFrom && x.BusinessDate < policy.EffectiveUntil, ct) ||
        await (from source in db.Set<CostSourceEvent>() join sale in db.Set<SalesPostingEntry>()
            on new { source.TenantId, source.CompanyId, Id = source.SalesEntryId } equals new { sale.TenantId, sale.CompanyId, Id = (Guid?)sale.Id }
            where source.TenantId == policy.TenantId && source.CompanyId == policy.CompanyId && (policy.ProductId == null || source.ProductId == policy.ProductId) &&
                sale.BusinessDate >= policy.EffectiveFrom && sale.BusinessDate < policy.EffectiveUntil
            select source.Sequence).AnyAsync(ct) || await db.Set<StockCostEntry>().AnyAsync(x => x.TenantId == policy.TenantId && x.CompanyId == policy.CompanyId &&
                (policy.ProductId == null || x.ProductId == policy.ProductId) && x.BusinessDate >= policy.EffectiveFrom && x.BusinessDate < policy.EffectiveUntil, ct);
    public Task<CostingPolicy?> Resolve(Guid tenant, Guid company, Guid product, DateOnly date, CancellationToken ct) =>
        db.Set<CostingPolicy>().AsNoTracking().Where(x => x.TenantId == tenant && x.CompanyId == company &&
            x.State == "Approved" && (x.ProductId == null || x.ProductId == product) && x.EffectiveFrom <= date && date < x.EffectiveUntil)
            .OrderByDescending(x => x.ProductId != null).FirstOrDefaultAsync(ct);
    public async Task<IReadOnlyList<PurchaseCostReceipt>> Receipts(Guid tenant, Guid company, Guid? before, int take, CancellationToken ct) =>
        await db.Set<PurchaseCostReceipt>().AsNoTracking().Where(x => x.TenantId == tenant && x.CompanyId == company &&
            (before == null || x.Id.CompareTo(before.Value) < 0)).OrderByDescending(x => x.Id).Take(take).ToArrayAsync(ct);
}
