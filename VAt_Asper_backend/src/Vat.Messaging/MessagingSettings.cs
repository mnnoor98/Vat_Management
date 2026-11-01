namespace Vat.Infrastructure.Messaging;

public sealed class MessagingSettings
{
    public bool Enabled { get; set; }
    public string Uri { get; set; } = "";
    public string Exchange { get; set; } = "vat.events.v1";
    public string ReportingQueue { get; set; } = "vat.reporting.v1";
    public string ManufacturingQueue { get; set; } = "vat.manufacturing.execution.v1";
    public int BatchSize { get; set; } = 25;
    public int PollSeconds { get; set; } = 2;
    public int PublishTimeoutSeconds { get; set; } = 15;
    public int LeaseSeconds { get; set; } = 60;
    public int MaxAttempts { get; set; } = 8;

    public bool IsValid() => BatchSize is >= 1 and <= 100 && PollSeconds is >= 1 and <= 60 && MaxAttempts is >= 1 and <= 20 &&
        PublishTimeoutSeconds is >= 1 and <= 60 && LeaseSeconds >= PublishTimeoutSeconds + 10 && LeaseSeconds <= 300 &&
        ValidName(Exchange) && ValidName(ReportingQueue) && ValidName(ManufacturingQueue) && (!Enabled ||
            (System.Uri.TryCreate(Uri, UriKind.Absolute, out var uri) && (uri.Scheme == "amqps" || uri.Scheme == "amqp" && uri.IsLoopback)));

    private static bool ValidName(string value) => value.Length is > 0 and <= 128 && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-');
}

public sealed record OutgoingMessage(Guid Id, string EventType, byte[] Body, Guid LeaseToken, int Attempt);
public interface IEventPublisher
{
    Task Publish(OutgoingMessage message, CancellationToken ct);
}
public sealed class MessagePublishException(string code) : Exception(code)
{
    public string Code { get; } = code;
}
