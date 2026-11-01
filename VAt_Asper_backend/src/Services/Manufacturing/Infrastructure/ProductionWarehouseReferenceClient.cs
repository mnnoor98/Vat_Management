using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Vat.Application.Security;
using Vat.Domain;
namespace Vat.Services.Manufacturing.Infrastructure;
public sealed class ProductionWarehouseReferenceClient(HttpClient client,IConfiguration configuration):IProductionWarehouseReferences
{
    public async Task<ProductionWarehouseReference> Warehouse(Actor actor,Guid company,Guid warehouse,string bearer,CancellationToken ct)
    {
        actor.RequireCompany(company);
        if(!Uri.TryCreate(configuration["ManufacturingReferences:InventoryBaseUrl"],UriKind.Absolute,out var root)||root.UserInfo.Length!=0||root.Query.Length!=0||root.Fragment.Length!=0||root.AbsolutePath!="/"||
            (root.Scheme!="https"&&!(root.Scheme=="http"&&root.IsLoopback&&configuration.GetValue<bool>("ManufacturingReferences:AllowLoopbackHttp"))))throw new DomainException("reference_service_unavailable","Configure the trusted Inventory endpoint.",503);
        if(!bearer.StartsWith("Bearer ",StringComparison.OrdinalIgnoreCase))throw new DomainException("reference_access_denied","Delegated bearer authentication required.",403);
        using var request=new HttpRequestMessage(HttpMethod.Get,new Uri(root,$"api/v1/companies/{company}/warehouses/{warehouse}"));request.Headers.Authorization=AuthenticationHeaderValue.Parse(bearer);
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(10));var token=timeout.Token;
        try{
            using var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token);
            if(response.StatusCode==HttpStatusCode.NotFound)throw new DomainException("not_found","Warehouse not found in this company.",404);
            if(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)throw new DomainException("reference_access_denied","Inventory read permission and a valid delegated token required.",403);
            if(!response.IsSuccessStatusCode)throw new DomainException("reference_service_unavailable","Warehouse validation failed.",503);
            await response.Content.LoadIntoBufferAsync(16384,token);var value=JsonSerializer.Deserialize<ProductionWarehouseReference>(await response.Content.ReadAsStringAsync(token),new JsonSerializerOptions(JsonSerializerDefaults.Web));
            if(value?.Id!=warehouse||string.IsNullOrWhiteSpace(value.Name)||value.Name.Length>200)throw new DomainException("reference_response_invalid","Invalid warehouse reference.",503);
            return value;
        }catch(Exception e)when(e is HttpRequestException or JsonException||e is OperationCanceledException&&!ct.IsCancellationRequested){throw new DomainException("reference_service_unavailable","Warehouse validation could not complete.",503);}
    }
}
