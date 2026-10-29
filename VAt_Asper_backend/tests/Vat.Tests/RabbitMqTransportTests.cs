using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Vat.Infrastructure.Messaging;
using Xunit;

namespace Vat.Tests;

public sealed class RabbitMqFactAttribute : FactAttribute
{
    public RabbitMqFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("VAT_TEST_RABBITMQ_URI")))
            Skip = "Set VAT_TEST_RABBITMQ_URI to run live RabbitMQ confirmation and manual-ack tests.";
    }
}

public sealed class RabbitMqTransportTests
{
    private static MessagingSettings Settings() => new()
    {
        Uri = Environment.GetEnvironmentVariable("VAT_TEST_RABBITMQ_URI")!,
        Exchange = "vat.test." + Guid.NewGuid().ToString("N"), ReportingQueue = "vat.test." + Guid.NewGuid().ToString("N")
    };

    [RabbitMqFact]
    public async Task Live_broker_confirms_delivery_and_consumer_requeues_until_processing_commits()
    {
        var settings = Settings();
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var transport = new RabbitMqTransport(Options.Create(settings));
        var id = Guid.NewGuid();
        var body = "{\"test\":true}"u8.ToArray();
        await transport.Publish(new(id, "TestEventV1", body, Guid.NewGuid(), 1), stop.Token);
        // A failed processing callback must leave the message unacknowledged and requeue on disconnect.
        await Assert.ThrowsAsync<InvalidOperationException>(() => transport.Consume((_, _) => throw new InvalidOperationException("Injected consumer failure"), stop.Token));
        var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var consume = transport.Consume((message, _) =>
        {
            Assert.Equal(id.ToString(), message.MessageId);
            Assert.Equal(body, message.Body);
            received.TrySetResult(); return Task.CompletedTask;
        }, stop.Token);
        await received.Task.WaitAsync(stop.Token);
        // Wait for a following channel operation to observe the acknowledgement, rather than racing cancellation.
        await Task.Delay(100, stop.Token);
        stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => consume);
        await Cleanup(settings);
    }

    [RabbitMqFact]
    public async Task Live_broker_unroutable_publication_is_not_reported_as_confirmed_success()
    {
        var settings = Settings();
        await using var transport = new RabbitMqTransport(Options.Create(settings));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var message = new OutgoingMessage(Guid.NewGuid(), "TestEventV1", "{}"u8.ToArray(), Guid.NewGuid(), 1);
        await transport.Publish(message, timeout.Token);
        await using var connection = await new ConnectionFactory { Uri = new Uri(settings.Uri) }.CreateConnectionAsync(timeout.Token);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: timeout.Token);
        await channel.QueueDeleteAsync(settings.ReportingQueue, ifUnused: false, ifEmpty: false, cancellationToken: timeout.Token);
        var error = await Assert.ThrowsAsync<MessagePublishException>(() => transport.Publish(message, timeout.Token));
        Assert.Equal("publisher_rejected", error.Code);
        await Cleanup(settings, queueExists: false);
    }

    private static async Task Cleanup(MessagingSettings settings, bool queueExists = true)
    {
        await using var connection = await new ConnectionFactory { Uri = new Uri(settings.Uri) }.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        if (queueExists) await channel.QueueDeleteAsync(settings.ReportingQueue, ifUnused: false, ifEmpty: false);
        await channel.ExchangeDeleteAsync(settings.Exchange, ifUnused: false);
    }
}
