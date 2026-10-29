using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Vat.Application;
using Vat.Application.Contracts;
using Vat.Infrastructure.Messaging;
using Vat.Infrastructure.Persistence;
using Vat.Messaging;
using Vat.Messaging.Contracts.V1;
using Vat.Messaging.Http;
using Xunit;

namespace Vat.Tests;

[Collection("PostgreSQL integration")]
public sealed class InboxDeliveryContractTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private static ReceivedMessage Message(string eventType = nameof(FinancialEntryRecordedV1))
    {
        var id = Guid.NewGuid(); var purchase = Guid.NewGuid();
        var payload = new FinancialEntryRecordedV1(Guid.NewGuid(), purchase, Guid.NewGuid(), Guid.NewGuid(), "Purchase",
            new(2026, 10, 23), "REMOTE-TEST", "BDT", 20m, 3m, 1.5m, null);
        var envelope = new IntegrationEvent<FinancialEntryRecordedV1>(id, eventType, 1, DateTimeOffset.UtcNow,
            Guid.NewGuid(), Guid.NewGuid(), purchase, 1, Guid.NewGuid().ToString(), payload);
        return new(JsonSerializer.SerializeToUtf8Bytes(envelope, new JsonSerializerOptions(JsonSerializerDefaults.Web)), id.ToString(), eventType, "application/json");
    }

    private async Task<WebApplication> Server(string key, SaveChangesInterceptor? interceptor = null)
    {
        var connection = await factory.Inspect(db => Task.FromResult(db.Database.GetConnectionString()));
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        { ["InboxServer:ServiceKey"] = key, ["InboxServer:AllowLoopbackHttp"] = "true", ["Logging:LogLevel:Default"] = "None" });
        builder.Services.AddDbContext<VatDbContext>(options =>
        {
            options.UseNpgsql(connection);
            if (interceptor is not null) options.AddInterceptors(interceptor);
        });
        builder.Services.AddScoped<IInboxDeliveryTargetV1>(sp => new ReportingInbox(sp.GetRequiredService<VatDbContext>(), TimeProvider.System));
        builder.Services.AddInboxHttpServer(builder.Configuration);
        var app = builder.Build(); app.UseAuthentication(); app.UseAuthorization(); app.MapInboxHttpServer();
        await app.StartAsync(); return app;
    }
    private static HttpInboxDeliveryTargetV1 Remote(WebApplication app, HttpClient client, string key) => new(client,
        Options.Create(new InboxHttpClientSettings { BaseAddress = app.Urls.Single(), ServiceKey = key, AllowLoopbackHttp = true }));

    [Theory]
    [InlineData("FinancialEntryRecordedV1", "Processed")]
    [InlineData("PurchaseApprovedV1", "Ignored")]
    public async Task Http_receipt_follows_visible_postgres_commit_and_redelivery_deduplicates(string type, string status)
    {
        var key = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        await using var app = await Server(key); using var client = new HttpClient();
        try
        {
            var delivery = InboxDeliveryV1.Create(Message(type)); var remote = Remote(app, client, key);
            var receipt = await remote.Deliver(delivery, default);
            Assert.True(receipt.IsCommittedFor(delivery)); Assert.Equal(status, receipt.Status);
            await factory.Inspect(async db =>
            {
                var id = Guid.Parse(delivery.MessageId!);
                var row = await db.Set<InboxMessage>().AsNoTracking().SingleAsync(x => x.EventId == id);
                Assert.Equal(status, row.Status); Assert.Equal(delivery.BodySha256, row.PayloadHash);
                Assert.Equal(status == "Processed" ? 1 : 0, await db.Set<ReportingEntry>().CountAsync(x => x.EventId == id));
                Assert.True(await db.Set<ReportingCheckpoint>().AnyAsync(x => x.TenantId == row.TenantId && x.CompanyId == row.CompanyId)); return 0;
            });
            Assert.Equal("Duplicate", (await remote.Deliver(delivery, default)).Status);
        }
        finally { await app.StopAsync(); }
    }

    [Fact]
    public async Task Oversized_remote_delivery_keeps_original_quarantine_fingerprint_and_local_replay_behavior()
    {
        var key = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)); await using var app = await Server(key); using var client = new HttpClient();
        try
        {
            var bytes = new byte[InboxDeliveryV1.MaxBodyBytes + 99]; bytes[^1] = 123;
            var original = new ReceivedMessage(bytes, Guid.NewGuid().ToString(), "LargeV1", "application/json");
            var delivery = InboxDeliveryV1.Create(original); var receipt = await Remote(app, client, key).Deliver(delivery, default);
            Assert.Equal("Quarantined", receipt.Status); Assert.True(receipt.IsCommittedFor(delivery));
            await factory.Inspect(async db =>
            {
                var row = await db.Set<InboxDeadLetter>().AsNoTracking().SingleAsync(x => x.Id == receipt.DeadLetterId);
                Assert.Equal("message_too_large", row.ErrorCode); Assert.True(row.IsTruncated);
                Assert.Equal(delivery.Fingerprint(), row.Fingerprint); Assert.Equal(delivery.BodyPrefix, row.Body);
                var local = await new ReportingInbox(db, TimeProvider.System).Process(original, default);
                Assert.Equal(receipt.DeadLetterId, local.DeadLetterId);
                Assert.Equal(2, (await db.Set<InboxDeadLetter>().AsNoTracking().SingleAsync(x => x.Id == row.Id)).Occurrences); return 0;
            });
        }
        finally { await app.StopAsync(); }
    }

    [Fact]
    public async Task Failed_owner_commit_returns_no_receipt_and_retry_commits_once()
    {
        var key = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)); var failure = new RejectSave();
        await using var app = await Server(key, failure); using var client = new HttpClient();
        try
        {
            var delivery = InboxDeliveryV1.Create(Message()); var remote = Remote(app, client, key);
            await Assert.ThrowsAsync<HttpRequestException>(() => remote.Deliver(delivery, default));
            await factory.Inspect(async db =>
            {
                var id = Guid.Parse(delivery.MessageId!);
                Assert.False(await db.Set<InboxMessage>().AnyAsync(x => x.EventId == id));
                Assert.False(await db.Set<ReportingEntry>().AnyAsync(x => x.EventId == id)); return 0;
            });
            failure.Reject = false;
            var receipt = await remote.Deliver(delivery, default); Assert.True(receipt.IsCommittedFor(delivery));
            Assert.Equal("Duplicate", (await remote.Deliver(delivery, default)).Status);
        }
        finally { await app.StopAsync(); }
    }

    [Fact]
    public async Task An_outer_transaction_cannot_produce_a_durable_broker_receipt()
    {
        await factory.Inspect(async db =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            var delivery = InboxDeliveryV1.Create(Message()); var target = new ReportingInbox(db, TimeProvider.System);
            await Assert.ThrowsAsync<InvalidOperationException>(() => target.Deliver(delivery, default));
            Assert.False(await db.Set<InboxMessage>().AnyAsync(x => x.EventId == Guid.Parse(delivery.MessageId!))); return 0;
        });
    }

    [Fact]
    public void Broker_and_worker_assemblies_have_no_financial_or_database_dependency()
    {
        Assert.Equal("Vat.Messaging.RabbitMq", typeof(RabbitMqTransport).Assembly.GetName().Name);
        Assert.Equal(typeof(RabbitMqTransport).Assembly, typeof(MessagingWorker).Assembly);
        Assert.DoesNotContain(typeof(RabbitMqTransport).Assembly.GetReferencedAssemblies(), x =>
            x.Name is "Vat.Application" or "Vat.Domain" or "Vat.Infrastructure" or "Vat.Hosting" or "Npgsql"
            || x.Name!.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
    }

    private sealed class RejectSave : SaveChangesInterceptor
    {
        public bool Reject = true;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken ct = default) => Reject
                ? throw new InvalidOperationException("Test owner commit failure.") : ValueTask.FromResult(result);
    }
}
