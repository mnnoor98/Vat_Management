using Vat.Application.Contracts;
using Vat.Application.Security;

namespace Vat.Application;

public interface IReportingService
{
    Task<FinancialReportResponse> Summary(Actor actor, Guid companyId, DateOnly from, DateOnly until, CancellationToken ct);
    Task<IReadOnlyList<ReportEntryResponse>> Entries(Actor actor, Guid companyId, DateOnly from, DateOnly until, Guid? before, int take, CancellationToken ct);
    Task<CommandOutcome> Backfill(Actor actor, Guid companyId, string reason, string correlationId, CancellationToken ct);
    Task<CommandOutcome> Rebuild(Actor actor, Guid companyId, string reason, string correlationId, CancellationToken ct);
    Task<IReadOnlyList<MessageStatusResponse>> Outbox(Actor actor, Guid companyId, Guid? before, int take, CancellationToken ct);
    Task<CommandOutcome> RetryOutbox(Actor actor, Guid companyId, Guid id, string reason, string correlationId, CancellationToken ct);
    Task<IReadOnlyList<DeadLetterResponse>> DeadLetters(Actor actor, Guid companyId, Guid? before, int take, CancellationToken ct);
    Task<CommandOutcome> Replay(Actor actor, Guid companyId, Guid id, string reason, string correlationId, CancellationToken ct);
}
