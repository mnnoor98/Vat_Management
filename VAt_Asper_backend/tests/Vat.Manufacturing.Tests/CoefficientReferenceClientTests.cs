using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Vat.Application.Security;
using Vat.Domain;
using Vat.Services.Manufacturing;
using Vat.Services.Manufacturing.Infrastructure;
using Xunit;

namespace Vat.Manufacturing.Tests;

public sealed class CoefficientReferenceClientTests
{
    [Theory]
    [InlineData("http://remote.example", false)]
    [InlineData("http://127.0.0.1:5103", false)]
    [InlineData("https://user:password@master.example", false)]
    [InlineData("https://master.example/path", false)]
    public async Task Unsafe_reference_endpoints_are_rejected_before_sending_credentials(string endpoint, bool local)
    {
        var handler = new Handler(); var client = Build(handler, endpoint, local);
        var actor = Actor();
        var error = await Assert.ThrowsAsync<DomainException>(() => client.Product(actor, actor.CompanyIds.Single(), Guid.NewGuid(), "Bearer secret", default));
        Assert.Equal("reference_service_unavailable", error.Code); Assert.Equal(0, handler.Calls);
    }
    [Fact]
    public async Task Reference_validation_forwards_delegated_token_and_checks_product_identity()
    {
        var actor = Actor(); var company = actor.CompanyIds.Single(); var product = Guid.NewGuid();
        var handler = new Handler { Response = new(HttpStatusCode.OK) { Content = JsonContent.Create(new ProductReference(product, "REF", new string('P', 500), "KG", "TEST")) } };
        var client = Build(handler, "https://master.example", false);
        Assert.Equal(product, (await client.Product(actor, company, product, "Bearer delegated", default)).Id);
        Assert.Equal("Bearer delegated", handler.Authorization); Assert.Equal($"/api/v1/companies/{company}/products/{product}", handler.Path);
        handler.Response = new(HttpStatusCode.OK) { Content = JsonContent.Create(new ProductReference(Guid.NewGuid(), "REF", "Other", "KG", "TEST")) };
        Assert.Equal("reference_response_invalid", (await Assert.ThrowsAsync<DomainException>(() => client.Product(actor, company, product, "Bearer delegated", default))).Code);
        handler.Response = new(HttpStatusCode.Redirect) { Content = new StringContent("") };
        Assert.Equal("reference_service_unavailable", (await Assert.ThrowsAsync<DomainException>(() => client.Product(actor, company, product, "Bearer delegated", default))).Code);
    }
    private static Actor Actor() => new(Guid.NewGuid(), "maker", new HashSet<Guid> { Guid.NewGuid() }, new HashSet<string> { Permissions.MasterDataRead });
    private static CoefficientReferenceClient Build(Handler handler, string endpoint, bool local) => new(new HttpClient(handler),
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ManufacturingReferences:MasterDataBaseUrl"] = endpoint,
            ["ManufacturingReferences:AllowLoopbackHttp"] = local.ToString() }).Build());
    private sealed class Handler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string? Authorization { get; private set; }
        public string? Path { get; private set; }
        public HttpResponseMessage Response { get; set; } = new(HttpStatusCode.ServiceUnavailable);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { Calls++; Authorization = request.Headers.Authorization?.ToString(); Path = request.RequestUri?.AbsolutePath; return Task.FromResult(Response); }
    }
}
