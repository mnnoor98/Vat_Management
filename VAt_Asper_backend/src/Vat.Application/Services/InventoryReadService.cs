using Vat.Application.Security;
using Vat.Domain;

namespace Vat.Application;

public sealed record TransferHistoryLine(Guid Id, Guid ProductId, string ProductCode, string ProductName,
    string Uom, decimal Quantity, decimal UnitPrice, decimal TotalValue);
public sealed record TransferHistoryItem(Guid Id, string TransferNumber, DateOnly DispatchDate, string DispatchTime,
    string State, string VehicleNumber, string DriverName, string Remarks, DateTimeOffset CreatedAt, string CreatedBy,
    Guid FromWarehouseId, string FromWarehouseCode, string FromWarehouseName, Guid FromBranchId, string FromBranchName,
    Guid ToWarehouseId, string ToWarehouseCode, string ToWarehouseName, Guid ToBranchId, string ToBranchName,
    IReadOnlyList<TransferHistoryLine> Lines, int Version = 1, string? ApprovedBy = null);
public interface IInventoryReadStore
{
    Task<IReadOnlyList<TransferHistoryItem>> Transfers(Guid tenant, Guid company, int skip, int take, CancellationToken ct);
}
public sealed class InventoryReadService(IInventoryReadStore store)
{
    public Task<IReadOnlyList<TransferHistoryItem>> Transfers(Actor actor, Guid company, int skip, int take, CancellationToken ct)
    {
        actor.RequireCompany(company);
        if (!actor.Permissions.Contains(Permissions.InventoryRead)) throw new DomainException("permission_denied", "Inventory read permission is required.", 403);
        if (skip is < 0 or > 10000 || take is < 1 or > 100) throw new DomainException("validation_failed", "Require skip 0–10000 and take 1–100.");
        return store.Transfers(actor.TenantId, company, skip, take, ct);
    }
}
