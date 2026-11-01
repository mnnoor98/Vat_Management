using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Vat.Application;
using Vat.Application.Contracts;
using Vat.Application.Security;
using Vat.Contracts.Manufacturing;
using Vat.Domain.Purchasing;
using Vat.Infrastructure.Persistence;
using Xunit;
namespace Vat.Tests;
public sealed partial class SalesPostingApiTests
{
    private sealed class ApprovedProduction(ProductionOrderSource source):IProductionExecutionReferences
    {public int Calls;public Task<ProductionOrderSource> Order(Actor actor,Guid company,Guid order,string bearer,CancellationToken ct){Calls++;return Task.FromResult(source);}}
    private sealed class FailProductionCommit:SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,InterceptionResult<int> result,CancellationToken ct=default){if(data.Context!.ChangeTracker.Entries<ProductionStockMovement>().Any(x=>x.State==EntityState.Added))throw new InvalidOperationException("Injected production commit failure after physical SQL.");return ValueTask.FromResult(result);}
    }
    private static async Task<ProductionOperationView> ProductionDraft(SetupData s,Guid session,string kind,IReadOnlyList<ProductionLineInput>? lines=null,string amount="0",string quantity="0",string labor="0",string utility="0",string overhead="0",Guid? reverses=null,DateOnly? date=null,string? outputUnit=null,Guid? outputProduct=null,Guid? outputWarehouse=null)
    {
        var h=(await s.Maker.GetFromJsonAsync<ProductionSessionView>($"{s.Root}/inventory/production-executions/{session}"))!;
        return await Created<ProductionOperationView>(s.Maker,$"{s.Root}/inventory/production-executions/{session}/operations",new CreateProductionOperationRequest(h.Version,kind,date??new(2026,10,24),"Reviewed actual production and charge evidence",lines??[],amount,quantity,labor,utility,overhead,reverses,outputUnit,outputProduct,outputWarehouse));
    }
    private static async Task PostProduction(SetupData s,Guid session,ProductionOperationView op)
    {var h=(await s.Maker.GetFromJsonAsync<ProductionSessionView>($"{s.Root}/inventory/production-executions/{session}"))!;var response=await Post(s.Checker,$"{s.Root}/inventory/production-executions/{session}/operations/{op.Id}/approve",new ApproveProductionOperationRequest(1,h.Version));Assert.True(response.IsSuccessStatusCode,await response.Content.ReadAsStringAsync());}
    [Theory]
    [InlineData("FIFO",false)]
    [InlineData("WEIGHTED_AVERAGE",false)]
    [InlineData("FIFO",true)]
    [InlineData("WEIGHTED_AVERAGE",true)]
    public async Task Production_actual_material_wip_partial_receipts_returns_loss_and_close_conserve_cost(string method,bool useUnits)
    {
        var s=await Setup();await ActivateCosting(s,method);
        var second=await Created<ProductResponse>(s.Maker,$"{s.Root}/products",new CreateProductRequest("RAW-B","Raw B","KG","TEST-DOMESTIC"));
        var finished=await Created<ProductResponse>(s.Maker,$"{s.Root}/products",new CreateProductRequest("FINISHED","Finished goods","PCS","TEST-DOMESTIC"));
        var output=await Created<WarehouseResponse>(s.Maker,$"{s.Root}/warehouses",new CreateWarehouseRequest(s.Branch,"OUTPUT","Finished output"));
        foreach(var line in new[]{new PurchaseLineInput("ITEM","Raw A",10,100),new PurchaseLineInput("RAW-B","Raw B",20,50)}){
            var p=await Approve(s,lines:[line]);var plan=await Prepare(s,p.Id);await ApproveLanded(s,plan,"55.0000");Assert.Equal(HttpStatusCode.OK,(await Post(s.Checker,$"{s.Purchase(p.Id)}/posting-plans/{plan.Id}/post",new TransitionRequest(4))).StatusCode);
        }
        var source=new ProductionOrderSource(s.Tenant,s.Company,Guid.NewGuid(),3,"Approved","PRODUCTION-1","owner-maker","owner-checker",new string('A',64),
            new(Guid.NewGuid(),1,3,new string('B',64),output.Id,"Finished output",new(2026,10,23),new(finished.Id,"FINISHED","PCS"),10,10,[new(new(s.Product,"ITEM","KG"),8,0),new(new(second.Id,"RAW-B","KG"),10,0)]));
        if(useUnits)source=source with{Evidence=source.Evidence with{UomConversions=ProductionUnits.Freeze(source.Evidence,[new(s.Product,"PACK","2"),new(s.Product,"G","0.001"),new(second.Id,"BOX","5"),new(finished.Id,"PAIR","2")])}};
        source=source with{EvidenceSha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(source.Evidence,new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))))};
        var references=new ApprovedProduction(source);ProductionSessionView head;var importKey=Guid.NewGuid().ToString();
        using(var host=factory.WithWebHostBuilder(b=>b.ConfigureTestServices(services=>services.AddSingleton<IProductionExecutionReferences>(references)))){
            using var client=host.CreateClient();client.DefaultRequestHeaders.Authorization=s.Maker.DefaultRequestHeaders.Authorization;
            var imported=await Post(client,$"{s.Root}/inventory/production-executions",new ImportProductionRequest(source.OrderId),importKey);Assert.Equal(HttpStatusCode.Created,imported.StatusCode);head=(await imported.Content.ReadFromJsonAsync<ProductionSessionView>())!;
            Assert.Equal(HttpStatusCode.Created,(await Post(client,$"{s.Root}/inventory/production-executions",new ImportProductionRequest(source.OrderId),importKey)).StatusCode);Assert.Equal(1,references.Calls);
        }
        var root=$"{s.Root}/inventory/production-executions/{head.Id}";
        if(useUnits){
            Assert.Equal(HttpStatusCode.Conflict,(await Post(s.Maker,$"{root}/operations",new CreateProductionOperationRequest(1,"Issue",new(2026,10,24),"Unapproved input unit",[new(s.Warehouse,s.Product,"1",UnitOfMeasure:"UNKNOWN")]))).StatusCode);
            Assert.Equal(HttpStatusCode.UnprocessableEntity,(await Post(s.Maker,$"{root}/operations",new CreateProductionOperationRequest(1,"Issue",new(2026,10,24),"No stock rounding",[new(s.Warehouse,s.Product,"0.000001",UnitOfMeasure:"G")]))).StatusCode);
            await factory.Inspect(async db=>{db.Add(new ProductionCostOperation{TenantId=s.Tenant,CompanyId=s.Company,Id=Guid.NewGuid(),SessionId=head.Id,SessionVersion=1,Kind="Issue",BusinessDate=new(2026,10,24),Evidence="Tampered unit proof",CreatedBy="tamper",CreatedAt=DateTimeOffset.UtcNow,Lines=System.Text.Json.JsonSerializer.Serialize(new[]{new{id=Guid.NewGuid(),warehouseId=s.Warehouse,productId=s.Product,quantity=8m,inputQuantity=5m,inputUnitOfMeasure="PACK"}})});await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync());return 0;});
        }
        var issue=await ProductionDraft(s,head.Id,"Issue",[new(s.Warehouse,s.Product,useUnits?"4":"8.000000",UnitOfMeasure:useUnits?"PACK":null),new(s.Warehouse,second.Id,useUnits?"2":"10.000000",UnitOfMeasure:useUnits?"BOX":null)]);
        if(useUnits){var line=issue.Lines.Single(x=>x.ProductId==s.Product);Assert.Equal("8.000000",line.Quantity);Assert.Equal("4.000000",line.InputQuantity);Assert.Equal("PACK",line.UnitOfMeasure);}
        Assert.Equal(HttpStatusCode.Forbidden,(await Post(s.Maker,$"{root}/operations/{issue.Id}/approve",new ApproveProductionOperationRequest(1,1))).StatusCode);
        var postKey=Guid.NewGuid().ToString();
        using(var host=factory.WithWebHostBuilder(b=>b.ConfigureTestServices(services=>services.AddDbContext<VatDbContext>(o=>o.AddInterceptors(new FailProductionCommit()))))){using var client=host.CreateClient();client.DefaultRequestHeaders.Authorization=s.Checker.DefaultRequestHeaders.Authorization;Assert.Equal(HttpStatusCode.InternalServerError,(await Post(client,$"{root}/operations/{issue.Id}/approve",new ApproveProductionOperationRequest(1,1),postKey)).StatusCode);}
        Assert.Equal("0.0000",(await s.Maker.GetFromJsonAsync<ProductionSessionView>(root))!.WipValue);Assert.Equal("2200.0000",(await s.Maker.GetFromJsonAsync<CostLedgerStatus>($"{s.Root}/inventory/cost-ledger/status"))!.StockValue);
        var posted=await Post(s.Checker,$"{root}/operations/{issue.Id}/approve",new ApproveProductionOperationRequest(1,1),postKey);Assert.True(posted.IsSuccessStatusCode,await posted.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK,(await Post(s.Checker,$"{root}/operations/{issue.Id}/approve",new ApproveProductionOperationRequest(1,1),postKey)).StatusCode);
        Assert.Equal("1430.0000",(await s.Maker.GetFromJsonAsync<ProductionSessionView>(root))!.WipValue);
        var returned=await ProductionDraft(s,head.Id,"Return",[new(s.Warehouse,s.Product,useUnits?"2000":"2.000000",issue.Lines.Single(x=>x.ProductId==s.Product).Id,UnitOfMeasure:useUnits?"G":null)]);await PostProduction(s,head.Id,returned);
        var conversion=await ProductionDraft(s,head.Id,"Conversion",labor:"100.0000",utility:"20.0000",overhead:"30.0000");
        var stale=await ProductionDraft(s,head.Id,"Receipt",amount:"500.0000",quantity:"4.000000");await PostProduction(s,head.Id,conversion);
        Assert.Equal(HttpStatusCode.Conflict,(await Post(s.Checker,$"{root}/operations/{stale.Id}/approve",new ApproveProductionOperationRequest(1,4))).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await Post(s.Maker,$"{root}/operations/{stale.Id}/cancel",new CancelProductionOperationRequest(1,"Replace stale WIP review"))).StatusCode);
        var partial=await ProductionDraft(s,head.Id,"Receipt",amount:"500.0000",quantity:useUnits?"2":"4.000000",outputUnit:useUnits?"PAIR":null);await PostProduction(s,head.Id,partial);
        Assert.Equal("860.0000",(await s.Maker.GetFromJsonAsync<ProductionSessionView>(root))!.WipValue);
        if(useUnits){Assert.Equal("4.000000",partial.OutputQuantity);Assert.Equal("2.000000",partial.OutputInputQuantity);var reversal=await ProductionDraft(s,head.Id,"Reverse",reverses:partial.Id);await PostProduction(s,head.Id,reversal);Assert.Equal("0.000000",(await s.Maker.GetFromJsonAsync<ProductionSessionView>(root))!.ReceivedQuantity);var replacement=await ProductionDraft(s,head.Id,"Receipt",amount:"500",quantity:"2",outputUnit:"PAIR");await PostProduction(s,head.Id,replacement);}
        var loss=await ProductionDraft(s,head.Id,"Loss",amount:"60.0000");await PostProduction(s,head.Id,loss);
        var remaining=await ProductionDraft(s,head.Id,"Receipt",amount:"800.0000",quantity:useUnits?"3":"6.000000",outputUnit:useUnits?"PAIR":null);await PostProduction(s,head.Id,remaining);
        var close=await ProductionDraft(s,head.Id,"Close");await PostProduction(s,head.Id,close);
        head=(await s.Maker.GetFromJsonAsync<ProductionSessionView>(root))!;Assert.Equal("Closed",head.State);Assert.Equal(useUnits?10:8,head.Version);Assert.Equal("0.0000",head.WipValue);Assert.Equal("10.000000",head.ReceivedQuantity);
        Assert.Equal("2290.0000",(await s.Maker.GetFromJsonAsync<CostLedgerStatus>($"{s.Root}/inventory/cost-ledger/status"))!.StockValue);
        var summary=(await s.Maker.GetFromJsonAsync<InventoryValuationSummary>($"{s.Root}/inventory/valuation-summary"))!;Assert.Equal("2290.0000",summary.TotalStockValuationExact);Assert.Equal(1300m,summary.TopValuedItems.Single(x=>x.ProductId==finished.Id).TotalValuation);
        Assert.Equal(HttpStatusCode.Forbidden,(await factory.Client(s.Tenant,s.Company,permissions:[Permissions.ManufacturingRead]).GetAsync(root)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await factory.Client(Guid.NewGuid(),s.Company).GetAsync(root)).StatusCode);
        await factory.Inspect(async db=>{Assert.Equal(useUnits?9:7,await db.Set<ProductionWipEntry>().CountAsync(x=>x.SessionId==head.Id));Assert.Equal(useUnits?7:5,await db.Set<ProductionStockMovement>().CountAsync(x=>x.SessionId==head.Id));Assert.True(await new PeriodStore(db).BalancesReconcile(s.Tenant,s.Company,default));await Assert.ThrowsAsync<PostgresException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM inventory.production_stock_movements WHERE \"SessionId\"={head.Id}"));return 0;});
        var period=(await s.Maker.GetFromJsonAsync<PeriodResponse[]>($"{s.Root}/periods"))!.Single();Assert.Equal(HttpStatusCode.OK,(await Post(s.Maker,$"{s.Root}/periods/{period.Id}/request-close",new PeriodActionRequest(1,"Production reconciled"))).StatusCode);
        var closed=await Post(s.Checker,$"{s.Root}/periods/{period.Id}/close",new PeriodActionRequest(2,"Reviewed production and WIP"));Assert.True(closed.IsSuccessStatusCode,await closed.Content.ReadAsStringAsync());var checkpoint=(await closed.Content.ReadFromJsonAsync<PeriodResponse>())!.Snapshot!;Assert.Equal(useUnits?9:7,checkpoint.ProductionOperationIds!.Count);Assert.Equal(0m,Assert.Single(checkpoint.ProductionWip!).WipValue);
    }
}
