using Vat.Application.Security;
using Vat.Domain;

namespace Vat.Services.Integration;

public enum MessageOperatorAction { ListOutbox, RetryOutbox, ListDeadLetters, ReplayDeadLetter }
public sealed record OperatorRetryRequest(string Reason);
public sealed record OperatorReply(int StatusCode, string Json, bool? Replayed = null);
public sealed record OperatorRequest(MessageOperatorAction Action, Guid CompanyId, Guid? Id, Guid? Before, int Take,
    string? Reason, string BearerToken, string? IdempotencyKey, string CorrelationId);
public interface IIntegrationOperatorClient
{
    Task<OperatorReply> Send(OperatorRequest request, CancellationToken ct);
}

public sealed class IntegrationOperatorService(IIntegrationOperatorClient client)
{
    public Task<OperatorReply> Execute(Actor actor, OperatorRequest request, CancellationToken ct)
    {
        actor.RequireCompany(request.CompanyId);
        var mutation = request.Action is MessageOperatorAction.RetryOutbox or MessageOperatorAction.ReplayDeadLetter;
        var permission = mutation ? Permissions.MessagingRetry : Permissions.MessagingRead;
        if (!actor.Permissions.Contains(permission)) throw new DomainException("permission_denied", "The required permission is not granted.", 403);
        if (!Enum.IsDefined(request.Action)) throw new DomainException("validation_failed", "Unknown operator action.");
        if (request.Take is < 1 or > 100) throw new DomainException("validation_failed", "take must be between 1 and 100.");
        if (mutation)
        {
            Guard.Id(request.Id ?? Guid.Empty, "id"); Guard.Text(request.Reason ?? "", "Reason", 1000);
            if (!Guid.TryParseExact(request.IdempotencyKey, "D", out var key) || key == Guid.Empty)
                throw new DomainException("idempotency_key_required", "Idempotency-Key must be a non-empty UUID in D format.", 400);
        }
        if (!request.BearerToken.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            throw new DomainException("delegated_token_required", "A delegated bearer token is required.", 403);
        if (!Guid.TryParseExact(request.CorrelationId, "D", out var correlation) || correlation == Guid.Empty)
            throw new DomainException("validation_failed", "A correlation UUID is required.");
        return client.Send(request, ct);
    }
}
