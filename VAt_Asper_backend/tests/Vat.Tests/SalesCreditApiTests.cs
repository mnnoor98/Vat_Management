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
public sealed class SalesCreditApiTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
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

    [Fact]
    public async Task Full_credit_returns_goods_negates_frozen_tax_and_preserves_original_entries()
    {
        var (s, invoice) = await PostedSale();
        var path = $"{s.Root}/sales/{invoice.Id}";
        var originals = await factory.Inspect(db => db.Set<SalesPostingEntry>().AsNoTracking().Where(x => x.InvoiceId == invoice.Id).OrderBy(x => x.Id).ToArrayAsync());
        var note = await Request(s, invoice.Id);
        Assert.Equal(-200m, note.NetAmount);
        Assert.Equal(-10m, note.OutputTaxAmount);
        Assert.Equal(-210m, note.GrossAmount);
        Assert.Equal(5, note.OriginalPostedVersion);
        Assert.True((await s.Maker.GetFromJsonAsync<SalesReconciliation>($"{path}/reconciliation"))!.IsReconciled);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(s.Maker, $"{path}/credit-notes/{note.Id}/approve", new TransitionRequest(6))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(s.Checker, $"{path}/credit-notes/{note.Id}/approve", new TransitionRequest(5))).StatusCode);
        var key = Guid.NewGuid().ToString();
        var approved = await Post(s.Checker, $"{path}/credit-notes/{note.Id}/approve", new TransitionRequest(6), key);
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        var replay = await Post(s.Checker, $"{path}/credit-notes/{note.Id}/approve", new TransitionRequest(6), key);
        Assert.Equal(await approved.Content.ReadAsStringAsync(), await replay.Content.ReadAsStringAsync());
        Assert.Equal($"CN-{note.Id:N}", (await approved.Content.ReadFromJsonAsync<SalesCreditResponse>())!.DocumentNumber);
        var current = (await s.Maker.GetFromJsonAsync<SalesInvoiceResponse>(path))!;
        Assert.Equal("Credited", current.Status);
        Assert.Equal(7, current.Version);
        Assert.Equal(5, current.PostedVersion);
        Assert.Equal(2.5m, (await Balance(s)).Quantity);
        Assert.Equal(0m, (await Balance(s)).ReservedQuantity);
        var rows = await factory.Inspect(db => db.Set<SalesPostingEntry>().AsNoTracking().Where(x => x.InvoiceId == invoice.Id).ToArrayAsync());
        Assert.Equal(4, rows.Length);
        foreach (var original in originals)
        {
            var stored = rows.Single(x => x.Id == original.Id);
            Assert.Equal(System.Text.Json.JsonSerializer.Serialize(original), System.Text.Json.JsonSerializer.Serialize(stored));
            var correction = rows.Single(x => x.ReversesEntryId == original.Id);
            Assert.Equal(note.Id, correction.CreditNoteId);
            Assert.Equal(-original.Quantity, correction.Quantity);
            Assert.Equal(-original.OutputTaxAmount, correction.OutputTaxAmount);
        }
        Assert.True((await s.Maker.GetFromJsonAsync<SalesReconciliation>($"{path}/reconciliation"))!.IsReconciled);
        var summary = (await s.Maker.GetFromJsonAsync<SalesSummary>($"{s.Root}/sales/summary?from=2026-10-01&until=2026-11-01"))!;
        Assert.Equal(0m, summary.GrossAmount);
        Assert.Equal(1, summary.InvoiceCount);
        Assert.Equal(1, summary.CreditNoteCount);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(s.Maker, $"{path}/credit-notes", new RequestSalesCredit(7, new(2026, 10, 24), "Again", "REF"))).StatusCode);
        var payload = await factory.Inspect(db => db.Outbox.Where(x => x.AggregateId == invoice.Id && x.EventType == nameof(SalesCreditPostedV1)).Select(x => x.Payload).SingleAsync());
        using var envelope = System.Text.Json.JsonDocument.Parse(payload);
        using var scope = factory.Services.CreateScope();
        var processed = await scope.ServiceProvider.GetRequiredService<IIncomingEventProcessor>().Process(new(
            System.Text.Encoding.UTF8.GetBytes(payload), envelope.RootElement.GetProperty("eventId").GetGuid().ToString(), nameof(SalesCreditPostedV1), "application/json"), default);
        Assert.Equal("Ignored", processed.Status);
    }

    [Fact]
    public async Task Cancelled_credit_preserves_posted_version_and_allows_new_independent_request()
    {
        var (s, invoice) = await PostedSale();
        var path = $"{s.Root}/sales/{invoice.Id}";
        var first = await Request(s, invoice.Id);
        Assert.Equal(HttpStatusCode.OK, (await Post(s.Maker, $"{path}/credit-notes/{first.Id}/cancel", new CancelSalesInvoiceRequest(6, "Return withdrawn"))).StatusCode);
        var source = (await s.Maker.GetFromJsonAsync<SalesInvoiceResponse>(path))!;
        Assert.Equal("Posted", source.Status);
        Assert.Equal(7, source.Version);
        Assert.Equal(5, source.PostedVersion);
        Assert.Equal(0.5m, (await Balance(s)).Quantity);
        Assert.True((await s.Maker.GetFromJsonAsync<SalesReconciliation>($"{path}/reconciliation"))!.IsReconciled);
        var second = await Request(s, invoice.Id, 7);
        Assert.Equal(8, second.SourceVersion);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(s.Checker, $"{path}/credit-notes/{first.Id}/approve", new TransitionRequest(6))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Post(s.Checker, $"{path}/credit-notes/{second.Id}/approve", new TransitionRequest(8))).StatusCode);
        Assert.True((await s.Maker.GetFromJsonAsync<SalesReconciliation>($"{path}/reconciliation"))!.IsReconciled);
        var history = (await s.Maker.GetFromJsonAsync<SalesCreditResponse[]>($"{path}/credit-notes"))!;
        Assert.Equal(2, history.Length);
        Assert.Contains(history, x => x.Status == "Cancelled");
        Assert.Contains(history, x => x.Status == "Posted");
    }

    [Fact]
    public async Task Concurrent_approvals_restore_stock_and_post_credit_only_once()
    {
        var (s, invoice) = await PostedSale();
        var note = await Request(s, invoice.Id);
        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Post(s.Checker,
            $"{s.Root}/sales/{invoice.Id}/credit-notes/{note.Id}/approve", new TransitionRequest(6))));
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.OK);
        Assert.All(results.Where(x => x.StatusCode != HttpStatusCode.OK), x => Assert.Equal(HttpStatusCode.Conflict, x.StatusCode));
        Assert.Equal(2.5m, (await Balance(s)).Quantity);
        Assert.Equal(2, await factory.Inspect(db => db.Set<SalesPostingEntry>().CountAsync(x => x.CreditNoteId == note.Id)));
        Assert.Equal(1, await factory.Inspect(db => db.Outbox.CountAsync(x => x.AggregateId == invoice.Id && x.EventType == nameof(SalesCreditPostedV1))));
    }

    [Fact]
    public async Task Concurrent_approval_and_cancel_leave_one_decision_and_matching_stock()
    {
        var (s, invoice) = await PostedSale();
        var note = await Request(s, invoice.Id);
        var path = $"{s.Root}/sales/{invoice.Id}/credit-notes/{note.Id}";
        var results = await Task.WhenAll(Post(s.Checker, $"{path}/approve", new TransitionRequest(6)),
            Post(s.Maker, $"{path}/cancel", new CancelSalesInvoiceRequest(6, "Withdrawn")));
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.Conflict);
        var current = (await s.Maker.GetFromJsonAsync<SalesCreditResponse>(path))!;
        Assert.Equal(current.Status == "Posted" ? 2.5m : 0.5m, (await Balance(s)).Quantity);
        Assert.True((await s.Maker.GetFromJsonAsync<SalesReconciliation>($"{s.Root}/sales/{invoice.Id}/reconciliation"))!.IsReconciled);
    }

    [Fact]
    public async Task Credit_request_and_period_close_are_serialized()
    {
        var (s, invoice) = await PostedSale();
        var period = (await s.Maker.GetFromJsonAsync<PeriodResponse[]>($"{s.Root}/periods"))!.Single();
        var results = await Task.WhenAll(Post(s.Maker, $"{s.Root}/sales/{invoice.Id}/credit-notes",
            new RequestSalesCredit(5, new(2026, 10, 24), "Full return", "REF")),
            Post(s.Maker, $"{s.Root}/periods/{period.Id}/request-close", new PeriodActionRequest(1, "Close")));
        Assert.Single(results, x => x.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created);
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Return_after_original_period_closed_posts_in_new_period_and_keeps_old_checkpoint()
    {
        var (s, invoice) = await PostedSale();
        var original = (await s.Maker.GetFromJsonAsync<PeriodResponse[]>($"{s.Root}/periods"))!.Single();
        Assert.Equal(HttpStatusCode.OK, (await Post(s.Maker, $"{s.Root}/periods/{original.Id}/request-close", new PeriodActionRequest(1, "Close October"))).StatusCode);
        var closed = await Post(s.Checker, $"{s.Root}/periods/{original.Id}/close", new PeriodActionRequest(2, "Reviewed"));
        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);
        var snapshot = (await closed.Content.ReadFromJsonAsync<PeriodResponse>())!.Snapshot!;
        Assert.Equal(10m, snapshot.Sales!.OutputTaxAmount);
        var next = await Created<PeriodResponse>(s.Maker, $"{s.Root}/periods", new CreatePeriodRequest("November", new(2026, 11, 1), new(2026, 12, 1)));
        Assert.Equal(HttpStatusCode.Conflict, (await Post(s.Maker, $"{s.Root}/sales/{invoice.Id}/credit-notes", new RequestSalesCredit(5, new(2026, 10, 24), "Return", "REF"))).StatusCode);
        var note = await Request(s, invoice.Id, date: new(2026, 11, 2));
        Assert.Equal(HttpStatusCode.Conflict, (await Post(s.Maker, $"{s.Root}/periods/{next.Id}/request-close", new PeriodActionRequest(1, "Close"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Post(s.Checker, $"{s.Root}/sales/{invoice.Id}/credit-notes/{note.Id}/approve", new TransitionRequest(6))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Post(s.Maker, $"{s.Root}/periods/{next.Id}/request-close", new PeriodActionRequest(1, "Close"))).StatusCode);
        closed = await Post(s.Checker, $"{s.Root}/periods/{next.Id}/close", new PeriodActionRequest(2, "Reviewed"));
        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);
        var correctionSnapshot = (await closed.Content.ReadFromJsonAsync<PeriodResponse>())!.Snapshot!;
        Assert.Equal(-10m, correctionSnapshot.Sales!.OutputTaxAmount);
        Assert.Equal(-200m, correctionSnapshot.Sales.NetAmount);
        Assert.Equal(2, correctionSnapshot.Sales.EntryIds.Count);
        var unchanged = (await s.Maker.GetFromJsonAsync<PeriodResponse>($"{s.Root}/periods/{original.Id}"))!.Snapshot!;
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(snapshot), System.Text.Json.JsonSerializer.Serialize(unchanged));
        var november = (await s.Maker.GetFromJsonAsync<SalesSummary>($"{s.Root}/sales/summary?from=2026-11-01&until=2026-12-01"))!;
        Assert.Equal(0, november.InvoiceCount);
        Assert.Equal(1, november.CreditNoteCount);
        Assert.Equal(-210m, november.GrossAmount);
    }

    [Fact]
    public async Task Failure_after_stock_return_sql_rolls_back_and_same_key_retry_recovers()
    {
        var (s, invoice) = await PostedSale();
        var note = await Request(s, invoice.Id);
        var interceptor = new FailFirstCreditCommit();
        using var host = factory.WithWebHostBuilder(b => b.ConfigureTestServices(services =>
            services.AddDbContext<Vat.Infrastructure.Persistence.VatDbContext>(o => o.AddInterceptors(interceptor))));
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = s.Checker.DefaultRequestHeaders.Authorization;
        var path = $"{s.Root}/sales/{invoice.Id}/credit-notes/{note.Id}";
        var key = Guid.NewGuid().ToString();
        Assert.Equal(HttpStatusCode.InternalServerError, (await Post(client, $"{path}/approve", new TransitionRequest(6), key)).StatusCode);
        Assert.Equal(0.5m, (await Balance(s)).Quantity);
        Assert.Equal("Pending", (await s.Maker.GetFromJsonAsync<SalesCreditResponse>(path))!.Status);
        Assert.Equal(0, await factory.Inspect(db => db.Set<SalesPostingEntry>().CountAsync(x => x.CreditNoteId == note.Id)));
        Assert.Equal(0, await factory.Inspect(db => db.Outbox.CountAsync(x => x.AggregateId == invoice.Id && x.EventType == nameof(SalesCreditPostedV1))));
        Assert.Equal(HttpStatusCode.OK, (await Post(client, $"{path}/approve", new TransitionRequest(6), key)).StatusCode);
        Assert.Equal(2.5m, (await Balance(s)).Quantity);
    }

    [Fact]
    public async Task Approval_requires_scoped_permission_gate_and_checker_distinct_from_requester_and_maker()
    {
        var (s, invoice) = await PostedSale();
        var root = $"{s.Root}/sales/{invoice.Id}/credit-notes";
        var request = await Post(s.Checker, root, new RequestSalesCredit(5, new(2026, 10, 24), "Return", "REF"));
        Assert.Equal(HttpStatusCode.Created, request.StatusCode);
        var note = (await request.Content.ReadFromJsonAsync<SalesCreditResponse>())!;
        var path = $"{root}/{note.Id}";
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(s.Checker, $"{path}/approve", new TransitionRequest(6))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(s.Maker, $"{path}/approve", new TransitionRequest(6))).StatusCode);
        using var noPermission = factory.Client(s.Tenant, s.Company, "reviewer", [Permissions.SalesPost]);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(noPermission, $"{path}/approve", new TransitionRequest(6))).StatusCode);
        using var otherTenant = factory.Client(Guid.NewGuid(), s.Company, "reviewer");
        using var otherCompany = factory.Client(s.Tenant, Guid.NewGuid(), "reviewer");
        Assert.Equal(HttpStatusCode.NotFound, (await otherTenant.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await otherCompany.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Post(otherTenant, $"{path}/approve", new TransitionRequest(6))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(otherCompany, $"{path}/cancel", new CancelSalesInvoiceRequest(6, "Wrong company"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await s.Maker.GetAsync($"{s.Root}/sales/{Guid.NewGuid()}/credit-notes/{note.Id}")).StatusCode);
        using var reviewer = factory.Client(s.Tenant, s.Company, "credit-reviewer");
        using var disabledHost = factory.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) =>
            c.AddInMemoryCollection(new Dictionary<string, string?> { ["Posting:Enabled"] = "false" })));
        using var disabled = disabledHost.CreateClient();
        disabled.DefaultRequestHeaders.Authorization = reviewer.DefaultRequestHeaders.Authorization;
        Assert.Equal(HttpStatusCode.Conflict, (await Post(disabled, $"{path}/approve", new TransitionRequest(6))).StatusCode);
        Assert.Equal(0.5m, (await Balance(s)).Quantity);
        Assert.Equal(HttpStatusCode.OK, (await Post(reviewer, $"{path}/approve", new TransitionRequest(6))).StatusCode);
    }

    [Fact]
    public async Task Invalid_request_and_damaged_original_never_admit_or_post_credit()
    {
        var (s, invoice) = await PostedSale();
        var root = $"{s.Root}/sales/{invoice.Id}/credit-notes";
        foreach (var invalid in new[] {
            new RequestSalesCredit(5, new(2026, 10, 22), "Return", "REF"),
            new RequestSalesCredit(5, new(2026, 10, 24), " ", "REF"),
            new RequestSalesCredit(5, new(2026, 10, 24), "Return", " ") })
            Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Post(s.Maker, root, invalid)).StatusCode);
        Assert.Equal(0, await factory.Inspect(db => db.Set<SalesCreditNote>().CountAsync(x => x.InvoiceId == invoice.Id)));
        await factory.Inspect(db => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE sales.posting_entries SET \"OutputTaxAmount\" = \"OutputTaxAmount\" + 1 WHERE \"InvoiceId\" = {invoice.Id}"));
        Assert.Equal(HttpStatusCode.Conflict, (await Post(s.Maker, root, new RequestSalesCredit(5, new(2026, 10, 24), "Return", "REF"))).StatusCode);
        Assert.Equal("Posted", (await s.Maker.GetFromJsonAsync<SalesInvoiceResponse>($"{s.Root}/sales/{invoice.Id}"))!.Status);
        await factory.Inspect(db => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE sales.posting_entries SET \"OutputTaxAmount\" = \"OutputTaxAmount\" - 1 WHERE \"InvoiceId\" = {invoice.Id}"));
        var note = await Request(s, invoice.Id);
        await factory.Inspect(db => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE sales.posting_entries SET \"OutputTaxAmount\" = \"OutputTaxAmount\" + 1 WHERE \"InvoiceId\" = {invoice.Id}"));
        Assert.Equal(HttpStatusCode.Conflict, (await Post(s.Checker, $"{root}/{note.Id}/approve", new TransitionRequest(6))).StatusCode);
        Assert.Equal(0.5m, (await Balance(s)).Quantity);
    }

    [Fact]
    public async Task Return_preserves_other_invoice_reservations_and_detects_missing_correction_at_close()
    {
        var (s, invoice) = await PostedSale();
        var another = await Sale(s, 0.5m);
        await Reserve(s, another.Id);
        var note = await Request(s, invoice.Id);
        Assert.Equal(HttpStatusCode.OK, (await Post(s.Checker, $"{s.Root}/sales/{invoice.Id}/credit-notes/{note.Id}/approve", new TransitionRequest(6))).StatusCode);
        Assert.Equal(2.5m, (await Balance(s)).Quantity);
        Assert.Equal(0.5m, (await Balance(s)).ReservedQuantity);
        Assert.Equal(HttpStatusCode.OK, (await Post(s.Maker, $"{s.Root}/sales/{another.Id}/cancel", new CancelSalesInvoiceRequest(4, "Withdrawn"))).StatusCode);
        await factory.Inspect(db => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM sales.posting_entries WHERE \"CreditNoteId\" = {note.Id}"));
        var period = (await s.Maker.GetFromJsonAsync<PeriodResponse[]>($"{s.Root}/periods"))!.Single();
        Assert.Equal(HttpStatusCode.OK, (await Post(s.Maker, $"{s.Root}/periods/{period.Id}/request-close", new PeriodActionRequest(1, "Close"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(s.Checker, $"{s.Root}/periods/{period.Id}/close", new PeriodActionRequest(2, "Review"))).StatusCode);
    }

    private sealed class FailFirstCreditCommit : SaveChangesInterceptor
    {
        private int _failed;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (data.Context!.ChangeTracker.Entries<SalesPostingEntry>().Any(x => x.State == EntityState.Added && x.Entity.CreditNoteId != null) &&
                Interlocked.Exchange(ref _failed, 1) == 0) throw new InvalidOperationException("Injected failure after stock return SQL.");
            return ValueTask.FromResult(result);
        }
    }

}
