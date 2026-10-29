using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using Vat.Application;

namespace Vat.Infrastructure.Messaging;

// Broker-only implementation; the legacy namespace preserves source compatibility.

public sealed class RabbitMqTransport(IOptions<MessagingSettings> options) : IEventPublisher, IAsyncDisposable
{
    private readonly SemaphoreSlim _publishGate = new(1, 1);
    private IConnection? _connection;
    private IChannel? _publisher;
    private int _disposed;

    private ConnectionFactory Factory() => new()
    {
        Uri = new Uri(options.Value.Uri), AutomaticRecoveryEnabled = false,
        RequestedConnectionTimeout = TimeSpan.FromSeconds(10), ClientProvidedName = "enterprise-vat"
    };

    public async Task Publish(OutgoingMessage message, CancellationToken ct)
    {
        await _publishGate.WaitAsync(ct);
        try
        {
            if (_publisher?.IsOpen != true)
            {
                await Reset();
                _connection = await Factory().CreateConnectionAsync(ct);
                _publisher = await _connection.CreateChannelAsync(new CreateChannelOptions(
                    publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true), ct);
                await Declare(_publisher, ct);
            }
            await _publisher.BasicPublishAsync(options.Value.Exchange, message.EventType, mandatory: true,
                basicProperties: new BasicProperties { Persistent = true, ContentType = "application/json", MessageId = message.Id.ToString(), Type = message.EventType },
                body: message.Body, cancellationToken: ct);
        }
        catch (PublishException)
        {
            await Reset();
            throw new MessagePublishException("publisher_rejected");
        }
        catch (OperationCanceledException) { await Reset(); throw; }
        catch (Exception)
        {
            await Reset();
            throw new MessagePublishException("broker_unavailable");
        }
        finally { _publishGate.Release(); }
    }

    public async Task Consume(Func<ReceivedMessage, CancellationToken, Task> process, CancellationToken ct)
    {
        await using var connection = await Factory().CreateConnectionAsync(ct);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);
        await Declare(channel, ct);
        await channel.BasicQosAsync(0, 16, false, ct);
        var failure = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        channel.ChannelShutdownAsync += (_, _) => { failure.TrySetException(new IOException("consumer_channel_closed")); return Task.CompletedTask; };
        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, delivery) =>
        {
            try
            {
                // RabbitMQ's delivery memory must be copied before it escapes the callback.
                await process(new ReceivedMessage(delivery.Body.ToArray(), delivery.BasicProperties.MessageId,
                    delivery.BasicProperties.Type, delivery.BasicProperties.ContentType), ct);
                await channel.BasicAckAsync(delivery.DeliveryTag, false, ct);
            }
            catch (Exception exception)
            {
                // Leave unacknowledged. Outer disposal requeues it; the worker reconnects with backoff.
                failure.TrySetException(exception);
            }
        };
        await channel.BasicConsumeAsync(options.Value.ReportingQueue, autoAck: false, consumer: consumer, cancellationToken: ct);
        await failure.Task.WaitAsync(ct);
    }

    private async Task Declare(IChannel channel, CancellationToken ct)
    {
        await channel.ExchangeDeclareAsync(options.Value.Exchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: ct);
        await channel.QueueDeclareAsync(options.Value.ReportingQueue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?> { ["x-queue-type"] = "quorum" }, cancellationToken: ct);
        // Consume all current notifications so mandatory publishes have a durable route.
        await channel.QueueBindAsync(options.Value.ReportingQueue, options.Value.Exchange, "#", cancellationToken: ct);
    }

    private async Task Reset()
    {
        if (_publisher is not null) { await _publisher.DisposeAsync(); _publisher = null; }
        if (_connection is not null) { await _connection.DisposeAsync(); _connection = null; }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        await _publishGate.WaitAsync();
        try { await Reset(); }
        finally { _publishGate.Release(); _publishGate.Dispose(); }
    }
}
