using System.Security.Cryptography;
using System.Text.Json;
using Vat.Application;

namespace Vat.Messaging.Contracts.V1;

// The bounded prefix allows durable oversized-message quarantine without forwarding
// arbitrarily large bodies. Full hashes/length are trusted service delivery metadata.
public sealed record InboxDeliveryV1(int ContractVersion, string ConsumerOwner, string? MessageId,
    string? EventType, string? ContentType, int OriginalBodyLength, string BodySha256, byte[] BodyPrefix,
    string PrefixSha256)
{
    public const string Owner = "reporting-v1";
    public const int MaxBodyBytes = 1_048_576;

    public static InboxDeliveryV1 Create(ReceivedMessage message)
    {
        var prefix = message.Body.AsSpan(0, Math.Min(MaxBodyBytes, message.Body.Length)).ToArray();
        return new(1, Owner, message.MessageId, message.EventType, message.ContentType, message.Body.Length,
            Convert.ToHexString(SHA256.HashData(message.Body)), prefix, Convert.ToHexString(SHA256.HashData(prefix)));
    }

    public bool IsValid() => ContractVersion == 1 && ConsumerOwner == Owner && OriginalBodyLength >= 0
        && BodyPrefix is not null && BodyPrefix.Length == Math.Min(OriginalBodyLength, MaxBodyBytes)
        && ValidHash(BodySha256) && ValidHash(PrefixSha256)
        && Convert.ToHexString(SHA256.HashData(BodyPrefix)) == PrefixSha256
        && (OriginalBodyLength > MaxBodyBytes || BodySha256 == PrefixSha256)
        && Bounded(MessageId) && Bounded(EventType) && Bounded(ContentType);

    // Matches the original financial inbox fingerprint, including property casing.
    public string Fingerprint() => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
        new { MessageId, EventType, ContentType, bodyHash = BodySha256 })));

    private static bool Bounded(string? value) => value is null || value.Length <= 4096;
    private static bool ValidHash(string? value) => value is { Length: 64 }
        && value.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F');
}

public sealed record InboxCommitReceiptV1(int ContractVersion, string ConsumerOwner, string DeliveryFingerprint,
    string Status, Guid? DeadLetterId = null)
{
    public bool IsCommittedFor(InboxDeliveryV1 delivery) => ContractVersion == 1 && ConsumerOwner == InboxDeliveryV1.Owner
        && DeliveryFingerprint == delivery.Fingerprint()
        && (Status is "Processed" or "Ignored" or "Duplicate" ? DeadLetterId is null
            : Status == "Quarantined" && DeadLetterId is { } id && id != Guid.Empty);
}

public interface IInboxDeliveryTargetV1
{
    // A receipt is returned only after the owner's inbox/projection/quarantine commit.
    Task<InboxCommitReceiptV1> Deliver(InboxDeliveryV1 delivery, CancellationToken ct);
}
