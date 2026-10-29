using Vat.Application.Security;
using Vat.Domain.Posting;
using Vat.Domain.Purchasing;

namespace Vat.Services.Purchase
{
    public sealed record CommandOutcome(int StatusCode, object Body, string? Location = null);
    public sealed record StoredResponse(int StatusCode, string Json, string? Location, bool Replayed);

    public interface ICommandExecutor
    {
        Task<StoredResponse> Execute(Actor actor, Guid companyId, string operation, string key,
            object request, Func<Task<CommandOutcome>> action, CancellationToken ct);
    }

    public interface IAuditWriter
    {
        void Append(Actor actor, Guid companyId, Guid resourceId, string action, string correlationId,
            long? version = null, string? reason = null);
    }

    public interface IEventWriter
    {
        void Append<T>(Guid tenantId, Guid companyId, Guid aggregateId, long version,
            string eventType, string correlationId, T payload);
    }

    public interface IPurchaseStore
    {
        Task<Vat.Domain.Purchasing.Purchase?> Find(Guid tenantId, Guid companyId, Guid purchaseId, CancellationToken ct);
        Task<IReadOnlyList<Vat.Domain.Purchasing.Purchase>> List(Guid tenantId, Guid companyId, Guid? before, int take, CancellationToken ct);
        void Add(Vat.Domain.Purchasing.Purchase purchase);
    }

    public interface IPurchasePostingStore
    {
        Task<PurchasePostingPlan?> FindPlan(Guid tenantId, Guid companyId, Guid purchaseId, Guid planId, CancellationToken ct);
        Task<PurchasePostingPlan?> LatestPlan(Guid tenantId, Guid companyId, Guid purchaseId, CancellationToken ct);
        void AddPlan(PurchasePostingPlan plan);
    }

    public interface IPurchaseReversalStore
    {
        Task<PurchaseReversal?> Find(Guid tenantId, Guid companyId, Guid purchaseId, Guid reversalId, CancellationToken ct);
        void Add(PurchaseReversal reversal);
    }
}

namespace Vat.Services.Purchase.Contracts
{
    public sealed record CreatePurchaseRequest(Guid BranchId, string ExternalReference, string SupplierReference, DateOnly BusinessDate, List<PurchaseLineInput> Lines);
    public sealed record PurchaseLineResponse(Guid Id, string ProductReference, string Description, decimal Quantity, decimal UnitPrice, decimal NetAmount);
    public sealed record PurchaseResponse(Guid Id, Guid CompanyId, Guid BranchId, string ExternalReference, string SupplierReference, DateOnly BusinessDate, string Status, decimal NetAmount, long Version, IReadOnlyList<PurchaseLineResponse> Lines)
    {
        public static PurchaseResponse From(Vat.Domain.Purchasing.Purchase p) => new(
            p.Id, p.CompanyId, p.BranchId, p.ExternalReference, p.SupplierReference, p.BusinessDate,
            p.Status.ToString(), p.NetAmount, p.Version,
            p.Lines.Select(l => new PurchaseLineResponse(l.Id, l.ProductReference, l.Description, l.Quantity, l.UnitPrice, l.NetAmount)).ToArray());
    }

    public sealed record PreparePostingRequest(long ExpectedVersion, Guid WarehouseId);
    public sealed record PostingPlanResponse(Guid Id, Guid PurchaseId, Guid WarehouseId, string Status,
        long SourceVersion, string PreparedBy, string? PostedBy, DateTimeOffset CreatedAt, DateTimeOffset? PostedAt,
        CalculationSnapshot Snapshot, string? CancellationReason)
    {
        public static PostingPlanResponse From(PurchasePostingPlan x) => new(x.Id, x.PurchaseId, x.WarehouseId,
            x.Status.ToString(), x.SourceVersion, x.PreparedBy, x.PostedBy, x.CreatedAt, x.PostedAt, x.Snapshot, x.CancellationReason);
    }
    public sealed record PostingStatusResponse(Guid PurchaseId, string Status, long Version, PostingPlanResponse? LatestPlan);

    public sealed record TransitionRequest(long ExpectedVersion);
    public sealed record RejectPurchaseRequest(long ExpectedVersion, string Reason);
    public sealed record CancelPostingRequest(long ExpectedVersion, string Reason);
    public sealed record RequestReversalRequest(DateOnly CorrectionDate, string Reason);
    public sealed record ApproveReversalRequest(long ExpectedVersion, string Reason);
    public sealed record PurchaseReversalResponse(Guid Id, Guid PurchaseId, string Status, string Reason, long Version)
    {
        public static PurchaseReversalResponse From(PurchaseReversal r) =>
            new(r.Id, r.PurchaseId, r.Status.ToString(), r.Reason, r.Version);
    }
}

