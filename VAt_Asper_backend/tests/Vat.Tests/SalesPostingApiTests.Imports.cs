using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Vat.Application;
using Vat.Application.Contracts;
using Vat.Infrastructure.Persistence;
using Xunit;

namespace Vat.Tests;

public sealed partial class SalesPostingApiTests
{
    [Theory]
    [InlineData("FIFO")]
    [InlineData("WEIGHTED_AVERAGE")]
    public async Task Reviewed_import_declaration_controls_landed_cost_and_receipt_value(string method)
    {
        var s=await Setup();await ActivateCosting(s,method);var purchase=await Approve(s,lines:[new("ITEM","Imported material",10,100)]);var plan=await Prepare(s,purchase.Id);
        var request=new CreateImportDeclarationRequest(plan.Id,"C-2026-0001",new(2026,10,22),"USD","8.0000","125.00000000","25.0000","40.0000","Bill of entry, invoice and reviewed exchange-rate evidence",
            [new("Freight","50.0000","Freight invoice",true),new("Insurance","20.0000","Insurance certificate",true),new("Clearing","10.0000","Agent bill",false)]);
        var declaration=await Created<ImportDeclarationView>(s.Maker,$"{s.Root}/inventory/import-declarations",request);
        Assert.Equal("1000.0000",declaration.FunctionalInvoiceAmount);Assert.Equal("70.0000",declaration.CapitalizableChargeAmount);
        Assert.Equal(HttpStatusCode.Forbidden,(await Post(s.Maker,$"{s.Root}/inventory/import-declarations/{declaration.Id}/approve",new ApproveCostingPolicyRequest(1))).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await Post(s.Checker,$"{s.Root}/inventory/import-declarations/{declaration.Id}/approve",new ApproveCostingPolicyRequest(1))).StatusCode);
        var line=Assert.Single(plan.Snapshot.Lines);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,(await Post(s.Maker,$"{s.Root}/inventory/landed-cost-reviews",new CreateLandedCostRequest(plan.Id,"69.0000","Does not reconcile",[new(line.PurchaseLineId,"69.0000")],declaration.Id))).StatusCode);
        var landed=await Created<LandedCostView>(s.Maker,$"{s.Root}/inventory/landed-cost-reviews",new CreateLandedCostRequest(plan.Id,"70.0000","Allocated from approved import declaration",[new(line.PurchaseLineId,"70.0000")],declaration.Id));
        Assert.Equal(HttpStatusCode.OK,(await Post(s.Checker,$"{s.Root}/inventory/landed-cost-reviews/{landed.Id}/approve",new ApproveCostingPolicyRequest(1))).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await Post(s.Checker,$"{s.Purchase(purchase.Id)}/posting-plans/{plan.Id}/post",new TransitionRequest(4))).StatusCode);
        Assert.Equal("1115.0000",(await s.Maker.GetFromJsonAsync<CostLedgerStatus>($"{s.Root}/inventory/cost-ledger/status"))!.StockValue);
        Assert.Equal(1,await factory.Inspect(db=>db.Set<ImportDeclaration>().CountAsync(x=>x.CompanyId==s.Company&&x.State=="Approved")));
    }
}
