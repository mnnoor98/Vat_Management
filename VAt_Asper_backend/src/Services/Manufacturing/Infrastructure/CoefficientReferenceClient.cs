using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Vat.Application.Security;
using Vat.Domain;

namespace Vat.Services.Manufacturing.Infrastructure;

// Delegated bearer authentication preserves the caller's tenant, company and read permission.
// No financial connection string or client-supplied display fields are trusted here.
public sealed class CoefficientReferenceClient(HttpClient client, IConfiguration configuration) : ICoefficientReferences
{
    public async Task<ProductReference> Product(Actor actor, Guid company, Guid product, string bearerToken, CancellationToken ct)
    {
        actor.RequireCompany(company);
        if (!Uri.TryCreate(configuration["ManufacturingReferences:MasterDataBaseUrl"], UriKind.Absolute, out var root) ||
            root.UserInfo.Length != 0 || root.Query.Length != 0 || root.Fragment.Length != 0 || root.AbsolutePath != "/" ||
            (root.Scheme != "https" && !(root.Scheme == "http" && root.IsLoopback && configuration.GetValue<bool>("ManufacturingReferences:AllowLoopbackHttp"))))
            throw new DomainException("reference_service_unavailable", "Configure a trusted MasterData service endpoint before creating coefficients.", 503);
        if (!bearerToken.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            throw new DomainException("reference_authorization_required", "Delegated bearer authentication is required.", 403);
        using var message = new HttpRequestMessage(HttpMethod.Get, new Uri(root, $"api/v1/companies/{company}/products/{product}"));
        message.Headers.Authorization = AuthenticationHeaderValue.Parse(bearerToken);
        try
        {
            using var response = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, ct);
            if (response.StatusCode == HttpStatusCode.NotFound) throw new DomainException("product_not_found", "Product was not found in this company.", 404);
            if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
                throw new DomainException("reference_access_denied", "MasterData read permission and a valid delegated token are required.", 403);
            if (!response.IsSuccessStatusCode) throw new DomainException("reference_service_unavailable", "MasterData validation could not be completed.", 503);
            await response.Content.LoadIntoBufferAsync(16384, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            var value = JsonSerializer.Deserialize<ProductReference>(body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            if (value is null || value.Id != product || string.IsNullOrWhiteSpace(value.Description) || value.Description.Length > 500 ||
                string.IsNullOrWhiteSpace(value.Reference) || value.Reference.Length > 100 || string.IsNullOrWhiteSpace(value.UnitOfMeasure) || value.UnitOfMeasure.Length > 40)
                throw new DomainException("reference_response_invalid", "MasterData returned an invalid product reference.", 503);
            return value;
        }
        catch (Exception error) when (error is HttpRequestException or JsonException || error is OperationCanceledException && !ct.IsCancellationRequested)
        { throw new DomainException("reference_service_unavailable", "MasterData validation could not be completed.", 503); }
    }
}
