using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vat.Application;
using Vat.Application.Contracts;
using Vat.Application.Security;
using Vat.Domain.Sales;
using Vat.Domain.Purchasing;
using Xunit;

namespace Vat.Tests;

[Collection("PostgreSQL integration")]
public sealed class PaymentCorrectionApiTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
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

    private async Task<SetupData> Setup(bool publishRule = true)
    {
        var tenant = Guid.NewGuid();
        var company = Guid.NewGuid();
        var maker = factory.Client(tenant, company);
        var checker = factory.Client(tenant, company, "checker");
        var root = $"/api/v1/companies/{company}";
        await Created<CompanyResponse>(maker, "/api/v1/companies", new CreateCompanyRequest(company, "Posting pilot", Guid.NewGuid().ToString()));
        await Created<PeriodResponse>(maker, $"{root}/periods", new CreatePeriodRequest("Test scope", new(2026, 1, 1), new(2026, 11, 1)));
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
            lines ?? [new(product, "Source description", 10m, 100m)]));
        Assert.Equal(HttpStatusCode.OK, (await Post(setup.Maker, $"{setup.Purchase(purchase.Id)}/submit", new TransitionRequest(1))).StatusCode);
        var approved = await Post(setup.Checker, $"{setup.Purchase(purchase.Id)}/approve", new TransitionRequest(2));
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        return (await approved.Content.ReadFromJsonAsync<PurchaseResponse>())!;
    }

    private static Task<PostingPlanResponse> Prepare(SetupData setup, Guid purchaseId, long version = 3) =>
        Created<PostingPlanResponse>(setup.Maker, $"{setup.Purchase(purchaseId)}/posting-plans", new PreparePostingRequest(version, setup.Warehouse));

    private async Task<(SetupData Setup, PurchaseResponse Purchase)> Stock(bool salesRule = true, decimal salesRate = 5m)
    {
        var s = await Setup();
        var purchase = await Approve(s);
        var plan = await Prepare(s, purchase.Id);
        Assert.Equal(HttpStatusCode.OK, (await Post(s.Checker, $"{s.Purchase(purchase.Id)}/posting-plans/{plan.Id}/post", new TransitionRequest(4))).StatusCode);
        if (salesRule)
        {
            var rule = await Created<TaxRuleResponse>(s.Maker, $"{s.Root}/tax/rules",
                Rule() with { RatePercent = salesRate, RecoverablePercent = 0m, Treatment = "Sale" });
            Assert.Equal("Sale", rule.Treatment);
            Assert.Equal(HttpStatusCode.OK, (await Post(s.Checker, $"{s.Root}/tax/rules/{rule.Id}/publish", new TransitionRequest(1))).StatusCode);
        }
        return (s, purchase);
    }

    private static async Task<SalesInvoiceResponse> Sale(SetupData s, decimal quantity = 2m, IReadOnlyList<SalesInvoiceLineInput>? lines = null)
    {
        var list = (await s.Maker.GetFromJsonAsync<CustomerResponse[]>($"{s.Root}/customers"))!;
        var customer = list.FirstOrDefault() ?? await Created<CustomerResponse>(s.Maker, $"{s.Root}/customers", new CreateCustomerRequest("CUSTOMER", "Customer"));
        var invoice = await Created<SalesInvoiceResponse>(s.Maker, $"{s.Root}/sales", new CreateSalesInvoiceRequest(
            s.Branch, Guid.NewGuid().ToString(), "CUSTOMER", new(2026, 10, 23), lines ?? [new("ITEM", "Goods", quantity, 100m)], customer.Id));
        var path = $"{s.Root}/sales/{invoice.Id}";
        Assert.Equal(HttpStatusCode.OK, (await Post(s.Maker, $"{path}/submit", new TransitionRequest(1))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Post(s.Checker, $"{path}/approve", new TransitionRequest(2))).StatusCode);
        return invoice;
    }

    private static Task<SalesPostingResponse> Reserve(SetupData s, Guid id) => Created<SalesPostingResponse>(s.Maker,
        $"{s.Root}/sales/{id}/reserve", new ReserveSalesRequest(3, s.Warehouse));

    private static async Task<StockBalanceResponse> Balance(SetupData s) => (await s.Maker.GetFromJsonAsync<StockBalanceResponse[]>(
        $"{s.Root}/inventory/balances?warehouseId={s.Warehouse}"))!.Single();

    private async Task<(SetupData Setup, SalesInvoiceResponse Invoice)> PostedSale()
    {
        var (s, _) = await Stock();
        var invoice = await Sale(s, lines: [new("ITEM", "First line", 1, 100), new("ITEM", "Second line", 1, 100)]);
        await Reserve(s, invoice.Id);
        var posted = await Post(s.Checker, $"{s.Root}/sales/{invoice.Id}/post", new TransitionRequest(4));
        Assert.Equal(HttpStatusCode.OK, posted.StatusCode);
        return (s, (await posted.Content.ReadFromJsonAsync<SalesInvoiceResponse>())!);
    }


    private static CreatePaymentRequest Receipt(Guid customer, decimal amount = 300, IReadOnlyList<PaymentAllocationInput>? lines = null) =>
        new(customer, Guid.NewGuid().ToString(), "Bank", "BANK-STATEMENT-001", new(2026, 10, 24), amount, lines);
    private static Task<PaymentResponse> Payment(SetupData s, CreatePaymentRequest request) => Created<PaymentResponse>(s.Maker, s.Root + "/payments", request);
    private static Task<HttpResponseMessage> PostPayment(SetupData s, PaymentResponse p, string? key = null) =>
        Post(s.Checker, s.Root + $"/payments/{p.Payment.Id}/approve", new TransitionRequest(p.Payment.Version), key);
    private static async Task<CustomerReceivables> Receivables(SetupData s, Guid customer) =>
        (await s.Maker.GetFromJsonAsync<CustomerReceivables>(s.Root + $"/customers/{customer}/receivables"))!;


    private static async Task<PaymentResponse> Paid(SetupData s,Guid customer,decimal amount=300,IReadOnlyList<PaymentAllocationInput>? lines=null)
    {
        var p=await Payment(s,Receipt(customer,amount,lines)); Assert.Equal(HttpStatusCode.OK,(await PostPayment(s,p)).StatusCode);
        return (await s.Maker.GetFromJsonAsync<PaymentResponse>(s.Root+$"/payments/{p.Payment.Id}"))!;
    }
    private static Task<PaymentCorrectionResponse> Reversal(SetupData s,PaymentResponse p,DateOnly? date=null) =>
        Created<PaymentCorrectionResponse>(s.Maker,s.Root+$"/payments/{p.Payment.Id}/reversals",new RequestReceiptReversal(p.Payment.Version,date??new(2026,10,26),"Bounced collection","BANK-REVERSAL-EVIDENCE"));
    private static Task<PaymentCorrectionResponse> Refund(SetupData s,PaymentResponse p,decimal amount,IReadOnlyList<PaymentReleaseInput>? releases=null,DateOnly? date=null) =>
        Created<PaymentCorrectionResponse>(s.Maker,s.Root+$"/payments/{p.Payment.Id}/refunds",new RequestCustomerRefund(p.Payment.Version,date??new(2026,10,25),amount,"Bank","Return customer credit","REFUND-EVIDENCE",releases));
    private static string Decision(SetupData s,PaymentCorrectionResponse n,string action="approve") => s.Root+$"/payments/{n.PaymentId}/{(n.Mode=="Reversal"?"reversals":"refunds")}/{n.Id}/{action}";
    private static Task<HttpResponseMessage> ApproveCorrection(SetupData s,PaymentCorrectionResponse n) => Post(s.Checker,Decision(s,n),new TransitionRequest(n.SourceVersion));
    private static async Task<PaymentResponse> ReadPayment(SetupData s,Guid id) => (await s.Maker.GetFromJsonAsync<PaymentResponse>(s.Root+$"/payments/{id}"))!;

    [Fact]
    public async Task Full_receipt_reversal_preserves_original_rows_releases_all_batches_and_restores_debt_asof_correction_date()
    {
        var (s,invoice)=await PostedSale(); var p=await Paid(s,invoice.CustomerId!.Value,300,[new(invoice.Id,100)]);
        Assert.Equal(HttpStatusCode.OK,(await Post(s.Maker,s.Root+$"/payments/{p.Payment.Id}/allocate",new AllocatePaymentRequest(2,new(2026,10,25),[new(invoice.Id,110)]))).StatusCode);
        p=await ReadPayment(s,p.Payment.Id); var originalIds=p.Allocations.Select(x=>x.Id).ToArray(); var stock=await Balance(s);
        var n=await Reversal(s,p); Assert.Equal(300,n.Amount); Assert.Equal(210,n.Releases.Sum(x=>x.Amount)); Assert.Equal(4,n.SourceVersion);
        Assert.Equal(HttpStatusCode.OK,(await ApproveCorrection(s,n)).StatusCode);
        p=await ReadPayment(s,p.Payment.Id); Assert.Equal("Reversed",p.Payment.Status); Assert.Equal(5,p.Payment.Version);
        Assert.Equal(300,p.Payment.Amount); Assert.Equal(300,p.ReturnedAmount); Assert.Equal(0,p.NetCollectedAmount); Assert.Equal(0,p.AllocatedAmount); Assert.Equal(0,p.UnallocatedAmount);
        Assert.Equal(originalIds,p.Allocations.Select(x=>x.Id)); Assert.Equal(2,p.AllocationReleases!.Count);
        var report=await Receivables(s,invoice.CustomerId.Value); Assert.Equal(300,report.CollectedAmount); Assert.Equal(300,report.ReturnedAmount); Assert.Equal(210,report.Balance); Assert.Equal(210,report.Invoices.Single().OutstandingAmount);
        var before=(await s.Maker.GetFromJsonAsync<CustomerAgeing>(s.Root+$"/customers/{invoice.CustomerId}/ageing?asOf=2026-10-25"))!;
        var after=(await s.Maker.GetFromJsonAsync<CustomerAgeing>(s.Root+$"/customers/{invoice.CustomerId}/ageing?asOf=2026-10-26"))!;
        Assert.Equal(-90,before.Balance); Assert.Equal(0,before.ReturnedAmount); Assert.Equal(210,after.Balance); Assert.Equal(300,after.ReturnedAmount);
        var statement=(await s.Maker.GetFromJsonAsync<CustomerStatement>(s.Root+$"/customers/{invoice.CustomerId}/statement?from=2026-10-01&until=2026-11-01"))!;
        Assert.Equal(new[]{"Invoice","Payment","ReceiptReversal"},statement.Lines.Select(x=>x.Type)); Assert.Equal(210,statement.ClosingBalance);
        Assert.Equal(stock.Quantity,(await Balance(s)).Quantity); Assert.Equal(5,(await s.Maker.GetFromJsonAsync<SalesInvoiceResponse>(s.Root+$"/sales/{invoice.Id}"))!.Version);
        Assert.Equal(HttpStatusCode.Conflict,(await Post(s.Maker,s.Root+$"/payments/{p.Payment.Id}/allocate",new AllocatePaymentRequest(5,new(2026,10,27),[new(invoice.Id,1)]))).StatusCode);
    }
    [Fact]
    public async Task Partial_advance_refund_then_invoice_credit_refund_and_remaining_reversal_cannot_return_money_twice()
    {
        var (s,invoice)=await PostedSale(); var p=await Paid(s,invoice.CustomerId!.Value,300,[new(invoice.Id,210)]);
        var first=await Refund(s,p,90); Assert.Empty(first.Releases); Assert.Equal(HttpStatusCode.OK,(await ApproveCorrection(s,first)).StatusCode);
        p=await ReadPayment(s,p.Payment.Id); Assert.Equal("Posted",p.Payment.Status); Assert.Equal(210,p.NetCollectedAmount); Assert.Equal(210,p.AllocatedAmount); Assert.Equal(0,p.UnallocatedAmount);
        var line=(await s.Maker.GetFromJsonAsync<SalesReturnableResponse>(s.Root+$"/sales/{invoice.Id}/returnable-lines"))!.Lines.First();
        var credit=await Created<SalesCreditResponse>(s.Maker,s.Root+$"/sales/{invoice.Id}/price-credits",new RequestSalesPriceCredit(5,new(2026,10,25),"Discount","PRICE-EVIDENCE",[new(line.LineId,20)]));
        Assert.Equal(HttpStatusCode.OK,(await Post(s.Checker,s.Root+$"/sales/{invoice.Id}/credit-notes/{credit.Id}/approve",new TransitionRequest(credit.SourceVersion))).StatusCode);
        var second=await Refund(s,p,21,[new(p.Allocations.Single().Id,21)],new(2026,10,26)); Assert.Equal(HttpStatusCode.OK,(await ApproveCorrection(s,second)).StatusCode);
        p=await ReadPayment(s,p.Payment.Id); Assert.Equal(111,p.ReturnedAmount); Assert.Equal(189,p.NetCollectedAmount); Assert.Equal(189,p.AllocatedAmount); Assert.Equal(0,(await Receivables(s,invoice.CustomerId.Value)).Balance);
        var reversal=await Reversal(s,p,new(2026,10,27)); Assert.Equal(189,reversal.Amount); Assert.Equal(189,reversal.Releases.Sum(x=>x.Amount));
        Assert.Equal(HttpStatusCode.OK,(await ApproveCorrection(s,reversal)).StatusCode);
        p=await ReadPayment(s,p.Payment.Id); Assert.Equal(300,p.ReturnedAmount); Assert.Equal(0,p.NetCollectedAmount); Assert.Equal(0,p.AllocatedAmount); Assert.Equal("Reversed",p.Payment.Status);
        Assert.Equal(189,(await Receivables(s,invoice.CustomerId.Value)).Balance);
        Assert.Equal(300,await factory.Inspect(db=>db.Set<PaymentCashAdjustment>().Where(x=>x.CompanyId==s.Company).SumAsync(x=>-x.Amount)));
    }
    [Fact]
    public async Task Full_unallocated_advance_refund_is_terminal_and_original_collection_stays_visible()
    {
        var s=await Setup(); var customer=await Created<CustomerResponse>(s.Maker,s.Root+"/customers",new CreateCustomerRequest("ADVANCE","Advance customer"));
        var p=await Paid(s,customer.Id); var n=await Refund(s,p,300); Assert.Equal(HttpStatusCode.OK,(await ApproveCorrection(s,n)).StatusCode);
        p=await ReadPayment(s,p.Payment.Id); Assert.Equal("Refunded",p.Payment.Status); Assert.Equal(0,p.NetCollectedAmount); Assert.Equal(300,p.ReturnedAmount);
        Assert.Equal(0,(await Receivables(s,customer.Id)).Balance);
        Assert.Equal(HttpStatusCode.Conflict,(await Post(s.Maker,s.Root+$"/payments/{p.Payment.Id}/refunds",new RequestCustomerRefund(p.Payment.Version,new(2026,10,26),1,"Cash","Again","EVIDENCE"))).StatusCode);
        Assert.Equal(1,await factory.Inspect(db=>db.Set<CustomerPayment>().CountAsync(x=>x.CompanyId==s.Company)));
    }
    [Fact]
    public async Task Refund_rejects_invalid_credit_funding_precision_evidence_and_foreign_release_without_mutation()
    {
        var (s,invoice)=await PostedSale(); var p=await Paid(s,invoice.CustomerId!.Value,300,[new(invoice.Id,100)]);
        var url=s.Root+$"/payments/{p.Payment.Id}/refunds";
        var body=new RequestCustomerRefund(2,new(2026,10,25),90,"Bank","Refund","EVIDENCE");
        Assert.Equal(HttpStatusCode.Conflict,(await Post(s.Maker,url,body with { Amount=91 })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await Post(s.Maker,url,body with { Releases=[new(p.Allocations.Single().Id,10)] })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await Post(s.Maker,url,body with { Releases=[new(Guid.NewGuid(),10)] })).StatusCode);
        foreach(var invalid in new[]{body with{Amount=1.00001m},body with{Method="Card"},body with{EvidenceReference=" "},body with{Releases=[new(p.Allocations.Single().Id,1),new(p.Allocations.Single().Id,1)]},body with{CorrectionDate=new(2026,10,23)}})
            Assert.Equal(HttpStatusCode.UnprocessableEntity,(await Post(s.Maker,url,invalid)).StatusCode);
        Assert.Equal("Posted",(await ReadPayment(s,p.Payment.Id)).Payment.Status);
        Assert.Equal(0,await factory.Inspect(db=>db.Set<PaymentCorrection>().CountAsync(x=>x.CompanyId==s.Company)));
    }
    [Fact]
    public async Task Refund_cannot_be_backdated_before_the_sales_credit_that_funds_it()
    {
        var (s,invoice)=await PostedSale(); var p=await Paid(s,invoice.CustomerId!.Value,210,[new(invoice.Id,210)]);
        var line=(await s.Maker.GetFromJsonAsync<SalesReturnableResponse>(s.Root+$"/sales/{invoice.Id}/returnable-lines"))!.Lines.First();
        var credit=await Created<SalesCreditResponse>(s.Maker,s.Root+$"/sales/{invoice.Id}/price-credits",new RequestSalesPriceCredit(5,new(2026,10,25),"Discount","PRICE-EVIDENCE",[new(line.LineId,20)]));
        Assert.Equal(HttpStatusCode.OK,(await Post(s.Checker,s.Root+$"/sales/{invoice.Id}/credit-notes/{credit.Id}/approve",new TransitionRequest(credit.SourceVersion))).StatusCode);
        var request=new RequestCustomerRefund(2,new(2026,10,24),21,"Bank","Return overpayment","EVIDENCE",[new(p.Allocations.Single().Id,21)]);
        Assert.Equal(HttpStatusCode.Conflict,(await Post(s.Maker,s.Root+$"/payments/{p.Payment.Id}/refunds",request)).StatusCode);
        Assert.Equal("Posted",(await ReadPayment(s,p.Payment.Id)).Payment.Status);
        var n=await Refund(s,p,21,request.Releases,new(2026,10,25)); Assert.Equal(HttpStatusCode.OK,(await ApproveCorrection(s,n)).StatusCode);
        Assert.Equal(0,(await Receivables(s,invoice.CustomerId.Value)).Balance);
    }
    [Fact]
    public async Task Concurrent_correction_allocation_and_approve_cancel_races_have_one_winner()
    {
        var (s,invoice)=await PostedSale(); var p=await Paid(s,invoice.CustomerId!.Value,300,[new(invoice.Id,100)]);
        var results=await Task.WhenAll(Post(s.Maker,s.Root+$"/payments/{p.Payment.Id}/refunds",new RequestCustomerRefund(2,new(2026,10,25),90,"Bank","Return credit","EVIDENCE")),
            Post(s.Maker,s.Root+$"/payments/{p.Payment.Id}/allocate",new AllocatePaymentRequest(2,new(2026,10,25),[new(invoice.Id,50)])));
        Assert.Single(results,x=>x.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK); Assert.Single(results,x=>x.StatusCode==HttpStatusCode.Conflict);
        if(results[0].StatusCode==HttpStatusCode.Created)
        {
            var old=(await results[0].Content.ReadFromJsonAsync<PaymentCorrectionResponse>())!;
            Assert.Equal(HttpStatusCode.OK,(await Post(s.Maker,Decision(s,old,"cancel"),new CancelSalesInvoiceRequest(old.SourceVersion,"Withdrawn"))).StatusCode);
        }
        p=await ReadPayment(s,p.Payment.Id); var n=await Refund(s,p,90,new List<PaymentReleaseInput>());
        results=await Task.WhenAll(ApproveCorrection(s,n),Post(s.Maker,Decision(s,n,"cancel"),new CancelSalesInvoiceRequest(n.SourceVersion,"Withdrawn")));
        Assert.Single(results,x=>x.StatusCode==HttpStatusCode.OK); Assert.Single(results,x=>x.StatusCode==HttpStatusCode.Conflict);
        var returned=(await ReadPayment(s,p.Payment.Id)).ReturnedAmount; Assert.True(returned is 0 or 90);
        Assert.Equal(210-300+returned,(await Receivables(s,invoice.CustomerId.Value)).Balance);
    }
    [Fact]
    public async Task Competing_refunds_on_two_receipts_revalidate_shared_customer_credit_at_approval()
    {
        var (s,invoice)=await PostedSale(); var p1=await Paid(s,invoice.CustomerId!.Value,300,[new(invoice.Id,100)]); var p2=await Paid(s,invoice.CustomerId.Value,50);
        var n1=await Refund(s,p1,100); var n2=await Refund(s,p2,50);
        var results=await Task.WhenAll(ApproveCorrection(s,n1),ApproveCorrection(s,n2));
        Assert.Single(results,x=>x.StatusCode==HttpStatusCode.OK); Assert.Single(results,x=>x.StatusCode==HttpStatusCode.Conflict);
        var report=await Receivables(s,invoice.CustomerId.Value); Assert.True(report.ReturnedAmount is 50 or 100); Assert.Equal(-140+report.ReturnedAmount,report.Balance);
    }
    private sealed class FailCorrectionCommit : SaveChangesInterceptor
    {
        private int remaining=1;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,InterceptionResult<int> result,CancellationToken cancellationToken=default)
        {
            if(eventData.Context!.ChangeTracker.Entries<PaymentCashAdjustment>().Any() && Interlocked.Exchange(ref remaining,0)==1) throw new InvalidOperationException("Injected correction failure");
            return ValueTask.FromResult(result);
        }
    }
    [Fact]
    public async Task Failed_correction_commit_rolls_back_and_same_key_concurrent_retries_post_once()
    {
        var (s,invoice)=await PostedSale(); var p=await Paid(s,invoice.CustomerId!.Value,300,[new(invoice.Id,100)]); var n=await Reversal(s,p);
        var fault=new FailCorrectionCommit(); using var host=factory.WithWebHostBuilder(builder=>builder.ConfigureTestServices(services=>services.AddDbContext<Vat.Infrastructure.Persistence.VatDbContext>(options=>options.AddInterceptors(fault))));
        using var client=host.CreateClient(); client.DefaultRequestHeaders.Authorization=s.Checker.DefaultRequestHeaders.Authorization;
        var key=Guid.NewGuid().ToString(); Assert.Equal(HttpStatusCode.InternalServerError,(await Post(client,Decision(s,n),new TransitionRequest(n.SourceVersion),key)).StatusCode);
        Assert.Equal(0,(await ReadPayment(s,p.Payment.Id)).ReturnedAmount); Assert.Equal("CorrectionPending",(await ReadPayment(s,p.Payment.Id)).Payment.Status);
        Assert.Equal(0,await factory.Inspect(db=>db.Set<PaymentAllocationRelease>().CountAsync(x=>x.CompanyId==s.Company)));
        var retries=await Task.WhenAll(Enumerable.Range(0,3).Select(_=>Post(client,Decision(s,n),new TransitionRequest(n.SourceVersion),key)));
        Assert.All(retries,x=>Assert.Equal(HttpStatusCode.OK,x.StatusCode)); Assert.Single((await Task.WhenAll(retries.Select(x=>x.Content.ReadAsStringAsync()))).Distinct());
        Assert.Equal(1,await factory.Inspect(db=>db.Set<PaymentCashAdjustment>().CountAsync(x=>x.CompanyId==s.Company)));
        Assert.Equal(1,await factory.Inspect(db=>db.Outbox.CountAsync(x=>x.CompanyId==s.Company && x.EventType==nameof(CustomerPaymentCorrectionPostedV1))));
    }
    [Fact]
    public async Task Later_period_correction_preserves_closed_collection_snapshot_and_blocks_pending_close()
    {
        var (s,invoice)=await PostedSale(); var p=await Paid(s,invoice.CustomerId!.Value,300,[new(invoice.Id,210)]);
        var original=(await s.Maker.GetFromJsonAsync<PeriodResponse[]>(s.Root+"/periods"))!.Single();
        Assert.Equal(HttpStatusCode.OK,(await Post(s.Maker,s.Root+$"/periods/{original.Id}/request-close",new PeriodActionRequest(1,"Review"))).StatusCode);
        var closed=await Post(s.Checker,s.Root+$"/periods/{original.Id}/close",new PeriodActionRequest(2,"Checked")); Assert.Equal(HttpStatusCode.OK,closed.StatusCode);
        var snapshot=(await closed.Content.ReadFromJsonAsync<PeriodResponse>())!.Snapshot!;
        var later=await Created<PeriodResponse>(s.Maker,s.Root+"/periods",new CreatePeriodRequest("November",new(2026,11,1),new(2026,12,1)));
        var n=await Reversal(s,p,new(2026,11,2));
        Assert.Equal(HttpStatusCode.Conflict,(await Post(s.Maker,s.Root+$"/periods/{later.Id}/request-close",new PeriodActionRequest(1,"Review"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await ApproveCorrection(s,n)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await Post(s.Maker,s.Root+$"/periods/{later.Id}/request-close",new PeriodActionRequest(1,"Review"))).StatusCode);
        closed=await Post(s.Checker,s.Root+$"/periods/{later.Id}/close",new PeriodActionRequest(2,"Checked")); Assert.Equal(HttpStatusCode.OK,closed.StatusCode);
        var checkpoint=(await closed.Content.ReadFromJsonAsync<PeriodResponse>())!.Snapshot!.Payments!;
        Assert.Equal(0,checkpoint.CollectedAmount); Assert.Equal(300,checkpoint.ReturnedAmount); Assert.Equal(210,checkpoint.ReleasedAmount); Assert.Single(checkpoint.CashAdjustmentIds!);
        var old=(await s.Maker.GetFromJsonAsync<PeriodResponse>(s.Root+$"/periods/{original.Id}"))!.Snapshot!; Assert.Equal(snapshot.LedgerFingerprint,old.LedgerFingerprint); Assert.Equal(300,old.Payments!.CollectedAmount); Assert.Equal(0,old.Payments.ReturnedAmount);
    }
    [Fact]
    public async Task Correction_policies_scope_checker_gate_mode_and_cancellation_are_enforced()
    {
        var (s,invoice)=await PostedSale(); var p=await Paid(s,invoice.CustomerId!.Value,300,[new(invoice.Id,100)]); var n=await Reversal(s,p);
        Assert.Equal(HttpStatusCode.Forbidden,(await Post(s.Maker,Decision(s,n),new TransitionRequest(n.SourceVersion))).StatusCode);
        using var wrongTenant=factory.Client(Guid.NewGuid(),s.Company); using var wrongCompany=factory.Client(s.Tenant,Guid.NewGuid());
        using var refundOnly=factory.Client(s.Tenant,s.Company,"refund-checker",permissions:[Permissions.PaymentRefundApprove]);
        Assert.Equal(HttpStatusCode.NotFound,(await wrongTenant.GetAsync(s.Root+$"/payments/{p.Payment.Id}/corrections/{n.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await wrongCompany.GetAsync(s.Root+$"/payments/{p.Payment.Id}/corrections/{n.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await Post(refundOnly,Decision(s,n),new TransitionRequest(n.SourceVersion))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await Post(refundOnly,s.Root+$"/payments/{p.Payment.Id}/refunds/{n.Id}/approve",new TransitionRequest(n.SourceVersion))).StatusCode);
        using var host=factory.WithWebHostBuilder(builder=>builder.ConfigureAppConfiguration((_,config)=>config.AddInMemoryCollection(new Dictionary<string,string?>{["Posting:Enabled"]="false"})));
        using var disabled=host.CreateClient(); disabled.DefaultRequestHeaders.Authorization=s.Checker.DefaultRequestHeaders.Authorization;
        Assert.Equal(HttpStatusCode.Conflict,(await Post(disabled,Decision(s,n),new TransitionRequest(n.SourceVersion))).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await Post(s.Maker,Decision(s,n,"cancel"),new CancelSalesInvoiceRequest(n.SourceVersion,"Withdrawn"))).StatusCode);
        p=await ReadPayment(s,p.Payment.Id); Assert.Equal("Posted",p.Payment.Status); Assert.Equal(4,p.Payment.Version); Assert.Equal(300,p.NetCollectedAmount);
        var replacement=await Refund(s,p,50); Assert.Equal(HttpStatusCode.OK,(await ApproveCorrection(s,replacement)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,(await Post(s.Maker,s.Root+$"/payments/{p.Payment.Id}/allocate",new AllocatePaymentRequest(6,new(2026,10,24),[new(invoice.Id,1)]))).StatusCode);
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Missing_cash_or_release_journal_blocks_reports_and_final_period_close(bool removeCash)
    {
        var (s,invoice)=await PostedSale(); var p=await Paid(s,invoice.CustomerId!.Value,300,[new(invoice.Id,100)]); var n=await Reversal(s,p); Assert.Equal(HttpStatusCode.OK,(await ApproveCorrection(s,n)).StatusCode);
        var period=(await s.Maker.GetFromJsonAsync<PeriodResponse[]>(s.Root+"/periods"))!.Single();
        Assert.Equal(HttpStatusCode.OK,(await Post(s.Maker,s.Root+$"/periods/{period.Id}/request-close",new PeriodActionRequest(1,"Review"))).StatusCode);
        await factory.Inspect(async db=>removeCash?await db.Set<PaymentCashAdjustment>().Where(x=>x.CompanyId==s.Company).ExecuteDeleteAsync():await db.Set<PaymentAllocationRelease>().Where(x=>x.CompanyId==s.Company).ExecuteDeleteAsync());
        Assert.Equal(HttpStatusCode.Conflict,(await s.Maker.GetAsync(s.Root+$"/customers/{invoice.CustomerId}/receivables")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await s.Maker.GetAsync(s.Root+$"/customers/{invoice.CustomerId}/ageing?asOf=2026-10-27")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await Post(s.Checker,s.Root+$"/periods/{period.Id}/close",new PeriodActionRequest(2,"Checked"))).StatusCode);
    }
}
