using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Vat.Application.Security;
using Vat.Services.Integration;
using Vat.Services.Integration.Infrastructure;
using Xunit;

namespace Vat.Integration.Tests;

public sealed class IntegrationOperatorApiTests(IntegrationApiFactory factory) : IClassFixture<IntegrationApiFactory>
{
    private static Task<HttpResponseMessage> Retry(HttpClient client, string path, string? key = null, string reason = "Reviewed recovery")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(new OperatorRetryRequest(reason)) };
        if (key is not null) request.Headers.Add("Idempotency-Key", key);
        return client.SendAsync(request);
    }
    [Fact]
    public async Task Independent_gateway_enforces_scope_permission_bounds_and_preserves_owner_command_identity()
    {
        var tenant=Guid.NewGuid();var company=Guid.NewGuid();var id=Guid.NewGuid(); var root=$"/api/v1/companies/{company}/messaging";
        var before=factory.Operators.Calls.Count; using var client=factory.Client(tenant,company);
        Assert.Equal(HttpStatusCode.Unauthorized,(await factory.CreateClient().GetAsync(root+"/outbox")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await factory.Client(tenant,company,permissions:[]).GetAsync(root+"/outbox")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await factory.Client(tenant,Guid.NewGuid()).GetAsync(root+"/outbox")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,(await client.GetAsync(root+"/outbox?take=101")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await Retry(client,root+$"/outbox/{id}/retry")).StatusCode);
        Assert.Equal(before,factory.Operators.Calls.Count);
        var key=Guid.NewGuid().ToString(); var correlation=Guid.NewGuid().ToString(); client.DefaultRequestHeaders.Add("X-Correlation-ID",correlation);
        var response=await Retry(client,root+$"/outbox/{id}/retry",key); response.EnsureSuccessStatusCode();
        Assert.Equal("true",response.Headers.GetValues("Idempotency-Replayed").Single());
        var call=factory.Operators.Calls.Last(); Assert.Equal(company,call.CompanyId);Assert.Equal(id,call.Id);Assert.Equal(key,call.IdempotencyKey);
        Assert.Equal(correlation,call.CorrelationId);Assert.Equal(client.DefaultRequestHeaders.Authorization!.ToString(),call.BearerToken);
        Assert.Equal(MessageOperatorAction.RetryOutbox,call.Action);
        (await client.GetAsync(root+"/dead-letters?take=1")).EnsureSuccessStatusCode(); Assert.Equal(MessageOperatorAction.ListDeadLetters,factory.Operators.Calls.Last().Action);
        (await Retry(client,root+$"/dead-letters/{id}/replay",Guid.NewGuid().ToString())).EnsureSuccessStatusCode();Assert.Equal(MessageOperatorAction.ReplayDeadLetter,factory.Operators.Calls.Last().Action);
        Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/health/ready")).StatusCode);
    }
    [Fact]
    public async Task Missing_operator_owner_fails_closed_without_losing_own_database_readiness()
    {
        using var host = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string,string?>
                { ["OperatorTargets:SourceBaseUrl"]="", ["OperatorTargets:InboxBaseUrl"]="" }));
            builder.ConfigureTestServices(services => { services.RemoveAll<IIntegrationOperatorClient>(); services.AddIntegrationOperators(); });
        });
        var tenant=Guid.NewGuid();var company=Guid.NewGuid();using var client=host.CreateClient();using var token=factory.Client(tenant,company);
        client.DefaultRequestHeaders.Authorization=token.DefaultRequestHeaders.Authorization;
        var response=await client.GetAsync($"/api/v1/companies/{company}/messaging/outbox");
        Assert.Equal(HttpStatusCode.ServiceUnavailable,response.StatusCode);
        Assert.Equal("application/problem+json",response.Content.Headers.ContentType!.MediaType);
        Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/health/ready")).StatusCode);
    }
    [Fact]
    public void Enabled_worker_refuses_legacy_local_financial_mode()
    {
        using var host=factory.WithWebHostBuilder(builder=>builder.ConfigureAppConfiguration((_,configuration)=>configuration.AddInMemoryCollection(new Dictionary<string,string?>
            { ["Messaging:Enabled"]="true",["Messaging:Uri"]="amqp://guest:guest@localhost:5672/%2F",["OutboxSource:Mode"]="Local" })));
        var error=Assert.ThrowsAny<Exception>(()=>host.CreateClient());
        Assert.Contains("Independent Integration requires remote",error.ToString());
    }
}
