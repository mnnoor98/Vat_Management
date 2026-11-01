using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Vat.Contracts.Manufacturing;
using Vat.Services.Manufacturing;
using Vat.Services.Manufacturing.Domain;
using Xunit;
namespace Vat.Manufacturing.Tests;
public sealed class ProductionOrderWorkflowTests(ManufacturingApiFactory factory):IClassFixture<ManufacturingApiFactory>
{
    private static Task<HttpResponseMessage> Send(HttpClient c,string path,object body,Guid? key=null,bool put=false){var r=new HttpRequestMessage(put?HttpMethod.Put:HttpMethod.Post,path){Content=JsonContent.Create(body)};r.Headers.Add("Idempotency-Key",(key??Guid.NewGuid()).ToString());return c.SendAsync(r);}
    private async Task<(Guid Tenant,Guid Company,Guid Coefficient,Guid Warehouse,HttpClient Maker,HttpClient Checker)> Setup()
    {
        var t=Guid.NewGuid();var c=Guid.NewGuid();var f=Guid.NewGuid();var a=Guid.NewGuid();var b=Guid.NewGuid();var w=Guid.NewGuid();
        factory.References.Products[(t,c,f)]=new(f,"FG","Finished","PCS","TEST");factory.References.Products[(t,c,a)]=new(a,"RAW-A","Raw A","KG","TEST");factory.References.Products[(t,c,b)]=new(b,"RAW-B","Raw B","KG","TEST");factory.References.Warehouses[(t,c,w)]=new(w,"Production output");
        var maker=factory.Client(t,c);var checker=factory.Client(t,c,"checker");var root=$"/api/v1/companies/{c}/manufacturing/coefficients";
        var created=await Send(maker,root,new CoefficientCreateRequest("BOM",f,10,new(2026,1,1),new(2027,1,1),900,800,700,[new(a,20,5,999),new(b,5,0,888)]));created.EnsureSuccessStatusCode();
        var coefficient=(await created.Content.ReadFromJsonAsync<CoefficientResult>())!;
        (await Send(maker,$"{root}/{coefficient.Id}/submit",new CoefficientActionRequest(1))).EnsureSuccessStatusCode();(await Send(checker,$"{root}/{coefficient.Id}/approve",new CoefficientActionRequest(2),put:true)).EnsureSuccessStatusCode();
        return(t,c,coefficient.Id,w,maker,checker);
    }
    [Fact]
    public async Task Approved_order_exports_scoped_frozen_cost_free_execution_source()
    {
        var s=await Setup();var root=$"/api/v1/companies/{s.Company}/manufacturing/production-orders";var request=new CreateProductionOrderRequest("PO-1",s.Coefficient,s.Warehouse,new(2026,10,31),"10.000001");var key=Guid.NewGuid();
        var response=await Send(s.Maker,root,request,key);Assert.Equal(HttpStatusCode.Created,response.StatusCode);var order=(await response.Content.ReadFromJsonAsync<ProductionOrderView>())!;
        Assert.Equal("10.000001",order.PlannedQuantityExact);Assert.Equal("Draft",order.State);Assert.Equal("true",(await Send(s.Maker,root,request,key)).Headers.GetValues("Idempotency-Replayed").Single());
        var path=$"{root}/{order.Id}";Assert.Equal(HttpStatusCode.Conflict,(await s.Maker.GetAsync(path+"/execution-source")).StatusCode);
        (await Send(s.Maker,path+"/submit",new CoefficientActionRequest(1))).EnsureSuccessStatusCode();Assert.Equal(HttpStatusCode.Forbidden,(await Send(s.Maker,path+"/approve",new CoefficientActionRequest(2))).StatusCode);
        (await Send(s.Checker,path+"/approve",new CoefficientActionRequest(2))).EnsureSuccessStatusCode();var exported=await s.Maker.GetAsync(path+"/execution-source");exported.EnsureSuccessStatusCode();
        var json=await exported.Content.ReadAsStringAsync();Assert.DoesNotContain("unitPrice",json);Assert.DoesNotContain("totalCost",json);Assert.DoesNotContain("directLaborCost",json);
        var source=(await exported.Content.ReadFromJsonAsync<ProductionOrderSource>())!;Assert.Equal(s.Tenant,source.TenantId);Assert.Equal(s.Company,source.CompanyId);Assert.Equal(3,source.Version);Assert.Equal(2,source.Evidence.Materials.Count);Assert.Equal(64,source.EvidenceSha256.Length);
        Assert.Equal(HttpStatusCode.Conflict,(await Send(s.Maker,path+"/cancel",new CoefficientActionRequest(3,"Cannot cancel accepted approval"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await factory.Client(Guid.NewGuid(),s.Company).GetAsync(path+"/execution-source")).StatusCode);
        await factory.Inspect(async db=>{Assert.Equal(3,await db.Set<ProductionOrderAudit>().CountAsync(x=>x.OrderId==order.Id));await Assert.ThrowsAsync<PostgresException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"UPDATE manufacturing.production_orders SET \"PlannedQuantity\"=20 WHERE \"TenantId\"={s.Tenant} AND \"Id\"={order.Id}"));await Assert.ThrowsAsync<PostgresException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM manufacturing.production_order_documents WHERE \"TenantId\"={s.Tenant} AND \"Id\"={order.Id}"));return 0;});
    }
    [Fact]
    public async Task Order_approval_freezes_reviewed_product_unit_ratios_and_rejects_invalid_aliases()
    {
        var s=await Setup();var root=$"/api/v1/companies/{s.Company}/manufacturing/production-orders";
        var coefficient=(await s.Maker.GetFromJsonAsync<CoefficientResult[]>($"/api/v1/companies/{s.Company}/manufacturing/coefficients"))!.Single(x=>x.Id==s.Coefficient);
        var raw=coefficient.Snapshot!.Materials[0].Product.Id;var finished=coefficient.Snapshot.FinishedProduct.Id;
        var request=new CreateProductionOrderRequest("UNIT-ORDER",s.Coefficient,s.Warehouse,new(2026,10,31),"24",[new(raw," bag ","25.000"),new(finished,"DOZEN","12")]);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,(await Send(s.Maker,root,request with{UomConversions=[new(raw,"KG","2")]})).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,(await Send(s.Maker,root,request with{UomConversions=[new(raw,"BAG","2"),new(raw,"bag","3")]})).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,(await Send(s.Maker,root,request with{UomConversions=[new(Guid.NewGuid(),"BAG","2")]})).StatusCode);
        var key=Guid.NewGuid();var created=await Send(s.Maker,root,request,key);created.EnsureSuccessStatusCode();var order=(await created.Content.ReadFromJsonAsync<ProductionOrderView>())!;
        Assert.Equal("25",order.Evidence.UomConversions!.Single(x=>x.ProductId==raw).StockUnitsPerInputUnit);Assert.Equal("BAG",order.Evidence.UomConversions!.Single(x=>x.ProductId==raw).InputUnitOfMeasure);
        Assert.Equal(HttpStatusCode.Conflict,(await Send(s.Maker,root,request with{UomConversions=[new(raw,"BAG","30")]},key)).StatusCode);
        (await Send(s.Maker,$"{root}/{order.Id}/submit",new CoefficientActionRequest(1))).EnsureSuccessStatusCode();(await Send(s.Checker,$"{root}/{order.Id}/approve",new CoefficientActionRequest(2))).EnsureSuccessStatusCode();
        var source=(await s.Maker.GetFromJsonAsync<ProductionOrderSource>($"{root}/{order.Id}/execution-source"))!;Assert.Equal(order.Evidence.UomConversions,source.Evidence.UomConversions);
        await factory.Inspect(async db=>{Assert.Equal(1,await db.Orders.CountAsync(x=>x.TenantId==s.Tenant));await Assert.ThrowsAsync<PostgresException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"UPDATE manufacturing.production_order_documents SET \"Json\"='{{}}' WHERE \"Id\"={order.Id}"));return 0;});
    }
    [Fact]
    public async Task Order_approval_freezes_independently_referenced_scrap_and_byproducts()
    {
        var s=await Setup();var scrap=Guid.NewGuid();var byproduct=Guid.NewGuid();factory.References.Products[(s.Tenant,s.Company,scrap)]=new(scrap,"SCRAP","Reusable scrap","KG","TEST");factory.References.Products[(s.Tenant,s.Company,byproduct)]=new(byproduct,"BYPRODUCT","Secondary output","LTR","TEST");
        var root=$"/api/v1/companies/{s.Company}/manufacturing/production-orders";var request=new CreateProductionOrderRequest("OUTPUT-ORDER",s.Coefficient,s.Warehouse,new(2026,10,31),"10",[new(scrap,"BAG","25")],[new(byproduct,"ByProduct"),new(scrap,"Scrap")]);
        var created=await Send(s.Maker,root,request);created.EnsureSuccessStatusCode();var order=(await created.Content.ReadFromJsonAsync<ProductionOrderView>())!;var outputs=order.Evidence.ByProducts!;Assert.Equal(new[]{scrap,byproduct}.OrderBy(x=>x),outputs.Select(x=>x.Product.Id).OrderBy(x=>x));Assert.Equal("Scrap",outputs.Single(x=>x.Product.Id==scrap).Classification);Assert.Equal("ByProduct",outputs.Single(x=>x.Product.Id==byproduct).Classification);Assert.Equal("BAG",Assert.Single(order.Evidence.UomConversions!).InputUnitOfMeasure);
        (await Send(s.Maker,$"{root}/{order.Id}/submit",new CoefficientActionRequest(1))).EnsureSuccessStatusCode();(await Send(s.Checker,$"{root}/{order.Id}/approve",new CoefficientActionRequest(2))).EnsureSuccessStatusCode();var source=(await s.Maker.GetFromJsonAsync<ProductionOrderSource>($"{root}/{order.Id}/execution-source"))!;Assert.Equal(2,source.Evidence.ByProducts!.Count);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,(await Send(s.Maker,root,new CreateProductionOrderRequest("DUP-OUTPUT",s.Coefficient,s.Warehouse,new(2026,10,31),"10",ByProducts:[new(scrap,"Scrap"),new(scrap,"ByProduct")]))).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,(await Send(s.Maker,root,new CreateProductionOrderRequest("BAD-CLASS",s.Coefficient,s.Warehouse,new(2026,10,31),"10",ByProducts:[new(byproduct,"Waste")]))).StatusCode);
    }
    [Fact]
    public async Task Missing_scoped_warehouse_and_invalid_plan_write_no_order_evidence()
    {
        var s=await Setup();var root=$"/api/v1/companies/{s.Company}/manufacturing/production-orders";var request=new CreateProductionOrderRequest("PO",s.Coefficient,Guid.NewGuid(),new(2026,10,31),"1.000000");
        Assert.Equal(HttpStatusCode.NotFound,(await Send(s.Maker,root,request)).StatusCode);Assert.Equal(HttpStatusCode.UnprocessableEntity,(await Send(s.Maker,root,request with{PlannedQuantity="1.0000001"})).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await Send(factory.Client(s.Tenant,s.Company,permissions:[]),root,request)).StatusCode);
        await factory.Inspect(async db=>{Assert.False(await db.Orders.AnyAsync(x=>x.TenantId==s.Tenant));Assert.False(await db.Set<ProductionOrderDocument>().AnyAsync(x=>x.TenantId==s.Tenant));return 0;});
    }
}
