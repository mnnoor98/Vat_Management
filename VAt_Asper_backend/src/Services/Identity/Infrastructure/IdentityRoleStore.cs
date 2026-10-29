using Microsoft.EntityFrameworkCore;
using Vat.Application;

namespace Vat.Services.Identity.Infrastructure;

public sealed class IdentityRoleStore(IdentityDbContext db) : IIdentityRoleStore
{
    public async Task<IReadOnlyList<ModuleRecord>> List(Guid tenantId, Guid companyId, Guid? before, int take, CancellationToken ct)
    {
        var query = db.Roles.AsNoTracking().Where(x => x.TenantId == tenantId && x.CompanyId == companyId);
        if (before is { } cursor) query = query.Where(x => x.Id.CompareTo(cursor) < 0);
        return await query.OrderByDescending(x => x.Id).Take(take)
            .Select(x => new ModuleRecord(x.Id, x.Code, null, x.CreatedAt, x.Version)).ToListAsync(ct);
    }
}
