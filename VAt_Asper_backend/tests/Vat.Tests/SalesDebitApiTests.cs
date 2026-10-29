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
public sealed class SalesDebitApiTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
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

    private async Task<(SetupData Setup, SalesInvoiceResponse Invoice)> PostedSale()
    {
        var (s, _) = await Stock();
        var invoice = await Sale(s, lines: [new("ITEM", "First line", 1, 100), new("ITEM", "Second line", 1, 100)]);
        await Reserve(s, invoice.Id);
        var posted = await Post(s.Checker, $"{s.Root}/sales/{invoice.Id}/post", new TransitionRequest(4));
        Assert.Equal(HttpStatusCode.OK, posted.StatusCode);
        return (s, (await posted.Content.ReadFromJsonAsync<SalesInvoiceResponse>())!);
    }

    private static Task<SalesCreditResponse> Request(SetupData s, Guid invoiceId, long version = 5, DateOnly? date = null) =>
        Created<SalesCreditResponse>(s.Maker, $"{s.Root}/sales/{invoiceId}/credit-notes",
            new RequestSalesCredit(version, date ?? new(2026, 10, 24), "Full goods return", "RETURN-RECEIPT-001"));

    private static async Task<SalesReturnableResponse> Returnable(SetupData s, Guid invoiceId) =>
        (await s.Maker.GetFromJsonAsync<SalesReturnableResponse>($"{s.Root}/sales/{invoiceId}/returnable-lines"))!;

    private static Task<SalesCreditResponse> Partial(SetupData s, Guid invoiceId, long version, SalesReturnLineInput line) =>
        Created<SalesCreditResponse>(s.Maker, $"{s.Root}/sales/{invoiceId}/credit-notes",
            new RequestSalesCredit(version, new(2026, 10, 24), "Partial goods return", "RETURN-PARTIAL", [line]));

    private static async Task ApproveCredit(SetupData s, Guid invoiceId, SalesCreditResponse note)
    {
        var response = await Post(s.Checker, $"{s.Root}/sales/{invoiceId}/credit-notes/{note.Id}/approve", new TransitionRequest(note.SourceVersion));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static Task<SalesCreditResponse> Price(SetupData s, Guid invoiceId, long version, Guid lineId, decimal net) =>
        Created<SalesCreditResponse>(s.Maker, $"{s.Root}/sales/{invoiceId}/price-credits",
            new RequestSalesPriceCredit(version, new(2026, 10, 24), "Agreed price reduction", "ADJUSTMENT-EVIDENCE", [new(lineId, net)]));

    private static Task<SalesDebitResponse> Debit(SetupData s, Guid invoiceId, long version, Guid lineId, decimal net) =>
        Created<SalesDebitResponse>(s.Maker, $"{s.Root}/sales/{invoiceId}/debit-notes",
            new RequestSalesDebit(version, new(2026, 10, 24), "Agreed price increase", "DEBIT-EVIDENCE", [new(lineId, net)]));

    private static async Task ApproveDebit(SetupData s, Guid invoiceId, SalesDebitResponse note)
    {
        Assert.Equal(HttpStatusCode.OK, (await Post(s.Checker, $"{s.Root}/sales/{invoiceId}/debit-notes/{note.Id}/approve", new TransitionRequest(note.SourceVersion))).StatusCode);
    }

    [Fact]
    public async Task Debit_increases_value_and_tax_without_changing_original_stock_or_reservations()
    {
        var (s, invoice) = await PostedSale();
        var originals = await factory.Inspect(db => db.Set<SalesPostingEntry>().AsNoTracking().Where(x => x.InvoiceId == invoice.Id).ToArrayAsync());
        var other = await Sale(s, 1);
        await Reserve(s, other.Id);
        var line = (await Returnable(s, invoice.Id)).Lines.First();
        var note = await Debit(s, invoice.Id, 5, line.LineId, 25);
        Assert.Equal(25m, note.NetAmount);
        Assert.Equal(1.25m, note.OutputTaxAmount);
        var pending = await Returnable(s, invoice.Id);
        Assert.Equal("DebitPending", pending.Status);
        Assert.False(pending.CanRequestCredit);
        Assert.False(pending.CanRequestDebit);
        Assert.Equal(25m, pending.Lines.Single(x => x.LineId == line.LineId).PendingNetDebitAmount);
        await ApproveDebit(s, invoice.Id, note);
        var current = (await s.Maker.GetFromJsonAsync<SalesInvoiceResponse>($"{s.Root}/sales/{invoice.Id}"))!;
        Assert.Equal("Posted", current.Status);
        Assert.Equal(7, current.Version);
        Assert.Equal(5, current.PostedVersion);
        Assert.Equal(8m, (await Balance(s)).Quantity);
        Assert.Equal(1m, (await Balance(s)).ReservedQuantity);
        var remaining = await Returnable(s, invoice.Id);
        Assert.Equal(125m, remaining.Lines.Single(x => x.LineId == line.LineId).RemainingNetAmount);
        Assert.True(remaining.CanRequestDebit);
        var rows = await factory.Inspect(db => db.Set<SalesPostingEntry>().AsNoTracking().Where(x => x.InvoiceId == invoice.Id).ToArrayAsync());
        var debit = rows.Single(x => x.DebitNoteId == note.Id);
        Assert.Equal("PriceDebit", debit.EntryType);
        Assert.Equal(0m, debit.Quantity);
        Assert.Null(debit.CreditNoteId);
        Assert.Null(debit.ReversesEntryId);
        Assert.NotNull(debit.AdjustsEntryId);
        foreach (var original in originals)
            Assert.Equal(System.Text.Json.JsonSerializer.Serialize(original), System.Text.Json.JsonSerializer.Serialize(rows.Single(x => x.Id == original.Id)));
        var summary = (await s.Maker.GetFromJsonAsync<SalesSummary>($"{s.Root}/sales/summary?from=2026-10-01&until=2026-11-01"))!;
        Assert.Equal(225m, summary.NetAmount);
        Assert.Equal(11.25m, summary.OutputTaxAmount);
        Assert.Equal(1, summary.InvoiceCount);
        Assert.Equal(0, summary.CreditNoteCount);
        Assert.Equal(1, summary.DebitNoteCount);
    }

    [Fact]
    public async Task Debit_then_price_credit_and_goods_return_consume_the_adjusted_pool_exactly()
    {
        var (s, invoice) = await PostedSale();
        var line = (await Returnable(s, invoice.Id)).Lines.First();
        var debit = await Debit(s, invoice.Id, 5, line.LineId, 25);
        await ApproveDebit(s, invoice.Id, debit);
        var price = await Price(s, invoice.Id, 7, line.LineId, 125);
        Assert.Equal(-6.25m, price.OutputTaxAmount);
        await ApproveCredit(s, invoice.Id, price);
        var goods = await Request(s, invoice.Id, 9);
        Assert.Equal(-100m, goods.NetAmount);
        Assert.Equal(-5m, goods.OutputTaxAmount);
        await ApproveCredit(s, invoice.Id, goods);
        Assert.Equal("Credited", (await Returnable(s, invoice.Id)).Status);
        Assert.Equal(10m, (await Balance(s)).Quantity);
        var rows = await factory.Inspect(db => db.Set<SalesPostingEntry>().Where(x => x.InvoiceId == invoice.Id).ToArrayAsync());
        Assert.Equal(0m, rows.Sum(x => x.NetAmount));
        Assert.Equal(0m, rows.Sum(x => x.OutputTaxAmount));
    }

    [Fact]
    public async Task Mixed_partial_returns_debits_and_price_credits_preserve_prior_allocations()
    {
        var (s, invoice) = await PostedSale();
        var line = (await Returnable(s, invoice.Id)).Lines.First();
        await ApproveCredit(s, invoice.Id, await Partial(s, invoice.Id, 5, new(line.LineId, 0.5m)));
        await ApproveDebit(s, invoice.Id, await Debit(s, invoice.Id, 7, line.LineId, 20));
        var goods = await Partial(s, invoice.Id, 9, new(line.LineId, 0.25m));
        Assert.Equal(-35m, goods.NetAmount);
        Assert.Equal(-1.75m, goods.OutputTaxAmount);
        await ApproveCredit(s, invoice.Id, goods);
        await ApproveCredit(s, invoice.Id, await Price(s, invoice.Id, 11, line.LineId, 5));
        await ApproveDebit(s, invoice.Id, await Debit(s, invoice.Id, 13, line.LineId, 10));
        var last = await Request(s, invoice.Id, 15);
        Assert.Equal(-140m, last.NetAmount);
        Assert.Equal(-7m, last.OutputTaxAmount);
        await ApproveCredit(s, invoice.Id, last);
        Assert.Equal(10m, (await Balance(s)).Quantity);
        Assert.Equal("Credited", (await Returnable(s, invoice.Id)).Status);
    }

    [Fact]
    public async Task Consecutive_debit_vat_rounds_cumulatively_then_all_increased_value_can_be_credited()
    {
        var (s, _) = await Stock();
        var invoice = await Sale(s, lines: [new("ITEM", "Fractional value", 3, 0.3333m)]);
        await Reserve(s, invoice.Id);
        Assert.Equal(HttpStatusCode.OK, (await Post(s.Checker, $"{s.Root}/sales/{invoice.Id}/post", new TransitionRequest(4))).StatusCode);
        var line = (await Returnable(s, invoice.Id)).Lines.Single();
        var taxes = new List<decimal>();
        for (var index = 0; index < 3; index++)
        {
            var note = await Debit(s, invoice.Id, 5 + index * 2, line.LineId, 0.3333m);
            taxes.Add(note.OutputTaxAmount);
            await ApproveDebit(s, invoice.Id, note);
        }
        Assert.Equal(new[] { 0.0167m, 0.0166m, 0.0167m }, taxes);
        var available = (await Returnable(s, invoice.Id)).Lines.Single();
        Assert.Equal(1.9998m, available.RemainingNetAmount);
        Assert.Equal(0.1m, available.RemainingOutputTaxAmount);
        await ApproveCredit(s, invoice.Id, await Price(s, invoice.Id, 11, line.LineId, 1.9998m));
        var goods = await Request(s, invoice.Id, 13);
        Assert.Equal(0m, goods.GrossAmount);
        await ApproveCredit(s, invoice.Id, goods);
        Assert.Equal(10m, (await Balance(s)).Quantity);
    }

    [Fact]
    public async Task Invalid_debit_evidence_precision_line_limit_and_returned_goods_are_rejected()
    {
        var (s, invoice) = await PostedSale();
        var line = (await Returnable(s, invoice.Id)).Lines.First();
        var path = $"{s.Root}/sales/{invoice.Id}/debit-notes";
        IReadOnlyList<SalesDebitLineInput>[] invalid = [[], [new(line.LineId, 0)], [new(line.LineId, -1)],
            [new(line.LineId, 0.00001m)], [new(Guid.NewGuid(), 1)], [new(line.LineId, 1), new(line.LineId, 1)], [null!]];
        foreach (var lines in invalid)
            Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Post(s.Maker, path, new RequestSalesDebit(5, new(2026, 10, 24), "Increase", "REF", lines))).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Post(s.Maker, path, new RequestSalesDebit(5, new(2026, 10, 24), "Increase", " ", [new(line.LineId, 1)]))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(s.Maker, path, new RequestSalesDebit(5, new(2026, 10, 24), "Increase", "REF", [new(line.LineId, SalesDebitAllocator.MaxLineValue)]))).StatusCode);
        Assert.Equal(5, (await Returnable(s, invoice.Id)).Version);
        await ApproveCredit(s, invoice.Id, await Partial(s, invoice.Id, 5, new(line.LineId, 1)));
        Assert.Equal(HttpStatusCode.Conflict, (await Post(s.Maker, path, new RequestSalesDebit(7, new(2026, 10, 24), "Increase", "REF", [new(line.LineId, 1)]))).StatusCode);
    }

    [Fact]
    public async Task Concurrent_credit_debit_admission_and_decision_allow_one_pending_winner()
    {
        var (s, invoice) = await PostedSale();
        var line = (await Returnable(s, invoice.Id)).Lines.First();
        var root = $"{s.Root}/sales/{invoice.Id}";
        var requests = await Task.WhenAll(
            Post(s.Maker, $"{root}/debit-notes", new RequestSalesDebit(5, new(2026, 10, 24), "Increase", "REF", [new(line.LineId, 25)])),
            Post(s.Maker, $"{root}/credit-notes", new RequestSalesCredit(5, new(2026, 10, 24), "Return", "REF", [new(line.LineId, 0.5m)])));
        Assert.Single(requests, x => x.StatusCode == HttpStatusCode.Created);
        Assert.Single(requests, x => x.StatusCode == HttpStatusCode.Conflict);
        var isDebit = requests[0].StatusCode == HttpStatusCode.Created;
        using var json = System.Text.Json.JsonDocument.Parse(await requests.Single(x => x.StatusCode == HttpStatusCode.Created).Content.ReadAsStringAsync());
        var id = json.RootElement.GetProperty("id").GetGuid();
        var path = $"{root}/{(isDebit ? "debit-notes" : "credit-notes")}/{id}";
        var decisions = await Task.WhenAll(Post(s.Checker, $"{path}/approve", new TransitionRequest(6)),
            Post(s.Maker, $"{path}/cancel", new CancelSalesInvoiceRequest(6, "Withdrawn")));
        Assert.Single(decisions, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Single(decisions, x => x.StatusCode == HttpStatusCode.Conflict);
        Assert.True((await s.Maker.GetFromJsonAsync<SalesReconciliation>($"{root}/reconciliation"))!.IsReconciled);
        Assert.Equal(!isDebit && decisions[0].StatusCode == HttpStatusCode.OK ? 8.5m : 8m, (await Balance(s)).Quantity);
    }

    [Fact]
    public async Task Failed_debit_commit_rolls_back_and_concurrent_retries_post_only_once()
    {
        var (s, invoice) = await PostedSale();
        var line = (await Returnable(s, invoice.Id)).Lines.First();
        var note = await Debit(s, invoice.Id, 5, line.LineId, 25);
        var interceptor = new FailFirstDebitCommit();
        using var host = factory.WithWebHostBuilder(b => b.ConfigureTestServices(services =>
            services.AddDbContext<Vat.Infrastructure.Persistence.VatDbContext>(o => o.AddInterceptors(interceptor))));
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = s.Checker.DefaultRequestHeaders.Authorization;
        var path = $"{s.Root}/sales/{invoice.Id}/debit-notes/{note.Id}/approve";
        var key = Guid.NewGuid().ToString();
        Assert.Equal(HttpStatusCode.InternalServerError, (await Post(client, path, new TransitionRequest(6), key)).StatusCode);
        Assert.Equal(8m, (await Balance(s)).Quantity);
        Assert.Equal(0, await factory.Inspect(db => db.Set<SalesPostingEntry>().CountAsync(x => x.DebitNoteId == note.Id)));
        var responses = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => Post(client, path, new TransitionRequest(6), key)));
        Assert.All(responses, x => Assert.Equal(HttpStatusCode.OK, x.StatusCode));
        Assert.Single((await Task.WhenAll(responses.Select(x => x.Content.ReadAsStringAsync()))).Distinct());
        Assert.Equal(1, await factory.Inspect(db => db.Set<SalesPostingEntry>().CountAsync(x => x.DebitNoteId == note.Id)));
    }

    [Fact]
    public async Task Later_period_debit_preserves_closed_original_snapshot_and_appears_as_positive_adjustment()
    {
        var (s, invoice) = await PostedSale();
        var line = (await Returnable(s, invoice.Id)).Lines.First();
        var original = (await s.Maker.GetFromJsonAsync<PeriodResponse[]>($"{s.Root}/periods"))!.Single();
        Assert.Equal(HttpStatusCode.OK, (await Post(s.Maker, $"{s.Root}/periods/{original.Id}/request-close", new PeriodActionRequest(1, "Close"))).StatusCode);
        var closed = await Post(s.Checker, $"{s.Root}/periods/{original.Id}/close", new PeriodActionRequest(2, "Reconciled"));
        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);
        var checkpoint = (await closed.Content.ReadFromJsonAsync<PeriodResponse>())!.Snapshot!;
        var next = await Created<PeriodResponse>(s.Maker, $"{s.Root}/periods", new CreatePeriodRequest("November", new(2026, 11, 1), new(2026, 12, 1)));
        var response = await Post(s.Maker, $"{s.Root}/sales/{invoice.Id}/debit-notes", new RequestSalesDebit(5, new(2026, 11, 2), "Increase", "REF", [new(line.LineId, 25)]));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var note = (await response.Content.ReadFromJsonAsync<SalesDebitResponse>())!;
        Assert.Equal(HttpStatusCode.Conflict, (await Post(s.Maker, $"{s.Root}/periods/{next.Id}/request-close", new PeriodActionRequest(1, "Close"))).StatusCode);
        await ApproveDebit(s, invoice.Id, note);
        Assert.Equal(HttpStatusCode.OK, (await Post(s.Maker, $"{s.Root}/periods/{next.Id}/request-close", new PeriodActionRequest(1, "Close"))).StatusCode);
        closed = await Post(s.Checker, $"{s.Root}/periods/{next.Id}/close", new PeriodActionRequest(2, "Reconciled"));
        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);
        var signed = (await closed.Content.ReadFromJsonAsync<PeriodResponse>())!.Snapshot!.Sales!;
        Assert.Equal(25m, signed.NetAmount);
        Assert.Equal(1.25m, signed.OutputTaxAmount);
        Assert.Equal(8m, (await Balance(s)).Quantity);
        var unchanged = (await s.Maker.GetFromJsonAsync<PeriodResponse>($"{s.Root}/periods/{original.Id}"))!.Snapshot!;
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(checkpoint), System.Text.Json.JsonSerializer.Serialize(unchanged));
    }

    [Fact]
    public async Task Debit_permissions_scope_checker_gate_and_cancel_preserve_prior_posted_version()
    {
        var (s, invoice) = await PostedSale();
        var line = (await Returnable(s, invoice.Id)).Lines.First();
        var root = $"{s.Root}/sales/{invoice.Id}/debit-notes";
        var body = new RequestSalesDebit(5, new(2026, 10, 24), "Increase", "REF", [new(line.LineId, 25)]);
        using var otherTenant = factory.Client(Guid.NewGuid(), s.Company);
        using var otherCompany = factory.Client(s.Tenant, Guid.NewGuid());
        using var creditsOnly = factory.Client(s.Tenant, s.Company, permissions: [Permissions.SalesCreditRequest, Permissions.SalesCreditApprove]);
        Assert.Equal(HttpStatusCode.NotFound, (await Post(otherTenant, root, body)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(otherCompany, root, body)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(creditsOnly, root, body)).StatusCode);
        var note = await Debit(s, invoice.Id, 5, line.LineId, 25);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(s.Maker, $"{root}/{note.Id}/approve", new TransitionRequest(6))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(creditsOnly, $"{root}/{note.Id}/approve", new TransitionRequest(6))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await otherTenant.GetAsync($"{root}/{note.Id}")).StatusCode);
        using var disabledHost = factory.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) =>
            c.AddInMemoryCollection(new Dictionary<string, string?> { ["Posting:Enabled"] = "false" })));
        using var disabled = disabledHost.CreateClient();
        disabled.DefaultRequestHeaders.Authorization = s.Checker.DefaultRequestHeaders.Authorization;
        Assert.Equal(HttpStatusCode.Conflict, (await Post(disabled, $"{root}/{note.Id}/approve", new TransitionRequest(6))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Post(s.Maker, $"{root}/{note.Id}/cancel", new CancelSalesInvoiceRequest(6, "Withdrawn"))).StatusCode);
        var current = (await s.Maker.GetFromJsonAsync<SalesInvoiceResponse>($"{s.Root}/sales/{invoice.Id}"))!;
        Assert.Equal("Posted", current.Status);
        Assert.Equal(5, current.PostedVersion);
        Assert.Equal(7, current.Version);
        var replacement = await Debit(s, invoice.Id, 7, line.LineId, 25);
        await ApproveDebit(s, invoice.Id, replacement);
        Assert.True((await s.Maker.GetFromJsonAsync<SalesReconciliation>($"{s.Root}/sales/{invoice.Id}/reconciliation"))!.IsReconciled);
    }

    [Fact]
    public async Task Separate_debit_event_has_no_stock_and_missing_financial_entry_blocks_final_close()
    {
        var (s, invoice) = await PostedSale();
        var line = (await Returnable(s, invoice.Id)).Lines.First();
        var note = await Debit(s, invoice.Id, 5, line.LineId, 25);
        await ApproveDebit(s, invoice.Id, note);
        var payload = await factory.Inspect(db => db.Outbox.Where(x => x.AggregateId == invoice.Id && x.EventType == nameof(SalesDebitPostedV1)).Select(x => x.Payload).SingleAsync());
        using var envelope = System.Text.Json.JsonDocument.Parse(payload);
        Assert.Equal(25m, envelope.RootElement.GetProperty("payload").GetProperty("netAmount").GetDecimal());
        using var scope = factory.Services.CreateScope();
        var processed = await scope.ServiceProvider.GetRequiredService<IIncomingEventProcessor>().Process(new(System.Text.Encoding.UTF8.GetBytes(payload),
            envelope.RootElement.GetProperty("eventId").GetGuid().ToString(), nameof(SalesDebitPostedV1), "application/json"), default);
        Assert.Equal("Ignored", processed.Status);
        await factory.Inspect(db => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM sales.posting_entries WHERE \"DebitNoteId\" = {note.Id}"));
        Assert.False((await s.Maker.GetFromJsonAsync<SalesReconciliation>($"{s.Root}/sales/{invoice.Id}/reconciliation"))!.IsReconciled);
        var period = (await s.Maker.GetFromJsonAsync<PeriodResponse[]>($"{s.Root}/periods"))!.Single();
        Assert.Equal(HttpStatusCode.OK, (await Post(s.Maker, $"{s.Root}/periods/{period.Id}/request-close", new PeriodActionRequest(1, "Close"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(s.Checker, $"{s.Root}/periods/{period.Id}/close", new PeriodActionRequest(2, "Review"))).StatusCode);
    }

    private sealed class FailFirstDebitCommit : SaveChangesInterceptor
    {
        private int _failed;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (data.Context!.ChangeTracker.Entries<SalesPostingEntry>().Any(x => x.State == EntityState.Added && x.Entity.EntryType == "PriceDebit") &&
                Interlocked.Exchange(ref _failed, 1) == 0) throw new InvalidOperationException("Injected debit-posting failure.");
            return ValueTask.FromResult(result);
        }
    }

}
