using Vat.Application.Security;
using Vat.Domain;

namespace Vat.Services.Inventory;

public sealed class InventoryReadService(IInventoryReadStore store)
{
    public Task<IReadOnlyList<TransferHistoryItem>> Transfers(Actor actor, Guid company, int skip, int take, CancellationToken ct)
    {
        actor.RequireCompany(company);
        if (!actor.Permissions.Contains(InventoryPermissions.InventoryRead)) throw new DomainException("permission_denied", "Inventory read permission is required.", 403);
        if (skip is < 0 or > 10000 || take is < 1 or > 100) throw new DomainException("validation_failed", "Require skip 0–10000 and take 1–100.");
        return store.Transfers(actor.TenantId, company, skip, take, ct);
    }
}
