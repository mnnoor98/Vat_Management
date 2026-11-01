using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Vat.Application;
using Vat.Application.Contracts;
using Vat.Contracts.Manufacturing;
using Vat.Domain.Purchasing;
using Vat.Infrastructure.Persistence;
using Xunit;

namespace Vat.Tests;
public sealed partial class SalesPostingApiTests
{
    [Theory]
    [InlineData("FIFO")]
    [InlineData("WEIGHTED_AVERAGE")]
    public async Task Production_reversals_preserve_closed_periods_original_cost_and_atomic_stock(string method)
    {
        var s=await Setup(periodUntil:new(2026,11,1));await ActivateCosting(s,method);
        var fg=await Created<ProductResponse>(s.Maker,$"{s.Root}/products",new CreateProductRequest("FG","Finished goods","PCS","TEST-DOMESTIC"));
        var purchase=await Approve(s,lines:[new PurchaseLineInput("ITEM","Raw",10,100)]);var plan=await Prepare(s,purchase.Id);await ApproveLanded(s,plan,"55.0000");
        Assert.Equal(HttpStatusCode.OK,(await Post(s.Checker,$"{s.Purchase(purchase.Id)}/posting-plans/{plan.Id}/post",new TransitionRequest(4))).StatusCode);
        var evidence=new ProductionOrderEvidence(Guid.NewGuid(),1,3,new string('B',64),s.Warehouse,"Main warehouse",new(2026,10,23),new(fg.Id,"FG","PCS"),10,10,[new(new(s.Product,"ITEM","KG"),8,0)]);
        var hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(evidence,new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))));
        var source=new ProductionOrderSource(s.Tenant,s.Company,Guid.NewGuid(),3,"Approved","CORRECTION","owner-maker","owner-checker",hash,evidence);
        ProductionSessionView head;
        using(var host=factory.WithWebHostBuilder(b=>b.ConfigureTestServices(services=>services.AddSingleton<IProductionExecutionReferences>(new ApprovedProduction(source))))){
            using var client=host.CreateClient();client.DefaultRequestHeaders.Authorization=s.Maker.DefaultRequestHeaders.Authorization;
            head=(await (await Post(client,$"{s.Root}/inventory/production-executions",new ImportProductionRequest(source.OrderId))).Content.ReadFromJsonAsync<ProductionSessionView>())!;
        }
        var root=$"{s.Root}/inventory/production-executions/{head.Id}";
        var issue=await ProductionDraft(s,head.Id,"Issue",[new(s.Warehouse,s.Product,"8")]);await PostProduction(s,head.Id,issue);
        var conversion=await ProductionDraft(s,head.Id,"Conversion",labor:"100");await PostProduction(s,head.Id,conversion);
        var loss=await ProductionDraft(s,head.Id,"Loss",amount:"80");await PostProduction(s,head.Id,loss);
        var receipt=await ProductionDraft(s,head.Id,"Receipt",amount:"900",quantity:"4");await PostProduction(s,head.Id,receipt);
        var period=(await s.Maker.GetFromJsonAsync<PeriodResponse[]>($"{s.Root}/periods"))!.Single();
        Assert.Equal(HttpStatusCode.OK,(await Post(s.Maker,$"{s.Root}/periods/{period.Id}/request-close",new PeriodActionRequest(1,"Original production reconciled"))).StatusCode);
        var closePeriod=await Post(s.Checker,$"{s.Root}/periods/{period.Id}/close",new PeriodActionRequest(2,"Retain original immutable checkpoint"));Assert.True(closePeriod.IsSuccessStatusCode,await closePeriod.Content.ReadAsStringAsync());
        var frozen=await (await s.Maker.GetAsync($"{s.Root}/periods/{period.Id}")).Content.ReadAsStringAsync();
        await Created<PeriodResponse>(s.Maker,$"{s.Root}/periods",new CreatePeriodRequest("Correction period",new(2026,11,1),new(2027,1,1)));
        var date=new DateOnly(2026,11,1);
        Assert.Equal(HttpStatusCode.Conflict,(await Post(s.Maker,$"{root}/operations",new CreateProductionOperationRequest(5,"Reverse",new(2026,10,24),"Closed-period correction denied",[],ReversesOperationId:receipt.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,(await Post(s.Maker,$"{root}/operations",new CreateProductionOperationRequest(5,"Reverse",date,"No manually supplied money",[],Amount:"1",ReversesOperationId:receipt.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await Post(s.Maker,$"{root}/operations",new CreateProductionOperationRequest(5,"Reverse",date,"Unsupported material issue reversal",[],ReversesOperationId:issue.Id))).StatusCode);
        var blocked=await ProductionDraft(s,head.Id,"Reverse",reverses:conversion.Id,date:date);
        Assert.Equal(HttpStatusCode.Forbidden,(await Post(s.Maker,$"{root}/operations/{blocked.Id}/approve",new ApproveProductionOperationRequest(1,5))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await Post(s.Checker,$"{root}/operations/{blocked.Id}/approve",new ApproveProductionOperationRequest(1,5))).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await Post(s.Maker,$"{root}/operations/{blocked.Id}/cancel",new CancelProductionOperationRequest(1,"Reverse receipt before removing released cost"))).StatusCode);
        var restoreLoss=await ProductionDraft(s,head.Id,"Reverse",reverses:loss.Id,date:date);await PostProduction(s,head.Id,restoreLoss);
        var restoreReceipt=await ProductionDraft(s,head.Id,"Reverse",reverses:receipt.Id,date:date);var key=Guid.NewGuid().ToString();
        using(var host=factory.WithWebHostBuilder(b=>b.ConfigureTestServices(services=>services.AddDbContext<VatDbContext>(options=>options.AddInterceptors(new FailProductionCommit()))))){using var client=host.CreateClient();client.DefaultRequestHeaders.Authorization=s.Checker.DefaultRequestHeaders.Authorization;Assert.Equal(HttpStatusCode.InternalServerError,(await Post(client,$"{root}/operations/{restoreReceipt.Id}/approve",new ApproveProductionOperationRequest(1,6),key)).StatusCode);}
        head=(await s.Maker.GetFromJsonAsync<ProductionSessionView>(root))!;Assert.Equal(6,head.Version);Assert.Equal("80.0000",head.WipValue);Assert.Equal("4.000000",head.ReceivedQuantity);
        Assert.Equal("1120.0000",(await s.Maker.GetFromJsonAsync<CostLedgerStatus>($"{s.Root}/inventory/cost-ledger/status"))!.StockValue);
        foreach(var _ in Enumerable.Range(0,2)){var response=await Post(s.Checker,$"{root}/operations/{restoreReceipt.Id}/approve",new ApproveProductionOperationRequest(1,6),key);Assert.True(response.IsSuccessStatusCode,await response.Content.ReadAsStringAsync());}
        head=(await s.Maker.GetFromJsonAsync<ProductionSessionView>(root))!;Assert.Equal("980.0000",head.WipValue);Assert.Equal("0.000000",head.ReceivedQuantity);
        var removeConversion=await ProductionDraft(s,head.Id,"Reverse",reverses:conversion.Id,date:date);await PostProduction(s,head.Id,removeConversion);
        head=(await s.Maker.GetFromJsonAsync<ProductionSessionView>(root))!;Assert.Equal("880.0000",head.WipValue);
        Assert.Equal(HttpStatusCode.Conflict,(await Post(s.Maker,$"{root}/operations",new CreateProductionOperationRequest(head.Version,"Reverse",date,"Duplicate source denied",[],ReversesOperationId:loss.Id))).StatusCode);
        var corrected=await ProductionDraft(s,head.Id,"Receipt",amount:"880",quantity:"8",date:date);await PostProduction(s,head.Id,corrected);
        var saleRule=await Created<TaxRuleResponse>(s.Maker,$"{s.Root}/tax/rules",Rule() with{Treatment="Sale",RatePercent=5,RecoverablePercent=0});
        Assert.Equal(HttpStatusCode.OK,(await Post(s.Checker,$"{s.Root}/tax/rules/{saleRule.Id}/publish",new TransitionRequest(1))).StatusCode);
        var sale=await Created<SalesInvoiceResponse>(s.Maker,$"{s.Root}/sales",new CreateSalesInvoiceRequest(s.Branch,Guid.NewGuid().ToString(),"CUSTOMER",date,[new("FG","Finished",1,200)]));
        Assert.Equal(HttpStatusCode.OK,(await Post(s.Maker,$"{s.Root}/sales/{sale.Id}/submit",new TransitionRequest(1))).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await Post(s.Checker,$"{s.Root}/sales/{sale.Id}/approve",new TransitionRequest(2))).StatusCode);_=await Reserve(s,sale.Id);
        var consumed=await ProductionDraft(s,head.Id,"Reverse",reverses:corrected.Id,date:date);
        Assert.Equal(HttpStatusCode.Conflict,(await Post(s.Checker,$"{root}/operations/{consumed.Id}/approve",new ApproveProductionOperationRequest(1,9))).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await Post(s.Checker,$"{s.Root}/sales/{sale.Id}/post",new TransitionRequest(4))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await Post(s.Checker,$"{root}/operations/{consumed.Id}/approve",new ApproveProductionOperationRequest(1,9))).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await Post(s.Maker,$"{root}/operations/{consumed.Id}/cancel",new CancelProductionOperationRequest(1,"Original finished goods consumed"))).StatusCode);
        Assert.Equal(frozen,await (await s.Maker.GetAsync($"{s.Root}/periods/{period.Id}")).Content.ReadAsStringAsync());
        Assert.Equal("990.0000",(await s.Maker.GetFromJsonAsync<CostLedgerStatus>($"{s.Root}/inventory/cost-ledger/status"))!.StockValue);
        var finish=await ProductionDraft(s,head.Id,"Close",date:date);await PostProduction(s,head.Id,finish);
        Assert.Equal(HttpStatusCode.Conflict,(await Post(s.Maker,$"{root}/operations",new CreateProductionOperationRequest(10,"Reverse",date,"Closed execution denied",[],ReversesOperationId:corrected.Id))).StatusCode);
        await factory.Inspect(async db=>{Assert.Equal(3,await db.Set<ProductionCostOperation>().CountAsync(x=>x.SessionId==head.Id&&x.Kind=="Reverse"&&x.State=="Posted"));Assert.Equal(1,await db.Set<ProductionStockMovement>().CountAsync(x=>x.SessionId==head.Id&&x.Kind=="ReceiptReversal"));Assert.True(await new PeriodStore(db).BalancesReconcile(s.Tenant,s.Company,default));await Assert.ThrowsAsync<PostgresException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"UPDATE inventory.production_cost_operations SET \"PostedValue\"=0 WHERE \"Id\"={receipt.Id}"));return 0;});
    }
}
