using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Vat.Application;
using Vat.Messaging.Contracts.V1;
using Vat.Messaging.Http;
using Xunit;

namespace Vat.Messaging.Http.Tests;

public sealed class InboxHttpTests
{
    private static readonly string Key = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private static InboxDeliveryV1 Delivery() => InboxDeliveryV1.Create(new("{}"u8.ToArray(), Guid.NewGuid().ToString(), "EventV1", "application/json"));
    private sealed class Target : IInboxDeliveryTargetV1
    {
        public int Calls;
        public string Status = "Processed";
        public InboxDeliveryV1? Received;
        public Task<InboxCommitReceiptV1> Deliver(InboxDeliveryV1 delivery, CancellationToken ct)
        {
            Calls++; Received = delivery;
            return Task.FromResult(new InboxCommitReceiptV1(1, InboxDeliveryV1.Owner, delivery.Fingerprint(), Status,
                Status == "Quarantined" ? Guid.NewGuid() : null));
        }
    }
    private static async Task<WebApplication> Server(Target target, bool allowHttp = false, bool user = false, string peer = "127.0.0.1")
    {
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        { ["InboxServer:ServiceKey"] = Key, ["InboxServer:AllowLoopbackHttp"] = allowHttp.ToString(), ["Logging:LogLevel:Default"] = "None" });
        builder.Services.AddSingleton<IInboxDeliveryTargetV1>(target); builder.Services.AddInboxHttpServer(builder.Configuration);
        var app = builder.Build();
        app.Use((context, next) =>
        {
            context.Connection.RemoteIpAddress = IPAddress.Parse(peer);
            if (user) context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "validated-user"), new Claim("permission", "messaging.retry")], "UserJwt"));
            return next(context);
        });
        app.UseAuthentication(); app.UseAuthorization(); app.MapInboxHttpServer(); await app.StartAsync(); return app;
    }
    private static HttpClient Client(WebApplication app, string scheme = "https")
    { var client = app.GetTestClient(); client.BaseAddress = new Uri(scheme + "://localhost"); return client; }
    private static HttpInboxDeliveryTargetV1 Remote(HttpClient client) => new(client, Options.Create(new InboxHttpClientSettings
    { BaseAddress = "https://localhost", ServiceKey = Key }));

    [Theory]
    [InlineData("anonymous")]
    [InlineData("wrong")]
    [InlineData("outbox")]
    [InlineData("duplicate")]
    [InlineData("authenticated-user")]
    public async Task Only_inbox_service_capability_can_deliver(string kind)
    {
        var target = new Target(); await using var app = await Server(target, user: kind == "authenticated-user"); using var client = Client(app);
        if (kind == "wrong") client.DefaultRequestHeaders.Add(InboxServiceAuthentication.HeaderName, new string('0', 64));
        if (kind == "outbox") client.DefaultRequestHeaders.Add(OutboxServiceAuthentication.HeaderName, Key);
        if (kind == "duplicate") client.DefaultRequestHeaders.Add(InboxServiceAuthentication.HeaderName, new[] { Key, Key });
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(InboxHttpEndpoints.Prefix + "/deliver", Delivery())).StatusCode);
        Assert.Equal(0, target.Calls);
    }

    [Theory]
    [InlineData(false, "127.0.0.1", 401)]
    [InlineData(true, "203.0.113.1", 401)]
    [InlineData(true, "127.0.0.1", 200)]
    public async Task Plain_http_is_explicitly_limited_to_actual_loopback(bool allow, string peer, int status)
    {
        var target = new Target(); await using var app = await Server(target, allow, peer: peer); using var client = Client(app, "http");
        client.DefaultRequestHeaders.Add(InboxServiceAuthentication.HeaderName, Key); client.DefaultRequestHeaders.Add("X-Forwarded-Proto", "https");
        Assert.Equal(status, (int)(await client.PostAsJsonAsync(InboxHttpEndpoints.Prefix + "/deliver", Delivery())).StatusCode);
    }

    [Theory]
    [InlineData("Processed")]
    [InlineData("Ignored")]
    [InlineData("Duplicate")]
    [InlineData("Quarantined")]
    public async Task Remote_terminal_receipt_matches_the_original_delivery(string status)
    {
        var target = new Target { Status = status }; await using var app = await Server(target); using var client = Client(app);
        var delivery = Delivery(); var receipt = await Remote(client).Deliver(delivery, default);
        Assert.True(receipt.IsCommittedFor(delivery)); Assert.Equal(status, receipt.Status);
        Assert.Equal(delivery.BodyPrefix, target.Received!.BodyPrefix); Assert.Equal(1, target.Calls);
    }

    [Fact]
    public async Task Oversized_message_uses_bounded_prefix_and_full_fingerprint()
    {
        var target = new Target { Status = "Quarantined" }; await using var app = await Server(target); using var client = Client(app);
        var message = new ReceivedMessage(new byte[InboxDeliveryV1.MaxBodyBytes + 99], "large", "EventV1", "application/json");
        var delivery = InboxDeliveryV1.Create(message); var receipt = await Remote(client).Deliver(delivery, default);
        Assert.True(receipt.IsCommittedFor(delivery)); Assert.Equal(message.Body.Length, target.Received!.OriginalBodyLength);
        Assert.Equal(InboxDeliveryV1.MaxBodyBytes, target.Received.BodyPrefix.Length);
    }

    [Theory]
    [InlineData("version")]
    [InlineData("owner")]
    [InlineData("prefix")]
    public async Task Invalid_contract_never_reaches_owner(string kind)
    {
        var target = new Target(); await using var app = await Server(target); using var client = Client(app);
        client.DefaultRequestHeaders.Add(InboxServiceAuthentication.HeaderName, Key);
        var delivery = Delivery(); delivery = kind switch { "version" => delivery with { ContractVersion = 2 },
            "owner" => delivery with { ConsumerOwner = "other" }, _ => delivery with { PrefixSha256 = new string('0', 64) } };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(InboxHttpEndpoints.Prefix + "/deliver", delivery)).StatusCode);
        Assert.Equal(0, target.Calls);
    }

    [Fact]
    public async Task Invalid_owner_receipt_is_not_returned_as_a_successful_commit()
    {
        var target = new Target { Status = "Pending" }; await using var app = await Server(target); using var client = Client(app);
        await Assert.ThrowsAsync<HttpRequestException>(() => Remote(client).Deliver(Delivery(), default)); Assert.Equal(1, target.Calls);
    }

    [Theory]
    [InlineData("lost")]
    [InlineData("redirect")]
    [InlineData("fingerprint")]
    [InlineData("oversized-response")]
    public async Task Ambiguous_or_invalid_response_cannot_ack_and_is_not_retried(string kind)
    {
        var calls = 0;
        using var client = new HttpClient(new Handler(_ =>
        {
            calls++; if (kind == "lost") throw new HttpRequestException("response_lost");
            return new HttpResponseMessage(kind == "redirect" ? HttpStatusCode.TemporaryRedirect : HttpStatusCode.OK)
            { Content = kind == "oversized-response" ? new StringContent(new string('x', 4097))
                : JsonContent.Create(new InboxCommitReceiptV1(1, InboxDeliveryV1.Owner, "wrong", "Processed")) };
        }));
        await Assert.ThrowsAnyAsync<HttpRequestException>(() => Remote(client).Deliver(Delivery(), default)); Assert.Equal(1, calls);
    }

    [Fact]
    public void Unsafe_client_configuration_fails_validation()
    {
        var config = new InboxHttpClientSettings { BaseAddress = "https://reporting.example", ServiceKey = Key };
        Assert.True(config.IsValid());
        foreach (var uri in new[] { "http://reporting.example", "https://user:pass@reporting.example", "https://reporting.example/path", "https://reporting.example?secret=x" })
        { config.BaseAddress = uri; Assert.False(config.IsValid()); }
        config.BaseAddress = "http://localhost"; config.AllowLoopbackHttp = true; Assert.True(config.IsValid());
        config.TimeoutSeconds = 31; Assert.False(config.IsValid());
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromResult(send(request));
    }
}
