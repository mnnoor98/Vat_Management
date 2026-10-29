using System.Net.Http.Headers;
using System.Text.Json;
using Vat.Domain;
using Vat.Messaging.Contracts.V1;
using Vat.Services.Reporting.Application;

namespace Vat.Services.Reporting.Infrastructure;

// One delegated operator identity per instance. No worker key, redirects, cookies or automatic retries.
public sealed class ReportingExportHttpSource : IReportingExportSource, ICustomerHistorySource, IDisposable
{
    private readonly HttpClient _client;
    private readonly Uri _origin;
    private readonly string _token;
    public ReportingExportHttpSource(string baseUrl, string bearerToken, bool allowLoopbackHttp = false)
        : this(baseUrl, bearerToken, allowLoopbackHttp, null) { }
    internal ReportingExportHttpSource(string baseUrl, string bearerToken, bool allowLoopbackHttp, HttpClient? client)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) || uri.UserInfo.Length != 0 || uri.Query.Length != 0
            || uri.Fragment.Length != 0 || uri.AbsolutePath != "/" || (uri.Scheme != "https" && !(allowLoopbackHttp && uri.Scheme == "http" && uri.IsLoopback)))
            throw new DomainException("invalid_source_configuration", "Source requires an HTTPS origin or explicitly allowed loopback HTTP.");
        if (string.IsNullOrWhiteSpace(bearerToken) || bearerToken.Length > 16384 || bearerToken.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)))
            throw new DomainException("invalid_source_configuration", "A delegated bearer token is required.");
        _origin = uri; _token = bearerToken;
        _client = client ?? new(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }) { Timeout = TimeSpan.FromSeconds(20) };
    }
    public Task<ReportingExportManifestV1> Manifest(Guid companyId, Guid exportId, CancellationToken ct)
        => Get<ReportingExportManifestV1>($"{Path(companyId)}/{exportId}", 32768, ct);
    public Task<ReportingExportPageV1> Page(Guid companyId, Guid exportId, int offset, int take, CancellationToken ct)
        => Get<ReportingExportPageV1>($"{Path(companyId)}/{exportId}/rows?offset={offset}&take={take}", 1024 * 1024, ct);
    public Task<CustomerHistoryExportV1> CustomerHistory(Guid company, Guid customer, Guid export, CancellationToken ct)
        => Get<CustomerHistoryExportV1>($"/internal/v1/reporting-source/companies/{company}/customers/{customer}/exports/{export}",
            CustomerHistoryV1.MaxBytes + 32768, ct);
    private static string Path(Guid company) => $"/internal/v1/reporting-source/companies/{company}/exports";
    private async Task<T> Get<T>(string path, int limit, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(_origin, path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        try
        {
            using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (response.StatusCode != System.Net.HttpStatusCode.OK || !string.Equals(response.Content.Headers.ContentType?.MediaType, "application/json", StringComparison.OrdinalIgnoreCase)
                || response.Content.Headers.ContentLength > limit) throw Unavailable();
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var bytes = new MemoryStream(); var buffer = new byte[4096];
            while (true)
            {
                var count = await stream.ReadAsync(buffer, ct); if (count == 0) break;
                if (bytes.Length + count > limit) throw Unavailable(); bytes.Write(buffer, 0, count);
            }
            return JsonSerializer.Deserialize<T>(bytes.ToArray(), ReportingSourceV1.Json) ?? throw Unavailable();
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException || ex is OperationCanceledException && !ct.IsCancellationRequested)
        { throw Unavailable(); }
    }
    private static DomainException Unavailable() => new("reporting_source_unavailable", "The financial source export is unavailable or invalid.", 503);
    public void Dispose() => _client.Dispose();
}
