using Vat.Application;

namespace Vat.Services.Identity;

public interface IIdentityRoleStore
{
    Task<IReadOnlyList<ModuleRecord>> List(Guid tenantId, Guid companyId, Guid? before, int take, CancellationToken ct);
}
