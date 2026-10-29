using System.Net;
using System.Text;
using System.Text.Json;
using Vat.Domain;
using Vat.Messaging.Contracts.V1;
using Vat.Services.Reporting.Infrastructure;
using Xunit;

namespace Vat.Reporting.Tests;

public sealed class ExportHttpSourceTests
{
    [Theory]
    [InlineData("http://example.com")]
    [InlineData("https://user:password@example.com")]
    [InlineData("https://example.com/path")]
    [InlineData("https://example.com/?token=secret")]
    [InlineData("https://example.com/#fragment")]
    public void Unsafe_source_origins_are_refused(string origin)
        => Assert.Throws<DomainException>(() => new ReportingExportHttpSource(origin, "delegated-token"));

    [Theory]
    [InlineData("redirect")]
    [InlineData("oversized")]
    [InlineData("streamed-oversized")]
    [InlineData("invalid-json")]
    public async Task Redirects_oversized_and_invalid_json_fail_closed_without_retries(string fault)
    {
        var handler = new Handler(fault);
        using var source = new ReportingExportHttpSource("https://source.example", "delegated-token", false, new HttpClient(handler));
        var error = await Assert.ThrowsAsync<DomainException>(() => source.Manifest(Guid.NewGuid(), Guid.NewGuid(), default));
        Assert.Equal("reporting_source_unavailable", error.Code); Assert.Equal(1, handler.Calls);
        Assert.Equal("Bearer delegated-token", handler.Authorization);
        Assert.StartsWith("https://source.example/internal/v1/reporting-source/companies/", handler.Url);
    }
    private sealed class Handler(string fault) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string Authorization { get; private set; } = "";
        public string Url { get; private set; } = "";
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++; Authorization = request.Headers.Authorization!.ToString(); Url = request.RequestUri!.ToString();
            return Task.FromResult(new HttpResponseMessage(fault == "redirect" ? HttpStatusCode.Redirect : HttpStatusCode.OK)
            {
                Content = fault == "streamed-oversized" ? new StreamedContent() : new StringContent(fault == "oversized" ? OversizedJson() : "not-json", Encoding.UTF8, "application/json")
            });
        }
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Customer_history_known_and_streamed_oversized_valid_json_are_bounded(bool streamed)
    {
        var tenant = Guid.NewGuid(); var company = Guid.NewGuid(); var customer = Guid.NewGuid(); var id = Guid.NewGuid();
        var data = new CustomerHistoryDataV1("C", "Customer", 1, "BDT", [], [], [], [], [], []);
        var manifest = new CustomerHistoryManifestV1(1, id, tenant, company, customer, CustomerHistoryV1.Scope,
            DateTimeOffset.UtcNow, true, 0, CustomerHistoryV1.Fingerprint(tenant, company, customer, data));
        // Valid export plus an ignored property: only transport byte enforcement can reject it.
        var json = JsonSerializer.Serialize(new { manifest, data, padding = new string('x', CustomerHistoryV1.MaxBytes + 32769) }, ReportingSourceV1.Json);
        var handler = new CustomerHandler(json, streamed);
        using var source = new ReportingExportHttpSource("https://source.example", "delegated-token", false, new HttpClient(handler));
        Assert.Equal("reporting_source_unavailable", (await Assert.ThrowsAsync<DomainException>(() => source.CustomerHistory(company, customer, id, default))).Code);
        Assert.Equal(1, handler.Calls); Assert.Equal("Bearer delegated-token", handler.Authorization);
        Assert.Equal($"https://source.example/internal/v1/reporting-source/companies/{company}/customers/{customer}/exports/{id}", handler.Url);
    }
    private sealed class CustomerHandler(string json, bool streamed) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string Authorization { get; private set; } = "";
        public string Url { get; private set; } = "";
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++; Authorization = request.Headers.Authorization!.ToString(); Url = request.RequestUri!.ToString();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = streamed ? new UnknownLengthContent(json) : new StringContent(json, Encoding.UTF8, "application/json") });
        }
    }
    private sealed class UnknownLengthContent : HttpContent
    {
        private readonly byte[] _bytes;
        public UnknownLengthContent(string json) { _bytes = Encoding.UTF8.GetBytes(json); Headers.ContentType = new("application/json"); }
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(_bytes).AsTask();
        protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult<Stream>(new MemoryStream(_bytes, false));
    }
    private sealed class StreamedContent : HttpContent
    {
        private readonly byte[] _bytes = Encoding.UTF8.GetBytes(OversizedJson());
        public StreamedContent() => Headers.ContentType = new("application/json");
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(_bytes).AsTask();
        protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult<Stream>(new MemoryStream(_bytes, false));
    }
    private static string OversizedJson() => JsonSerializer.Serialize(new
    {
        Version = 1, ExportId = Guid.NewGuid(), TenantId = Guid.NewGuid(), CompanyId = Guid.NewGuid(), Scope = ReportingSourceV1.Scope,
        CapturedAt = DateTimeOffset.UtcNow, ReconciledPurchases = 0, ReconciledReversals = 0, EventCount = 0, CoverageComplete = true,
        FingerprintSha256 = new string('a', 64), Padding = new string('x', 32769)
    }, ReportingSourceV1.Json);
}
