using System.Security.Cryptography;
using System.Text.Json;
using Vat.Application;
using Vat.Messaging.Contracts.V1;
using Xunit;

namespace Vat.Messaging.Tests;

public sealed class InboxDeliveryTests
{
    private static ReceivedMessage Message() => new("{\"text\":\"বাংলা\"}"u8.ToArray(), Guid.NewGuid().ToString(), "EventV1", "application/json");

    [Fact]
    public void Contract_preserves_original_fingerprint_and_bounds_oversized_prefix()
    {
        var message = Message(); var delivery = InboxDeliveryV1.Create(message);
        var bodyHash = Convert.ToHexString(SHA256.HashData(message.Body));
        var oldFingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
            new { message.MessageId, message.EventType, message.ContentType, bodyHash })));
        Assert.True(delivery.IsValid()); Assert.Equal(oldFingerprint, delivery.Fingerprint());
        var restored = JsonSerializer.Deserialize<InboxDeliveryV1>(JsonSerializer.Serialize(delivery))!;
        Assert.True(restored.IsValid()); Assert.Equal(delivery.Fingerprint(), restored.Fingerprint());
        var large = message with { Body = new byte[InboxDeliveryV1.MaxBodyBytes + 100] };
        large.Body[^1] = 99;
        var bounded = InboxDeliveryV1.Create(large);
        Assert.True(bounded.IsValid()); Assert.Equal(InboxDeliveryV1.MaxBodyBytes, bounded.BodyPrefix.Length);
        Assert.Equal(large.Body.Length, bounded.OriginalBodyLength); Assert.NotEqual(bounded.BodySha256, bounded.PrefixSha256);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(large.Body)), bounded.BodySha256);
    }

    [Theory]
    [InlineData("version")]
    [InlineData("owner")]
    [InlineData("hash")]
    [InlineData("length")]
    [InlineData("metadata")]
    public void Invalid_deliveries_are_rejected(string kind)
    {
        var valid = InboxDeliveryV1.Create(Message());
        var invalid = kind switch
        {
            "version" => valid with { ContractVersion = 2 }, "owner" => valid with { ConsumerOwner = "other" },
            "hash" => valid with { BodySha256 = new string('0', 64) },
            "length" => valid with { OriginalBodyLength = valid.OriginalBodyLength + 1 },
            _ => valid with { EventType = new string('x', 4097) }
        };
        Assert.False(invalid.IsValid());
    }

    [Theory]
    [InlineData("Processed")]
    [InlineData("Ignored")]
    [InlineData("Duplicate")]
    [InlineData("Quarantined")]
    public async Task Only_committed_terminal_receipts_complete_broker_callback(string status)
    {
        var target = new Target(delivery => new(1, InboxDeliveryV1.Owner, delivery.Fingerprint(), status,
            status == "Quarantined" ? Guid.NewGuid() : null));
        await new InboxDeliveryHandler(target).Process(Message(), default);
        Assert.Equal(1, target.Calls);
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("fingerprint")]
    [InlineData("owner")]
    [InlineData("version")]
    [InlineData("quarantine-without-id")]
    [InlineData("processed-with-id")]
    public async Task Invalid_receipts_never_complete_broker_callback(string kind)
    {
        var target = new Target(delivery => new(kind == "version" ? 2 : 1, kind == "owner" ? "other" : InboxDeliveryV1.Owner,
            kind == "fingerprint" ? "wrong" : delivery.Fingerprint(),
            kind == "quarantine-without-id" ? "Quarantined" : kind == "Pending" ? "Pending" : "Processed",
            kind == "processed-with-id" ? Guid.NewGuid() : null));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new InboxDeliveryHandler(target).Process(Message(), default));
    }

    [Fact]
    public async Task Lost_response_and_cancellation_propagate_without_ack_or_automatic_retry()
    {
        var target = new ThrowingTarget(new HttpRequestException("response_lost"));
        await Assert.ThrowsAsync<HttpRequestException>(() => new InboxDeliveryHandler(target).Process(Message(), default));
        Assert.Equal(1, target.Calls);
        target = new ThrowingTarget(new OperationCanceledException());
        await Assert.ThrowsAsync<OperationCanceledException>(() => new InboxDeliveryHandler(target).Process(Message(), default));
        Assert.Equal(1, target.Calls);
    }

    private sealed class Target(Func<InboxDeliveryV1, InboxCommitReceiptV1> reply) : IInboxDeliveryTargetV1
    {
        public int Calls;
        public Task<InboxCommitReceiptV1> Deliver(InboxDeliveryV1 delivery, CancellationToken ct) { Calls++; return Task.FromResult(reply(delivery)); }
    }
    private sealed class ThrowingTarget(Exception exception) : IInboxDeliveryTargetV1
    {
        public int Calls;
        public Task<InboxCommitReceiptV1> Deliver(InboxDeliveryV1 delivery, CancellationToken ct) { Calls++; throw exception; }
    }
}
