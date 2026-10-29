using Vat.Application;
using Vat.Messaging.Contracts.V1;

namespace Vat.Messaging;

public sealed class InboxDeliveryHandler(IInboxDeliveryTargetV1 target)
{
    // RabbitMQ's callback may acknowledge only after this task succeeds.
    public async Task Process(ReceivedMessage message, CancellationToken ct)
    {
        var delivery = InboxDeliveryV1.Create(message);
        if (!delivery.IsValid()) throw new InvalidOperationException("Invalid inbox delivery contract.");
        var receipt = await target.Deliver(delivery, ct);
        if (receipt is null || !receipt.IsCommittedFor(delivery))
            throw new InvalidOperationException("Invalid inbox commit receipt.");
    }
}
