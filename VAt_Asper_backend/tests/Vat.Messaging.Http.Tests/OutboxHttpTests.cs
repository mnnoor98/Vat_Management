using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Vat.Messaging.Contracts.V1;
using Vat.Messaging.Http;
using Xunit;

namespace Vat.Messaging.Http.Tests;

public sealed class OutboxHttpTests
{
    // Random, per-process test credential, never a deployed/default credential.
    private static readonly string Key = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    private sealed class Source : IOutboxLeaseSourceV1
    {
        public int Claims, Confirmations, Failures;
        public bool Settles = true;
        public ClaimedOutboxMessageV1? Message;
        public Task<ClaimedOutboxMessageV1?> Claim(CancellationToken ct) { Claims++; return Task.FromResult(Message); }
        public Task<bool> Confirm(OutboxReceiptV1 receipt, CancellationToken ct) { Confirmations++; return Task.FromResult(Settles); }
        public Task<bool> Fail(OutboxReceiptV1 receipt, string errorCode, CancellationToken ct) { Failures++; return Task.FromResult(Settles); }
    }

    private static ClaimedOutboxMessageV1 Message()
    {
        byte[] payload = "{\"schemaVersion\":1,\"value\":\"বাংলা\"}"u8.ToArray();
        return new(new(1, "financial-coordinated", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()),
            Guid.NewGuid(), 3, "RecordedV1", DateTimeOffset.UtcNow, payload, Convert.ToHexString(SHA256.HashData(payload)), DateTimeOffset.UtcNow.AddMinutes(1), 1);
    }

