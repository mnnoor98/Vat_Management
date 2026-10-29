using Vat.Domain.Purchasing;

namespace Vat.Application.Contracts;

public sealed record CreateCompanyRequest(Guid Id, string Name, string RegistrationReference);
public sealed record CreateBranchRequest(string Name);
public sealed record CompanyResponse(Guid Id, string Name, string RegistrationReference);
public sealed record BranchResponse(Guid Id, Guid CompanyId, string Name);
public sealed record CreatePurchaseRequest(Guid BranchId, string ExternalReference, string SupplierReference,
    DateOnly BusinessDate, IReadOnlyList<PurchaseLineInput> Lines);
public sealed record TransitionRequest(long ExpectedVersion);
public sealed record RejectPurchaseRequest(long ExpectedVersion, string Reason);
public sealed record PurchaseLineResponse(string ProductReference, string Description, decimal Quantity,
    decimal UnitPrice, decimal NetAmount);
public sealed record PurchaseResponse(Guid Id, Guid CompanyId, Guid BranchId, string ExternalReference,
    string SupplierReference, DateOnly BusinessDate, string Currency, string Status, long Version,
    decimal NetAmount, string CreatedBy, string? ApprovedBy, string? RejectionReason,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, IReadOnlyList<PurchaseLineResponse> Lines,
    string? CancellationReason, long? PostedVersion)
{
    public static PurchaseResponse From(Purchase purchase) => new(purchase.Id, purchase.CompanyId,
        purchase.BranchId, purchase.ExternalReference, purchase.SupplierReference, purchase.BusinessDate,
        purchase.Currency, purchase.Status.ToString(), purchase.Version, purchase.NetAmount,
        purchase.CreatedBy, purchase.ApprovedBy, purchase.RejectionReason, purchase.CreatedAt, purchase.UpdatedAt,
        purchase.Lines.Select(x => new PurchaseLineResponse(x.ProductReference, x.Description,
            x.Quantity, x.UnitPrice, x.NetAmount)).ToArray(), purchase.CancellationReason, purchase.PostedVersion);
}

public sealed record PurchaseApprovedV1(Guid PurchaseId, Guid BranchId, long DocumentVersion,
    string ExternalReference, DateOnly BusinessDate, string Currency, decimal NetAmount,
    string MakerId, string ApproverId);

public sealed record IntegrationEvent<T>(Guid EventId, string EventType, int SchemaVersion,
    DateTimeOffset OccurredAt, Guid TenantId, Guid CompanyId, Guid AggregateId, long AggregateVersion,
    string CorrelationId, T Payload);
