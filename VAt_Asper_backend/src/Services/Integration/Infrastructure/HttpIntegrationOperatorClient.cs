using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vat.Domain;
using Vat.Messaging.Contracts.V1;

namespace Vat.Services.Integration.Infrastructure;

// No retries or local command receipt: the owner may already have committed when a response is lost.
public sealed class HttpIntegrationOperatorClient(HttpClient http, IConfiguration configuration) : IIntegrationOperatorClient
{
    public async Task<OperatorReply> Send(OperatorRequest request, CancellationToken ct)
    {
        var source = request.Action is MessageOperatorAction.ListOutbox or MessageOperatorAction.RetryOutbox;
        var configured = configuration[source ? "OperatorTargets:SourceBaseUrl" : "OperatorTargets:InboxBaseUrl"];
        if (!Uri.TryCreate(configured, UriKind.Absolute, out var origin) || origin.UserInfo.Length != 0 || origin.AbsolutePath != "/" ||
            origin.Query.Length != 0 || origin.Fragment.Length != 0 || (origin.Scheme != "https" &&
                !(origin.Scheme == "http" && origin.IsLoopback && configuration.GetValue<bool>("OperatorTargets:AllowLoopbackHttp"))))
            throw Unavailable();
        var mutation = request.Action is MessageOperatorAction.RetryOutbox or MessageOperatorAction.ReplayDeadLetter;
        var suffix = request.Action switch
        {
            MessageOperatorAction.ListOutbox => "/outbox",
            MessageOperatorAction.ListDeadLetters => "/dead-letters",
            MessageOperatorAction.RetryOutbox => $"/outbox/{request.Id}/retry",
            MessageOperatorAction.ReplayDeadLetter => $"/dead-letters/{request.Id}/replay",
            _ => throw new DomainException("validation_failed", "Unknown operator action.")
        };
        if (!mutation) suffix += $"?take={request.Take}" + (request.Before is null ? "" : $"&before={request.Before}");
        using var message = new HttpRequestMessage(mutation ? HttpMethod.Post : HttpMethod.Get,
            new Uri(origin, MessageOperatorV1.CompanyPath(request.CompanyId) + suffix));
        message.Headers.Authorization = AuthenticationHeaderValue.Parse(request.BearerToken);
        message.Headers.Add("X-Correlation-ID", request.CorrelationId);
        if (mutation)
        {
            message.Headers.Add("Idempotency-Key", request.IdempotencyKey);
            message.Content = JsonContent.Create(new RetryMessageV1(request.Reason!));
        }
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            using var response = await http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            var status = (int)response.StatusCode;
            if (status is not (200 or 400 or 401 or 403 or 404 or 409 or 422 or 503)) throw Unavailable();
            await response.Content.LoadIntoBufferAsync(1_048_576, deadline.Token);
            var json = await response.Content.ReadAsStringAsync(deadline.Token);
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 });
            if (document.RootElement.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array)) throw Unavailable();
            bool? replayed = null;
            if (mutation && status == 200)
            {
                if (!response.Headers.TryGetValues("Idempotency-Replayed", out var values)) throw Unavailable();
                var receipts = values.ToArray();
                if (receipts.Length != 1 || !bool.TryParse(receipts[0], out var value)) throw Unavailable();
                replayed = value;
            }
            return new(status, json, replayed);
        }
        catch (Exception error) when (error is HttpRequestException or JsonException || error is OperationCanceledException && !ct.IsCancellationRequested)
        { throw Unavailable(); }
    }
    private static DomainException Unavailable() => new("operator_owner_unavailable", "The message owner is unavailable. Retry commands with the same idempotency key.", 503);
}

public static class IntegrationOperatorRegistration
{
    public static IServiceCollection AddIntegrationOperators(this IServiceCollection services)
    {
        services.AddScoped<IntegrationOperatorService>();
        services.AddHttpClient<IIntegrationOperatorClient, HttpIntegrationOperatorClient>(http => http.Timeout = TimeSpan.FromSeconds(35))
            .RedactLoggedHeaders(["Authorization", "Idempotency-Key"])
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
        return services;
    }
}
