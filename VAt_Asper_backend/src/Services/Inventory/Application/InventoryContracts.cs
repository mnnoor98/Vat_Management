using Vat.Application.Security;
using Vat.Domain;

namespace Vat.Services.Inventory;

public sealed record CommandOutcome(int StatusCode, object Body, string? Location = null);
public sealed record StoredResponse(int StatusCode, string Json, string? Location, bool Replayed);

public interface ICommandExecutor
{
    Task<StoredResponse> Execute(Actor actor, Guid companyId, string operation, string key,
        object request, Func<Task<CommandOutcome>> action, CancellationToken ct);
}

public static class InventoryPermissions
{
    public const string InventoryManage = "inventory.manage";
    public const string InventoryRead = "inventory.read";
    public const string InventoryApprove = "inventory.approve";
    public const string InventoryPost = "inventory.post";
}

public sealed record CreateWarehouseRequest(Guid BranchId, string Code, string Name);
public sealed record WarehouseResponse(Guid Id, Guid BranchId, string Code, string Name)
{
    public static WarehouseResponse From(Warehouse x) => new(x.Id, x.BranchId, x.Code, x.Name);
}

public sealed record WarehouseListItem(Guid Id, string Code, string Name, Guid BranchId, string BranchName);
public sealed record StockBalanceResponse(Guid WarehouseId, Guid ProductId, decimal Quantity, decimal ReservedQuantity, decimal AvailableQuantity);

public sealed record AllStockBalanceItem(
    Guid WarehouseId,
    string WarehouseCode,
    string WarehouseName,
    Guid BranchId,
    string BranchName,
    Guid ProductId,
    string ProductReference,
    string ProductDescription,
    string UnitOfMeasure,
    string TaxCode,
    decimal Quantity,
    decimal ReservedQuantity,
    decimal AvailableQuantity
);

public sealed record TransferLineDto(Guid ProductId, decimal Quantity, decimal UnitPrice, string? Remarks);
public sealed record TransferHistoryLine(Guid Id, Guid ProductId, string ProductCode, string ProductName,
    string Uom, decimal Quantity, decimal UnitPrice, decimal TotalValue);
public sealed record TransferHistoryItem(Guid Id, string TransferNumber, DateOnly DispatchDate, string DispatchTime,
    string State, string VehicleNumber, string DriverName, string Remarks, DateTimeOffset CreatedAt, string CreatedBy,
    Guid FromWarehouseId, string FromWarehouseCode, string FromWarehouseName, Guid FromBranchId, string FromBranchName,
    Guid ToWarehouseId, string ToWarehouseCode, string ToWarehouseName, Guid ToBranchId, string ToBranchName,
    IReadOnlyList<TransferHistoryLine> Lines, int Version = 1, string? ApprovedBy = null);

public sealed record TransferLineRequest(Guid ProductId, decimal Quantity, decimal UnitPrice, string? Remarks);
public sealed record TransferCreateRequest(string? TransferNumber, DateOnly DispatchDate, string? DispatchTime,
    Guid FromWarehouseId, Guid ToWarehouseId, string? VehicleNumber, string? DriverName, string? Remarks,
    List<TransferLineRequest> Lines);

public sealed record TransferActionRequest(int ExpectedVersion, string? Reason = null);
public sealed record TransferResult(Guid Id, string Reference, string State, int Version);
public sealed record TransferPostedV1(Guid TransferId, string Reference, int Version);

public interface IWarehouseStore
{
    Task<WarehouseResponse> Create(Actor actor, Guid companyId, CreateWarehouseRequest request, string correlationId, CancellationToken ct);
    Task<WarehouseResponse?> Get(Actor actor, Guid companyId, Guid id, CancellationToken ct);
    Task<IReadOnlyList<WarehouseListItem>> List(Actor actor, Guid companyId, int skip, int take, CancellationToken ct);
}

public interface IStockStore
{
    Task<IReadOnlyList<StockBalanceResponse>> Balances(Actor actor, Guid companyId, Guid warehouseId, Guid? beforeProduct, int take, CancellationToken ct);
    Task<IReadOnlyList<AllStockBalanceItem>> AllBalances(Actor actor, Guid companyId, Guid? warehouseId, int skip, int take, CancellationToken ct);
}

public interface IInventoryReadStore
{
    Task<IReadOnlyList<TransferHistoryItem>> Transfers(Guid tenant, Guid company, int skip, int take, CancellationToken ct);
}

public interface ITransferStore
{
    Task<TransferResult> Create(Actor actor, Guid company, TransferCreateRequest request, CancellationToken ct);
    Task<TransferResult> Transition(Actor actor, Guid company, Guid id, string action, TransferActionRequest request, CancellationToken ct);
}

public interface IPeriodGuard
{
    Task RequireOpen(Guid tenantId, Guid companyId, DateOnly businessDate, CancellationToken ct);
}

public interface IAuditWriter
{
    void Append(Actor actor, Guid company, Guid resourceId, string action, string correlation, int version = 1, string? reason = null);
}

public interface IEventWriter
{
    void Append<T>(Guid tenant, Guid company, Guid aggregateId, int version, string type, string correlation, T payload) where T : class;
}
