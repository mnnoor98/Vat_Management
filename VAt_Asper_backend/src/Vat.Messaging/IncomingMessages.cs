// Namespaces retained for existing application/controller source compatibility.
namespace Vat.Application
{
    public sealed record ReceivedMessage(byte[] Body, string? MessageId, string? EventType, string? ContentType);
    public interface IIncomingEventProcessor
    {
        Task<Contracts.ProcessedMessage> Process(ReceivedMessage message, CancellationToken ct);
    }
}

namespace Vat.Application.Contracts
{
    public sealed record ProcessedMessage(string Status, Guid? DeadLetterId = null);
}
