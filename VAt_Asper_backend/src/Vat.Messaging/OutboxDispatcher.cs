using Microsoft.Extensions.Options;
using Vat.Messaging.Contracts.V1;

namespace Vat.Infrastructure.Messaging;

// Independent dispatch algorithm: no DbContext, SQL or financial domain dependency.
public sealed class OutboxDispatcher(IOutboxLeaseSourceV1 source, IEventPublisher publisher, IOptions<MessagingSettings> settings)
{
    public async Task<int> DispatchBatch(CancellationToken ct)
    {
        var count = 0;
        for (; count < settings.Value.BatchSize; count++)
        {
            ct.ThrowIfCancellationRequested();
            var message = await source.Claim(ct);
            if (message is null) break;
            try
            {
                if (!message.HasValidIntegrity()) throw new MessagePublishException("invalid_dispatch_contract");
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(TimeSpan.FromSeconds(settings.Value.PublishTimeoutSeconds));
                // A transport cannot mutate the claim used for settlement.
                await publisher.Publish(new OutgoingMessage(message.Receipt.MessageId, message.EventType,
                    message.Payload.ToArray(), message.Receipt.LeaseToken, message.Attempt), timeout.Token);
                await source.Confirm(message.Receipt, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                var code = exception is MessagePublishException known ? known.Code
                    : exception is OperationCanceledException ? "publish_timeout" : "broker_unavailable";
                await source.Fail(message.Receipt, code, ct);
            }
        }
        return count;
    }
}
