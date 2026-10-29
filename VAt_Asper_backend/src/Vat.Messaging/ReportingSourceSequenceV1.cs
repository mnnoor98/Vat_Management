namespace Vat.Messaging.Contracts.V1;

// Sequence orders commits after LegacyThroughSequence within a company epoch.
// Earlier entries are a migration baseline, not reconstructed historical commit order.
public sealed record ReportingSourceBoundaryV1(Guid Epoch, long Sequence, long LegacyThroughSequence);

public static class ReportingSourceJournalV1
{
    public const int MaxTake = 100;
    public const int MaxEventBytes = 128 * 1024;
    public const int MaxPageBytes = 1024 * 1024;
}

public sealed record ReportingSourceJournalEventV1(long Sequence, Guid EventId, string EnvelopeJson);
public sealed record ReportingSourceJournalPageV1(int Version, Guid TenantId, Guid CompanyId,
    string Scope, Guid Epoch, long LegacyThroughSequence, long AfterSequence, long ThroughSequence,
    long NextAfterSequence, bool Complete, IReadOnlyList<ReportingSourceJournalEventV1> Events);
