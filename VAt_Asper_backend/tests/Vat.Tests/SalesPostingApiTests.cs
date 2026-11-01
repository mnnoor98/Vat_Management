using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vat.Application.Contracts;
using Vat.Application.Security;
using Vat.Application;
using Vat.Domain.Sales;
using Vat.Domain.Posting;
using Vat.Domain.Purchasing;
using Vat.Infrastructure.Persistence;
using Npgsql;
using Xunit;

namespace Vat.Tests;

[Collection("PostgreSQL integration")]
public sealed partial class SalesPostingApiTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private sealed record SetupData(Guid Tenant, Guid Company, Guid Branch, Guid Warehouse, Guid Product,
        Guid Rule, HttpClient Maker, HttpClient Checker)
    {
        public string Root => $"/api/v1/companies/{Company}";
        public string Purchase(Guid id) => $"{Root}/purchases/{id}";
    }

    private static async Task<HttpResponseMessage> Post(HttpClient client, string url, object body, string? key = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString());
        return await client.SendAsync(request);
    }

    private static async Task<T> Created<T>(HttpClient client, string url, object body)
    {
        var response = await Post(client, url, body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(response.Headers.Location)).StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private static CreateTaxRuleRequest Rule(string code = "TEST-DOMESTIC", DateOnly? from = null, DateOnly? until = null) => new(
        code, from ?? new(2026, 1, 1), until ?? new(2027, 1, 1), 7.5m, 40m,
        "Synthetic test calculation only; no legal rate asserted", "TEST-EVIDENCE-001");

    private async Task<SetupData> Setup(bool publishRule = true,DateOnly? periodUntil=null)
    {
        var tenant = Guid.NewGuid();
        var company = Guid.NewGuid();
        var maker = factory.Client(tenant, company);
        var checker = factory.Client(tenant, company, "checker");
        var root = $"/api/v1/companies/{company}";
        await Created<CompanyResponse>(maker, "/api/v1/companies", new CreateCompanyRequest(company, "Posting pilot", Guid.NewGuid().ToString()));
        await Created<PeriodResponse>(maker, $"{root}/periods", new CreatePeriodRequest("Test scope", new(2026, 1, 1), periodUntil??new(2029, 1, 1)));
        var branch = await Created<BranchResponse>(maker, $"{root}/branches", new CreateBranchRequest("Dhaka"));
        var warehouse = await Created<WarehouseResponse>(maker, $"{root}/warehouses", new CreateWarehouseRequest(branch.Id, "MAIN", "Main warehouse"));
        var product = await Created<ProductResponse>(maker, $"{root}/products", new CreateProductRequest("ITEM", "Materials", "KG", "TEST-DOMESTIC"));
        await Created<SupplierResponse>(maker, $"{root}/suppliers", new CreateSupplierRequest("SUP", "Test supplier"));
        var rule = await Created<TaxRuleResponse>(maker, $"{root}/tax/rules", Rule());
        if (publishRule)
            Assert.Equal(HttpStatusCode.OK, (await Post(checker, $"{root}/tax/rules/{rule.Id}/publish", new TransitionRequest(1))).StatusCode);
        return new(tenant, company, branch.Id, warehouse.Id, product.Id, rule.Id, maker, checker);
    }

    private static async Task<PurchaseResponse> Approve(SetupData setup, string product = "ITEM", string supplier = "SUP",
        DateOnly? date = null, IReadOnlyList<PurchaseLineInput>? lines = null)
    {
        var purchase = await Created<PurchaseResponse>(setup.Maker, $"{setup.Root}/purchases", new CreatePurchaseRequest(
            setup.Branch, Guid.NewGuid().ToString(), supplier, date ?? new(2026, 10, 23),
            lines ?? [new(product, "Source description", 2.5m, 100m)]));
        Assert.Equal(HttpStatusCode.OK, (await Post(setup.Maker, $"{setup.Purchase(purchase.Id)}/submit", new TransitionRequest(1))).StatusCode);
        var approved = await Post(setup.Checker, $"{setup.Purchase(purchase.Id)}/approve", new TransitionRequest(2));
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        return (await approved.Content.ReadFromJsonAsync<PurchaseResponse>())!;
    }

    private static Task<PostingPlanResponse> Prepare(SetupData setup, Guid purchaseId, long version = 3) =>
        Created<PostingPlanResponse>(setup.Maker, $"{setup.Purchase(purchaseId)}/posting-plans", new PreparePostingRequest(version, setup.Warehouse));

    private async Task<(SetupData Setup, PurchaseResponse Purchase)> Stock(bool salesRule = true)
    {
        var s = await Setup();
        var purchase = await Approve(s);
        var plan = await Prepare(s, purchase.Id);
        Assert.Equal(HttpStatusCode.OK, (await Post(s.Checker, $"{s.Purchase(purchase.Id)}/posting-plans/{plan.Id}/post", new TransitionRequest(4))).StatusCode);
        if (salesRule)
        {
            var rule = await Created<TaxRuleResponse>(s.Maker, $"{s.Root}/tax/rules",
                Rule() with { RatePercent = 5m, RecoverablePercent = 0m, Treatment = "Sale" });
            Assert.Equal("Sale", rule.Treatment);
            Assert.Equal(HttpStatusCode.OK, (await Post(s.Checker, $"{s.Root}/tax/rules/{rule.Id}/publish", new TransitionRequest(1))).StatusCode);
        }
        return (s, purchase);
    }

    private static async Task<SalesInvoiceResponse> Sale(SetupData s, decimal quantity = 2m, IReadOnlyList<SalesInvoiceLineInput>? lines = null)
    {
        var invoice = await Created<SalesInvoiceResponse>(s.Maker, $"{s.Root}/sales", new CreateSalesInvoiceRequest(
            s.Branch, Guid.NewGuid().ToString(), "CUSTOMER", new(2026, 10, 23), lines ?? [new("ITEM", "Goods", quantity, 100m)]));
        var path = $"{s.Root}/sales/{invoice.Id}";
        Assert.Equal(HttpStatusCode.OK, (await Post(s.Maker, $"{path}/submit", new TransitionRequest(1))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Post(s.Checker, $"{path}/approve", new TransitionRequest(2))).StatusCode);
        return invoice;
    }

    private static Task<SalesPostingResponse> Reserve(SetupData s, Guid id) => Created<SalesPostingResponse>(s.Maker,
        $"{s.Root}/sales/{id}/reserve", new ReserveSalesRequest(3, s.Warehouse));

    private static async Task<StockBalanceResponse> Balance(SetupData s) => (await s.Maker.GetFromJsonAsync<StockBalanceResponse[]>(
        $"{s.Root}/inventory/balances?warehouseId={s.Warehouse}"))!.Single();

    private static async Task ActivateCosting(SetupData s, string method)
    {
        var policy = await Created<CostingPolicyView>(s.Maker,$"{s.Root}/inventory/costing-policies",new CreateCostingPolicyRequest(null,method,new(2026,1,1),new(2027,1,1)));
        Assert.Equal(HttpStatusCode.OK,(await Post(s.Checker,$"{s.Root}/inventory/costing-policies/{policy.Id}/approve",new ApproveCostingPolicyRequest(1))).StatusCode);
        var a=await Created<CostAdoptionView>(s.Maker,$"{s.Root}/inventory/cost-adoptions",new CreateCostAdoptionRequest(new(2026,1,1),"NetPlusNonrecoverableTaxAndLanded","Reviewed empty company opening",[]));
        Assert.Equal(HttpStatusCode.Forbidden,(await Post(s.Maker,$"{s.Root}/inventory/cost-adoptions/{a.Id}/approve",new ApproveCostingPolicyRequest(1))).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await Post(s.Checker,$"{s.Root}/inventory/cost-adoptions/{a.Id}/approve",new ApproveCostingPolicyRequest(1))).StatusCode);
    }
    private static async Task ApproveLanded(SetupData s, PostingPlanResponse plan, string amount)
    {
        var source=(await s.Maker.GetFromJsonAsync<LandedSource>($"{s.Root}/inventory/landed-cost-sources/{plan.Id}"))!;
        Assert.Equal(plan.Id,source.PlanId); Assert.Equal(plan.Snapshot.Lines.Count,source.Lines.Count);
        var review=await Created<LandedCostView>(s.Maker,$"{s.Root}/inventory/landed-cost-reviews",new CreateLandedCostRequest(plan.Id,amount,"Reviewed BDT freight / allocation evidence",
            plan.Snapshot.Lines.Select(x=>new LandedCostInput(x.PurchaseLineId,amount)).ToArray()));
        Assert.Equal(HttpStatusCode.Forbidden,(await Post(s.Maker,$"{s.Root}/inventory/landed-cost-reviews/{review.Id}/approve",new ApproveCostingPolicyRequest(1))).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await Post(s.Checker,$"{s.Root}/inventory/landed-cost-reviews/{review.Id}/approve",new ApproveCostingPolicyRequest(1))).StatusCode);
    }
    [Theory]
    [InlineData("FIFO","2200.0000","1100.0000")]
    [InlineData("WEIGHTED_AVERAGE","2475.0000","825.0000")]
    public async Task Active_full_basis_ledger_costs_receipts_issues_partial_returns_transfers_and_reversals(string method,string issued,string closing)
    {
        var s=await Setup(); await ActivateCosting(s,method); var purchases=new List<PurchaseResponse>();
        foreach(var price in new[]{100m,200m})
        {
            var p=await Approve(s,lines:[new("ITEM","Full cost",10,price)]); purchases.Add(p); var plan=await Prepare(s,p.Id);
            Assert.Equal(HttpStatusCode.Conflict,(await Post(s.Checker,$"{s.Purchase(p.Id)}/posting-plans/{plan.Id}/post",new TransitionRequest(4))).StatusCode);
            await ApproveLanded(s,plan,price==100?"55.0000":"110.0000");
            Assert.Equal(HttpStatusCode.OK,(await Post(s.Checker,$"{s.Purchase(p.Id)}/posting-plans/{plan.Id}/post",new TransitionRequest(4))).StatusCode);
        }
        var rule=await Created<TaxRuleResponse>(s.Maker,$"{s.Root}/tax/rules",Rule() with {RatePercent=5m,RecoverablePercent=0m,Treatment="Sale"});
        Assert.Equal(HttpStatusCode.OK,(await Post(s.Checker,$"{s.Root}/tax/rules/{rule.Id}/publish",new TransitionRequest(1))).StatusCode);
        var sale=await Sale(s,15); var reserved=await Reserve(s,sale.Id);
        var summary=(await s.Maker.GetFromJsonAsync<InventoryValuationSummary>($"{s.Root}/inventory/valuation-summary"))!;
        Assert.Equal(3300m,summary.TotalStockValuation); Assert.Equal("Available",summary.ValuationStatus);
        var postKey=Guid.NewGuid().ToString();
        using(var failedHost=factory.WithWebHostBuilder(b=>b.ConfigureTestServices(services=>services.AddDbContext<VatDbContext>(o=>o.AddInterceptors(new FailFirstSalesCommit())))))
        {
            using var failing=failedHost.CreateClient(); failing.DefaultRequestHeaders.Authorization=s.Checker.DefaultRequestHeaders.Authorization;
            Assert.Equal(HttpStatusCode.InternalServerError,(await Post(failing,$"{s.Root}/sales/{sale.Id}/post",new TransitionRequest(4),postKey)).StatusCode);
        }
        var afterFailure=(await s.Maker.GetFromJsonAsync<CostLedgerStatus>($"{s.Root}/inventory/cost-ledger/status"))!;
        Assert.Equal("3300.0000",afterFailure.StockValue); Assert.Equal("2",afterFailure.Sequence);
        for(var retry=0;retry<2;retry++) Assert.Equal(HttpStatusCode.OK,(await Post(s.Checker,$"{s.Root}/sales/{sale.Id}/post",new TransitionRequest(4),postKey)).StatusCode);
        var status=(await s.Maker.GetFromJsonAsync<CostLedgerStatus>($"{s.Root}/inventory/cost-ledger/status"))!;
        Assert.Equal(closing,status.StockValue); Assert.Equal("Reconciled",status.State); Assert.Equal("NetPlusNonrecoverableTaxAndLanded",status.Basis);
        var journal=(await s.Maker.GetFromJsonAsync<StockCostEntryView[]>($"{s.Root}/inventory/cost-ledger/entries"))!;
        Assert.Equal("-"+issued,Assert.Single(journal,x=>x.Kind=="Issue").Value);
        var partial=await Created<SalesCreditResponse>(s.Maker,$"{s.Root}/sales/{sale.Id}/credit-notes",new RequestSalesCredit(5,new(2026,10,24),"Partial return","RETURN-EVIDENCE",[new(reserved.Snapshot.Lines.Single().LineId,3)]));
        Assert.Equal(HttpStatusCode.OK,(await Post(s.Checker,$"{s.Root}/sales/{sale.Id}/credit-notes/{partial.Id}/approve",new TransitionRequest(partial.SourceVersion))).StatusCode);
        var remaining=(await s.Maker.GetFromJsonAsync<SalesReturnableResponse>($"{s.Root}/sales/{sale.Id}/returnable-lines"))!;
        var full=await Created<SalesCreditResponse>(s.Maker,$"{s.Root}/sales/{sale.Id}/credit-notes",new RequestSalesCredit(remaining.Version,new(2026,10,24),"Remaining return","RETURN-EVIDENCE-2"));
        Assert.Equal(HttpStatusCode.OK,(await Post(s.Checker,$"{s.Root}/sales/{sale.Id}/credit-notes/{full.Id}/approve",new TransitionRequest(full.SourceVersion))).StatusCode);
        Assert.Equal("3300.0000",(await s.Maker.GetFromJsonAsync<CostLedgerStatus>($"{s.Root}/inventory/cost-ledger/status"))!.StockValue);
        var to=await Created<WarehouseResponse>(s.Maker,$"{s.Root}/warehouses",new CreateWarehouseRequest(s.Branch,"SECOND","Second warehouse"));
        foreach(var sides in new[]{(s.Warehouse,to.Id),(to.Id,s.Warehouse)})
        {
            var transfer=await Created<TransferResult>(s.Maker,$"{s.Root}/transfers",new TransferCreateRequest(Guid.NewGuid().ToString(),new(2026,10,25),null,sides.Item1,sides.Item2,null,null,"Cost transfer",[new(s.Product,7,0,null)]));
            Assert.Equal(HttpStatusCode.OK,(await Post(s.Maker,$"{s.Root}/transfers/{transfer.Id}/submit",new TransferActionRequest(1))).StatusCode);
            Assert.Equal(HttpStatusCode.OK,(await Post(s.Checker,$"{s.Root}/transfers/{transfer.Id}/approve",new TransferActionRequest(2))).StatusCode);
            Assert.Equal(HttpStatusCode.OK,(await Post(s.Checker,$"{s.Root}/transfers/{transfer.Id}/post",new TransferActionRequest(3))).StatusCode);
            Assert.Equal("3300.0000",(await s.Maker.GetFromJsonAsync<CostLedgerStatus>($"{s.Root}/inventory/cost-ledger/status"))!.StockValue);
        }
        foreach(var p in purchases)
        {
            var reversal=await Created<ReversalResponse>(s.Maker,$"{s.Purchase(p.Id)}/reversals",new RequestReversalRequest(5,new(2026,10,26),"Reviewed supplier return"));
            Assert.Equal(HttpStatusCode.OK,(await Post(s.Checker,$"{s.Purchase(p.Id)}/reversals/{reversal.Id}/approve",new TransitionRequest(6))).StatusCode);
        }
        Assert.Equal("0.0000",(await s.Maker.GetFromJsonAsync<CostLedgerStatus>($"{s.Root}/inventory/cost-ledger/status"))!.StockValue);
        await factory.Inspect(async db=>{
            Assert.Equal(0m,await db.Set<StockCostEntry>().Where(x=>x.CompanyId==s.Company).SumAsync(x=>x.Value));
            await Assert.ThrowsAsync<PostgresException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM inventory.cost_entries WHERE \"CompanyId\"={s.Company}"));
            await Assert.ThrowsAsync<PostgresException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"UPDATE inventory.cost_positions SET \"Value\"=1 WHERE \"CompanyId\"={s.Company}"));
            await Assert.ThrowsAsync<PostgresException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"UPDATE inventory.cost_ledger_heads SET \"Sequence\"=\"Sequence\"+1 WHERE \"CompanyId\"={s.Company}"));
            await Assert.ThrowsAsync<PostgresException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"UPDATE inventory.cost_landed_reviews SET \"TotalAmount\"=0 WHERE \"CompanyId\"={s.Company} AND \"State\"='Approved'"));
            return 0;
        });
    }

    [Fact]
    public async Task Opening_adoption_detects_stale_activity_and_requires_complete_independent_review()
    {
        var (s,_)=await Stock();
        var openingPositions=(await s.Maker.GetFromJsonAsync<CostOpeningPosition[]>($"{s.Root}/inventory/cost-opening-positions"))!;
        Assert.Equal("2.500000",Assert.Single(openingPositions).Quantity);
        var policy=await Created<CostingPolicyView>(s.Maker,$"{s.Root}/inventory/costing-policies",new CreateCostingPolicyRequest(null,"FIFO",new(2026,10,24),new(2027,1,1)));
        Assert.Equal(HttpStatusCode.OK,(await Post(s.Checker,$"{s.Root}/inventory/costing-policies/{policy.Id}/approve",new ApproveCostingPolicyRequest(1))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await Post(s.Maker,$"{s.Root}/inventory/cost-adoptions",new CreateCostAdoptionRequest(new(2026,10,24),"NetPlusNonrecoverableTaxAndLanded","Incomplete opening",[]))).StatusCode);
        var a=await Created<CostAdoptionView>(s.Maker,$"{s.Root}/inventory/cost-adoptions",new CreateCostAdoptionRequest(new(2026,10,24),"NetPlusNonrecoverableTaxAndLanded","Reviewed opening",[new(s.Warehouse,s.Product,"2.500000","300.0000")]));
        var p=await Approve(s); var plan=await Prepare(s,p.Id);
        Assert.Equal(HttpStatusCode.OK,(await Post(s.Checker,$"{s.Purchase(p.Id)}/posting-plans/{plan.Id}/post",new TransitionRequest(4))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await Post(s.Checker,$"{s.Root}/inventory/cost-adoptions/{a.Id}/approve",new ApproveCostingPolicyRequest(1))).StatusCode);
        var fresh=await Created<CostAdoptionView>(s.Maker,$"{s.Root}/inventory/cost-adoptions",new CreateCostAdoptionRequest(new(2026,10,24),"NetPlusNonrecoverableTaxAndLanded","New reviewed opening",[new(s.Warehouse,s.Product,"5.000000","600.0000")]));
        Assert.Equal(HttpStatusCode.OK,(await Post(s.Checker,$"{s.Root}/inventory/cost-adoptions/{fresh.Id}/approve",new ApproveCostingPolicyRequest(1))).StatusCode);
        var balances=(await s.Maker.GetFromJsonAsync<FinancialStockItem[]>($"{s.Root}/inventory/all-balances"))!;
        Assert.Equal(600,Assert.Single(balances).TotalValuation); Assert.Equal("Available",balances[0].ValuationStatus); Assert.Equal("NetPlusNonrecoverableTaxAndLanded",balances[0].ValuationBasis);
        using var denied=factory.Client(s.Tenant,s.Company,permissions:[Permissions.InventoryRead]);
        Assert.Equal(HttpStatusCode.Forbidden,(await denied.GetAsync($"{s.Root}/inventory/cost-ledger/status")).StatusCode);
        using var foreign=factory.Client(Guid.NewGuid(),s.Company);
        Assert.Equal(HttpStatusCode.NotFound,(await foreign.GetAsync($"{s.Root}/inventory/cost-adoptions/{fresh.Id}")).StatusCode);
    }

    [Fact]
    public async Task Concurrent_full_cost_receipts_preserve_multiline_allocations_and_product_policy_methods()
    {
        var s=await Setup(); await ActivateCosting(s,"FIFO");
        var product=await Created<ProductResponse>(s.Maker,$"{s.Root}/products",new CreateProductRequest("SECOND","Second product","KG","TEST-DOMESTIC"));
        var overridePolicy=await Created<CostingPolicyView>(s.Maker,$"{s.Root}/inventory/costing-policies",new CreateCostingPolicyRequest(product.Id,"WEIGHTED_AVERAGE",new(2026,1,1),new(2027,1,1)));
        Assert.Equal(HttpStatusCode.OK,(await Post(s.Checker,$"{s.Root}/inventory/costing-policies/{overridePolicy.Id}/approve",new ApproveCostingPolicyRequest(1))).StatusCode);
        var ready=new List<(PurchaseResponse Purchase,PostingPlanResponse Plan)>();
        for(var i=0;i<4;i++)
        {
            var p=await Approve(s,lines:i==0?[new("ITEM","First",4,1),new("ITEM","Second",6,1)]:i==3?[new("SECOND","Other policy",5,2)]:[new("ITEM","Concurrent",10,1)]);
            var plan=await Prepare(s,p.Id);ready.Add((p,plan));
            if(i==0)
            {
                var inputs=plan.Snapshot.Lines.Select((x,n)=>new LandedCostInput(x.PurchaseLineId,n==0?"0.4000":"0.6000")).ToArray();
                Assert.Equal(HttpStatusCode.UnprocessableEntity,(await Post(s.Maker,$"{s.Root}/inventory/landed-cost-reviews",new CreateLandedCostRequest(plan.Id,"2.0000","Incorrect allocation",inputs))).StatusCode);
                var a=await Created<LandedCostView>(s.Maker,$"{s.Root}/inventory/landed-cost-reviews",new CreateLandedCostRequest(plan.Id,"1.0000","Split freight evidence",inputs));
                Assert.Equal(HttpStatusCode.OK,(await Post(s.Checker,$"{s.Root}/inventory/landed-cost-reviews/{a.Id}/approve",new ApproveCostingPolicyRequest(1))).StatusCode);
            }
            else await ApproveLanded(s,plan,"0.0000");
        }
        var results=await Task.WhenAll(ready.Select(x=>Post(s.Checker,$"{s.Purchase(x.Purchase.Id)}/posting-plans/{x.Plan.Id}/post",new TransitionRequest(4))));
        Assert.All(results,x=>Assert.Equal(HttpStatusCode.OK,x.StatusCode));
        var summary=(await s.Maker.GetFromJsonAsync<InventoryValuationSummary>($"{s.Root}/inventory/valuation-summary?method=FIFO"))!;
        Assert.Equal(42.8m,summary.TotalStockValuation);Assert.Equal("MIXED",summary.CostingMethod);Assert.Equal("42.8000",summary.TotalStockValuationExact);
        var balances=(await s.Maker.GetFromJsonAsync<FinancialStockItem[]>($"{s.Root}/inventory/all-balances?method=FIFO"))!;
        Assert.Equal("WEIGHTED_AVERAGE",Assert.Single(balances,x=>x.ProductId==product.Id).CostingMethod);
        var status=(await s.Maker.GetFromJsonAsync<CostLedgerStatus>($"{s.Root}/inventory/cost-ledger/status"))!;Assert.Equal("5",status.Sequence);
        var first=(await s.Maker.GetFromJsonAsync<StockCostEntryView[]>($"{s.Root}/inventory/cost-ledger/entries?take=2"))!;
        var next=(await s.Maker.GetFromJsonAsync<StockCostEntryView[]>($"{s.Root}/inventory/cost-ledger/entries?take=2&before={first[^1].Sequence}"))!;
        Assert.Empty(first.Select(x=>x.Sequence).Intersect(next.Select(x=>x.Sequence)));
    }

    [Theory]
    [InlineData("FIFO", "2000.0000", "1000.0000")]
    [InlineData("WEIGHTED_AVERAGE", "2250.0000", "750.0000")]
    public async Task Source_cost_preview_replays_posted_evidence_and_rejects_ledger_tampering(string method, string issued, string closing)
    {
        var s = await Setup();
        var policy = await Created<CostingPolicyView>(s.Maker, $"{s.Root}/inventory/costing-policies",
            new CreateCostingPolicyRequest(null, method, new(2026, 1, 1), new(2027, 1, 1)));
        Assert.Equal(HttpStatusCode.OK, (await Post(s.Checker, $"{s.Root}/inventory/costing-policies/{policy.Id}/approve", new ApproveCostingPolicyRequest(1))).StatusCode);
        foreach (var price in new[] { 100m, 200m })
        {
            var purchase = await Approve(s, lines: [new("ITEM", "Cost source", 10, price)]);
            var plan = await Prepare(s, purchase.Id);
            Assert.Equal(HttpStatusCode.OK, (await Post(s.Checker, $"{s.Purchase(purchase.Id)}/posting-plans/{plan.Id}/post", new TransitionRequest(4))).StatusCode);
        }
        var rule = await Created<TaxRuleResponse>(s.Maker, $"{s.Root}/tax/rules", Rule() with { RatePercent = 5m, RecoverablePercent = 0m, Treatment = "Sale" });
        Assert.Equal(HttpStatusCode.OK, (await Post(s.Checker, $"{s.Root}/tax/rules/{rule.Id}/publish", new TransitionRequest(1))).StatusCode);
        var sale = await Sale(s, 15);
        await Reserve(s, sale.Id);
        var key = Guid.NewGuid().ToString();
        for (var retry = 0; retry < 2; retry++)
            Assert.Equal(HttpStatusCode.OK, (await Post(s.Checker, $"{s.Root}/sales/{sale.Id}/post", new TransitionRequest(4), key)).StatusCode);
        var url = $"{s.Root}/inventory/cost-preview?warehouseId={s.Warehouse}&productId={s.Product}";
        var preview = (await s.Maker.GetFromJsonAsync<CostPreviewView>(url))!;
        Assert.Equal("Preview", preview.State); Assert.Equal("PurchaseNetOnly", preview.Basis);
        Assert.Equal("3", preview.SupportedSourceSequence); Assert.Equal(3, preview.SourceCount);
        Assert.Equal(method, preview.Calculation!.Method); Assert.Equal("5.000000", preview.Calculation.ClosingQuantity);
        Assert.Equal(issued, preview.Calculation.IssuedValue); Assert.Equal(closing, preview.Calculation.ClosingValue);
        Assert.Equal(issued, Assert.Single(preview.Calculation.Issues).Value);
        Assert.True(await factory.Inspect(db => db.Set<CostSourceHead>().Where(x => x.CompanyId == s.Company).Select(x => x.EmptyOpeningProven).SingleAsync()));
        Assert.Equal(3, await factory.Inspect(db => db.Set<CostSourceEvent>().CountAsync(x => x.CompanyId == s.Company && x.PolicyId == policy.Id)));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VatDbContext>();
        await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM inventory.cost_source_events WHERE \"CompanyId\"={s.Company}"));
        await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE inventory.cost_source_heads SET \"Sequence\"=\"Sequence\"+1 WHERE \"CompanyId\"={s.Company}"));
        await db.Set<SalesPostingEntry>().Where(x => x.CompanyId == s.Company).ExecuteUpdateAsync(x => x.SetProperty(b => b.Quantity, -14m));
        var changedSource = (await s.Maker.GetFromJsonAsync<CostPreviewView>(url))!;
        Assert.Equal("cost_source_mismatch", changedSource.Reason); Assert.Null(changedSource.Calculation);
        await db.Set<SalesPostingEntry>().Where(x => x.CompanyId == s.Company).ExecuteUpdateAsync(x => x.SetProperty(b => b.Quantity, -15m));
        await db.Set<StockBalance>().Where(x => x.CompanyId == s.Company).ExecuteUpdateAsync(x => x.SetProperty(b => b.Quantity, 6m));
        var mismatch = (await s.Maker.GetFromJsonAsync<CostPreviewView>(url))!;
        Assert.Equal("Unavailable", mismatch.State); Assert.Equal("cost_stock_reconciliation_failed", mismatch.Reason); Assert.Null(mismatch.Calculation);
    }

    [Fact]
    public async Task Missing_policy_and_reversal_never_produce_partial_source_costs()
    {
        var (s, purchase) = await Stock();
        var url = $"{s.Root}/inventory/cost-preview?warehouseId={s.Warehouse}&productId={s.Product}";
        var unassigned = (await s.Maker.GetFromJsonAsync<CostPreviewView>(url))!;
        Assert.Equal("cost_policy_unassigned", unassigned.Reason); Assert.Null(unassigned.Calculation);
        var reversal = await Created<ReversalResponse>(s.Maker, $"{s.Purchase(purchase.Id)}/reversals",
            new RequestReversalRequest(5, new(2026, 10, 24), "Supplier return"));
        Assert.Equal(HttpStatusCode.OK, (await Post(s.Checker, $"{s.Purchase(purchase.Id)}/reversals/{reversal.Id}/approve", new TransitionRequest(6))).StatusCode);
        var unsupported = (await s.Maker.GetFromJsonAsync<CostPreviewView>(url))!;
        Assert.Equal("unsupported_cost_movements", unsupported.Reason); Assert.Null(unsupported.Calculation);
    }

    [Fact]
    public async Task Existing_opening_stock_requires_adoption_and_preview_enforces_scope()
    {
        var s = await Setup();
        var url = $"{s.Root}/inventory/cost-preview?warehouseId={s.Warehouse}&productId={s.Product}";
        Assert.Equal("cost_source_journal_missing", (await s.Maker.GetFromJsonAsync<CostPreviewView>(url))!.Reason);
        await factory.Inspect(async db => {
            var balance = new StockBalance(); db.Add(balance);
            db.Entry(balance).Property(x => x.TenantId).CurrentValue = s.Tenant;
            db.Entry(balance).Property(x => x.CompanyId).CurrentValue = s.Company;
            db.Entry(balance).Property(x => x.WarehouseId).CurrentValue = s.Warehouse;
            db.Entry(balance).Property(x => x.ProductId).CurrentValue = s.Product;
            db.Entry(balance).Property(x => x.Quantity).CurrentValue = 5m;
            return await db.SaveChangesAsync();
        });
        var purchase = await Approve(s); var plan = await Prepare(s, purchase.Id);
        Assert.Equal(HttpStatusCode.OK, (await Post(s.Checker, $"{s.Purchase(purchase.Id)}/posting-plans/{plan.Id}/post", new TransitionRequest(4))).StatusCode);
        var opening = (await s.Maker.GetFromJsonAsync<CostPreviewView>(url))!;
        Assert.Equal("opening_cost_adoption_required", opening.Reason); Assert.Null(opening.Calculation);
        Assert.Equal(HttpStatusCode.NotFound, (await s.Maker.GetAsync($"{s.Root}/inventory/cost-preview?warehouseId={Guid.NewGuid()}&productId={s.Product}")).StatusCode);
        using var foreign = factory.Client(Guid.NewGuid(), s.Company);
        Assert.Equal(HttpStatusCode.NotFound, (await foreign.GetAsync(url)).StatusCode);
        using var inventoryOnly = factory.Client(s.Tenant, s.Company, permissions: [Permissions.InventoryRead]);
        Assert.Equal(HttpStatusCode.Forbidden, (await inventoryOnly.GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task Sale_reserves_then_atomically_posts_stock_output_tax_and_closes_with_checkpoint()
    {
        var (s, _) = await Stock();
        var invoice = await Sale(s, lines: [new("ITEM", "First", 1, 100), new("ITEM", "Second", 1, 100)]);
        var plan = await Reserve(s, invoice.Id);
        Assert.Equal(200m, plan.Snapshot.NetAmount);
        Assert.Equal(10m, plan.Snapshot.OutputTaxAmount);
        Assert.Equal(210m, plan.Snapshot.GrossAmount);
        Assert.All(plan.Snapshot.Lines, x => Assert.Equal(5m, x.RatePercent));
        var balance = await Balance(s);
        Assert.Equal(2.5m, balance.Quantity);
        Assert.Equal(2m, balance.ReservedQuantity);
        Assert.Equal(0.5m, balance.AvailableQuantity);
        var path = $"{s.Root}/sales/{invoice.Id}";
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(s.Maker, $"{path}/post", new TransitionRequest(4))).StatusCode);
        var key = Guid.NewGuid().ToString();
        var responses = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => Post(s.Checker, $"{path}/post", new TransitionRequest(4), key)));
        Assert.All(responses, x => Assert.Equal(HttpStatusCode.OK, x.StatusCode));
        Assert.Single((await Task.WhenAll(responses.Select(x => x.Content.ReadAsStringAsync()))).Distinct());
        balance = await Balance(s);
        Assert.Equal(0.5m, balance.Quantity);
        Assert.Equal(0m, balance.ReservedQuantity);
        Assert.Equal("Posted", (await s.Maker.GetFromJsonAsync<SalesInvoiceResponse>(path))!.Status);
        Assert.True((await s.Maker.GetFromJsonAsync<SalesReconciliation>($"{path}/reconciliation"))!.IsReconciled);
        var fulfillment = (await s.Maker.GetFromJsonAsync<SalesPostingResponse>($"{path}/fulfillment"))!;
        Assert.Equal($"S-{invoice.Id:N}", fulfillment.DocumentNumber);
        var ledger = (await s.Maker.GetFromJsonAsync<SalesLedgerPage>($"{s.Root}/sales/ledger"))!;
        Assert.Equal(2, ledger.Entries.Count);
        Assert.Equal(-2m, ledger.Entries.Sum(x => x.Quantity));
        Assert.Equal(10m, ledger.Entries.Sum(x => x.OutputTaxAmount));
        var firstPage = (await s.Maker.GetFromJsonAsync<SalesLedgerPage>($"{s.Root}/sales/ledger?take=1"))!;
        var secondPage = (await s.Maker.GetFromJsonAsync<SalesLedgerPage>($"{s.Root}/sales/ledger?take=1&before={firstPage.NextBefore}"))!;
        Assert.NotEqual(firstPage.Entries.Single().Id, secondPage.Entries.Single().Id);
        Assert.Null(secondPage.NextBefore);
        var summary = (await s.Maker.GetFromJsonAsync<SalesSummary>($"{s.Root}/sales/summary?from=2026-10-01&until=2026-11-01"))!;
        Assert.Equal(1, summary.InvoiceCount);
        Assert.Equal(210m, summary.GrossAmount);
        var payload = await factory.Inspect(db => db.Outbox.Where(x => x.AggregateId == invoice.Id && x.EventType == nameof(SalesPostedV1)).Select(x => x.Payload).SingleAsync());
        using var envelope = System.Text.Json.JsonDocument.Parse(payload);
        using var scope = factory.Services.CreateScope();
        var processed = await scope.ServiceProvider.GetRequiredService<IIncomingEventProcessor>().Process(new(
            System.Text.Encoding.UTF8.GetBytes(payload), envelope.RootElement.GetProperty("eventId").GetGuid().ToString(), nameof(SalesPostedV1), "application/json"), default);
        Assert.Equal("Ignored", processed.Status);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(s.Maker, $"{path}/cancel", new CancelSalesInvoiceRequest(5, "Cannot cancel posted"))).StatusCode);
        var period = (await s.Maker.GetFromJsonAsync<PeriodResponse[]>($"{s.Root}/periods"))!.Single();
        Assert.Equal(HttpStatusCode.OK, (await Post(s.Maker, $"{s.Root}/periods/{period.Id}/request-close", new PeriodActionRequest(1, "Close"))).StatusCode);
        var close = await Post(s.Checker, $"{s.Root}/periods/{period.Id}/close", new PeriodActionRequest(2, "Reconciled"));
        Assert.Equal(HttpStatusCode.OK, close.StatusCode);
        var checkpoint = (await close.Content.ReadFromJsonAsync<PeriodResponse>())!.Snapshot!.Sales!;
        Assert.Equal(invoice.Id, Assert.Single(checkpoint.InvoiceIds));
        Assert.Equal(10m, checkpoint.OutputTaxAmount);
    }

    [Fact]
    public async Task Competing_reservations_cannot_oversell_and_cancel_releases_exactly_once()
    {
        var (s, _) = await Stock();
        var one = await Sale(s);
        var two = await Sale(s);
        var invoices = new[] { one, two };
        var results = await Task.WhenAll(invoices.Select(x => Post(s.Maker, $"{s.Root}/sales/{x.Id}/reserve", new ReserveSalesRequest(3, s.Warehouse))));
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.Created);
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(2m, (await Balance(s)).ReservedQuantity);
        var winner = invoices[Array.FindIndex(results, x => x.StatusCode == HttpStatusCode.Created)];
        var loser = invoices.Single(x => x.Id != winner.Id);
        var key = Guid.NewGuid().ToString();
        var cancellations = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => Post(s.Maker, $"{s.Root}/sales/{winner.Id}/cancel",
            new CancelSalesInvoiceRequest(4, "Order withdrawn"), key)));
        Assert.All(cancellations, x => Assert.Equal(HttpStatusCode.OK, x.StatusCode));
        Assert.Equal(0m, (await Balance(s)).ReservedQuantity);
        Assert.Equal(2.5m, (await Balance(s)).Quantity);
        Assert.Equal("Cancelled", (await s.Maker.GetFromJsonAsync<SalesPostingResponse>($"{s.Root}/sales/{winner.Id}/fulfillment"))!.Status);
        await Reserve(s, loser.Id);
    }

    [Fact]
    public async Task Concurrent_post_and_cancel_have_one_winner_and_balanced_inventory()
    {
        var (s, _) = await Stock();
        var invoice = await Sale(s);
        await Reserve(s, invoice.Id);
        var path = $"{s.Root}/sales/{invoice.Id}";
        var results = await Task.WhenAll(Post(s.Checker, $"{path}/post", new TransitionRequest(4)),
            Post(s.Maker, $"{path}/cancel", new CancelSalesInvoiceRequest(4, "Withdrawn")));
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.Conflict);
        var current = (await s.Maker.GetFromJsonAsync<SalesInvoiceResponse>(path))!;
        var balance = await Balance(s);
        Assert.Equal(0m, balance.ReservedQuantity);
        Assert.Equal(current.Status == "Posted" ? 0.5m : 2.5m, balance.Quantity);
        Assert.Equal(current.Status == "Posted" ? 1 : 0,
            await factory.Inspect(db => db.Set<SalesPostingEntry>().CountAsync(x => x.InvoiceId == invoice.Id)));
    }

    [Fact]
    public async Task Purchase_rules_never_supply_sales_vat_and_published_sales_overlaps_are_rejected()
    {
        var (s, _) = await Stock(false);
        var invoice = await Sale(s);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Post(s.Maker, $"{s.Root}/sales/{invoice.Id}/reserve", new ReserveSalesRequest(3, s.Warehouse))).StatusCode);
        Assert.Equal(0m, (await Balance(s)).ReservedQuantity);
        Assert.Equal("ApprovedPendingPosting", (await s.Maker.GetFromJsonAsync<SalesInvoiceResponse>($"{s.Root}/sales/{invoice.Id}"))!.Status);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Post(s.Maker, $"{s.Root}/tax/rules", Rule() with { Treatment = "Sale" })).StatusCode);
        var rule = await Created<TaxRuleResponse>(s.Maker, $"{s.Root}/tax/rules", Rule() with { Treatment = "Sale", RecoverablePercent = 0 });
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(s.Maker, $"{s.Root}/tax/rules/{rule.Id}/publish", new TransitionRequest(1))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Post(s.Checker, $"{s.Root}/tax/rules/{rule.Id}/publish", new TransitionRequest(1))).StatusCode);
        var overlap = await Created<TaxRuleResponse>(s.Maker, $"{s.Root}/tax/rules", Rule() with { Treatment = "Sale", RecoverablePercent = 0 });
        Assert.Equal(HttpStatusCode.Conflict, (await Post(s.Checker, $"{s.Root}/tax/rules/{overlap.Id}/publish", new TransitionRequest(1))).StatusCode);
        await Reserve(s, invoice.Id);
    }

    [Fact]
    public async Task Purchase_reversal_cannot_consume_reserved_stock()
    {
        var (s, purchase) = await Stock();
        var invoice = await Sale(s);
        await Reserve(s, invoice.Id);
        var reversal = await Created<ReversalResponse>(s.Maker, $"{s.Purchase(purchase.Id)}/reversals",
            new RequestReversalRequest(5, new(2026, 10, 24), "Supplier return"));
        Assert.Equal(HttpStatusCode.Conflict, (await Post(s.Checker, $"{s.Purchase(purchase.Id)}/reversals/{reversal.Id}/approve", new TransitionRequest(6))).StatusCode);
        Assert.Equal(2.5m, (await Balance(s)).Quantity);
        Assert.Equal(2m, (await Balance(s)).ReservedQuantity);
        Assert.Equal(HttpStatusCode.OK, (await Post(s.Maker, $"{s.Root}/sales/{invoice.Id}/cancel", new CancelSalesInvoiceRequest(4, "Withdrawn"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Post(s.Checker, $"{s.Purchase(purchase.Id)}/reversals/{reversal.Id}/approve", new TransitionRequest(6))).StatusCode);
        Assert.Equal(0m, (await Balance(s)).Quantity);
    }

    [Fact]
    public async Task Posting_failure_after_stock_sql_rolls_back_and_same_key_retry_recovers()
    {
        var (s, _) = await Stock();
        var invoice = await Sale(s);
        await Reserve(s, invoice.Id);
        var interceptor = new FailFirstSalesCommit();
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddDbContext<Vat.Infrastructure.Persistence.VatDbContext>(o => o.AddInterceptors(interceptor))));
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = s.Checker.DefaultRequestHeaders.Authorization;
        var path = $"{s.Root}/sales/{invoice.Id}/post";
        var key = Guid.NewGuid().ToString();
        Assert.Equal(HttpStatusCode.InternalServerError, (await Post(client, path, new TransitionRequest(4), key)).StatusCode);
        Assert.Equal(2.5m, (await Balance(s)).Quantity);
        Assert.Equal(2m, (await Balance(s)).ReservedQuantity);
        Assert.Equal(0, await factory.Inspect(db => db.Set<SalesPostingEntry>().CountAsync(x => x.InvoiceId == invoice.Id)));
        Assert.Equal(1, await factory.Inspect(db => db.Outbox.CountAsync(x => x.AggregateId == invoice.Id)));
        Assert.Equal(1, await factory.Inspect(db => db.Set<CostSourceEvent>().CountAsync(x => x.CompanyId == s.Company)));
        Assert.Equal(1L, await factory.Inspect(db => db.Set<CostSourceHead>().Where(x => x.CompanyId == s.Company).Select(x => x.Sequence).SingleAsync()));
        Assert.Equal(HttpStatusCode.OK, (await Post(client, path, new TransitionRequest(4), key)).StatusCode);
        Assert.Equal(2L, await factory.Inspect(db => db.Set<CostSourceHead>().Where(x => x.CompanyId == s.Company).Select(x => x.Sequence).SingleAsync()));
    }

    [Fact]
    public async Task Multi_product_reservation_failure_rolls_back_previously_reserved_product()
    {
        var (s, _) = await Stock();
        await Created<ProductResponse>(s.Maker, $"{s.Root}/products", new CreateProductRequest("NO-STOCK", "Missing stock", "EA", "TEST-DOMESTIC"));
        var invoice = await Sale(s, lines: [new("ITEM", "Available", 1, 10), new("NO-STOCK", "Unavailable", 1, 10)]);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(s.Maker, $"{s.Root}/sales/{invoice.Id}/reserve", new ReserveSalesRequest(3, s.Warehouse))).StatusCode);
        Assert.Equal(0m, (await Balance(s)).ReservedQuantity);
        Assert.Equal(0, await factory.Inspect(db => db.Set<SalesFulfillment>().CountAsync(x => x.InvoiceId == invoice.Id)));
        Assert.Equal(3, (await s.Maker.GetFromJsonAsync<SalesInvoiceResponse>($"{s.Root}/sales/{invoice.Id}"))!.Version);
    }

    [Fact]
    public async Task Reservation_and_posting_require_scoped_permissions_branch_warehouse_and_enabled_gate()
    {
        var (s, _) = await Stock();
        var invoice = await Sale(s);
        var path = $"{s.Root}/sales/{invoice.Id}";
        var otherBranch = await Created<BranchResponse>(s.Maker, $"{s.Root}/branches", new CreateBranchRequest("Other branch"));
        var otherWarehouse = await Created<WarehouseResponse>(s.Maker, $"{s.Root}/warehouses", new CreateWarehouseRequest(otherBranch.Id, "OTHER", "Other"));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Post(s.Maker, $"{path}/reserve", new ReserveSalesRequest(3, otherWarehouse.Id))).StatusCode);
        using var noPermission = factory.Client(s.Tenant, s.Company, "checker", [Permissions.PurchasePost]);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(noPermission, $"{path}/reserve", new ReserveSalesRequest(3, s.Warehouse))).StatusCode);
        await Reserve(s, invoice.Id);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(noPermission, $"{path}/post", new TransitionRequest(4))).StatusCode);
        using var otherTenant = factory.Client(Guid.NewGuid(), s.Company);
        using var otherCompany = factory.Client(s.Tenant, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.NotFound, (await otherTenant.GetAsync($"{path}/fulfillment")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await otherCompany.GetAsync($"{path}/fulfillment")).StatusCode);
        Assert.Empty((await otherTenant.GetFromJsonAsync<SalesLedgerPage>($"{s.Root}/sales/ledger"))!.Entries);
        using var disabledHost = factory.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["Posting:Enabled"] = "false" })));
        using var disabled = disabledHost.CreateClient();
        disabled.DefaultRequestHeaders.Authorization = s.Checker.DefaultRequestHeaders.Authorization;
        Assert.Equal(HttpStatusCode.Conflict, (await Post(disabled, $"{path}/post", new TransitionRequest(4))).StatusCode);
        Assert.Equal(2.5m, (await Balance(s)).Quantity);
        Assert.Equal(2m, (await Balance(s)).ReservedQuantity);
    }

    [Fact]
    public async Task Reserved_invoice_blocks_close_and_damaged_posting_blocks_final_close()
    {
        var (s, _) = await Stock();
        var invoice = await Sale(s);
        await Reserve(s, invoice.Id);
        var period = (await s.Maker.GetFromJsonAsync<PeriodResponse[]>($"{s.Root}/periods"))!.Single();
        var closeRoot = $"{s.Root}/periods/{period.Id}";
        Assert.Equal(HttpStatusCode.Conflict, (await Post(s.Maker, $"{closeRoot}/request-close", new PeriodActionRequest(1, "Close"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Post(s.Checker, $"{s.Root}/sales/{invoice.Id}/post", new TransitionRequest(4))).StatusCode);
        await factory.Inspect(db => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE sales.posting_entries SET \"OutputTaxAmount\" = \"OutputTaxAmount\" + 1 WHERE \"InvoiceId\" = {invoice.Id}"));
        Assert.False((await s.Maker.GetFromJsonAsync<SalesReconciliation>($"{s.Root}/sales/{invoice.Id}/reconciliation"))!.IsReconciled);
        Assert.Equal(HttpStatusCode.OK, (await Post(s.Maker, $"{closeRoot}/request-close", new PeriodActionRequest(1, "Close"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(s.Checker, $"{closeRoot}/close", new PeriodActionRequest(2, "Review"))).StatusCode);
        Assert.Equal("Closing", (await s.Maker.GetFromJsonAsync<PeriodResponse>(closeRoot))!.Status);
    }

    private sealed class FailFirstSalesCommit : SaveChangesInterceptor
    {
        private int _failed;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (data.Context!.ChangeTracker.Entries<SalesPostingEntry>().Any(x => x.State == EntityState.Added) && Interlocked.Exchange(ref _failed, 1) == 0)
                throw new InvalidOperationException("Injected sales ledger failure after stock SQL.");
            return ValueTask.FromResult(result);
        }
    }

}
