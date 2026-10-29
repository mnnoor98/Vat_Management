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
public sealed class SalesPriceCreditApiTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
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

    [Fact]
    public async Task Price_credit_changes_only_value_preserving_stock_reservations_and_original_entries()
    {
        var (s, invoice) = await PostedSale();
        var originals = await factory.Inspect(db => db.Set<SalesPostingEntry>().AsNoTracking().Where(x => x.InvoiceId == invoice.Id).OrderBy(x => x.Id).ToArrayAsync());
        var other = await Sale(s, 1);
        await Reserve(s, other.Id);
        var line = (await Returnable(s, invoice.Id)).Lines.First();
        var note = await Price(s, invoice.Id, 5, line.LineId, 25);
        Assert.Equal("PriceOnly", note.Mode);
        Assert.Equal("", note.ReturnReceiptReference);
        Assert.Equal("ADJUSTMENT-EVIDENCE", note.AdjustmentEvidenceReference);
        Assert.Equal(-25m, note.NetAmount);
        Assert.Equal(-1.25m, note.OutputTaxAmount);
        Assert.Equal(0m, note.Lines.Single().Quantity);
        var pending = (await Returnable(s, invoice.Id)).Lines.Single(x => x.LineId == line.LineId);
        Assert.Equal(0m, pending.PendingQuantity);
        Assert.Equal(25m, pending.PendingNetCreditAmount);
        await ApproveCredit(s, invoice.Id, note);
        var balance = await Balance(s);
        Assert.Equal(8m, balance.Quantity);
        Assert.Equal(1m, balance.ReservedQuantity);
        var remaining = await Returnable(s, invoice.Id);
        Assert.Equal("PartiallyCredited", remaining.Status);
        Assert.True(remaining.CanRequestPriceCredit);
        Assert.Equal(75m, remaining.Lines.Single(x => x.LineId == line.LineId).RemainingNetAmount);
        Assert.Equal(1m, remaining.Lines.Single(x => x.LineId == line.LineId).RemainingQuantity);
        var rows = await factory.Inspect(db => db.Set<SalesPostingEntry>().AsNoTracking().Where(x => x.InvoiceId == invoice.Id).ToArrayAsync());
        var price = rows.Single(x => x.CreditNoteId == note.Id);
        Assert.Equal("PriceCredit", price.EntryType);
        Assert.Equal(0m, price.Quantity);
        foreach (var original in originals)
            Assert.Equal(System.Text.Json.JsonSerializer.Serialize(original), System.Text.Json.JsonSerializer.Serialize(rows.Single(x => x.Id == original.Id)));
        var summary = (await s.Maker.GetFromJsonAsync<SalesSummary>($"{s.Root}/sales/summary?from=2026-10-01&until=2026-11-01"))!;
        Assert.Equal(175m, summary.NetAmount);
        Assert.Equal(8.75m, summary.OutputTaxAmount);
        Assert.Equal(1, summary.CreditNoteCount);
        Assert.Equal(1, summary.PriceCreditCount);
    }

    [Fact]
    public async Task Full_price_reduction_then_goods_return_credits_only_remaining_value()
    {
        var (s, invoice) = await PostedSale();
        var line = (await Returnable(s, invoice.Id)).Lines.First();
        var price = await Price(s, invoice.Id, 5, line.LineId, 100);
        await ApproveCredit(s, invoice.Id, price);
        Assert.Equal(8m, (await Balance(s)).Quantity);
        var goods = await Request(s, invoice.Id, 7);
        Assert.Equal("PartialReturn", goods.Mode);
        Assert.Equal(-100m, goods.NetAmount);
        Assert.Equal(-5m, goods.OutputTaxAmount);
        Assert.Equal(0m, goods.Lines.Single(x => x.LineId == line.LineId).NetAmount);
        await ApproveCredit(s, invoice.Id, goods);
        Assert.Equal(10m, (await Balance(s)).Quantity);
        Assert.Equal("Credited", (await Returnable(s, invoice.Id)).Status);
        var rows = await factory.Inspect(db => db.Set<SalesPostingEntry>().Where(x => x.InvoiceId == invoice.Id).ToArrayAsync());
        Assert.Equal(0m, rows.Sum(x => x.NetAmount));
        Assert.Equal(0m, rows.Sum(x => x.OutputTaxAmount));
        Assert.Equal(0, await factory.Inspect(db => db.Outbox.CountAsync(x => x.AggregateId == invoice.Id && x.EventType == nameof(SalesCreditPostedV1))));
    }

    [Fact]
    public async Task Alternating_goods_and_price_credits_allocate_only_each_remaining_pool()
    {
        var (s, invoice) = await PostedSale();
        var line = (await Returnable(s, invoice.Id)).Lines.First();
        var goods = await Partial(s, invoice.Id, 5, new(line.LineId, 0.5m));
        await ApproveCredit(s, invoice.Id, goods);
        var price = await Price(s, invoice.Id, 7, line.LineId, 20);
        Assert.Equal(-1m, price.OutputTaxAmount);
        await ApproveCredit(s, invoice.Id, price);
        goods = await Partial(s, invoice.Id, 9, new(line.LineId, 0.25m));
        Assert.Equal(-15m, goods.NetAmount);
        Assert.Equal(-0.75m, goods.OutputTaxAmount);
        await ApproveCredit(s, invoice.Id, goods);
        price = await Price(s, invoice.Id, 11, line.LineId, 5);
        Assert.Equal(-0.25m, price.OutputTaxAmount);
        await ApproveCredit(s, invoice.Id, price);
        goods = await Partial(s, invoice.Id, 13, new(line.LineId, 0.25m));
        Assert.Equal(-10m, goods.NetAmount);
        Assert.Equal(-0.5m, goods.OutputTaxAmount);
        await ApproveCredit(s, invoice.Id, goods);
        Assert.Equal(9m, (await Balance(s)).Quantity);
        var last = await Request(s, invoice.Id, 15);
        await ApproveCredit(s, invoice.Id, last);
        Assert.Equal("Credited", (await Returnable(s, invoice.Id)).Status);
        Assert.Equal(10m, (await Balance(s)).Quantity);
    }

    [Fact]
    public async Task Repeated_price_credits_round_cumulatively_and_zero_value_goods_still_return()
    {
        var (s, _) = await Stock();
        var invoice = await Sale(s, lines: [new("ITEM", "Fractional price", 3, 0.3333m)]);
        await Reserve(s, invoice.Id);
        Assert.Equal(HttpStatusCode.OK, (await Post(s.Checker, $"{s.Root}/sales/{invoice.Id}/post", new TransitionRequest(4))).StatusCode);
        var line = (await Returnable(s, invoice.Id)).Lines.Single();
        var taxes = new List<decimal>();
        for (var step = 0; step < 3; step++)
        {
            var note = await Price(s, invoice.Id, 5 + step * 2, line.LineId, 0.3333m);
            taxes.Add(note.OutputTaxAmount);
            await ApproveCredit(s, invoice.Id, note);
        }
        Assert.Equal(new[] { -0.0167m, -0.0166m, -0.0167m }, taxes);
        var state = await Returnable(s, invoice.Id);
        Assert.False(state.CanRequestPriceCredit);
        Assert.True(state.CanRequestCredit);
        Assert.Equal(3m, state.Lines.Single().RemainingQuantity);
        Assert.Equal(0m, state.Lines.Single().RemainingOutputTaxAmount);
        var goods = await Request(s, invoice.Id, 11);
        Assert.Equal(0m, goods.GrossAmount);
        await ApproveCredit(s, invoice.Id, goods);
        Assert.Equal(10m, (await Balance(s)).Quantity);
    }

    [Fact]
    public async Task Invalid_evidence_lines_precision_and_excess_value_leave_source_unchanged()
    {
        var (s, invoice) = await PostedSale();
        var line = (await Returnable(s, invoice.Id)).Lines.First();
        var path = $"{s.Root}/sales/{invoice.Id}/price-credits";
        IReadOnlyList<SalesPriceLineInput>[] invalid = [[], [new(line.LineId, 0)], [new(line.LineId, -1)],
            [new(line.LineId, 0.00001m)], [new(Guid.NewGuid(), 1)], [new(line.LineId, 1), new(line.LineId, 1)], [null!]];
        foreach (var lines in invalid)
            Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Post(s.Maker, path, new RequestSalesPriceCredit(5, new(2026, 10, 24), "Reduction", "REF", lines))).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Post(s.Maker, path, new RequestSalesPriceCredit(5, new(2026, 10, 24), "Reduction", " ", [new(line.LineId, 1)]))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(s.Maker, path, new RequestSalesPriceCredit(5, new(2026, 10, 24), "Reduction", "REF", [new(line.LineId, 100.0001m)]))).StatusCode);
        Assert.Equal(5, (await Returnable(s, invoice.Id)).Version);
        Assert.Equal(8m, (await Balance(s)).Quantity);
        var price = await Price(s, invoice.Id, 5, line.LineId, 75);
        await ApproveCredit(s, invoice.Id, price);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(s.Maker, path, new RequestSalesPriceCredit(7, new(2026, 10, 24), "Reduction", "REF", [new(line.LineId, 25.0001m)]))).StatusCode);
    }

    [Fact]
    public async Task Competing_goods_and_price_requests_share_one_pending_slot_and_one_decision()
    {
        var (s, invoice) = await PostedSale();
        var line = (await Returnable(s, invoice.Id)).Lines.First();
        var root = $"{s.Root}/sales/{invoice.Id}";
        var results = await Task.WhenAll(
            Post(s.Maker, $"{root}/price-credits", new RequestSalesPriceCredit(5, new(2026, 10, 24), "Reduction", "REF", [new(line.LineId, 75)])),
            Post(s.Maker, $"{root}/credit-notes", new RequestSalesCredit(5, new(2026, 10, 24), "Return", "REF", [new(line.LineId, 0.5m)])));
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.Created);
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.Conflict);
        var note = (await results.Single(x => x.StatusCode == HttpStatusCode.Created).Content.ReadFromJsonAsync<SalesCreditResponse>())!;
        results = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => Post(s.Checker, $"{root}/credit-notes/{note.Id}/approve", new TransitionRequest(6))));
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.OK);
        Assert.All(results.Where(x => x.StatusCode != HttpStatusCode.OK), x => Assert.Equal(HttpStatusCode.Conflict, x.StatusCode));
        Assert.Equal(note.Mode == "PriceOnly" ? 8m : 8.5m, (await Balance(s)).Quantity);
        Assert.True((await s.Maker.GetFromJsonAsync<SalesReconciliation>($"{root}/reconciliation"))!.IsReconciled);
    }

    [Fact]
    public async Task Failed_price_commit_rolls_back_and_same_key_retry_posts_once()
    {
        var (s, invoice) = await PostedSale();
        var line = (await Returnable(s, invoice.Id)).Lines.First();
        var note = await Price(s, invoice.Id, 5, line.LineId, 25);
        var interceptor = new FailFirstPriceCommit();
        using var host = factory.WithWebHostBuilder(b => b.ConfigureTestServices(services =>
            services.AddDbContext<Vat.Infrastructure.Persistence.VatDbContext>(o => o.AddInterceptors(interceptor))));
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = s.Checker.DefaultRequestHeaders.Authorization;
        var path = $"{s.Root}/sales/{invoice.Id}/credit-notes/{note.Id}";
        var key = Guid.NewGuid().ToString();
        Assert.Equal(HttpStatusCode.InternalServerError, (await Post(client, $"{path}/approve", new TransitionRequest(6), key)).StatusCode);
        Assert.Equal(8m, (await Balance(s)).Quantity);
        Assert.Equal("Pending", (await s.Maker.GetFromJsonAsync<SalesCreditResponse>(path))!.Status);
        Assert.Equal(0, await factory.Inspect(db => db.Set<SalesPostingEntry>().CountAsync(x => x.CreditNoteId == note.Id)));
        Assert.Equal(HttpStatusCode.OK, (await Post(client, $"{path}/approve", new TransitionRequest(6), key)).StatusCode);
        var replay = await Post(client, $"{path}/approve", new TransitionRequest(6), key);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal("true", replay.Headers.GetValues("Idempotency-Replayed").Single());
        Assert.Equal(1, await factory.Inspect(db => db.Set<SalesPostingEntry>().CountAsync(x => x.CreditNoteId == note.Id)));
    }

    [Fact]
    public async Task Price_credit_in_new_period_preserves_closed_original_checkpoint_and_stock()
    {
        var (s, invoice) = await PostedSale();
        var line = (await Returnable(s, invoice.Id)).Lines.First();
        var original = (await s.Maker.GetFromJsonAsync<PeriodResponse[]>($"{s.Root}/periods"))!.Single();
        Assert.Equal(HttpStatusCode.OK, (await Post(s.Maker, $"{s.Root}/periods/{original.Id}/request-close", new PeriodActionRequest(1, "Close"))).StatusCode);
        var close = await Post(s.Checker, $"{s.Root}/periods/{original.Id}/close", new PeriodActionRequest(2, "Reconciled"));
        Assert.Equal(HttpStatusCode.OK, close.StatusCode);
        var checkpoint = (await close.Content.ReadFromJsonAsync<PeriodResponse>())!.Snapshot!;
        var next = await Created<PeriodResponse>(s.Maker, $"{s.Root}/periods", new CreatePeriodRequest("November", new(2026, 11, 1), new(2026, 12, 1)));
        var created = await Post(s.Maker, $"{s.Root}/sales/{invoice.Id}/price-credits", new RequestSalesPriceCredit(5, new(2026, 11, 2), "Reduction", "REF", [new(line.LineId, 25)]));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var note = (await created.Content.ReadFromJsonAsync<SalesCreditResponse>())!;
        Assert.Equal(HttpStatusCode.Conflict, (await Post(s.Maker, $"{s.Root}/periods/{next.Id}/request-close", new PeriodActionRequest(1, "Close"))).StatusCode);
        await ApproveCredit(s, invoice.Id, note);
        Assert.Equal(HttpStatusCode.OK, (await Post(s.Maker, $"{s.Root}/periods/{next.Id}/request-close", new PeriodActionRequest(1, "Close"))).StatusCode);
        close = await Post(s.Checker, $"{s.Root}/periods/{next.Id}/close", new PeriodActionRequest(2, "Reconciled"));
        Assert.Equal(HttpStatusCode.OK, close.StatusCode);
        var corrected = (await close.Content.ReadFromJsonAsync<PeriodResponse>())!.Snapshot!.Sales!;
        Assert.Equal(-25m, corrected.NetAmount);
        Assert.Equal(-1.25m, corrected.OutputTaxAmount);
        Assert.Equal(8m, (await Balance(s)).Quantity);
        var unchanged = (await s.Maker.GetFromJsonAsync<PeriodResponse>($"{s.Root}/periods/{original.Id}"))!.Snapshot!;
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(checkpoint), System.Text.Json.JsonSerializer.Serialize(unchanged));
    }

    [Fact]
    public async Task Price_request_scope_gate_independent_approval_and_cancel_are_enforced()
    {
        var (s, invoice) = await PostedSale();
        var line = (await Returnable(s, invoice.Id)).Lines.First();
        var path = $"{s.Root}/sales/{invoice.Id}/price-credits";
        var body = new RequestSalesPriceCredit(5, new(2026, 10, 24), "Reduction", "REF", [new(line.LineId, 25)]);
        using var wrongTenant = factory.Client(Guid.NewGuid(), s.Company);
        using var wrongCompany = factory.Client(s.Tenant, Guid.NewGuid());
        using var noPermission = factory.Client(s.Tenant, s.Company, permissions: [Permissions.SalesPost]);
        Assert.Equal(HttpStatusCode.NotFound, (await Post(wrongTenant, path, body)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(wrongCompany, path, body)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(noPermission, path, body)).StatusCode);
        var note = await Price(s, invoice.Id, 5, line.LineId, 25);
        var approval = $"{s.Root}/sales/{invoice.Id}/credit-notes/{note.Id}/approve";
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(s.Maker, approval, new TransitionRequest(6))).StatusCode);
        using var disabledHost = factory.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) =>
            c.AddInMemoryCollection(new Dictionary<string, string?> { ["Posting:Enabled"] = "false" })));
        using var disabled = disabledHost.CreateClient();
        disabled.DefaultRequestHeaders.Authorization = s.Checker.DefaultRequestHeaders.Authorization;
        Assert.Equal(HttpStatusCode.Conflict, (await Post(disabled, approval, new TransitionRequest(6))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Post(s.Maker, $"{s.Root}/sales/{invoice.Id}/credit-notes/{note.Id}/cancel", new CancelSalesInvoiceRequest(6, "Withdrawn"))).StatusCode);
        Assert.Equal(100m, (await Returnable(s, invoice.Id)).Lines.Single(x => x.LineId == line.LineId).RemainingNetAmount);
        var replacement = await Price(s, invoice.Id, 7, line.LineId, 25);
        await ApproveCredit(s, invoice.Id, replacement);
    }

    [Fact]
    public async Task Price_event_is_separate_and_corrupted_financial_entry_blocks_period_close()
    {
        var (s, invoice) = await PostedSale();
        var line = (await Returnable(s, invoice.Id)).Lines.First();
        var note = await Price(s, invoice.Id, 5, line.LineId, 25);
        await ApproveCredit(s, invoice.Id, note);
        var payload = await factory.Inspect(db => db.Outbox.Where(x => x.AggregateId == invoice.Id && x.EventType == nameof(SalesPriceCreditPostedV1)).Select(x => x.Payload).SingleAsync());
        using var envelope = System.Text.Json.JsonDocument.Parse(payload);
        Assert.Equal(0m, envelope.RootElement.GetProperty("payload").GetProperty("lines")[0].GetProperty("quantity").GetDecimal());
        using var scope = factory.Services.CreateScope();
        var processed = await scope.ServiceProvider.GetRequiredService<IIncomingEventProcessor>().Process(new(System.Text.Encoding.UTF8.GetBytes(payload),
            envelope.RootElement.GetProperty("eventId").GetGuid().ToString(), nameof(SalesPriceCreditPostedV1), "application/json"), default);
        Assert.Equal("Ignored", processed.Status);
        await factory.Inspect(db => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE sales.posting_entries SET \"OutputTaxAmount\" = \"OutputTaxAmount\" - 1 WHERE \"CreditNoteId\" = {note.Id}"));
        Assert.False((await s.Maker.GetFromJsonAsync<SalesReconciliation>($"{s.Root}/sales/{invoice.Id}/reconciliation"))!.IsReconciled);
        var period = (await s.Maker.GetFromJsonAsync<PeriodResponse[]>($"{s.Root}/periods"))!.Single();
        Assert.Equal(HttpStatusCode.OK, (await Post(s.Maker, $"{s.Root}/periods/{period.Id}/request-close", new PeriodActionRequest(1, "Close"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(s.Checker, $"{s.Root}/periods/{period.Id}/close", new PeriodActionRequest(2, "Review"))).StatusCode);
    }

    private sealed class FailFirstPriceCommit : SaveChangesInterceptor
    {
        private int _failed;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (data.Context!.ChangeTracker.Entries<SalesPostingEntry>().Any(x => x.State == EntityState.Added && x.Entity.EntryType == "PriceCredit") &&
                Interlocked.Exchange(ref _failed, 1) == 0) throw new InvalidOperationException("Injected price-credit failure before journal commit.");
            return ValueTask.FromResult(result);
        }
    }

}
