using Vat.Application.Security;
using Vat.Domain;

namespace Vat.Services.Inventory;

public sealed class TransferService(ITransferStore store, IAuditWriter audit, IEventWriter events)
{
    public async Task<CommandOutcome> Create(Actor actor, Guid company, TransferCreateRequest request, string correlation, CancellationToken ct)
    {
        Require(actor, company, InventoryPermissions.InventoryManage);
        Guard.Id(request.FromWarehouseId, "FromWarehouseId"); Guard.Id(request.ToWarehouseId, "ToWarehouseId");
        if (request.FromWarehouseId == request.ToWarehouseId || request.DispatchDate == default || request.Lines is null || request.Lines.Count is < 1 or > 100)
            throw new DomainException("validation_failed", "Require a date, two different warehouses and 1 to 100 lines.");
        if (request.Lines.Select(x => x.ProductId).Distinct().Count() != request.Lines.Count || request.Lines.Any(x =>
                x.ProductId == Guid.Empty || x.Quantity <= 0 || x.Quantity >= 1000000000000m || x.UnitPrice < 0 || x.UnitPrice >= 1000000000000m ||
                decimal.Round(x.Quantity, 6) != x.Quantity || decimal.Round(x.UnitPrice, 6) != x.UnitPrice))
            throw new DomainException("validation_failed", "Require unique products, positive quantities and nonnegative prices with at most six decimal places.");
        if (request.TransferNumber is { Length: > 100 } || request.Remarks is { Length: > 1000 } || request.DriverName is { Length: > 200 } ||
            request.VehicleNumber is { Length: > 100 } || request.DispatchTime is { Length: > 20 } || request.Lines.Any(x => x.Remarks is { Length: > 1000 }))
            throw new DomainException("validation_failed", "Transfer text exceeds its maximum length.");
        var result = await store.Create(actor, company, request, ct);
        audit.Append(actor, company, result.Id, "TransferCreated", correlation, result.Version);
        return new(201, result, $"/api/v1/companies/{company}/transfers");
    }

    public async Task<CommandOutcome> Transition(Actor actor, Guid company, Guid id, string action, TransferActionRequest request, string correlation, CancellationToken ct)
    {
        Require(actor, company, action == "approve" ? InventoryPermissions.InventoryApprove : action == "post" ? InventoryPermissions.InventoryPost : InventoryPermissions.InventoryManage);
        if (request.ExpectedVersion < 1) throw new DomainException("validation_failed", "ExpectedVersion must be positive.");
        if (request.Reason is { Length: > 1000 }) throw new DomainException("validation_failed", "Reason exceeds 1000 characters.");
        if (action == "cancel") Guard.Text(request.Reason ?? "", "Reason", 1000);
        var result = await store.Transition(actor, company, id, action, request, ct);
        audit.Append(actor, company, id, "Transfer" + action, correlation, result.Version, request.Reason);
        if (action == "post") events.Append(actor.TenantId, company, id, result.Version, nameof(TransferPostedV1), correlation,
            new TransferPostedV1(id, result.Reference, result.Version));
        return new(200, result);
    }

    private static void Require(Actor actor, Guid company, string permission)
    {
        actor.RequireCompany(company);
        if (!actor.Permissions.Contains(permission)) throw new DomainException("permission_denied", "The required permission is not granted.", 403);
    }
}
