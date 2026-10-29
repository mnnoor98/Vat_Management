using Vat.Application.Security;
using Vat.Messaging.Contracts.V1;

namespace Vat.Services.Reporting.Application;

public sealed record OpenFinancialInboxRequest(Guid GenerationId, string Reason);
public sealed record RetireFinancialInboxRequest(Guid GenerationId, long ExpectedVersion, string Reason);
public sealed record FinancialInboxStateResult(Guid GenerationId, string Status, long Version, bool LiveCoverageVerified = false);
public sealed record FinancialInboxSummary(Guid GenerationId, DateOnly From, DateOnly Until, IReadOnlyList<SnapshotCurrencyTotals> Totals,
    int PendingOriginals, int QuarantinedMessages, DateTimeOffset? LastDeliveryAt, bool LiveCoverageVerified = false);
public interface IFinancialGenerationInbox
{
    Task<FinancialInboxStateResult> Open(Actor actor, Guid company, Guid key, OpenFinancialInboxRequest request, CancellationToken ct);
    Task<FinancialInboxStateResult> Retire(Actor actor, Guid company, Guid key, RetireFinancialInboxRequest request, CancellationToken ct);
    // Expected scope comes from a future authenticated delivery host, never the event body.
    Task<ReportingGenerationReceiptV1> Deliver(Guid tenant, Guid company, Guid generation, InboxDeliveryV1 delivery, CancellationToken ct);
    Task<ReportingGenerationReceiptV1> Replay(Actor actor, Guid company, Guid generation, Guid deadLetter,
        Guid key, string reason, CancellationToken ct);
    Task<FinancialInboxSummary> Summary(Actor actor, Guid company, Guid generation, DateOnly from, DateOnly until, CancellationToken ct);
}
