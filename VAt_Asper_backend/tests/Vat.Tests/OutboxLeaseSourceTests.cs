using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Vat.Infrastructure.Messaging;
using Vat.Infrastructure.Persistence;
using Vat.Messaging.Contracts.V1;
using Xunit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vat.Messaging.Http;

namespace Vat.Tests;

[Collection("PostgreSQL integration")]
public sealed class OutboxLeaseSourceTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private sealed class OwnerClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new DateTimeOffset(DateTimeOffset.UtcNow.Ticks / 10 * 10, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private static FinancialOutboxLeaseSourceV1 Source(VatDbContext db, OwnerClock clock, int maximum = 8) =>
        new(db, clock, Options.Create(new MessagingSettings { MaxAttempts = maximum }));

    private static async Task<Guid> Seed(VatDbContext db, OwnerClock clock, int attempts = 0)
    {
        // This is the factory's private disposable database; each case resets only its test outbox.
        await db.Outbox.ExecuteDeleteAsync(); db.ChangeTracker.Clear();
        var row = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            CompanyId = Guid.NewGuid(),
            AggregateId = Guid.NewGuid(),
            AggregateVersion = 5,
            EventType = "FinancialEntryRecordedV1",
            CreatedAt = clock.Now.AddSeconds(-1),
            Payload = "{\"schemaVersion\":1,\"payload\":{\"value\":12}}",
            AttemptCount = attempts,
            TotalAttempts = attempts
        };
        db.Add(row); await db.SaveChangesAsync(); db.ChangeTracker.Clear(); return row.Id;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Real_http_source_preserves_postgres_scope_and_settlement(bool fail)
    {
        await factory.Inspect(async db =>
        {
            var clock = new OwnerClock(); var id = await Seed(db, clock);
            var key = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Copied API Kestrel endpoints override UseUrls; never claim the application port.
                ["Kestrel:Endpoints:Http:Url"] = "http://127.0.0.1:0",
                ["OutboxServer:ServiceKey"] = key, ["OutboxServer:AllowLoopbackHttp"] = "true",
                ["Logging:LogLevel:Default"] = "Warning"
            });
            builder.Services.AddSingleton<IOutboxLeaseSourceV1>(Source(db, clock));
            builder.Services.AddOutboxHttpServer(builder.Configuration);
            await using var app = builder.Build();
            app.UseAuthentication(); app.UseAuthorization(); app.MapOutboxHttpServer();
            await app.StartAsync();
            try
            {
                using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
                var address = app.Urls.Single();
                Assert.Equal(System.Net.HttpStatusCode.Unauthorized,
                    (await http.PostAsync(address + OutboxHttpEndpoints.Prefix + "/claim", null)).StatusCode);
                var remote = new HttpOutboxLeaseSourceV1(http, Options.Create(new OutboxHttpClientSettings
                { BaseAddress = address, ServiceKey = key, AllowLoopbackHttp = true }));
                var claim = (await remote.Claim(default))!;
                Assert.Equal(id, claim.Receipt.MessageId); Assert.True(claim.HasValidIntegrity());
                Assert.False(await remote.Confirm(claim.Receipt with { CompanyId = Guid.NewGuid() }, default));
                if (fail) Assert.True(await remote.Fail(claim.Receipt, "publish_timeout", default));
                else Assert.True(await remote.Confirm(claim.Receipt, default));
                Assert.False(await remote.Confirm(claim.Receipt, default));
                var row = await db.Outbox.AsNoTracking().SingleAsync(x => x.Id == id);
                Assert.Null(row.LeaseToken); Assert.Equal(1, row.AttemptCount);
                if (fail) { Assert.Null(row.DispatchedAt); Assert.Equal("publish_timeout", row.LastError); Assert.NotNull(row.NextAttemptAt); }
                else { Assert.NotNull(row.DispatchedAt); Assert.Null(row.LastError); }
            }
            finally { await app.StopAsync(); }
            return 0;
        });
    }

    [Fact]
    public async Task Claim_exposes_versioned_scope_digest_and_original_stored_payload()
    {
        await factory.Inspect(async db =>
        {
            var clock = new OwnerClock(); var id = await Seed(db, clock); var source = Source(db, clock);
            var claim = Assert.IsType<ClaimedOutboxMessageV1>(await source.Claim(default));
            var row = await db.Outbox.AsNoTracking().SingleAsync(x => x.Id == id);
            Assert.True(claim.HasValidIntegrity()); Assert.Equal(1, claim.Receipt.ContractVersion);
            Assert.Equal(FinancialOutboxLeaseSourceV1.SourceOwner, claim.Receipt.SourceOwner);
            Assert.Equal(row.TenantId, claim.Receipt.TenantId); Assert.Equal(row.CompanyId, claim.Receipt.CompanyId);
            Assert.Equal(row.AggregateId, claim.AggregateId); Assert.Equal(row.AggregateVersion, claim.AggregateVersion);
            Assert.Equal(Encoding.UTF8.GetBytes(row.Payload), claim.Payload);
            Assert.Equal(Convert.ToHexString(SHA256.HashData(claim.Payload)), claim.PayloadSha256);
            Assert.Equal(clock.Now.AddSeconds(60), claim.LeaseExpiresAt); Assert.Equal(1, claim.Attempt);
            Assert.True(await source.Confirm(claim.Receipt, default)); return 0;
        });
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("company")]
    [InlineData("event")]
    [InlineData("token")]
    [InlineData("owner")]
    [InlineData("version")]
    public async Task Settlement_rejects_wrong_scope_token_owner_and_protocol(string mismatch)
    {
        await factory.Inspect(async db =>
        {
            var clock = new OwnerClock(); var id = await Seed(db, clock); var source = Source(db, clock);
            var claim = (await source.Claim(default))!; var valid = claim.Receipt;
            var invalid = mismatch switch
            {
                "tenant" => valid with { TenantId = Guid.NewGuid() },
                "company" => valid with { CompanyId = Guid.NewGuid() },
                "event" => valid with { MessageId = Guid.NewGuid() },
                "token" => valid with { LeaseToken = Guid.NewGuid() },
                "owner" => valid with { SourceOwner = "another-owner" },
                _ => valid with { ContractVersion = 2 }
            };
            Assert.False(await source.Confirm(invalid, default)); Assert.False(await source.Fail(invalid, "publisher_rejected", default));
            var untouched = await db.Outbox.AsNoTracking().SingleAsync(x => x.Id == id);
            Assert.Null(untouched.DispatchedAt); Assert.Null(untouched.LastError); Assert.Equal(valid.LeaseToken, untouched.LeaseToken);
            Assert.Equal(1, untouched.AttemptCount); Assert.True(await source.Confirm(valid, default));
            Assert.False(await source.Confirm(valid, default)); Assert.False(await source.Fail(valid, "publisher_rejected", default)); return 0;
        });
    }

    [Fact]
    public async Task Expired_receipt_cannot_settle_before_or_after_reclaim()
    {
        await factory.Inspect(async db =>
        {
            var clock = new OwnerClock(); var id = await Seed(db, clock); var source = Source(db, clock);
            var old = (await source.Claim(default))!;
            clock.Now = old.LeaseExpiresAt.AddSeconds(1);
            Assert.False(await source.Confirm(old.Receipt, default)); Assert.False(await source.Fail(old.Receipt, "publisher_rejected", default));
            var fresh = (await source.Claim(default))!;
            Assert.Equal(id, fresh.Receipt.MessageId); Assert.NotEqual(old.Receipt.LeaseToken, fresh.Receipt.LeaseToken);
            Assert.Equal(old.Payload, fresh.Payload); Assert.Equal(old.PayloadSha256, fresh.PayloadSha256); Assert.Equal(2, fresh.Attempt);
            Assert.False(await source.Confirm(old.Receipt, default)); Assert.False(await source.Fail(old.Receipt, "publisher_rejected", default));
            Assert.True(await source.Confirm(fresh.Receipt, default));
            var row = await db.Outbox.AsNoTracking().SingleAsync(x => x.Id == id);
            Assert.NotNull(row.DispatchedAt); Assert.Equal(2, row.TotalAttempts); return 0;
        });
    }

    [Fact]
    public async Task Failure_uses_owner_attempt_policy_and_quarantines_without_publishing_again()
    {
        await factory.Inspect(async db =>
        {
            var clock = new OwnerClock(); var id = await Seed(db, clock); var source = Source(db, clock, 2);
            var first = (await source.Claim(default))!;
            Assert.True(await source.Fail(first.Receipt, "private broker exception text", default));
            var retry = await db.Outbox.AsNoTracking().SingleAsync(x => x.Id == id);
            Assert.Equal("broker_unavailable", retry.LastError); Assert.Null(retry.QuarantinedAt);
            Assert.Equal(clock.Now.AddSeconds(1), retry.NextAttemptAt); Assert.Null(retry.LeaseToken);
            Assert.Null(await source.Claim(default)); clock.Now = clock.Now.AddSeconds(2);
            var second = (await source.Claim(default))!; Assert.Equal(2, second.Attempt);
            Assert.True(await source.Fail(second.Receipt, "publisher_rejected", default)); Assert.Null(await source.Claim(default));
            var quarantined = await db.Outbox.AsNoTracking().SingleAsync(x => x.Id == id);
            Assert.NotNull(quarantined.QuarantinedAt); Assert.Null(quarantined.DispatchedAt); Assert.Equal(2, quarantined.TotalAttempts);
            Assert.False(await source.Confirm(second.Receipt, default)); return 0;
        });
    }

    [Fact]
    public async Task Exhausted_unleased_record_is_quarantined_without_another_claim()
    {
        await factory.Inspect(async db =>
        {
            var clock = new OwnerClock(); var id = await Seed(db, clock, 2); var source = Source(db, clock, 2);
            Assert.Null(await source.Claim(default)); var row = await db.Outbox.AsNoTracking().SingleAsync(x => x.Id == id);
            Assert.NotNull(row.QuarantinedAt); Assert.Equal("retry_limit_reached", row.LastError); Assert.Equal(2, row.TotalAttempts); return 0;
        });
    }

    [Fact]
    public async Task Failed_claim_commit_rolls_back_attempts_and_can_be_recovered()
    {
        await factory.Inspect(async db =>
        {
            var clock = new OwnerClock(); var id = await Seed(db, clock);
            await using (var failing = new VatDbContext(new DbContextOptionsBuilder<VatDbContext>()
                .UseNpgsql(db.Database.GetConnectionString()).AddInterceptors(new RejectClaim()).Options))
                await Assert.ThrowsAsync<InvalidOperationException>(() => Source(failing, clock).Claim(default));
            var unchanged = await db.Outbox.AsNoTracking().SingleAsync(x => x.Id == id);
            Assert.Equal(0, unchanged.AttemptCount); Assert.Null(unchanged.LeaseToken); Assert.Null(unchanged.DispatchedAt);
            var source = Source(db, clock); var claim = (await source.Claim(default))!; Assert.Equal(id, claim.Receipt.MessageId);
            Assert.Equal(1, claim.Attempt); Assert.True(await source.Confirm(claim.Receipt, default)); return 0;
        });
    }
    private sealed class RejectClaim : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,
            InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (data.Context!.ChangeTracker.Entries<OutboxMessage>().Any(x => x.State == EntityState.Modified))
                throw new InvalidOperationException("claim commit rejected for rollback test");
            return ValueTask.FromResult(result);
        }
    }
}
