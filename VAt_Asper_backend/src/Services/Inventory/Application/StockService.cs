using Vat.Application.Security;
using Vat.Domain;

namespace Vat.Services.Inventory;

public sealed class StockService(IStockStore store)
{
    public async Task<IReadOnlyList<StockBalanceResponse>> Balances(Actor actor, Guid companyId, Guid warehouseId, Guid? beforeProduct, int take, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        if (!actor.Permissions.Contains(InventoryPermissions.InventoryRead))
            throw new DomainException("permission_denied", "Inventory read permission is required.", 403);
        if (take is < 1 or > 100)
            throw new DomainException("validation_failed", "Require take 1–100.");
        return await store.Balances(actor, companyId, warehouseId, beforeProduct, take, ct);
    }

    public async Task<IReadOnlyList<AllStockBalanceItem>> AllBalances(Actor actor, Guid companyId, Guid? warehouseId, int skip, int take, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        if (!actor.Permissions.Contains(InventoryPermissions.InventoryRead))
            throw new DomainException("permission_denied", "Inventory read permission is required.", 403);
        if (skip is < 0 or > 10000 || take is < 1 or > 100)
            throw new DomainException("validation_failed", "Require skip 0–10000 and take 1–100.");
        return await store.AllBalances(actor, companyId, warehouseId, skip, take, ct);
    }
}

