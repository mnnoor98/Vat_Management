using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Vat.Application.Security;
using Vat.Contracts.Manufacturing;
using Vat.Domain;
using Vat.Infrastructure.Persistence;
using Xunit;

namespace Vat.Tests;

public sealed class ProductionReferenceClientTests
{
    [Theory]
    [InlineData("http://remote.example")]
    [InlineData("http://127.0.0.1:5108")]
    [InlineData("https://user:password@manufacturing.example")]
    [InlineData("https://manufacturing.example/path")]
    public async Task Untrusted_origin_never_receives_delegated_credentials(string endpoint)
    {
        var handler=new Handler();var (actor,source)=Evidence();
        var error=await Assert.ThrowsAsync<DomainException>(()=>Client(handler,endpoint).Order(actor,source.CompanyId,source.OrderId,"Bearer delegated",default));
        Assert.Equal("reference_service_unavailable",error.Code);Assert.Equal(0,handler.Calls);
    }

    [Fact]
    public async Task Approved_source_is_scoped_and_uses_delegated_authentication()
    {
        var (actor,source)=Evidence();var handler=new Handler{Response=new(HttpStatusCode.OK){Content=JsonContent.Create(source)}};
        var value=await Client(handler).Order(actor,source.CompanyId,source.OrderId,"Bearer delegated",default);
        Assert.Equal(source.OrderId,value.OrderId);Assert.Equal("Bearer delegated",handler.Authorization);
        Assert.Equal($"/api/v1/companies/{source.CompanyId}/manufacturing/production-orders/{source.OrderId}/execution-source",handler.Path);
        handler.Response=new(HttpStatusCode.OK){Content=JsonContent.Create(source with{CompanyId=Guid.NewGuid()})};
        Assert.Equal("reference_response_invalid",(await Assert.ThrowsAsync<DomainException>(()=>Client(handler).Order(actor,source.CompanyId,source.OrderId,"Bearer delegated",default))).Code);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden,"reference_access_denied")]
    [InlineData(HttpStatusCode.NotFound,"not_found")]
    [InlineData(HttpStatusCode.Conflict,"production_order_not_approved")]
    [InlineData(HttpStatusCode.Redirect,"reference_service_unavailable")]
    public async Task Owner_denial_and_redirect_never_become_execution_evidence(HttpStatusCode status,string code)
    {
        var(actor,source)=Evidence();var handler=new Handler{Response=new(status)};
        Assert.Equal(code,(await Assert.ThrowsAsync<DomainException>(()=>Client(handler).Order(actor,source.CompanyId,source.OrderId,"Bearer delegated",default))).Code);
    }

    [Fact]
    public void Maker_approval_and_null_material_fail_closed()
    {
        var(actor,source)=Evidence();
        Assert.Equal("reference_response_invalid",Assert.Throws<DomainException>(()=>ProductionExecutionReferenceClient.Validate(source with{ApprovedBy=source.CreatedBy},actor,source.CompanyId,source.OrderId)).Code);
        var malformed=source with{Evidence=source.Evidence with{Materials=[null!]}};
        Assert.Equal("reference_response_invalid",Assert.Throws<DomainException>(()=>ProductionExecutionReferenceClient.Validate(malformed,actor,source.CompanyId,source.OrderId)).Code);
        var tampered=source with{Evidence=source.Evidence with{PlannedQuantity=11}};
        Assert.Equal("reference_response_invalid",Assert.Throws<DomainException>(()=>ProductionExecutionReferenceClient.Validate(tampered,actor,source.CompanyId,source.OrderId)).Code);
    }
    [Fact]
    public void Frozen_physical_outputs_are_canonical_scoped_and_digest_bound()
    {
        var(actor,source)=Evidence();var a=Guid.NewGuid();var b=Guid.NewGuid();var evidence=source.Evidence with{ByProducts=[new(new(a,"SCRAP","KG"),"Scrap"),new(new(b,"SECONDARY","LTR"),"ByProduct")]};
        evidence=evidence with{ByProducts=evidence.ByProducts!.OrderBy(x=>x.Product.Id).ToArray()};var valid=Sign(source,evidence);ProductionExecutionReferenceClient.Validate(valid,actor,source.CompanyId,source.OrderId);
        var noncanonical=Sign(source,evidence with{ByProducts=evidence.ByProducts.Reverse().ToArray()});Assert.Equal("reference_response_invalid",Assert.Throws<DomainException>(()=>ProductionExecutionReferenceClient.Validate(noncanonical,actor,source.CompanyId,source.OrderId)).Code);
        var overlap=Sign(source,evidence with{ByProducts=[new(source.Evidence.FinishedProduct,"Scrap")]});Assert.Equal("reference_response_invalid",Assert.Throws<DomainException>(()=>ProductionExecutionReferenceClient.Validate(overlap,actor,source.CompanyId,source.OrderId)).Code);
    }

    private static (Actor,ProductionOrderSource) Evidence()
    {
        var tenant=Guid.NewGuid();var company=Guid.NewGuid();var actor=new Actor(tenant,"maker",new HashSet<Guid>{company},new HashSet<string>());
        var evidence=new ProductionOrderEvidence(Guid.NewGuid(),1,3,new string('B',64),Guid.NewGuid(),"Output",new(2026,10,23),new(Guid.NewGuid(),"FG","PCS"),10,10,[new(new(Guid.NewGuid(),"RAW","KG"),5,0)]);
        var digest=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(evidence,new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))));
        return(actor,new(tenant,company,Guid.NewGuid(),3,"Approved","ORDER","owner-maker","owner-checker",digest,evidence));
    }
    private static ProductionOrderSource Sign(ProductionOrderSource source,ProductionOrderEvidence evidence)=>source with{Evidence=evidence,EvidenceSha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(evidence,new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))))};
    private static ProductionExecutionReferenceClient Client(Handler handler,string endpoint="https://manufacturing.example")=>new(new HttpClient(handler),new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["ProductionReferences:ManufacturingBaseUrl"]=endpoint}).Build());
    private sealed class Handler:HttpMessageHandler
    {
        public int Calls;public string? Authorization;public string? Path;
        public HttpResponseMessage Response=new(HttpStatusCode.ServiceUnavailable);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
        {Calls++;Authorization=request.Headers.Authorization?.ToString();Path=request.RequestUri?.AbsolutePath;return Task.FromResult(Response);}
    }
}
