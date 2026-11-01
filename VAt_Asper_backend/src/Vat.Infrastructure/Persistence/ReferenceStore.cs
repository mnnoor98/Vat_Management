using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Vat.Application;
using Vat.Domain.Catalog;
using Vat.Domain.Tax;

namespace Vat.Infrastructure.Persistence;

public sealed class ReferenceStore(VatDbContext db) : IReferenceStore
{
    public Task<Product?> ProductByReference(Guid tenantId, Guid companyId, string reference, CancellationToken ct) =>
        db.Set<Product>().SingleOrDefaultAsync(x => x.TenantId == tenantId && x.CompanyId == companyId && x.Reference == reference, ct);
    public Task<Product?> ProductById(Guid tenantId, Guid companyId, Guid id, CancellationToken ct) =>
        db.Set<Product>().SingleOrDefaultAsync(x => x.TenantId == tenantId && x.CompanyId == companyId && x.Id == id, ct);
    public Task<Supplier?> SupplierByReference(Guid tenantId, Guid companyId, string reference, CancellationToken ct) =>
        db.Set<Supplier>().SingleOrDefaultAsync(x => x.TenantId == tenantId && x.CompanyId == companyId && x.Reference == reference, ct);
    public Task<Supplier?> SupplierById(Guid tenantId, Guid companyId, Guid id, CancellationToken ct) =>
        db.Set<Supplier>().SingleOrDefaultAsync(x => x.TenantId == tenantId && x.CompanyId == companyId && x.Id == id, ct);
    public Task<Warehouse?> Warehouse(Guid tenantId, Guid companyId, Guid id, CancellationToken ct) =>
        db.Set<Warehouse>().SingleOrDefaultAsync(x => x.TenantId == tenantId && x.CompanyId == companyId && x.Id == id, ct);
    public async Task<IReadOnlyList<TaxRule>> Rules(Guid tenantId, Guid companyId, int skip, int take, CancellationToken ct) =>
        await db.Set<TaxRule>().AsNoTracking().Where(x => x.TenantId == tenantId && x.CompanyId == companyId)
            .OrderBy(x => x.TaxCode).ThenBy(x => x.EffectiveFrom).ThenBy(x => x.Id).Skip(skip).Take(take).ToListAsync(ct);
    public Task<TaxRule?> Rule(Guid tenantId, Guid companyId, Guid id, CancellationToken ct) =>
        db.Set<TaxRule>().SingleOrDefaultAsync(x => x.TenantId == tenantId && x.CompanyId == companyId && x.Id == id, ct);
    public Task<TaxRule?> EffectiveRule(Guid tenantId, Guid companyId, string taxCode, DateOnly date, CancellationToken ct) =>
        db.Set<TaxRule>().SingleOrDefaultAsync(x => x.TenantId == tenantId && x.CompanyId == companyId && x.TaxCode == taxCode &&
            x.Treatment == "Purchase" && x.PublishedAt != null && x.EffectiveFrom <= date && date < x.EffectiveUntil, ct);
    public Task<TaxRule?> EffectiveSalesRule(Guid tenantId, Guid companyId, string taxCode, DateOnly date, CancellationToken ct) =>
        db.Set<TaxRule>().SingleOrDefaultAsync(x => x.TenantId == tenantId && x.CompanyId == companyId && x.TaxCode == taxCode &&
            x.Treatment == "Sale" && x.PublishedAt != null && x.EffectiveFrom <= date && date < x.EffectiveUntil, ct);
    public Task<bool> HasPublishedOverlap(TaxRule rule, CancellationToken ct) => db.Set<TaxRule>().AnyAsync(x =>
        x.TenantId == rule.TenantId && x.CompanyId == rule.CompanyId && x.TaxCode == rule.TaxCode && x.Treatment == rule.Treatment && x.Id != rule.Id &&
        x.PublishedAt != null && x.EffectiveFrom < rule.EffectiveUntil && rule.EffectiveFrom < x.EffectiveUntil, ct);

    public async Task LockTaxCode(Guid tenantId, Guid companyId, string taxCode, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Rule publication requires a transaction.");
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"tax-rule:{tenantId}:{companyId}:{taxCode}"));
        var lockId = BinaryPrimitives.ReadInt64BigEndian(hash);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({lockId})", ct);
    }

    public void Add(Product value) => db.Add(value);
    public void Add(Supplier value) => db.Add(value);
    public void Add(Warehouse value) => db.Add(value);
    public void Add(TaxRule value) => db.Add(value);
}
