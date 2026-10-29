using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Vat.Messaging.Contracts.V1;

namespace Vat.Messaging.Http;

public sealed class HttpInboxDeliveryTargetV1(HttpClient http, IOptions<InboxHttpClientSettings> options) : IInboxDeliveryTargetV1
{
    public async Task<InboxCommitReceiptV1> Deliver(InboxDeliveryV1 delivery, CancellationToken ct)
    {
        var config = options.Value;
        if (!config.IsValid()) throw new InvalidOperationException("Invalid inbox client configuration.");
        if (!delivery.IsValid()) throw new InvalidOperationException("Invalid inbox delivery contract.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(config.TimeoutSeconds));
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(config.BaseAddress), InboxHttpEndpoints.Prefix + "/deliver"));
        request.Headers.Add(InboxServiceAuthentication.HeaderName, config.ServiceKey);
        request.Content = JsonContent.Create(delivery);
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        if (response.StatusCode != HttpStatusCode.OK)
            throw new HttpRequestException("Inbox delivery request failed.", null, response.StatusCode);
        await response.Content.LoadIntoBufferAsync(4096, timeout.Token);
        var receipt = await response.Content.ReadFromJsonAsync<InboxCommitReceiptV1>(cancellationToken: timeout.Token);
        if (receipt is null || !receipt.IsCommittedFor(delivery)) throw new HttpRequestException("Invalid inbox commit receipt.");
        return receipt;
    }
}

public static class InboxHttpClientRegistration
{
    public static IServiceCollection AddRemoteInboxTarget(this IServiceCollection services, IConfiguration configuration, bool whenMessagingEnabledOnly = false)
    {
        services.AddOptions<InboxHttpClientSettings>().Bind(configuration.GetSection("InboxTarget"))
            .Validate(x => whenMessagingEnabledOnly && !configuration.GetValue<bool>("Messaging:Enabled") || x.IsValid(),
                "Configure a secure InboxTarget origin, timeout and 256-bit hexadecimal service key.").ValidateOnStart();
        services.AddHttpClient<HttpInboxDeliveryTargetV1>(client => client.Timeout = Timeout.InfiniteTimeSpan)
            .RedactLoggedHeaders([InboxServiceAuthentication.HeaderName])
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
        services.AddScoped<IInboxDeliveryTargetV1>(provider => provider.GetRequiredService<HttpInboxDeliveryTargetV1>());
        return services;
    }
}
