using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Vat.Messaging.Contracts.V1;

namespace Vat.Messaging.Http;

// No automatic retries: a lost response may already have committed a claim or settlement.
public sealed class HttpOutboxLeaseSourceV1(HttpClient http, IOptions<OutboxHttpClientSettings> options) : IOutboxLeaseSourceV1
{
    public async Task<ClaimedOutboxMessageV1?> Claim(CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.TimeoutSeconds));
        using var response = await Send("claim", null, timeout.Token);
        if (response.StatusCode == HttpStatusCode.NoContent) return null;
        Ensure(response);
        if (response.StatusCode != HttpStatusCode.OK) throw new HttpRequestException("Invalid outbox claim response.");
        // Bound before parsing, including responses without a Content-Length header.
        await response.Content.LoadIntoBufferAsync(4 * 1024 * 1024, timeout.Token);
        var claim = await response.Content.ReadFromJsonAsync<ClaimedOutboxMessageV1>(cancellationToken: timeout.Token);
        if (claim is null || !claim.HasValidIntegrity() || claim.Receipt.SourceOwner != options.Value.SourceOwner)
            throw new HttpRequestException("Invalid outbox source response.");
        return claim;
    }

    public Task<bool> Confirm(OutboxReceiptV1 receipt, CancellationToken ct) => Settle("confirm", receipt, receipt, ct);
    public Task<bool> Fail(OutboxReceiptV1 receipt, string errorCode, CancellationToken ct) => Settle("fail", receipt, new FailOutboxRequestV1(receipt, errorCode), ct);

    private async Task<bool> Settle(string operation, OutboxReceiptV1 receipt, object body, CancellationToken ct)
    {
        if (!receipt.IsWellFormed() || receipt.SourceOwner != options.Value.SourceOwner) return false;
        using var response = await Send(operation, body, ct);
        if (response.StatusCode == HttpStatusCode.Conflict) return false;
        Ensure(response);
        if (response.StatusCode != HttpStatusCode.NoContent) throw new HttpRequestException("Invalid outbox settlement response.");
        return true;
    }

    private async Task<HttpResponseMessage> Send(string operation, object? body, CancellationToken ct)
    {
        var config = options.Value;
        if (!config.IsValid()) throw new InvalidOperationException("Invalid outbox client configuration.");
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(config.BaseAddress), OutboxHttpEndpoints.Prefix + "/" + operation));
        request.Headers.Add(OutboxServiceAuthentication.HeaderName, config.ServiceKey);
        if (body is not null) request.Content = JsonContent.Create(body, body.GetType());
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(config.TimeoutSeconds));
        return await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
    }

    private static void Ensure(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode) throw new HttpRequestException("Outbox source request failed.", null, response.StatusCode);
    }
}

public static class OutboxHttpClientRegistration
{
    public static IServiceCollection AddRemoteOutboxSource(this IServiceCollection services, IConfiguration configuration, bool whenMessagingEnabledOnly = false)
    {
        services.AddOptions<OutboxHttpClientSettings>().Bind(configuration.GetSection("OutboxSource"))
            .Validate(x => whenMessagingEnabledOnly && !configuration.GetValue<bool>("Messaging:Enabled") || x.IsValid(),
                "Configure a secure OutboxSource address, source owner, timeout and 256-bit hexadecimal service key.").ValidateOnStart();
        services.AddHttpClient<HttpOutboxLeaseSourceV1>(client => client.Timeout = Timeout.InfiniteTimeSpan)
            .RedactLoggedHeaders([OutboxServiceAuthentication.HeaderName])
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
        services.AddScoped<IOutboxLeaseSourceV1>(provider => provider.GetRequiredService<HttpOutboxLeaseSourceV1>());
        return services;
    }
}
