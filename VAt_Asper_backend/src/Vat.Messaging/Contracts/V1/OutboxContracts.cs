using System.Security.Cryptography;

namespace Vat.Messaging.Contracts.V1;

// Trusted source-worker port, not a public user-facing HTTP API. Version is the
// dispatch protocol version; the original event JSON/schema is carried unchanged.
public sealed record OutboxReceiptV1(int ContractVersion, string SourceOwner, Guid MessageId,
    Guid TenantId, Guid CompanyId, Guid LeaseToken)
{
    public bool IsWellFormed() => ContractVersion == 1 && !string.IsNullOrWhiteSpace(SourceOwner)
        && SourceOwner.Length <= 80 && MessageId != Guid.Empty && TenantId != Guid.Empty
        && CompanyId != Guid.Empty && LeaseToken != Guid.Empty;
}

public sealed record ClaimedOutboxMessageV1(OutboxReceiptV1 Receipt, Guid AggregateId, long AggregateVersion,
    string EventType, DateTimeOffset CommittedAt, byte[] Payload, string PayloadSha256,
    DateTimeOffset LeaseExpiresAt, int Attempt)
{
    public bool HasValidIntegrity() => Receipt is not null && Receipt.IsWellFormed()
        && AggregateId != Guid.Empty && AggregateVersion > 0 && !string.IsNullOrWhiteSpace(EventType)
        && EventType.Length <= 200 && CommittedAt != default && LeaseExpiresAt != default
        && Attempt > 0 && Payload is { Length: > 0 }
        && string.Equals(Convert.ToHexString(SHA256.HashData(Payload)), PayloadSha256, StringComparison.OrdinalIgnoreCase);
}

public interface IOutboxLeaseSourceV1
{
    Task<ClaimedOutboxMessageV1?> Claim(CancellationToken ct);
    // False means the scoped receipt is stale/invalid or was settled already.
    Task<bool> Confirm(OutboxReceiptV1 receipt, CancellationToken ct);
    Task<bool> Fail(OutboxReceiptV1 receipt, string errorCode, CancellationToken ct);
}
