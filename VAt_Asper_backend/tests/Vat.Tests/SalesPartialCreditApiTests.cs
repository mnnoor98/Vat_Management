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
public sealed class SalesPartialCreditApiTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
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

    [Fact]
    public async Task Partial_then_remaining_return_reuses_original_links_without_overcrediting()
    {
        var (s, invoice) = await PostedSale();
        var before = await Returnable(s, invoice.Id);
        var selected = before.Lines[0];
        var first = await Partial(s, invoice.Id, 5, new(selected.LineId, 0.5m));
        Assert.Equal("PartialReturn", first.Mode);
        Assert.Equal(-50m, first.NetAmount);
        Assert.Equal(-2.5m, first.OutputTaxAmount);
        var pending = await Returnable(s, invoice.Id);
        Assert.False(pending.CanRequestCredit);
        Assert.Equal(0.5m, pending.Lines.Single(x => x.LineId == selected.LineId).PendingQuantity);
        await ApproveCredit(s, invoice.Id, first);
        Assert.Equal(8.5m, (await Balance(s)).Quantity);
        var remaining = await Returnable(s, invoice.Id);
        Assert.Equal("PartiallyCredited", remaining.Status);
        Assert.True(remaining.CanRequestCredit);
        Assert.Equal(0.5m, remaining.Lines.Single(x => x.LineId == selected.LineId).RemainingQuantity);
        Assert.Equal(50m, remaining.Lines.Single(x => x.LineId == selected.LineId).RemainingNetAmount);
        Assert.Equal(5, (await s.Maker.GetFromJsonAsync<SalesInvoiceResponse>($"{s.Root}/sales/{invoice.Id}"))!.PostedVersion);
        var last = await Request(s, invoice.Id, 7);
        Assert.Equal("PartialReturn", last.Mode);
        Assert.Equal(-150m, last.NetAmount);
        await ApproveCredit(s, invoice.Id, last);
        Assert.Equal(10m, (await Balance(s)).Quantity);
        var completed = await Returnable(s, invoice.Id);
        Assert.Equal("Credited", completed.Status);
        Assert.False(completed.CanRequestCredit);
        Assert.All(completed.Lines, x => { Assert.Equal(0m, x.RemainingQuantity); Assert.Equal(0m, x.RemainingOutputTaxAmount); });
        Assert.Equal(5, await factory.Inspect(db => db.Set<SalesPostingEntry>().CountAsync(x => x.InvoiceId == invoice.Id)));
        var summary = (await s.Maker.GetFromJsonAsync<SalesSummary>($"{s.Root}/sales/summary?from=2026-10-01&until=2026-11-01"))!;
        Assert.Equal(0m, summary.GrossAmount);
        Assert.Equal(2, summary.CreditNoteCount);
        Assert.Equal(0, await factory.Inspect(db => db.Outbox.CountAsync(x => x.AggregateId == invoice.Id && x.EventType == nameof(SalesCreditPostedV1))));
        Assert.Equal(2, await factory.Inspect(db => db.Outbox.CountAsync(x => x.AggregateId == invoice.Id && x.EventType == nameof(SalesPartialCreditPostedV1))));
    }

    [Fact]
    public async Task Cumulative_rounding_reconciles_three_returns_to_exact_original_tax()
    {
        var (s, _) = await Stock();
        var invoice = await Sale(s, lines: [new("ITEM", "Fractional value", 3, 0.3333m)]);
        await Reserve(s, invoice.Id);
        Assert.Equal(HttpStatusCode.OK, (await Post(s.Checker, $"{s.Root}/sales/{invoice.Id}/post", new TransitionRequest(4))).StatusCode);
        var line = (await Returnable(s, invoice.Id)).Lines.Single();
        var taxes = new List<decimal>();
        for (var step = 0; step < 3; step++)
        {
            var note = await Partial(s, invoice.Id, 5 + step * 2, new(line.LineId, 1));
            taxes.Add(note.OutputTaxAmount);
            Assert.Equal(-0.3333m, note.NetAmount);
            await ApproveCredit(s, invoice.Id, note);
        }
        Assert.Equal(new[] { -0.0167m, -0.0166m, -0.0167m }, taxes);
        Assert.Equal(-0.05m, taxes.Sum());
        Assert.True((await s.Maker.GetFromJsonAsync<SalesReconciliation>($"{s.Root}/sales/{invoice.Id}/reconciliation"))!.IsReconciled);
        Assert.Equal(10m, (await Balance(s)).Quantity);
    }

    [Fact]
    public async Task Exact_midpoint_goes_away_from_zero_and_final_return_uses_zero_tax_remainder()
    {
        var (s, _) = await Stock(salesRate: 50m);
        var invoice = await Sale(s, lines: [new("ITEM", "Tiny value", 2, 0.0001m)]);
        await Reserve(s, invoice.Id);
        Assert.Equal(HttpStatusCode.OK, (await Post(s.Checker, $"{s.Root}/sales/{invoice.Id}/post", new TransitionRequest(4))).StatusCode);
        var line = (await Returnable(s, invoice.Id)).Lines.Single();
        var first = await Partial(s, invoice.Id, 5, new(line.LineId, 1));
        Assert.Equal(-0.0001m, first.OutputTaxAmount);
        await ApproveCredit(s, invoice.Id, first);
        var last = await Partial(s, invoice.Id, 7, new(line.LineId, 1));
        Assert.Equal(0m, last.OutputTaxAmount);
        Assert.Equal(-0.0001m, last.NetAmount);
        await ApproveCredit(s, invoice.Id, last);
        Assert.Equal("Credited", (await Returnable(s, invoice.Id)).Status);
    }

    [Fact]
    public async Task Invalid_duplicate_foreign_and_excessive_return_lines_leave_source_unchanged()
    {
        var (s, invoice) = await PostedSale();
        var line = (await Returnable(s, invoice.Id)).Lines.First();
        var root = $"{s.Root}/sales/{invoice.Id}/credit-notes";
        IReadOnlyList<SalesReturnLineInput>[] invalid = [[], [new(line.LineId, 0)], [new(line.LineId, -1)],
            [new(line.LineId, 0.0000001m)], [new(Guid.NewGuid(), 1)], [new(line.LineId, 0.5m), new(line.LineId, 0.5m)], [null!]];
        foreach (var lines in invalid)
            Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Post(s.Maker, root, new RequestSalesCredit(5, new(2026, 10, 24), "Return", "REF", lines))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(s.Maker, root, new RequestSalesCredit(5, new(2026, 10, 24), "Return", "REF", [new(line.LineId, 1.000001m)]))).StatusCode);
        Assert.Equal(5, (await Returnable(s, invoice.Id)).Version);
        Assert.Equal(0, await factory.Inspect(db => db.Set<SalesCreditNote>().CountAsync(x => x.InvoiceId == invoice.Id)));
        var first = await Partial(s, invoice.Id, 5, new(line.LineId, 0.75m));
        await ApproveCredit(s, invoice.Id, first);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(s.Maker, root, new RequestSalesCredit(7, new(2026, 10, 24), "Return", "REF", [new(line.LineId, 0.250001m)]))).StatusCode);
        Assert.Equal(8.75m, (await Balance(s)).Quantity);
    }

    [Fact]
    public async Task Cancelled_partial_does_not_consume_capacity_or_rounding_allocation()
    {
        var (s, invoice) = await PostedSale();
        var line = (await Returnable(s, invoice.Id)).Lines.First();
        var first = await Partial(s, invoice.Id, 5, new(line.LineId, 0.25m));
        await ApproveCredit(s, invoice.Id, first);
        var abandoned = await Partial(s, invoice.Id, 7, new(line.LineId, 0.5m));
        Assert.Equal(HttpStatusCode.OK, (await Post(s.Maker, $"{s.Root}/sales/{invoice.Id}/credit-notes/{abandoned.Id}/cancel", new CancelSalesInvoiceRequest(8, "Withdrawn"))).StatusCode);
        var state = await Returnable(s, invoice.Id);
        Assert.Equal("PartiallyCredited", state.Status);
        Assert.Equal(9, state.Version);
        Assert.Equal(0.75m, state.Lines.Single(x => x.LineId == line.LineId).RemainingQuantity);
        var replacement = await Partial(s, invoice.Id, 9, new(line.LineId, 0.75m));
        Assert.Equal(-75m, replacement.NetAmount);
        await ApproveCredit(s, invoice.Id, replacement);
        Assert.Equal("PartiallyCredited", (await Returnable(s, invoice.Id)).Status);
        Assert.Equal(9m, (await Balance(s)).Quantity);
    }

    [Fact]
    public async Task Competing_partial_requests_and_duplicate_approvals_consume_capacity_once()
    {
        var (s, invoice) = await PostedSale();
        var line = (await Returnable(s, invoice.Id)).Lines.First();
        var path = $"{s.Root}/sales/{invoice.Id}/credit-notes";
        var results = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => Post(s.Maker, path,
            new RequestSalesCredit(5, new(2026, 10, 24), "Return", "REF", [new(line.LineId, 0.75m)]))));
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.Created);
        Assert.All(results.Where(x => x.StatusCode != HttpStatusCode.Created), x => Assert.Equal(HttpStatusCode.Conflict, x.StatusCode));
        var note = (await results.Single(x => x.StatusCode == HttpStatusCode.Created).Content.ReadFromJsonAsync<SalesCreditResponse>())!;
        results = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => Post(s.Checker, $"{path}/{note.Id}/approve", new TransitionRequest(6))));
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Equal(8.75m, (await Balance(s)).Quantity);
        Assert.Equal(1, await factory.Inspect(db => db.Set<SalesPostingEntry>().CountAsync(x => x.CreditNoteId == note.Id)));
    }

    [Fact]
    public async Task Partial_failure_rolls_back_stock_and_frozen_capacity_then_same_key_recovers()
    {
        var (s, invoice) = await PostedSale();
        var line = (await Returnable(s, invoice.Id)).Lines.First();
        var note = await Partial(s, invoice.Id, 5, new(line.LineId, 0.5m));
        var interceptor = new FailFirstPartialCommit();
        using var host = factory.WithWebHostBuilder(b => b.ConfigureTestServices(services =>
            services.AddDbContext<Vat.Infrastructure.Persistence.VatDbContext>(o => o.AddInterceptors(interceptor))));
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = s.Checker.DefaultRequestHeaders.Authorization;
        var path = $"{s.Root}/sales/{invoice.Id}/credit-notes/{note.Id}/approve";
        var key = Guid.NewGuid().ToString();
        Assert.Equal(HttpStatusCode.InternalServerError, (await Post(client, path, new TransitionRequest(6), key)).StatusCode);
        Assert.Equal(8m, (await Balance(s)).Quantity);
        var state = await Returnable(s, invoice.Id);
        Assert.Equal(0m, state.Lines.Single(x => x.LineId == line.LineId).ReturnedQuantity);
        Assert.Equal(0.5m, state.Lines.Single(x => x.LineId == line.LineId).PendingQuantity);
        Assert.Equal(HttpStatusCode.OK, (await Post(client, path, new TransitionRequest(6), key)).StatusCode);
        Assert.Equal(8.5m, (await Balance(s)).Quantity);
    }

    [Fact]
    public async Task Partial_event_is_separate_and_returnable_read_is_tenant_and_permission_scoped()
    {
        var (s, invoice) = await PostedSale();
        var line = (await Returnable(s, invoice.Id)).Lines.First();
        var note = await Partial(s, invoice.Id, 5, new(line.LineId, 0.25m));
        await ApproveCredit(s, invoice.Id, note);
        var payload = await factory.Inspect(db => db.Outbox.Where(x => x.AggregateId == invoice.Id && x.EventType == nameof(SalesPartialCreditPostedV1)).Select(x => x.Payload).SingleAsync());
        using var envelope = System.Text.Json.JsonDocument.Parse(payload);
        var body = envelope.RootElement.GetProperty("payload");
        Assert.Equal(-25m, body.GetProperty("netAmount").GetDecimal());
        Assert.Equal(0.25m, body.GetProperty("lines")[0].GetProperty("quantity").GetDecimal());
        using var scope = factory.Services.CreateScope();
        var processed = await scope.ServiceProvider.GetRequiredService<IIncomingEventProcessor>().Process(new(
            System.Text.Encoding.UTF8.GetBytes(payload), envelope.RootElement.GetProperty("eventId").GetGuid().ToString(), nameof(SalesPartialCreditPostedV1), "application/json"), default);
        Assert.Equal("Ignored", processed.Status);
        using var otherTenant = factory.Client(Guid.NewGuid(), s.Company);
        using var otherCompany = factory.Client(s.Tenant, Guid.NewGuid());
        using var noRead = factory.Client(s.Tenant, s.Company, permissions: [Permissions.SalesCreditRequest]);
        var path = $"{s.Root}/sales/{invoice.Id}/returnable-lines";
        Assert.Equal(HttpStatusCode.NotFound, (await otherTenant.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await otherCompany.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await noRead.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task Corrupt_partial_allocation_blocks_reconciliation_and_final_period_close()
    {
        var (s, invoice) = await PostedSale();
        var line = (await Returnable(s, invoice.Id)).Lines.First();
        var note = await Partial(s, invoice.Id, 5, new(line.LineId, 0.5m));
        await ApproveCredit(s, invoice.Id, note);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Post(s.Maker, $"{s.Root}/sales/{invoice.Id}/credit-notes",
            new RequestSalesCredit(7, new(2026, 10, 23), "Backdated", "REF", [new(line.LineId, 0.25m)]))).StatusCode);
        await factory.Inspect(db => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE sales.credit_notes SET \"Lines\" = '[]'::jsonb WHERE \"Id\" = {note.Id}"));
        var reconciliation = (await s.Maker.GetFromJsonAsync<SalesReconciliation>($"{s.Root}/sales/{invoice.Id}/reconciliation"))!;
        Assert.False(reconciliation.IsReconciled);
        var period = (await s.Maker.GetFromJsonAsync<PeriodResponse[]>($"{s.Root}/periods"))!.Single();
        Assert.Equal(HttpStatusCode.OK, (await Post(s.Maker, $"{s.Root}/periods/{period.Id}/request-close", new PeriodActionRequest(1, "Close"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(s.Checker, $"{s.Root}/periods/{period.Id}/close", new PeriodActionRequest(2, "Reviewed"))).StatusCode);
    }

    [Fact]
    public async Task Migration_allocation_backfill_preserves_legacy_pending_full_credit()
    {
        var (s, invoice) = await PostedSale();
        var note = await Request(s, invoice.Id);
        await factory.Inspect(db => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE sales.credit_notes SET \"Lines\" = '[]'::jsonb WHERE \"Id\" = {note.Id}"));
        var migration = new Vat.Infrastructure.Persistence.Migrations.PartialSalesReturns();
        var backfill = migration.UpOperations.OfType<Microsoft.EntityFrameworkCore.Migrations.Operations.SqlOperation>().Single().Sql;
        await factory.Inspect(db => db.Database.ExecuteSqlRawAsync(backfill));
        var restored = (await s.Maker.GetFromJsonAsync<SalesCreditResponse>($"{s.Root}/sales/{invoice.Id}/credit-notes/{note.Id}"))!;
        Assert.Equal("FullInvoice", restored.Mode);
        Assert.Equal(2, restored.Lines.Count);
        Assert.Equal(-10m, restored.Lines.Sum(x => x.OutputTaxAmount));
        await ApproveCredit(s, invoice.Id, restored);
        Assert.Equal("Credited", (await Returnable(s, invoice.Id)).Status);
        Assert.Equal(1, await factory.Inspect(db => db.Outbox.CountAsync(x => x.AggregateId == invoice.Id && x.EventType == nameof(SalesCreditPostedV1))));
    }

    private sealed class FailFirstPartialCommit : SaveChangesInterceptor
    {
        private int _failed;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (data.Context!.ChangeTracker.Entries<SalesPostingEntry>().Any(x => x.State == EntityState.Added && x.Entity.CreditNoteId != null) &&
                Interlocked.Exchange(ref _failed, 1) == 0) throw new InvalidOperationException("Injected partial-return failure after stock SQL.");
            return ValueTask.FromResult(result);
        }
    }

}
