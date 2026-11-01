using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
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
    public async Task Reviewed_export_shipment_reuses_immutable_sales_dispatch_cost(string method)
    {
        var s=await Setup();await ActivateCosting(s,method);var purchase=await Approve(s);var receipt=await Prepare(s,purchase.Id);await ApproveLanded(s,receipt,"0.0000");
        Assert.Equal(HttpStatusCode.OK,(await Post(s.Checker,$"{s.Purchase(purchase.Id)}/posting-plans/{receipt.Id}/post",new TransitionRequest(4))).StatusCode);
        var zero=await Created<TaxRuleResponse>(s.Maker,$"{s.Root}/tax/rules",Rule() with {RatePercent=0,RecoverablePercent=0,Treatment="Sale",LegalBasis="Reviewed export zero-rate test evidence"});
        Assert.Equal(HttpStatusCode.OK,(await Post(s.Checker,$"{s.Root}/tax/rules/{zero.Id}/publish",new TransitionRequest(1))).StatusCode);
        var sale=await Sale(s,2);await Reserve(s,sale.Id);Assert.Equal(HttpStatusCode.OK,(await Post(s.Checker,$"{s.Root}/sales/{sale.Id}/post",new TransitionRequest(4))).StatusCode);
        var source=Assert.Single((await s.Maker.GetFromJsonAsync<ExportSourceView[]>($"{s.Root}/inventory/export-shipment-sources"))!);Assert.Equal(sale.Id,source.InvoiceId);Assert.Equal("209.0000",source.DispatchCostAmount);
        var draft=await Created<ExportShipmentView>(s.Maker,$"{s.Root}/inventory/export-shipments",new CreateExportShipmentRequest(sale.Id,"EXP-2026-0001",new(2026,10,24),"US","B/L-001","USD","2.0000","125.00000000","ZeroRated","Bill of export, transport and reviewed tax evidence"));
        Assert.Equal("250.0000",draft.FunctionalProceedsAmount);Assert.Equal("209.0000",draft.DispatchCostAmount);Assert.Equal("0.0000",draft.FrozenOutputTaxAmount);
        Assert.Equal(HttpStatusCode.Forbidden,(await Post(s.Maker,$"{s.Root}/inventory/export-shipments/{draft.Id}/approve",new ApproveCostingPolicyRequest(1))).StatusCode);
        var approved=await Post(s.Checker,$"{s.Root}/inventory/export-shipments/{draft.Id}/approve",new ApproveCostingPolicyRequest(1));Assert.Equal(HttpStatusCode.OK,approved.StatusCode);
        Assert.Equal("Approved",(await approved.Content.ReadFromJsonAsync<ExportShipmentView>())!.State);
        await factory.Inspect(async db=>{await Assert.ThrowsAsync<PostgresException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"UPDATE inventory.export_shipments SET \"DispatchCostAmount\"=0 WHERE \"CompanyId\"={s.Company} AND \"Id\"={draft.Id}"));return 0;});
    }
}
