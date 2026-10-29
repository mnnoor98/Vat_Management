using Vat.Domain.Company;
using Vat.Domain.Posting;

namespace Vat.Application.Contracts;

public sealed record CreatePeriodRequest(string Name, DateOnly From, DateOnly Until);
public sealed record PeriodActionRequest(long ExpectedVersion, string Reason);
public sealed record PeriodResponse(Guid Id, string Name, DateOnly From, DateOnly Until, string Status, long Version,
    string? CloseRequestedBy, string? ClosedBy, DateTimeOffset? ClosedAt, PeriodCloseSnapshot? Snapshot)
{
    public static PeriodResponse FromPeriod(FiscalPeriod period) => new(period.Id, period.Name, period.From, period.Until,
        period.Status.ToString(), period.Version, period.CloseRequestedBy, period.ClosedBy, period.ClosedAt, period.Snapshot);
}
public sealed record RequestReversalRequest(long ExpectedVersion, DateOnly CorrectionDate, string Reason);
public sealed record ReversalResponse(Guid Id, Guid PurchaseId, Guid PlanId, long SourceVersion, DateOnly CorrectionDate,
    string Reason, string RequestedBy, string? ApprovedBy, string Status, DateTimeOffset CreatedAt, DateTimeOffset? PostedAt, string? CancellationReason)
{
    public static ReversalResponse From(PurchaseReversal x) => new(x.Id, x.PurchaseId, x.PlanId, x.SourceVersion, x.CorrectionDate,
        x.Reason, x.RequestedBy, x.ApprovedBy, x.Status.ToString(), x.CreatedAt, x.PostedAt, x.CancellationReason);
}
public sealed record PeriodClosedV1(Guid PeriodId, DateOnly From, DateOnly Until, string LedgerFingerprint,
    decimal NetAmount, decimal TaxAmount, decimal RecoverableTaxAmount);
public sealed record PurchaseReversedV1(Guid PurchaseId, Guid ReversalId, Guid PlanId, DateOnly CorrectionDate, string Reason);
