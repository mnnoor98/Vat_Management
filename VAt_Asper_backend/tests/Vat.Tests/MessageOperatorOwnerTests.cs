using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vat.Application.Contracts;
using Vat.Application.Security;
using Vat.Infrastructure.Persistence;
using Vat.Messaging.Contracts.V1;
using Xunit;

namespace Vat.Tests;

[Collection("PostgreSQL integration")]
public sealed class MessageOperatorOwnerTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> Host(bool rejectAudit = false) => factory.WithWebHostBuilder(builder =>
    {
        builder.ConfigureAppConfiguration((_,configuration)=>configuration.AddInMemoryCollection(new Dictionary<string,string?> { ["OperatorServer:Enabled"]="true" }));
        if(rejectAudit)builder.ConfigureTestServices(services=>services.AddDbContext<VatDbContext>(options=>options.AddInterceptors(new RejectAudit())));
    });
    private HttpClient Authorized(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> host,Guid tenant,Guid company,string actor="maker",string[]? permissions=null)
    {
        var client=host.CreateClient();using var token=factory.Client(tenant,company,actor,permissions);
        client.DefaultRequestHeaders.Authorization=token.DefaultRequestHeaders.Authorization;return client;
    }
    private static Task<HttpResponseMessage> Command(HttpClient client,string path,Guid key,string reason="Reviewed recovery")
    {
        var request=new HttpRequestMessage(HttpMethod.Post,path){Content=JsonContent.Create(new RetryMessageV1(reason))};
        request.Headers.Add("Idempotency-Key",key.ToString());return client.SendAsync(request);
    }
    [Fact]
    public async Task Owner_retry_and_compatibility_route_share_actor_bound_idempotency_and_one_audit()
    {
        var tenant=Guid.NewGuid();var company=Guid.NewGuid();var id=Guid.NewGuid();var key=Guid.NewGuid();
        await factory.Inspect(async db=> { db.Outbox.Add(new OutboxMessage { Id=id,TenantId=tenant,CompanyId=company,AggregateId=Guid.NewGuid(),AggregateVersion=1,
            EventType="OperatorFixture",Payload="{}",CreatedAt=DateTimeOffset.UtcNow,AttemptCount=8,TotalAttempts=8,QuarantinedAt=DateTimeOffset.UtcNow,LastError="broker_unavailable" });await db.SaveChangesAsync();return 0; });
        using var host=Host();using var client=Authorized(host,tenant,company);var path=MessageOperatorV1.CompanyPath(company)+$"/outbox/{id}/retry";
        Assert.Equal(HttpStatusCode.Unauthorized,(await Command(host.CreateClient(),path,key)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await Command(Authorized(host,tenant,company,permissions:[]),path,key)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await Command(Authorized(host,tenant,Guid.NewGuid()),path,key)).StatusCode);
        (await Command(client,path,key)).EnsureSuccessStatusCode();var repeat=await Command(client,path,key);repeat.EnsureSuccessStatusCode();
        Assert.Equal("true",repeat.Headers.GetValues("Idempotency-Replayed").Single());
        using var compatibility=factory.Client(tenant,company);
        var legacy=await Command(compatibility,$"/api/v1/companies/{company}/messaging/outbox/{id}/retry",key);
        legacy.EnsureSuccessStatusCode();Assert.Equal("true",legacy.Headers.GetValues("Idempotency-Replayed").Single());
        Assert.Equal(HttpStatusCode.Conflict,(await Command(client,path,key,"Changed reason")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await Command(Authorized(host,tenant,company,"checker"),path,key)).StatusCode);
        await factory.Inspect(async db=> { Assert.Equal(1,await db.Audit.CountAsync(x=>x.ResourceId==id));Assert.Equal(1,await db.Idempotency.CountAsync(x=>x.CompanyId==company));return 0; });
        Assert.Equal(HttpStatusCode.UnprocessableEntity,(await client.GetAsync(MessageOperatorV1.CompanyPath(company)+"/outbox?take=101")).StatusCode);
        using var other=Authorized(host,Guid.NewGuid(),company);
        Assert.Equal("[]",await other.GetStringAsync(MessageOperatorV1.CompanyPath(company)+"/outbox"));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Owner_replay_commits_projection_audit_and_receipt_together_or_rolls_back_all(bool rejectAudit)
    {
        var tenant=Guid.NewGuid();var company=Guid.NewGuid();var deadId=Guid.NewGuid();var eventId=Guid.NewGuid();var transactionId=Guid.NewGuid();var key=Guid.NewGuid();
        var data=new FinancialEntryRecordedV1(transactionId,Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),"Purchase",new(2026,10,24),"TEST-ONLY","BDT",250,18.75m,7.5m,null);
        var type=nameof(FinancialEntryRecordedV1);var messageId=eventId.ToString();const string contentType="application/json";
        var body=JsonSerializer.SerializeToUtf8Bytes(new IntegrationEvent<FinancialEntryRecordedV1>(eventId,type,1,DateTimeOffset.UtcNow,tenant,company,data.PurchaseId,5,Guid.NewGuid().ToString(),data),Json);
        var bodyHash=Convert.ToHexString(SHA256.HashData(body));
        var fingerprint=Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { MessageId=messageId,EventType=type,ContentType=contentType,bodyHash })));
        await factory.Inspect(async db=> { db.Add(new InboxDeadLetter { Id=deadId,TenantId=tenant,CompanyId=company,Fingerprint=fingerprint,Body=body,MessageId=messageId,
            EventType=type,ContentType=contentType,ErrorCode="temporary_failure",Occurrences=1,FirstSeenAt=DateTimeOffset.UtcNow,LastSeenAt=DateTimeOffset.UtcNow });await db.SaveChangesAsync();return 0; });
        using var host=Host(rejectAudit);using var client=Authorized(host,tenant,company);var path=MessageOperatorV1.CompanyPath(company)+$"/dead-letters/{deadId}/replay";
        var response=await Command(client,path,key);Assert.Equal(rejectAudit?HttpStatusCode.InternalServerError:HttpStatusCode.OK,response.StatusCode);
        await factory.Inspect(async db=>
        {
            Assert.Equal(!rejectAudit,await db.Set<ReportingEntry>().AnyAsync(x=>x.TransactionId==transactionId));
            Assert.Equal(!rejectAudit,await db.Set<InboxMessage>().AnyAsync(x=>x.EventId==eventId));
            Assert.Equal(!rejectAudit,await db.Audit.AnyAsync(x=>x.ResourceId==deadId));
            Assert.Equal(!rejectAudit,await db.Idempotency.AnyAsync(x=>x.CompanyId==company));
            Assert.Equal(!rejectAudit,(await db.Set<InboxDeadLetter>().FindAsync(deadId))!.ResolvedAt is not null);return 0;
        });
        if(!rejectAudit){var repeat=await Command(client,path,key);repeat.EnsureSuccessStatusCode();Assert.Equal("true",repeat.Headers.GetValues("Idempotency-Replayed").Single());}
    }
    private sealed class RejectAudit : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,InterceptionResult<int> result,CancellationToken ct=default)
        {
            if(data.Context!.ChangeTracker.Entries<AuditEntry>().Any(x=>x.State==EntityState.Added&&x.Entity.Action=="InboxReplayRequested"))
                throw new InvalidOperationException("Injected owner audit failure");
            return ValueTask.FromResult(result);
        }
    }
}
