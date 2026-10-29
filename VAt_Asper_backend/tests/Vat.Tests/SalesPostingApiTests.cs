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
using Xunit;

namespace Vat.Tests;

[Collection("PostgreSQL integration")]
public sealed class SalesPostingApiTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
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
        await Created<PeriodResponse>(maker, $"{root}/periods", new CreatePeriodRequest("Test scope", new(2026, 1, 1), new(2029, 1, 1)));
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
        Assert.Equal(HttpStatusCode.OK, (await Post(client, path, new TransitionRequest(4), key)).StatusCode);
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
