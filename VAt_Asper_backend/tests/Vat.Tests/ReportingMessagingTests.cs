using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Vat.Application;
using Vat.Application.Contracts;
using Vat.Application.Security;
using Vat.Infrastructure.Messaging;
using Vat.Infrastructure.Persistence;
using Xunit;

namespace Vat.Tests;

[Collection("PostgreSQL integration")]
public sealed class ReportingMessagingTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly DateOnly Date = new(2026, 10, 23);
    private static FinancialEntryRecordedV1 Data() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
        "Purchase", Date, "TEST-ONLY", "BDT", 250m, 18.75m, 7.5m, null);

    private static ReceivedMessage Message(Guid tenant, Guid company, FinancialEntryRecordedV1 data, Guid? eventId = null, int schema = 1) =>
        Wrap(tenant, company, data.PurchaseId, nameof(FinancialEntryRecordedV1), data, eventId, schema);

    private static ReceivedMessage Wrap(Guid tenant, Guid company, Guid aggregateId, string eventType, object data, Guid? eventId = null, int schema = 1)
    {
        var id = eventId ?? Guid.NewGuid();
        var envelope = new IntegrationEvent<object>(id, eventType, schema, DateTimeOffset.UtcNow, tenant, company, aggregateId, 5, Guid.NewGuid().ToString(), data);
        return new(JsonSerializer.SerializeToUtf8Bytes(envelope, Json), id.ToString(), eventType, "application/json");
    }

    private async Task<ProcessedMessage> Process(ReceivedMessage message, SaveChangesInterceptor? interceptor = null)
    {
        using var scope = factory.Services.CreateScope();
        if (interceptor is null) return await scope.ServiceProvider.GetRequiredService<IIncomingEventProcessor>().Process(message, default);
        var source = scope.ServiceProvider.GetRequiredService<VatDbContext>();
        await using var db = new VatDbContext(new DbContextOptionsBuilder<VatDbContext>()
            .UseNpgsql(source.Database.GetConnectionString()).AddInterceptors(interceptor).Options);
        return await new ReportingInbox(db, TimeProvider.System).Process(message, default);
    }

    private async Task<Guid> Seed(ReceivedMessage message)
    {
        var envelope = JsonSerializer.Deserialize<IntegrationEvent<JsonElement>>(message.Body, Json)!;
        return await factory.Inspect(async db =>
        {
            db.Outbox.Add(new OutboxMessage { Id = envelope.EventId, TenantId = envelope.TenantId, CompanyId = envelope.CompanyId,
                AggregateId = envelope.AggregateId, AggregateVersion = envelope.AggregateVersion, EventType = envelope.EventType,
                Payload = Encoding.UTF8.GetString(message.Body), CreatedAt = envelope.OccurredAt });
            await db.SaveChangesAsync();
            return envelope.EventId;
        });
    }

    private async Task<int> Dispatch(IEventPublisher publisher, MessagingSettings? settings = null)
    {
        using var scope = factory.Services.CreateScope();
        var options = Options.Create(settings ?? new MessagingSettings());
        return await new OutboxDispatcher(new FinancialOutboxLeaseSourceV1(scope.ServiceProvider.GetRequiredService<VatDbContext>(), TimeProvider.System, options), publisher, options).DispatchBatch(default);
    }

    private static async Task<HttpResponseMessage> Post(HttpClient client, string url, string reason = "Reviewed recovery", string? key = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(new RetryMessageRequest(reason)) };
        request.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString());
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task Concurrent_redelivery_commits_one_inbox_and_one_financial_entry()
    {
        var tenant = Guid.NewGuid(); var company = Guid.NewGuid();
        var message = Message(tenant, company, Data());
        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Process(message)));
        Assert.Single(results, x => x.Status == "Processed");
        Assert.Equal(5, results.Count(x => x.Status == "Duplicate"));
        Assert.Equal(1, await factory.Inspect(db => db.Set<ReportingEntry>().CountAsync(x => x.CompanyId == company)));
        Assert.Equal(1, await factory.Inspect(db => db.Set<InboxMessage>().CountAsync(x => x.CompanyId == company)));
    }

    [Fact]
    public async Task Reversal_before_original_produces_correct_signed_period_totals()
    {
        var tenant = Guid.NewGuid(); var company = Guid.NewGuid(); var original = Data();
        var reversal = original with { TransactionId = Guid.NewGuid(), Kind = "PurchaseReversal", BusinessDate = new(2026, 11, 2),
            NetAmount = -250m, TaxAmount = -18.75m, RecoverableTaxAmount = -7.5m, ReversalOfTransactionId = original.TransactionId };
        Assert.Equal("Processed", (await Process(Message(tenant, company, reversal))).Status);
        Assert.Equal("Processed", (await Process(Message(tenant, company, original))).Status);
        using var client = factory.Client(tenant, company);
        var october = (await client.GetFromJsonAsync<FinancialReportResponse>($"/api/v1/companies/{company}/reports/financial-summary?from=2026-10-01&until=2026-11-01"))!;
        var combined = (await client.GetFromJsonAsync<FinancialReportResponse>($"/api/v1/companies/{company}/reports/financial-summary?from=2026-10-01&until=2026-12-01"))!;
        Assert.Equal(250m, october.NetAmount);
        Assert.Equal(0m, combined.NetAmount);
        Assert.Equal(0m, combined.TaxAmount);
        Assert.Equal(2, combined.TransactionCount);
        Assert.NotNull(combined.Freshness.LastProcessedAt);
    }

    [Fact]
    public async Task Business_duplicate_with_another_event_id_does_not_double_count_and_conflicts_are_quarantined()
    {
        var tenant = Guid.NewGuid(); var company = Guid.NewGuid(); var data = Data();
        var first = Message(tenant, company, data);
        await Process(first);
        Assert.Equal("Processed", (await Process(Message(tenant, company, data))).Status);
        Assert.Equal("Quarantined", (await Process(Message(tenant, company, data with { NetAmount = 500m }))).Status);
        Assert.Equal("Quarantined", (await Process(Message(tenant, company, data with { NetAmount = 600m }, Guid.Parse(first.MessageId!)))).Status);
        Assert.Equal(1, await factory.Inspect(db => db.Set<ReportingEntry>().CountAsync(x => x.CompanyId == company)));
        Assert.Equal(2, await factory.Inspect(db => db.Set<InboxDeadLetter>().CountAsync(x => x.CompanyId == company)));
    }

    [Theory]
    [InlineData("schema")]
    [InlineData("metadata")]
    [InlineData("invalid_amount")]
    [InlineData("invalid_payload")]
    public async Task Invalid_events_are_durably_quarantined_without_projection(string scenario)
    {
        var tenant = Guid.NewGuid(); var company = Guid.NewGuid(); var data = Data();
        var message = scenario switch
        {
            "schema" => Message(tenant, company, data, schema: 2),
            "metadata" => Message(tenant, company, data) with { MessageId = Guid.NewGuid().ToString() },
            "invalid_amount" => Message(tenant, company, data with { NetAmount = decimal.MinValue }),
            _ => Wrap(tenant, company, data.PurchaseId, nameof(FinancialEntryRecordedV1), new { unexpected = "value" })
        };
        Assert.Equal("Quarantined", (await Process(message)).Status);
        Assert.Equal("Quarantined", (await Process(message)).Status);
        Assert.Equal(0, await factory.Inspect(db => db.Set<ReportingEntry>().CountAsync(x => x.CompanyId == company)));
        Assert.Equal(2, await factory.Inspect(db => db.Set<InboxDeadLetter>().Where(x => x.CompanyId == company).Select(x => x.Occurrences).SingleAsync()));
    }

    [Fact]
    public async Task Malformed_json_and_unknown_notifications_do_not_break_consumer_processing()
    {
        Assert.Equal("Quarantined", (await Process(new(Encoding.UTF8.GetBytes("{ broken json"), null, null, "application/json"))).Status);
        var tenant = Guid.NewGuid(); var company = Guid.NewGuid();
        Assert.Equal("Quarantined", (await Process(Wrap(tenant, company, Guid.NewGuid(), "FutureEventV9", new { }))).Status);
        Assert.Equal("Ignored", (await Process(Wrap(tenant, company, Guid.NewGuid(), nameof(PurchaseApprovedV1), new { }))).Status);
    }

    [Fact]
    public async Task Failed_projection_commit_does_not_commit_inbox_and_redelivery_recovers()
    {
        var tenant = Guid.NewGuid(); var company = Guid.NewGuid(); var message = Message(tenant, company, Data());
        await Assert.ThrowsAsync<InvalidOperationException>(() => Process(message, new RejectProjectionCommit()));
        Assert.Equal(0, await factory.Inspect(db => db.Set<InboxMessage>().CountAsync(x => x.CompanyId == company)));
        Assert.Equal(0, await factory.Inspect(db => db.Set<ReportingEntry>().CountAsync(x => x.CompanyId == company)));
        Assert.Equal("Processed", (await Process(message)).Status);
    }

    [Fact]
    public async Task Dispatcher_claims_are_exclusive_and_only_confirmed_messages_are_marked_delivered()
    {
        var tenant = Guid.NewGuid(); var company = Guid.NewGuid();
        var id = await Seed(Message(tenant, company, Data()));
        var publisher = new RecordingPublisher();
        await Task.WhenAll(Dispatch(publisher), Dispatch(publisher));
        Assert.Equal(1, publisher.Ids.Count(x => x == id));
        Assert.NotNull(await factory.Inspect(db => db.Outbox.Where(x => x.Id == id).Select(x => x.DispatchedAt).SingleAsync()));
    }

    [Fact]
    public async Task Broker_failure_quarantines_at_retry_limit_and_operator_retry_is_audited()
    {
        var tenant = Guid.NewGuid(); var company = Guid.NewGuid();
        var id = await Seed(Message(tenant, company, Data()));
        await Dispatch(new RecordingPublisher(fail: true), new MessagingSettings { MaxAttempts = 1 });
        var failed = await factory.Inspect(db => db.Outbox.AsNoTracking().SingleAsync(x => x.Id == id));
        Assert.Null(failed.DispatchedAt);
        Assert.NotNull(failed.QuarantinedAt);
        Assert.Equal("publisher_rejected", failed.LastError);
        using var client = factory.Client(tenant, company);
        Assert.Equal(HttpStatusCode.OK, (await Post(client, $"/api/v1/companies/{company}/messaging/outbox/{id}/retry")).StatusCode);
        var publisher = new RecordingPublisher();
        await Dispatch(publisher);
        Assert.Contains(id, publisher.Ids);
        Assert.Equal(2, await factory.Inspect(db => db.Outbox.Where(x => x.Id == id).Select(x => x.TotalAttempts).SingleAsync()));
        Assert.Equal(1, await factory.Inspect(db => db.Audit.CountAsync(x => x.ResourceId == id && x.Action == "OutboxRetryRequested")));
    }

    [Fact]
    public async Task Expired_lease_is_recoverable_but_active_lease_is_not_published()
    {
        var tenant = Guid.NewGuid(); var company = Guid.NewGuid();
        var id = await Seed(Message(tenant, company, Data()));
        await factory.Inspect(async db =>
        {
            var row = await db.Outbox.SingleAsync(x => x.Id == id);
            row.LeaseToken = Guid.NewGuid(); row.LeaseExpiresAt = DateTimeOffset.UtcNow.AddMinutes(1);
            await db.SaveChangesAsync(); return true;
        });
        var publisher = new RecordingPublisher(); await Dispatch(publisher);
        Assert.DoesNotContain(id, publisher.Ids);
        await factory.Inspect(db => db.Outbox.Where(x => x.Id == id).ExecuteUpdateAsync(set => set.SetProperty(x => x.LeaseExpiresAt, DateTimeOffset.UtcNow.AddSeconds(-1))));
        await Dispatch(publisher);
        Assert.Contains(id, publisher.Ids);
    }

    [Fact]
    public async Task Report_freshness_distinguishes_broker_dispatch_from_projection_commit()
    {
        var tenant = Guid.NewGuid(); var company = Guid.NewGuid();
        var id = await Seed(Message(tenant, company, Data()));
        using var client = factory.Client(tenant, company);
        var path = $"/api/v1/companies/{company}/reports/financial-summary?from=2026-10-01&until=2026-11-01";
        var publisher = new RecordingPublisher(); await Dispatch(publisher);
        var pending = (await client.GetFromJsonAsync<FinancialReportResponse>(path))!;
        Assert.False(pending.Freshness.IsCaughtUp);
        Assert.Equal(1, pending.Freshness.UnprocessedFinancialEvents);
        var message = publisher.Messages.Single(x => x.Id == id);
        await Process(new(message.Body, id.ToString(), message.EventType, "application/json"));
        var current = (await client.GetFromJsonAsync<FinancialReportResponse>(path))!;
        Assert.True(current.Freshness.IsCaughtUp);
        Assert.Equal(18.75m, current.TaxAmount);
    }

    [Fact]
    public async Task Tenant_permissions_and_rebuild_are_scoped_and_replayed_entries_do_not_duplicate()
    {
        var tenant = Guid.NewGuid(); var company = Guid.NewGuid(); var message = Message(tenant, company, Data());
        var id = await Seed(message);
        var row = await factory.Inspect(db => db.Outbox.AsNoTracking().SingleAsync(x => x.Id == id));
        var received = new ReceivedMessage(Encoding.UTF8.GetBytes(row.Payload), id.ToString(), row.EventType, "application/json");
        await Process(received);
        using var client = factory.Client(tenant, company);
        using var other = factory.Client(Guid.NewGuid(), company);
        using var denied = factory.Client(tenant, Guid.NewGuid());
        using var noPermission = factory.Client(tenant, company, permissions: [Permissions.PurchaseRead]);
        var path = $"/api/v1/companies/{company}/reports/financial-summary?from=2026-10-01&until=2026-11-01";
        Assert.Equal(0, (await other.GetFromJsonAsync<FinancialReportResponse>(path))!.TransactionCount);
        Assert.Equal(HttpStatusCode.Forbidden, (await denied.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await noPermission.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Post(client, $"/api/v1/companies/{company}/reports/rebuild")).StatusCode);
        Assert.Equal(0, await factory.Inspect(db => db.Set<ReportingEntry>().CountAsync(x => x.CompanyId == company)));
        Assert.Null(await factory.Inspect(db => db.Outbox.Where(x => x.Id == id).Select(x => x.DispatchedAt).SingleAsync()));
        await Process(received); await Process(received);
        Assert.Equal(1, await factory.Inspect(db => db.Set<ReportingEntry>().CountAsync(x => x.CompanyId == company)));
    }

    [Fact]
    public async Task Unsupported_dead_letter_replay_reports_quarantine_instead_of_successful_projection()
    {
        var tenant = Guid.NewGuid(); var company = Guid.NewGuid();
        var dead = await Process(Message(tenant, company, Data(), schema: 9));
        using var client = factory.Client(tenant, company);
        var response = await Post(client, $"/api/v1/companies/{company}/messaging/dead-letters/{dead.DeadLetterId}/replay");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Quarantined", (await response.Content.ReadFromJsonAsync<ProcessedMessage>())!.Status);
        Assert.Equal(0, await factory.Inspect(db => db.Set<ReportingEntry>().CountAsync(x => x.CompanyId == company)));
    }

    private sealed class RecordingPublisher(bool fail = false) : IEventPublisher
    {
        public System.Collections.Concurrent.ConcurrentBag<OutgoingMessage> Messages { get; } = [];
        public IEnumerable<Guid> Ids => Messages.Select(x => x.Id);
        public Task Publish(OutgoingMessage message, CancellationToken ct)
        {
            if (fail) throw new MessagePublishException("publisher_rejected");
            Messages.Add(message); return Task.CompletedTask;
        }
    }
    private sealed class RejectProjectionCommit : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Injected projection commit failure.");
    }
}
