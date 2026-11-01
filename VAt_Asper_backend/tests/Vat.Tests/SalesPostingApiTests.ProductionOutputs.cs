using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Vat.Application;
using Vat.Application.Contracts;
using Vat.Contracts.Manufacturing;
using Vat.Infrastructure.Persistence;
using Xunit;
namespace Vat.Tests;
public sealed partial class SalesPostingApiTests
{
    [Theory]
    [InlineData("FIFO")]
    [InlineData("WEIGHTED_AVERAGE")]
    public async Task Approved_byproduct_creates_valued_stock_and_full_reversal_restores_wip(string method)
    {
        var s=await Setup();await ActivateCosting(s,method);
        var finished=await Created<ProductResponse>(s.Maker,$"{s.Root}/products",new CreateProductRequest("FG-OUT","Finished","PCS","TEST-DOMESTIC"));
        var scrap=await Created<ProductResponse>(s.Maker,$"{s.Root}/products",new CreateProductRequest("SCRAP-OUT","Reusable scrap","KG","TEST-DOMESTIC"));
        var output=await Created<WarehouseResponse>(s.Maker,$"{s.Root}/warehouses",new CreateWarehouseRequest(s.Branch,"PROD-OUT","Production output"));
        var evidence=new ProductionOrderEvidence(Guid.NewGuid(),1,3,new string('B',64),output.Id,"Production output",new(2026,10,23),new(finished.Id,"FG-OUT","PCS"),1,1,[new(new(s.Product,"ITEM","KG"),1,0)],
            [new(scrap.Id,"BAG","2")],[new(new(scrap.Id,"SCRAP-OUT","KG"),"Scrap")]);
        var source=new ProductionOrderSource(s.Tenant,s.Company,Guid.NewGuid(),3,"Approved","OUTPUT-1","owner-maker","owner-checker",Digest(evidence),evidence);
        ProductionSessionView head;using(var host=factory.WithWebHostBuilder(b=>b.ConfigureTestServices(x=>x.AddSingleton<IProductionExecutionReferences>(new ApprovedProduction(source))))){using var client=host.CreateClient();client.DefaultRequestHeaders.Authorization=s.Maker.DefaultRequestHeaders.Authorization;head=await Created<ProductionSessionView>(client,$"{s.Root}/inventory/production-executions",new ImportProductionRequest(source.OrderId));}
        var conversion=await ProductionDraft(s,head.Id,"Conversion",labor:"100");await PostProduction(s,head.Id,conversion);
        Assert.Equal(HttpStatusCode.NotFound,(await Post(s.Maker,$"{s.Root}/inventory/production-executions/{head.Id}/operations",new CreateProductionOperationRequest(2,"ByProduct",new(2026,10,24),"Unknown output",[],"30","1",OutputProductId:Guid.NewGuid(),OutputWarehouseId:output.Id))).StatusCode);
        var byproduct=await ProductionDraft(s,head.Id,"ByProduct",amount:"30",quantity:"2",outputUnit:"BAG",outputProduct:scrap.Id,outputWarehouse:output.Id);Assert.Equal("4.000000",byproduct.OutputQuantity);Assert.Equal("2.000000",byproduct.OutputInputQuantity);await PostProduction(s,head.Id,byproduct);
        Assert.Equal("70.0000",(await s.Maker.GetFromJsonAsync<ProductionSessionView>($"{s.Root}/inventory/production-executions/{head.Id}"))!.WipValue);
        var summary=(await s.Maker.GetFromJsonAsync<InventoryValuationSummary>($"{s.Root}/inventory/valuation-summary"))!;Assert.Equal(30m,summary.TopValuedItems.Single(x=>x.ProductId==scrap.Id).TotalValuation);
        await factory.Inspect(async db=>{var movement=await db.Set<ProductionStockMovement>().SingleAsync(x=>x.OperationId==byproduct.Id);Assert.Equal("Receipt",movement.Kind);Assert.Equal(4,movement.Quantity);Assert.Equal(30,movement.Value);await Assert.ThrowsAsync<PostgresException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"UPDATE inventory.production_cost_operations SET \"OutputProductId\"={finished.Id} WHERE \"Id\"={byproduct.Id}"));return 0;});
        var reverse=await ProductionDraft(s,head.Id,"Reverse",reverses:byproduct.Id);await PostProduction(s,head.Id,reverse);Assert.Equal("100.0000",(await s.Maker.GetFromJsonAsync<ProductionSessionView>($"{s.Root}/inventory/production-executions/{head.Id}"))!.WipValue);
        var replacement=await ProductionDraft(s,head.Id,"ByProduct",amount:"30",quantity:"2",outputUnit:"BAG",outputProduct:scrap.Id,outputWarehouse:output.Id);await PostProduction(s,head.Id,replacement);
        var receipt=await ProductionDraft(s,head.Id,"Receipt",amount:"70",quantity:"1");await PostProduction(s,head.Id,receipt);var close=await ProductionDraft(s,head.Id,"Close");await PostProduction(s,head.Id,close);
        head=(await s.Maker.GetFromJsonAsync<ProductionSessionView>($"{s.Root}/inventory/production-executions/{head.Id}"))!;Assert.Equal("Closed",head.State);Assert.Equal("0.0000",head.WipValue);Assert.Equal("1.000000",head.ReceivedQuantity);
        summary=(await s.Maker.GetFromJsonAsync<InventoryValuationSummary>($"{s.Root}/inventory/valuation-summary"))!;Assert.Equal(100m,summary.TotalStockValuation);Assert.Equal(30m,summary.TopValuedItems.Single(x=>x.ProductId==scrap.Id).TotalValuation);Assert.Equal(70m,summary.TopValuedItems.Single(x=>x.ProductId==finished.Id).TotalValuation);
        await factory.Inspect(async db=>{Assert.Equal(2,await db.Set<ProductionStockMovement>().CountAsync(x=>x.SessionId==head.Id&&x.ProductId==scrap.Id&&x.Quantity>0));Assert.Equal(1,await db.Set<ProductionStockMovement>().CountAsync(x=>x.SessionId==head.Id&&x.ProductId==scrap.Id&&x.Quantity<0));Assert.True(await new PeriodStore(db).BalancesReconcile(s.Tenant,s.Company,default));return 0;});
    }
    private static string Digest(ProductionOrderEvidence evidence)=>Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(evidence,new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))));
}
