using System.Net;
using Microsoft.Extensions.Configuration;
using Vat.Domain;
using Vat.Services.Integration;
using Vat.Services.Integration.Infrastructure;
using Xunit;

namespace Vat.Integration.Tests;

public sealed class HttpIntegrationOperatorClientTests
{
    private static OperatorRequest Request() => new(MessageOperatorAction.RetryOutbox,Guid.NewGuid(),Guid.NewGuid(),null,25,"Reviewed recovery","Bearer delegated",Guid.NewGuid().ToString(),Guid.NewGuid().ToString());
    private static HttpIntegrationOperatorClient Client(Handler handler,string origin) => new(new HttpClient(handler),new ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string,string?> { ["OperatorTargets:SourceBaseUrl"]=origin,["OperatorTargets:InboxBaseUrl"]="https://inbox.example/" }).Build());
    [Theory]
    [InlineData("http://remote.example/")]
    [InlineData("http://127.0.0.1:5113/")]
    [InlineData("https://user:password@owner.example/")]
    [InlineData("https://owner.example/path")]
    public async Task Unsafe_targets_never_receive_delegated_credentials(string origin)
    {
        var handler=new Handler();var error=await Assert.ThrowsAsync<DomainException>(()=>Client(handler,origin).Send(Request(),default));
        Assert.Equal("operator_owner_unavailable",error.Code);Assert.Equal(0,handler.Calls);
    }
    [Fact]
    public async Task Command_forwarding_preserves_token_key_correlation_body_and_owner_receipt()
    {
        var handler=new Handler();var request=Request();var client=Client(handler,"https://source.example/");
        var response=await client.Send(request,default);Assert.True(response.Replayed);Assert.Equal(200,response.StatusCode);
        Assert.Equal("source.example",handler.Host);Assert.Contains($"/outbox/{request.Id}/retry",handler.Path);
        Assert.Equal(request.BearerToken,handler.Token);Assert.Equal(request.IdempotencyKey,handler.Key);Assert.Equal(request.CorrelationId,handler.Correlation);
        Assert.Equal("{\"reason\":\"Reviewed recovery\"}",handler.Body);
        await client.Send(request with { Action=MessageOperatorAction.ListDeadLetters,Id=null },default);Assert.Equal("inbox.example",handler.Host);
    }
    [Theory]
    [InlineData(302,true)]
    [InlineData(200,false)]
    public async Task Redirect_or_missing_commit_response_fails_closed_without_retry(int status,bool receipt)
    {
        var handler=new Handler { Status=(HttpStatusCode)status,Receipt=receipt };
        Assert.Equal(503,(await Assert.ThrowsAsync<DomainException>(()=>Client(handler,"https://source.example/").Send(Request(),default))).Status);
        Assert.Equal(1,handler.Calls);
    }
    private sealed class Handler : HttpMessageHandler
    {
        public int Calls; public string? Host,Path,Token,Key,Correlation,Body; public HttpStatusCode Status=HttpStatusCode.OK; public bool Receipt=true;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            Calls++; Host=request.RequestUri!.Host;Path=request.RequestUri.AbsolutePath;Token=request.Headers.Authorization?.ToString();
            Key=request.Headers.TryGetValues("Idempotency-Key",out var keys)?keys.Single():null;Correlation=request.Headers.GetValues("X-Correlation-ID").Single();
            Body=request.Content is null?null:await request.Content.ReadAsStringAsync(ct);
            var response=new HttpResponseMessage(Status) { Content=new StringContent("{\"status\":\"Pending\"}") };
            if(Receipt)response.Headers.Add("Idempotency-Replayed","true");return response;
        }
    }
}
