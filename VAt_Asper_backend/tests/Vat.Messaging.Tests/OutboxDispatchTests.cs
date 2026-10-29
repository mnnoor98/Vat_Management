using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Vat.Infrastructure.Messaging;
using Vat.Messaging.Contracts.V1;
using Xunit;

namespace Vat.Messaging.Tests;

public sealed class OutboxDispatchTests
{
    private static ClaimedOutboxMessageV1 Message()
    {
        var body = "{\"schemaVersion\":1,\"payload\":{\"value\":12}}"u8.ToArray();
        return new(new(1, "financial-coordinated", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()),
            Guid.NewGuid(), 5, "FinancialEntryRecordedV1", DateTimeOffset.UtcNow, body, Convert.ToHexString(SHA256.HashData(body)), DateTimeOffset.UtcNow.AddMinutes(1), 1);
    }

    [Fact]
    public void Protocol_round_trips_and_has_no_financial_or_database_dependencies()
    {
        var original = Message(); var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var restored = JsonSerializer.Deserialize<ClaimedOutboxMessageV1>(JsonSerializer.Serialize(original, options), options)!;
        Assert.True(restored.HasValidIntegrity()); Assert.Equal(original.Receipt, restored.Receipt); Assert.Equal(original.Payload, restored.Payload);
        Assert.DoesNotContain(typeof(OutboxDispatcher).Assembly.GetReferencedAssemblies(), x =>
            x.Name is "Vat.Infrastructure" or "Vat.Application" or "Vat.Domain" or "Npgsql" or "RabbitMQ.Client"
            || x.Name!.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Confirmed_publish_preserves_event_identity_and_original_wire_bytes()
    {
        var claim = Message(); var source = new MemorySource(claim); var broker = new Broker();
        Assert.Equal(1, await Dispatcher(source, broker).DispatchBatch(default));
        var sent = Assert.Single(broker.Messages);
        Assert.Equal(claim.Receipt.MessageId, sent.Id); Assert.Equal(claim.EventType, sent.EventType);
        Assert.Equal(claim.Payload, sent.Body); Assert.Equal(claim.Attempt, sent.Attempt); Assert.Equal(claim.Receipt.LeaseToken, sent.LeaseToken);
        Assert.Equal(claim.Receipt, Assert.Single(source.Confirmations)); Assert.Empty(source.Failures);
    }

    [Theory]
    [InlineData("version")]
    [InlineData("digest")]
    [InlineData("scope")]
    public async Task Invalid_dispatch_contract_is_not_published(string invalid)
    {
        var claim = Message(); claim = invalid switch
        {
            "version" => claim with { Receipt = claim.Receipt with { ContractVersion = 2 } },
            "scope" => claim with { Receipt = claim.Receipt with { TenantId = Guid.Empty } },
            _ => claim with { Payload = "tampered"u8.ToArray() }
        };
        var source = new MemorySource(claim); var broker = new Broker();
        await Dispatcher(source, broker).DispatchBatch(default);
        Assert.Empty(broker.Messages); Assert.Empty(source.Confirmations); Assert.Equal("invalid_dispatch_contract", Assert.Single(source.Failures));
    }

    [Fact]
    public async Task Publisher_cannot_mutate_the_claim_and_stale_confirmation_is_not_forced()
    {
        var claim = Message(); var source = new MemorySource(claim) { ConfirmResult = false };
        var broker = new Broker((message, _) => { message.Body[0] ^= 1; return Task.CompletedTask; });
        Assert.Equal(1, await Dispatcher(source, broker).DispatchBatch(default));
        Assert.True(claim.HasValidIntegrity()); Assert.Single(source.Confirmations); Assert.Empty(source.Failures);
    }

    [Theory]
    [InlineData("known", "publisher_rejected")]
    [InlineData("timeout", "publish_timeout")]
    [InlineData("unknown", "broker_unavailable")]
    public async Task Publish_failure_uses_safe_codes_without_acknowledgement(string failure, string expected)
    {
        var source = new MemorySource(Message()); var broker = new Broker((_, _) => throw failure switch
        {
            "known" => new MessagePublishException("publisher_rejected"),
            "timeout" => new OperationCanceledException(),
            _ => new InvalidOperationException("private connection details must not become the error code")
        });
        await Dispatcher(source, broker).DispatchBatch(default);
        Assert.Empty(source.Confirmations); Assert.Equal(expected, Assert.Single(source.Failures));
    }

    [Fact]
    public async Task Shutdown_leaves_the_lease_for_source_recovery()
    {
        using var cancellation = new CancellationTokenSource(); var source = new MemorySource(Message());
        var broker = new Broker((_, _) => { cancellation.Cancel(); throw new OperationCanceledException(cancellation.Token); });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Dispatcher(source, broker).DispatchBatch(cancellation.Token));
        Assert.Empty(source.Confirmations); Assert.Empty(source.Failures);
    }

    [Fact]
    public async Task Batch_limit_is_enforced_without_claiming_the_next_message()
    {
        var source = new MemorySource(Message(), Message(), Message());
        Assert.Equal(2, await Dispatcher(source, new Broker(), 2).DispatchBatch(default));
        Assert.Equal(1, source.Pending); Assert.Equal(2, source.Confirmations.Count);
    }

    private static OutboxDispatcher Dispatcher(MemorySource source, Broker broker, int size = 25) =>
        new(source, broker, Options.Create(new MessagingSettings { BatchSize = size }));

    private sealed class MemorySource(params ClaimedOutboxMessageV1[] messages) : IOutboxLeaseSourceV1
    {
        private readonly Queue<ClaimedOutboxMessageV1> _messages = new(messages);
        public int Pending => _messages.Count;
        public bool ConfirmResult { get; init; } = true;
        public List<OutboxReceiptV1> Confirmations { get; } = [];
        public List<string> Failures { get; } = [];
        public Task<ClaimedOutboxMessageV1?> Claim(CancellationToken ct) => Task.FromResult(_messages.TryDequeue(out var message) ? message : null);
        public Task<bool> Confirm(OutboxReceiptV1 receipt, CancellationToken ct) { Confirmations.Add(receipt); return Task.FromResult(ConfirmResult); }
        public Task<bool> Fail(OutboxReceiptV1 receipt, string code, CancellationToken ct) { Failures.Add(code); return Task.FromResult(true); }
    }
    private sealed class Broker(Func<OutgoingMessage, CancellationToken, Task>? publish = null) : IEventPublisher
    {
        public List<OutgoingMessage> Messages { get; } = [];
        public Task Publish(OutgoingMessage message, CancellationToken ct) { Messages.Add(message); return publish?.Invoke(message, ct) ?? Task.CompletedTask; }
    }
}
