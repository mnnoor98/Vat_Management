using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Vat.Application;
using Vat.Application.Security;
using Vat.Contracts.Manufacturing;
using Vat.Domain;
namespace Vat.Infrastructure.Persistence;
public sealed class ProductionExecutionReferenceClient(HttpClient client,IConfiguration configuration):IProductionExecutionReferences
{
    public async Task<ProductionOrderSource> Order(Actor actor,Guid company,Guid order,string bearer,CancellationToken ct)
    {
        actor.RequireCompany(company);
        if(!Uri.TryCreate(configuration["ProductionReferences:ManufacturingBaseUrl"],UriKind.Absolute,out var root)||root.UserInfo.Length!=0||root.Query.Length!=0||root.Fragment.Length!=0||root.AbsolutePath!="/"||
            (root.Scheme!="https"&&!(root.Scheme=="http"&&root.IsLoopback&&bool.TryParse(configuration["ProductionReferences:AllowLoopbackHttp"],out var allow)&&allow)))throw new DomainException("reference_service_unavailable","Configure the trusted Manufacturing endpoint.",503);
        if(!bearer.StartsWith("Bearer ",StringComparison.OrdinalIgnoreCase))throw new DomainException("reference_access_denied","Delegated bearer required.",403);
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(10));var token=timeout.Token;
        using var request=new HttpRequestMessage(HttpMethod.Get,new Uri(root!,$"api/v1/companies/{company}/manufacturing/production-orders/{order}/execution-source"));request.Headers.Authorization=AuthenticationHeaderValue.Parse(bearer);
        try{
            using var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token);
            if(response.StatusCode==HttpStatusCode.NotFound)throw new DomainException("not_found","Production order not found in granted company.",404);
            if(response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)throw new DomainException("reference_access_denied","Manufacturing read permission and valid delegated token required.",403);
            if(response.StatusCode==HttpStatusCode.Conflict)throw new DomainException("production_order_not_approved","Require a verified independently approved Manufacturing order.",409);
            if(!response.IsSuccessStatusCode)throw new DomainException("reference_service_unavailable","Manufacturing source is unavailable.",503);
            await response.Content.LoadIntoBufferAsync(262144,token);var source=JsonSerializer.Deserialize<ProductionOrderSource>(await response.Content.ReadAsStringAsync(token),new JsonSerializerOptions(JsonSerializerDefaults.Web));
            Validate(source,actor,company,order);return source!;
        }catch(Exception e)when(e is HttpRequestException or JsonException||e is OperationCanceledException&&!ct.IsCancellationRequested){throw new DomainException("reference_service_unavailable","Manufacturing source could not be verified.",503);}
    }
    public static void Validate(ProductionOrderSource? s,Actor actor,Guid company,Guid order)
    {
        var e=s?.Evidence;
        if(s is null||s.TenantId!=actor.TenantId||s.CompanyId!=company||s.OrderId!=order||s.State!="Approved"||s.Version!=3||string.IsNullOrWhiteSpace(s.CreatedBy)||string.IsNullOrWhiteSpace(s.ApprovedBy)||s.CreatedBy==s.ApprovedBy||
            !Regex.IsMatch(s.EvidenceSha256??"","^[0-9A-F]{64}$")||e is null||e.WarehouseId==Guid.Empty||e.CoefficientId==Guid.Empty||e.BusinessDate==default||e.BatchQuantity<=0||e.PlannedQuantity<=0||e.PlannedQuantity>=1000000000000m||
            e.FinishedProduct is null||e.FinishedProduct.Id==Guid.Empty||string.IsNullOrWhiteSpace(e.FinishedProduct.UnitOfMeasure)||e.Materials is null||e.Materials.Count is <1 or >100||e.Materials.Select(x=>x?.Product?.Id).Distinct().Count()!=e.Materials.Count||
            e.Materials.Any(x=>x is null||x.Product is null||x.Product.Id==Guid.Empty||string.IsNullOrWhiteSpace(x.Product.UnitOfMeasure)||x.Product.Id==e.FinishedProduct.Id||x.QuantityPerBatch<=0||x.WastagePercent<0||x.WastagePercent>100)||
            e.ByProducts is {Count:>20}||e.ByProducts?.Any(x=>x is null||x.Product is null||x.Product.Id==Guid.Empty||string.IsNullOrWhiteSpace(x.Product.Reference)||string.IsNullOrWhiteSpace(x.Product.UnitOfMeasure)||x.Classification is not("ByProduct" or "Scrap"))==true||
            e.ByProducts?.Select(x=>x.Product.Id).Distinct().Count()!=e.ByProducts?.Count||e.ByProducts?.Any(x=>x.Product.Id==e.FinishedProduct.Id||e.Materials.Any(m=>m.Product.Id==x.Product.Id))==true||
            e.ByProducts is not null&&!e.ByProducts.OrderBy(x=>x.Product.Id).SequenceEqual(e.ByProducts))throw new DomainException("reference_response_invalid","Manufacturing returned invalid or mismatched execution evidence.",503);
        var digest=Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(e,new JsonSerializerOptions(JsonSerializerDefaults.Web))));
        if(digest!=s.EvidenceSha256)throw new DomainException("reference_response_invalid","Manufacturing evidence digest does not match its payload.",503);
        try{var normalized=ProductionUnits.Freeze(e,e.UomConversions);if(e.UomConversions is not null&&(normalized is null||!normalized.SequenceEqual(e.UomConversions)))throw new DomainException("validation_failed","Noncanonical conversion evidence.");}
        catch(DomainException){throw new DomainException("reference_response_invalid","Manufacturing returned invalid or noncanonical unit conversion evidence.",503);}
    }
}
