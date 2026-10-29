using Vat.Application;
using Vat.Application.Security;
using Vat.Domain;

namespace Vat.Services.Manufacturing;

public sealed class ManufacturingReadService(IManufacturingOrderStore store)
{
    public Task<IReadOnlyList<ModuleRecord>> List(Actor actor, Guid companyId, Guid? before, int take, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        if (!actor.Permissions.Contains(Permissions.ManufacturingRead))
            throw new DomainException("permission_denied", "The required permission is not granted.", 403);
        if (take is < 1 or > 100)
            throw new DomainException("validation_failed", "take must be between 1 and 100.");
        return store.List(actor.TenantId, companyId, before, take, ct);
    }
}
