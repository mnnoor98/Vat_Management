using Vat.Application.Security;
using Vat.Domain;

namespace Vat.Services.Inventory;

public sealed class WarehouseService(IWarehouseStore store, IAuditWriter audit)
{
    public async Task<CommandOutcome> Create(Actor actor, Guid companyId, CreateWarehouseRequest request, string correlationId, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        if (!actor.Permissions.Contains(InventoryPermissions.InventoryManage))
            throw new DomainException("permission_denied", "Inventory manage permission is required.", 403);
        Guard.Id(request.BranchId, nameof(request.BranchId));
        Guard.Text(request.Code, nameof(request.Code), 100);
        Guard.Text(request.Name, nameof(request.Name), 200);

        var result = await store.Create(actor, companyId, request, correlationId, ct);
        audit.Append(actor, companyId, result.Id, "WarehouseCreated", correlationId);
        return new(201, result, $"/api/v1/companies/{companyId}/warehouses/{result.Id}");
    }

    public async Task<CommandOutcome> Get(Actor actor, Guid companyId, Guid id, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        if (!actor.Permissions.Contains(InventoryPermissions.InventoryRead))
            throw new DomainException("permission_denied", "Inventory read permission is required.", 403);
        var item = await store.Get(actor, companyId, id, ct);
        if (item is null)
            throw new DomainException("warehouse_not_found", "Warehouse not found.", 404);
        return new(200, item);
    }

    public async Task<IReadOnlyList<WarehouseListItem>> List(Actor actor, Guid companyId, int skip, int take, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        if (!actor.Permissions.Contains(InventoryPermissions.InventoryRead))
            throw new DomainException("permission_denied", "Inventory read permission is required.", 403);
        if (skip is < 0 or > 10000 || take is < 1 or > 100)
            throw new DomainException("validation_failed", "Require skip 0–10000 and take 1–100.");
        return await store.List(actor, companyId, skip, take, ct);
    }
}

