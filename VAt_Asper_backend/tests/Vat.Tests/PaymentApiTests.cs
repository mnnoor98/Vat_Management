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
using Vat.Domain.Purchasing;
using Xunit;

namespace Vat.Tests;

[Collection("PostgreSQL integration")]
public sealed class PaymentApiTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
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

    [Fact]
    public async Task Partial_payment_and_later_allocation_preserve_stock_vat_and_show_unallocated_credit()
    {
        var (s, invoice) = await PostedSale(); var customer = invoice.CustomerId!.Value;
        var stock = await Balance(s);
        var p = await Payment(s, Receipt(customer, lines: [new(invoice.Id, 100)]));
        Assert.Equal("Pending", p.Payment.Status); Assert.Equal(0, (await Receivables(s, customer)).CollectedAmount);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(s.Maker, s.Root + $"/payments/{p.Payment.Id}/approve", new TransitionRequest(1))).StatusCode);
        var posted = await PostPayment(s, p); Assert.Equal(HttpStatusCode.OK, posted.StatusCode);
        p = (await posted.Content.ReadFromJsonAsync<PaymentResponse>())!;
        Assert.Equal(100, p.AllocatedAmount); Assert.Equal(200, p.UnallocatedAmount);
        var report = await Receivables(s, customer); Assert.Equal(210, report.SalesGrossAmount); Assert.Equal(-90, report.Balance);
        Assert.Equal(110, report.Invoices.Single().OutstandingAmount);
        var allocation = await Post(s.Maker, s.Root + $"/payments/{p.Payment.Id}/allocate", new AllocatePaymentRequest(2, new(2026, 10, 25), [new(invoice.Id, 110)]));
        Assert.Equal(HttpStatusCode.OK, allocation.StatusCode);
        p = (await allocation.Content.ReadFromJsonAsync<PaymentResponse>())!;
        Assert.Equal(90, p.UnallocatedAmount); Assert.Equal(2, p.Allocations.Count);
        Assert.Equal(0, (await Receivables(s, customer)).Invoices.Single().OutstandingAmount);
        Assert.Equal(stock.Quantity, (await Balance(s)).Quantity);
        Assert.Equal(5, (await s.Maker.GetFromJsonAsync<SalesInvoiceResponse>(s.Root + $"/sales/{invoice.Id}"))!.Version);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(s.Maker, s.Root + $"/payments/{p.Payment.Id}/cancel", new CancelSalesInvoiceRequest(3, "Cannot cancel posted receipt"))).StatusCode);
        Assert.Equal(1, await factory.Inspect(db => db.Outbox.CountAsync(x => x.CompanyId == s.Company && x.EventType == nameof(CustomerPaymentPostedV1))));
        Assert.Equal(1, await factory.Inspect(db => db.Outbox.CountAsync(x => x.CompanyId == s.Company && x.EventType == nameof(CustomerPaymentAllocatedV1))));
    }
    [Fact]
    public async Task Competing_receipts_cannot_overallocate_invoice_and_pending_loser_can_be_cancelled()
    {
        var (s, invoice) = await PostedSale(); var customer = invoice.CustomerId!.Value;
        var p1 = await Payment(s, Receipt(customer, 150, [new(invoice.Id, 150)]));
        var p2 = await Payment(s, Receipt(customer, 150, [new(invoice.Id, 150)]));
        var results = await Task.WhenAll(PostPayment(s, p1), PostPayment(s, p2));
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.OK); Assert.Single(results, x => x.StatusCode == HttpStatusCode.Conflict);
        var posted = (await results.Single(x => x.StatusCode == HttpStatusCode.OK).Content.ReadFromJsonAsync<PaymentResponse>())!;
        var loser = posted.Payment.Id == p1.Payment.Id ? p2 : p1;
        Assert.Equal(HttpStatusCode.OK, (await Post(s.Maker, s.Root + $"/payments/{loser.Payment.Id}/cancel", new CancelSalesInvoiceRequest(1, "Duplicate collection request"))).StatusCode);
        var report = await Receivables(s, customer); Assert.Equal(150, report.CollectedAmount); Assert.Equal(60, report.Balance);
        var advance = await Payment(s, Receipt(customer, 100)); Assert.Equal(HttpStatusCode.OK, (await PostPayment(s, advance)).StatusCode);
        var path = s.Root + $"/payments/{advance.Payment.Id}/allocate";
        results = await Task.WhenAll(Post(s.Maker, path, new AllocatePaymentRequest(2, new(2026,10,25), [new(invoice.Id,40)])),
            Post(s.Maker, path, new AllocatePaymentRequest(2, new(2026,10,25), [new(invoice.Id,40)])));
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.OK); Assert.Single(results, x => x.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(20, (await Receivables(s, customer)).Invoices.Single().OutstandingAmount);
    }
    [Fact]
    public async Task Receipts_support_multiple_invoices_and_enforce_remaining_payment_budget()
    {
        var (s, first) = await PostedSale(); var second = await Sale(s); await Reserve(s, second.Id);
        Assert.Equal(HttpStatusCode.OK, (await Post(s.Checker, s.Root + $"/sales/{second.Id}/post", new TransitionRequest(4))).StatusCode);
        var p = await Payment(s, Receipt(first.CustomerId!.Value, 150, [new(first.Id, 75), new(second.Id, 50)]));
        Assert.Equal(HttpStatusCode.OK, (await PostPayment(s, p)).StatusCode);
        var url = s.Root + $"/payments/{p.Payment.Id}/allocate";
        Assert.Equal(HttpStatusCode.Conflict, (await Post(s.Maker, url, new AllocatePaymentRequest(2, new(2026,10,25), [new(second.Id,26)]))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Post(s.Maker, url, new AllocatePaymentRequest(2, new(2026,10,25), [new(second.Id,25)]))).StatusCode);
        var report = await Receivables(s, first.CustomerId.Value); Assert.Equal(420, report.SalesGrossAmount); Assert.Equal(150, report.AllocatedAmount); Assert.Equal(270, report.Balance);
    }
    private sealed class FailReceiptCommit : SaveChangesInterceptor
    {
        private int remaining = 1;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<CustomerPayment>().Any(x => x.Entity.Status == "Posted") && Interlocked.Exchange(ref remaining, 0) == 1)
                throw new InvalidOperationException("Injected receipt commit failure");
            return ValueTask.FromResult(result);
        }
    }
    [Fact]
    public async Task Failed_post_rolls_back_receipt_allocations_events_and_idempotency_then_retries_post_once()
    {
        var (s, invoice) = await PostedSale(); var p = await Payment(s, Receipt(invoice.CustomerId!.Value, 100, [new(invoice.Id,100)]));
        var fault = new FailReceiptCommit();
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddDbContext<Vat.Infrastructure.Persistence.VatDbContext>(options => options.AddInterceptors(fault))));
        using var client = host.CreateClient(); client.DefaultRequestHeaders.Authorization = s.Checker.DefaultRequestHeaders.Authorization;
        var key = Guid.NewGuid().ToString(); var url = s.Root + $"/payments/{p.Payment.Id}/approve";
        Assert.Equal(HttpStatusCode.InternalServerError, (await Post(client, url, new TransitionRequest(1), key)).StatusCode);
        var state = (await s.Maker.GetFromJsonAsync<PaymentResponse>(s.Root + $"/payments/{p.Payment.Id}"))!;
        Assert.Equal("Pending", state.Payment.Status); Assert.Empty(state.Allocations);
        Assert.Equal(0, await factory.Inspect(db => db.Outbox.CountAsync(x => x.CompanyId == s.Company && x.EventType == nameof(CustomerPaymentPostedV1))));
        var retries = await Task.WhenAll(Enumerable.Range(0,3).Select(_ => Post(client, url, new TransitionRequest(1), key)));
        Assert.All(retries, x => Assert.Equal(HttpStatusCode.OK, x.StatusCode));
        Assert.Single((await Task.WhenAll(retries.Select(x => x.Content.ReadAsStringAsync()))).Distinct());
        Assert.Equal(1, await factory.Inspect(db => db.Set<PaymentAllocation>().CountAsync(x => x.CompanyId == s.Company)));
        Assert.Equal(100, (await Receivables(s, invoice.CustomerId.Value)).CollectedAmount);
    }
    [Fact]
    public async Task Invalid_money_duplicate_reference_permissions_scope_and_unposted_invoices_are_rejected()
    {
        var (s, invoice) = await PostedSale(); var customer = invoice.CustomerId!.Value;
        foreach (var body in new[] { Receipt(customer,0), Receipt(customer,1.00001m), Receipt(customer) with { Method="Card" }, Receipt(customer) with { EvidenceReference=" " }, Receipt(customer,100,[new(invoice.Id,50),new(invoice.Id,20)]), Receipt(customer,100,[new(invoice.Id,101)]) })
            Assert.True((await Post(s.Maker,s.Root+"/payments",body)).StatusCode is HttpStatusCode.UnprocessableEntity or HttpStatusCode.Conflict);
        var p = await Payment(s,Receipt(customer));
        Assert.Equal(HttpStatusCode.Conflict,(await Post(s.Maker,s.Root+"/payments",Receipt(customer) with { Reference=p.Payment.Reference })).StatusCode);
        using var wrongTenant = factory.Client(Guid.NewGuid(),s.Company);
        using var wrongCompany = factory.Client(s.Tenant,Guid.NewGuid());
        using var reader = factory.Client(s.Tenant,s.Company,permissions:[Permissions.PaymentRead]);
        Assert.Equal(HttpStatusCode.NotFound,(await wrongTenant.GetAsync(s.Root+$"/payments/{p.Payment.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await wrongCompany.GetAsync(s.Root+$"/payments/{p.Payment.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await Post(reader,s.Root+"/payments",Receipt(customer))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await Post(reader,s.Root+$"/payments/{p.Payment.Id}/approve",new TransitionRequest(1))).StatusCode);
        var unposted = await Sale(s);
        Assert.Equal(HttpStatusCode.Conflict,(await Post(s.Maker,s.Root+"/payments",Receipt(customer,100,[new(unposted.Id,10)]))).StatusCode);
        var other = await Created<CustomerResponse>(s.Maker,s.Root+"/customers",new CreateCustomerRequest("OTHER","Other"));
        Assert.Equal(HttpStatusCode.NotFound,(await Post(s.Maker,s.Root+"/payments",Receipt(other.Id,100,[new(invoice.Id,10)]))).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,(await s.Maker.GetAsync(s.Root+"/payments?take=101")).StatusCode);
    }
    [Fact]
    public async Task Credits_after_collection_create_customer_credit_without_mutating_receipt_or_vat()
    {
        var (s, invoice) = await PostedSale(); var customer=invoice.CustomerId!.Value;
        var p = await Payment(s,Receipt(customer,210,[new(invoice.Id,210)])); Assert.Equal(HttpStatusCode.OK,(await PostPayment(s,p)).StatusCode);
        var line = (await s.Maker.GetFromJsonAsync<SalesReturnableResponse>(s.Root+$"/sales/{invoice.Id}/returnable-lines"))!.Lines.First();
        var credit = await Created<SalesCreditResponse>(s.Maker,s.Root+$"/sales/{invoice.Id}/price-credits",new RequestSalesPriceCredit(5,new(2026,10,25),"Reduction","AGREEMENT",[new(line.LineId,20)]));
        Assert.Equal(HttpStatusCode.OK,(await Post(s.Checker,s.Root+$"/sales/{invoice.Id}/credit-notes/{credit.Id}/approve",new TransitionRequest(credit.SourceVersion))).StatusCode);
        var report=await Receivables(s,customer); Assert.Equal(-21,report.Balance); Assert.Equal(-21,report.Invoices.Single().OutstandingAmount);
        Assert.Equal(210,report.CollectedAmount); Assert.Equal(210,report.AllocatedAmount);
        var debit=await Created<SalesDebitResponse>(s.Maker,s.Root+$"/sales/{invoice.Id}/debit-notes",new RequestSalesDebit(7,new(2026,10,25),"Increase","AGREEMENT",[new(line.LineId,10)]));
        Assert.Equal(HttpStatusCode.OK,(await Post(s.Checker,s.Root+$"/sales/{invoice.Id}/debit-notes/{debit.Id}/approve",new TransitionRequest(debit.SourceVersion))).StatusCode);
        Assert.Equal(-10.5m,(await Receivables(s,customer)).Balance);
    }
    [Fact]
    public async Task Period_close_includes_receipts_and_later_allocation_preserves_closed_snapshot()
    {
        var (s,invoice)=await PostedSale(); var customer=invoice.CustomerId!.Value;
        var p=await Payment(s,Receipt(customer,300,[new(invoice.Id,100)]));
        var period=(await s.Maker.GetFromJsonAsync<PeriodResponse[]>(s.Root+"/periods"))!.Single();
        Assert.Equal(HttpStatusCode.Conflict,(await Post(s.Maker,s.Root+$"/periods/{period.Id}/request-close",new PeriodActionRequest(1,"Month end"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await PostPayment(s,p)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await Post(s.Maker,s.Root+$"/periods/{period.Id}/request-close",new PeriodActionRequest(1,"Month end"))).StatusCode);
        var closed=await Post(s.Checker,s.Root+$"/periods/{period.Id}/close",new PeriodActionRequest(2,"Checked")); Assert.Equal(HttpStatusCode.OK,closed.StatusCode);
        var snapshot=(await closed.Content.ReadFromJsonAsync<PeriodResponse>())!.Snapshot!;
        Assert.Equal(300,snapshot.Payments!.CollectedAmount); Assert.Equal(100,snapshot.Payments.AllocatedAmount);
        Assert.Equal(HttpStatusCode.Conflict,(await Post(s.Maker,s.Root+$"/payments/{p.Payment.Id}/allocate",new AllocatePaymentRequest(2,new(2026,10,25),[new(invoice.Id,110)]))).StatusCode);
        await Created<PeriodResponse>(s.Maker,s.Root+"/periods",new CreatePeriodRequest("November",new(2026,11,1),new(2026,12,1)));
        Assert.Equal(HttpStatusCode.OK,(await Post(s.Maker,s.Root+$"/payments/{p.Payment.Id}/allocate",new AllocatePaymentRequest(2,new(2026,11,1),[new(invoice.Id,110)]))).StatusCode);
        var old=(await s.Maker.GetFromJsonAsync<PeriodResponse>(s.Root+$"/periods/{period.Id}"))!.Snapshot!;
        Assert.Equal(snapshot.LedgerFingerprint,old.LedgerFingerprint); Assert.Equal(100,old.Payments!.AllocatedAmount);
    }
    [Fact]
    public async Task Missing_allocation_blocks_receivables_and_final_period_close()
    {
        var (s,invoice)=await PostedSale(); var p=await Payment(s,Receipt(invoice.CustomerId!.Value,100,[new(invoice.Id,100)]));
        Assert.Equal(HttpStatusCode.OK,(await PostPayment(s,p)).StatusCode);
        var period=(await s.Maker.GetFromJsonAsync<PeriodResponse[]>(s.Root+"/periods"))!.Single();
        Assert.Equal(HttpStatusCode.OK,(await Post(s.Maker,s.Root+$"/periods/{period.Id}/request-close",new PeriodActionRequest(1,"Review"))).StatusCode);
        await factory.Inspect(async db => await db.Set<PaymentAllocation>().Where(x=>x.CompanyId==s.Company).ExecuteDeleteAsync());
        Assert.Equal(HttpStatusCode.Conflict,(await s.Maker.GetAsync(s.Root+$"/customers/{invoice.CustomerId}/receivables")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await Post(s.Checker,s.Root+$"/periods/{period.Id}/close",new PeriodActionRequest(2,"Review"))).StatusCode);
    }
}
