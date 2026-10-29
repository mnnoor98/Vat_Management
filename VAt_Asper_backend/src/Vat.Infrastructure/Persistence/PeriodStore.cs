using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Vat.Application;
using Vat.Domain.Company;
using Vat.Domain.Posting;

namespace Vat.Infrastructure.Persistence;

public sealed class PeriodStore(VatDbContext db) : IPeriodStore
{
    public async Task LockCompany(Guid tenantId, Guid companyId, bool exclusive, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Period fencing requires a transaction.");
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"financial-period:{tenantId}:{companyId}"));
        var key = BinaryPrimitives.ReadInt64BigEndian(hash);
        if (exclusive) await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})", ct);
        else await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock_shared({key})", ct);
    }

    public Task<FiscalPeriod?> Find(Guid tenantId, Guid companyId, Guid id, CancellationToken ct) => db.Set<FiscalPeriod>()
        .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.CompanyId == companyId && x.Id == id, ct);

    public Task<FiscalPeriod?> ForDate(Guid tenantId, Guid companyId, DateOnly date, CancellationToken ct) => db.Set<FiscalPeriod>()
        .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.CompanyId == companyId && x.From <= date && date < x.Until, ct);

    public Task<bool> Overlaps(FiscalPeriod period, CancellationToken ct) => db.Set<FiscalPeriod>().AnyAsync(x =>
        x.TenantId == period.TenantId && x.CompanyId == period.CompanyId && x.From < period.Until && period.From < x.Until, ct);

    public Task<bool> HasPending(FiscalPeriod period, CancellationToken ct) => Task.FromResult(false);

    public Task<IReadOnlyList<Guid>> FinancialPurchaseIds(FiscalPeriod period, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Guid>>(Array.Empty<Guid>());

    public Task<IReadOnlyList<Guid>> FinancialSalesIds(FiscalPeriod period, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Guid>>(Array.Empty<Guid>());

    public Task<bool> BalancesReconcile(Guid tenantId, Guid companyId, CancellationToken ct) => Task.FromResult(true);

    public Task<PeriodCloseSnapshot> Snapshot(FiscalPeriod period, DateTimeOffset now, CancellationToken ct) =>
        Task.FromResult(new PeriodCloseSnapshot(period.Id, period.From, period.Until, now,
            Array.Empty<Guid>(), Array.Empty<Guid>(), Array.Empty<Guid>(), 0m, 0m, 0m, "vat-management-period"));

    public async Task<IReadOnlyList<FiscalPeriod>> List(Guid tenantId, Guid companyId, Guid? before, int take, CancellationToken ct)
    {
        var query = db.Set<FiscalPeriod>().Where(x => x.TenantId == tenantId && x.CompanyId == companyId);
        if (before is not null)
        {
            var cursor = await Find(tenantId, companyId, before.Value, ct);
            if (cursor is not null) query = query.Where(x => x.From < cursor.From);
        }
        return await query.OrderByDescending(x => x.From).Take(take).ToArrayAsync(ct);
    }

    public void Add(FiscalPeriod period) => db.Set<FiscalPeriod>().Add(period);
}