    private static async Task<WebApplication> Server(Source source, bool loopback = false, string remote = "127.0.0.1")
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OutboxServer:ServiceKey"] = Key, ["OutboxServer:AllowLoopbackHttp"] = loopback.ToString(),
            ["Logging:LogLevel:Default"] = "None"
        });
        builder.Services.AddSingleton<IOutboxLeaseSourceV1>(source);
        builder.Services.AddOutboxHttpServer(builder.Configuration);
        var app = builder.Build();
        app.Use((context, next) => { context.Connection.RemoteIpAddress = IPAddress.Parse(remote); return next(context); });
        app.UseAuthentication(); app.UseAuthorization(); app.MapOutboxHttpServer();
        await app.StartAsync(); return app;
    }

    private static HttpClient Client(WebApplication app, string? key = null, string scheme = "https")
    {
        var client = app.GetTestClient(); client.BaseAddress = new Uri(scheme + "://localhost");
        if (key is not null) client.DefaultRequestHeaders.Add(OutboxServiceAuthentication.HeaderName, key);
        return client;
    }

    private static HttpOutboxLeaseSourceV1 Remote(HttpClient http, string scheme = "https", bool loopback = false) => new(http,
        Options.Create(new OutboxHttpClientSettings { BaseAddress = scheme + "://localhost", ServiceKey = Key, AllowLoopbackHttp = loopback }));

    [Theory]
    [InlineData("missing")]
    [InlineData("wrong")]
    [InlineData("bearer")]
    [InlineData("duplicate")]
    public async Task Only_dedicated_service_credential_can_claim(string kind)
    {
        var source = new Source(); await using var app = await Server(source);
        using var client = Client(app, kind is "wrong" ? new string('0', 64) : kind is "duplicate" ? Key : null);
        if (kind == "duplicate") client.DefaultRequestHeaders.Add(OutboxServiceAuthentication.HeaderName, Key);
        if (kind == "bearer") client.DefaultRequestHeaders.Authorization = new("Bearer", "a-user-token");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync(OutboxHttpEndpoints.Prefix + "/claim", null)).StatusCode);
        Assert.Equal(0, source.Claims);
    }

    [Theory]
    [InlineData(false, "127.0.0.1", 401)]
    [InlineData(true, "203.0.113.1", 401)]
    [InlineData(true, "127.0.0.1", 204)]
    public async Task Plain_http_requires_explicit_loopback_exception(bool allowed, string address, int status)
    {
        var source = new Source(); await using var app = await Server(source, allowed, address);
        using var client = Client(app, Key, "http");
        client.DefaultRequestHeaders.Add("X-Forwarded-Proto", "https");
        Assert.Equal(status, (int)(await client.PostAsync(OutboxHttpEndpoints.Prefix + "/claim", null)).StatusCode);
    }

    [Fact]
    public async Task Remote_contract_preserves_payload_receipt_and_stale_settlement()
    {
        var source = new Source { Message = Message() }; await using var app = await Server(source);
        using var http = Client(app); var remote = Remote(http);
        var claim = (await remote.Claim(default))!;
        Assert.Equal(source.Message.Receipt, claim.Receipt); Assert.Equal(source.Message.Payload, claim.Payload);
        Assert.True(await remote.Confirm(claim.Receipt, default));
        Assert.True(await remote.Fail(claim.Receipt, "publish_timeout", default));
        source.Settles = false;
        Assert.False(await remote.Confirm(claim.Receipt, default)); Assert.False(await remote.Fail(claim.Receipt, "broker_unavailable", default));
        source.Message = null; Assert.Null(await remote.Claim(default));
        Assert.Equal(2, source.Claims); Assert.Equal(2, source.Confirmations); Assert.Equal(2, source.Failures);
    }

    [Theory]
    [InlineData("version")]
    [InlineData("owner")]
    [InlineData("scope")]
    [InlineData("error")]
    public async Task Invalid_contracts_are_rejected_before_owner_mutation(string kind)
    {
        var source = new Source(); await using var app = await Server(source); using var client = Client(app, Key);
        var receipt = Message().Receipt;
        receipt = kind switch { "version" => receipt with { ContractVersion = 2 }, "owner" => receipt with { SourceOwner = "other" },
            "scope" => receipt with { TenantId = Guid.Empty }, _ => receipt };
        using var response = kind == "error"
            ? await client.PostAsJsonAsync(OutboxHttpEndpoints.Prefix + "/fail", new FailOutboxRequestV1(receipt, "raw_secret_error"))
            : await client.PostAsJsonAsync(OutboxHttpEndpoints.Prefix + "/confirm", receipt);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); Assert.Equal(0, source.Confirmations + source.Failures);
    }

    [Fact]
    public async Task Oversized_and_malformed_requests_do_not_reach_owner()
    {
        var source = new Source(); await using var app = await Server(source); using var client = Client(app, Key);
        using var big = new StringContent(new string('x', 5000), System.Text.Encoding.UTF8, "application/json");
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await client.PostAsync(OutboxHttpEndpoints.Prefix + "/confirm", big)).StatusCode);
        using var malformed = new StringContent("{bad", System.Text.Encoding.UTF8, "application/json");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync(OutboxHttpEndpoints.Prefix + "/confirm", malformed)).StatusCode);
        Assert.Equal(0, source.Confirmations);
    }

    [Theory]
    [InlineData("digest")]
    [InlineData("owner")]
    public async Task Invalid_source_response_is_never_accepted(string kind)
    {
        var message = Message();
        var source = new Source { Message = kind == "digest" ? message with { PayloadSha256 = "bad" }
            : message with { Receipt = message.Receipt with { SourceOwner = "other" } } };
        await using var app = await Server(source); using var client = Client(app);
        await Assert.ThrowsAsync<HttpRequestException>(() => Remote(client).Claim(default)); Assert.Equal(1, source.Claims);
    }

    [Theory]
    [InlineData("redirect")]
    [InlineData("lost")]
    [InlineData("oversized")]
    public async Task Ambiguous_or_invalid_responses_are_not_retried(string kind)
    {
        var calls = 0;
        using var http = new HttpClient(new Handler(_ =>
        {
            calls++;
            if (kind == "lost") throw new HttpRequestException("Lost response");
            return new HttpResponseMessage(kind == "redirect" ? HttpStatusCode.TemporaryRedirect : HttpStatusCode.OK)
            { Content = new StringContent(new string('x', kind == "oversized" ? 4 * 1024 * 1024 + 1 : 0)) };
        }));
        await Assert.ThrowsAnyAsync<HttpRequestException>(() => Remote(http).Claim(default)); Assert.Equal(1, calls);
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromResult(send(request));
    }

    [Fact]
    public void Unsafe_configuration_is_rejected()
    {
        var config = new OutboxHttpClientSettings { BaseAddress = "https://owner.example", ServiceKey = Key };
        Assert.True(config.IsValid());
        foreach (var address in new[] { "http://owner.example", "https://user:pass@owner.example", "https://owner.example/path", "https://owner.example?token=x" })
        { config.BaseAddress = address; Assert.False(config.IsValid()); }
        config.BaseAddress = "http://localhost"; config.AllowLoopbackHttp = true; Assert.True(config.IsValid());
        config.ServiceKey = "short"; Assert.False(config.IsValid());
    }
}
