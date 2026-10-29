using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Vat.Application.Contracts;
using Vat.Application;
using Vat.Application.Security;
using Vat.Domain.Company;
using Vat.Domain.Posting;
using Xunit;

namespace Vat.Tests;

[Collection("PostgreSQL integration")]
public sealed class PeriodReversalApiTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private sealed record SetupData(Guid Tenant, Guid Company, Guid Branch, Guid Warehouse, Guid Period,
        HttpClient Maker, HttpClient Checker)
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

    private static async Task<T> Created<T>(HttpClient client, string path, object request)
    {
        var response = await Post(client, path, request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private async Task<SetupData> Setup(bool withPeriod = true)
    {
        var tenant = Guid.NewGuid();
        var company = Guid.NewGuid();
        var maker = factory.Client(tenant, company);
        var checker = factory.Client(tenant, company, "checker");
        var root = $"/api/v1/companies/{company}";
        await Created<CompanyResponse>(maker, "/api/v1/companies", new CreateCompanyRequest(company, "Period pilot", Guid.NewGuid().ToString()));
        var branch = await Created<BranchResponse>(maker, $"{root}/branches", new CreateBranchRequest("Dhaka"));
        var warehouse = await Created<WarehouseResponse>(maker, $"{root}/warehouses", new CreateWarehouseRequest(branch.Id, "MAIN", "Main"));
        await Created<ProductResponse>(maker, $"{root}/products", new CreateProductRequest("ITEM", "Test goods", "KG", "TEST"));
        await Created<SupplierResponse>(maker, $"{root}/suppliers", new CreateSupplierRequest("SUP", "Test supplier"));
        var rule = await Created<TaxRuleResponse>(maker, $"{root}/tax/rules", new CreateTaxRuleRequest("TEST", new(2026, 1, 1), new(2027, 1, 1),
            7.5m, 40m, "Synthetic test fixture, not a legal rate", "TEST-EVIDENCE"));
        Assert.Equal(HttpStatusCode.OK, (await Post(checker, $"{root}/tax/rules/{rule.Id}/publish", new TransitionRequest(1))).StatusCode);
        var period = withPeriod ? (await Created<PeriodResponse>(maker, $"{root}/periods", new CreatePeriodRequest("October", new(2026, 10, 1), new(2026, 11, 1)))).Id : Guid.Empty;
        return new(tenant, company, branch.Id, warehouse.Id, period, maker, checker);
    }

    private static async Task<PurchaseResponse> Submitted(SetupData setup)
    {
        var purchase = await Created<PurchaseResponse>(setup.Maker, $"{setup.Root}/purchases", new CreatePurchaseRequest(
            setup.Branch, Guid.NewGuid().ToString(), "SUP", new(2026, 10, 23), [new("ITEM", "Test goods", 2.5m, 100m)]));
        var response = await Post(setup.Maker, $"{setup.Purchase(purchase.Id)}/submit", new TransitionRequest(1));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PurchaseResponse>())!;
    }

    private static async Task<PurchaseResponse> Approved(SetupData setup)
    {
        var purchase = await Submitted(setup);
        var response = await Post(setup.Checker, $"{setup.Purchase(purchase.Id)}/approve", new TransitionRequest(2));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PurchaseResponse>())!;
    }

    private static async Task<PurchaseResponse> Posted(SetupData setup)
    {
        var purchase = await Approved(setup);
        var plan = await Created<PostingPlanResponse>(setup.Maker, $"{setup.Purchase(purchase.Id)}/posting-plans", new PreparePostingRequest(3, setup.Warehouse));
        var response = await Post(setup.Checker, $"{setup.Purchase(purchase.Id)}/posting-plans/{plan.Id}/post", new TransitionRequest(4));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PurchaseResponse>())!;
    }

    private static async Task<PeriodResponse> Close(SetupData setup, Guid? periodId = null)
    {
        var id = periodId ?? setup.Period;
        Assert.Equal(HttpStatusCode.OK, (await Post(setup.Maker, $"{setup.Root}/periods/{id}/request-close", new PeriodActionRequest(1, "Ready for close"))).StatusCode);
        var response = await Post(setup.Checker, $"{setup.Root}/periods/{id}/close", new PeriodActionRequest(2, "Reconciled and reviewed"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PeriodResponse>())!;
    }

    private static Task<ReversalResponse> RequestReversal(SetupData setup, Guid id, DateOnly? date = null, long version = 5) =>
        Created<ReversalResponse>(setup.Maker, $"{setup.Purchase(id)}/reversals", new RequestReversalRequest(version, date ?? new(2026, 10, 24), "Correct entire posting"));

    [Fact]
    public async Task Approval_requires_an_open_period_and_closing_prevents_new_admission()
    {
        var setup = await Setup(withPeriod: false);
        var purchase = await Submitted(setup);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(setup.Checker, $"{setup.Purchase(purchase.Id)}/approve", new TransitionRequest(2))).StatusCode);
        var period = await Created<PeriodResponse>(setup.Maker, $"{setup.Root}/periods", new CreatePeriodRequest("October", new(2026, 10, 1), new(2026, 11, 1)));
        Assert.Equal(HttpStatusCode.OK, (await Post(setup.Maker, $"{setup.Root}/periods/{period.Id}/request-close", new PeriodActionRequest(1, "Closing"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(setup.Checker, $"{setup.Purchase(purchase.Id)}/approve", new TransitionRequest(2))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(setup.Maker, $"{setup.Root}/periods/{period.Id}/close", new PeriodActionRequest(2, "Self approval"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Post(setup.Maker, $"{setup.Root}/periods/{period.Id}/cancel-close", new PeriodActionRequest(2, "More work needed"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Post(setup.Checker, $"{setup.Purchase(purchase.Id)}/approve", new TransitionRequest(2))).StatusCode);
    }

    [Fact]
    public async Task Overlapping_period_creations_serialize_and_exclusive_endpoints_are_adjacent()
    {
        var setup = await Setup(withPeriod: false);
        var request = new CreatePeriodRequest("October", new(2026, 10, 1), new(2026, 11, 1));
        var responses = await Task.WhenAll(Post(setup.Maker, $"{setup.Root}/periods", request), Post(setup.Maker, $"{setup.Root}/periods", request));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
        await Created<PeriodResponse>(setup.Maker, $"{setup.Root}/periods", new CreatePeriodRequest("November", new(2026, 11, 1), new(2026, 12, 1)));
    }

    [Fact]
    public async Task Pending_work_blocks_close_until_the_unposted_purchase_is_explicitly_cancelled()
    {
        var setup = await Setup();
        var purchase = await Approved(setup);
        var path = $"{setup.Root}/periods/{setup.Period}/request-close";
        Assert.Equal(HttpStatusCode.Conflict, (await Post(setup.Maker, path, new PeriodActionRequest(1, "Close"))).StatusCode);
        var plan = await Created<PostingPlanResponse>(setup.Maker, $"{setup.Purchase(purchase.Id)}/posting-plans", new PreparePostingRequest(3, setup.Warehouse));
        Assert.Equal(HttpStatusCode.Conflict, (await Post(setup.Maker, path, new PeriodActionRequest(1, "Close"))).StatusCode);
        await Post(setup.Maker, $"{setup.Purchase(purchase.Id)}/posting-plans/{plan.Id}/cancel", new CancelPostingRequest(4, "Do not post"));
        var cancelled = await Post(setup.Maker, $"{setup.Purchase(purchase.Id)}/cancel-unposted", new CancelPostingRequest(5, "Duplicate source invoice"));
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        var result = (await cancelled.Content.ReadFromJsonAsync<PurchaseResponse>())!;
        Assert.Equal("Cancelled", result.Status);
        Assert.Equal("Duplicate source invoice", result.CancellationReason);
        var closed = await Close(setup);
        Assert.Equal(0, closed.Snapshot!.NetAmount);
        Assert.Empty(closed.Snapshot.StockEntryIds);
    }

    [Fact]
    public async Task Later_period_reversal_preserves_the_original_closed_snapshot_and_entries()
    {
        var setup = await Setup();
        var purchase = await Posted(setup);
        var originalStockId = await factory.Inspect(db => db.Set<StockMovement>().Where(x => x.PurchaseId == purchase.Id).Select(x => x.Id).SingleAsync());
        var originalTaxId = await factory.Inspect(db => db.Set<TaxLedgerEntry>().Where(x => x.PurchaseId == purchase.Id).Select(x => x.Id).SingleAsync());
        var closed = await Close(setup);
        Assert.Equal(250m, closed.Snapshot!.NetAmount);
        Assert.Equal(18.75m, closed.Snapshot.TaxAmount);
        Assert.Equal(64, closed.Snapshot.LedgerFingerprint.Length);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(setup.Maker, $"{setup.Purchase(purchase.Id)}/reversals",
            new RequestReversalRequest(5, new(2026, 10, 24), "Closed period"))).StatusCode);
        var november = await Created<PeriodResponse>(setup.Maker, $"{setup.Root}/periods", new CreatePeriodRequest("November", new(2026, 11, 1), new(2026, 12, 1)));
        var reversal = await RequestReversal(setup, purchase.Id, new(2026, 11, 2));
        Assert.Equal(HttpStatusCode.Conflict, (await Post(setup.Maker, $"{setup.Root}/periods/{november.Id}/request-close", new PeriodActionRequest(1, "Pending reversal"))).StatusCode);
        var key = Guid.NewGuid().ToString();
        var path = $"{setup.Purchase(purchase.Id)}/reversals/{reversal.Id}/approve";
        Assert.Equal(HttpStatusCode.OK, (await Post(setup.Checker, path, new TransitionRequest(6), key)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Post(setup.Checker, path, new TransitionRequest(6), key)).StatusCode);
        var stock = await factory.Inspect(db => db.Set<StockMovement>().Where(x => x.PurchaseId == purchase.Id).ToArrayAsync());
        var tax = await factory.Inspect(db => db.Set<TaxLedgerEntry>().Where(x => x.PurchaseId == purchase.Id).ToArrayAsync());
        Assert.Equal(2, stock.Length);
        Assert.Equal(2.5m, stock.Single(x => x.Id == originalStockId).Quantity);
        Assert.Equal(originalStockId, stock.Single(x => x.Quantity < 0).ReversesEntryId);
        Assert.Equal(18.75m, tax.Single(x => x.Id == originalTaxId).TaxAmount);
        Assert.Equal(originalTaxId, tax.Single(x => x.TaxAmount < 0).ReversesEntryId);
        Assert.Equal(0m, await factory.Inspect(db => db.Set<StockBalance>().Where(x => x.CompanyId == setup.Company).Select(x => x.Quantity).SingleAsync()));
        var reconcile = (await setup.Maker.GetFromJsonAsync<ReconciliationResponse>($"{setup.Purchase(purchase.Id)}/reconciliation"))!;
        Assert.True(reconcile.IsReconciled);
        Assert.Equal(0m, reconcile.TaxAmount);
        var closedAgain = (await setup.Maker.GetFromJsonAsync<PeriodResponse>($"{setup.Root}/periods/{setup.Period}"))!;
        Assert.Equal(closed.Snapshot.LedgerFingerprint, closedAgain.Snapshot!.LedgerFingerprint);
        Assert.Equal(250m, closedAgain.Snapshot.NetAmount);
        var correctionClose = await Close(setup, november.Id);
        Assert.Equal(-250m, correctionClose.Snapshot!.NetAmount);
        Assert.Equal(-18.75m, correctionClose.Snapshot.TaxAmount);
        Assert.Equal(-7.5m, correctionClose.Snapshot.RecoverableTaxAmount);
    }

    [Fact]
    public async Task Reversal_requires_distinct_checker_and_cannot_precede_source_date()
    {
        var setup = await Setup();
        var purchase = await Posted(setup);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Post(setup.Maker, $"{setup.Purchase(purchase.Id)}/reversals",
            new RequestReversalRequest(5, new(2026, 10, 22), "Too early"))).StatusCode);
        var reversal = await RequestReversal(setup, purchase.Id);
        var path = $"{setup.Purchase(purchase.Id)}/reversals/{reversal.Id}/approve";
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(setup.Maker, path, new TransitionRequest(6))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(setup.Checker, path, new TransitionRequest(5))).StatusCode);
        Assert.Equal(1, await factory.Inspect(db => db.Set<TaxLedgerEntry>().CountAsync(x => x.PurchaseId == purchase.Id)));
    }

    [Fact]
    public async Task Concurrent_reversal_approvals_create_one_correcting_effect()
    {
        var setup = await Setup();
        var purchase = await Posted(setup);
        var reversal = await RequestReversal(setup, purchase.Id);
        var path = $"{setup.Purchase(purchase.Id)}/reversals/{reversal.Id}/approve";
        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Post(setup.Checker, path, new TransitionRequest(6))));
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.OK);
        Assert.All(results.Where(x => x.StatusCode != HttpStatusCode.OK), x => Assert.Equal(HttpStatusCode.Conflict, x.StatusCode));
        Assert.Equal(2, await factory.Inspect(db => db.Set<StockMovement>().CountAsync(x => x.PurchaseId == purchase.Id)));
        Assert.Equal(2, await factory.Inspect(db => db.Set<TaxLedgerEntry>().CountAsync(x => x.PurchaseId == purchase.Id)));
        Assert.Equal(1, await factory.Inspect(db => db.Outbox.CountAsync(x => x.AggregateId == purchase.Id && x.EventType == "PurchaseReversedV1")));
    }

    [Fact]
    public async Task Cancelled_reversal_preserves_posted_version_and_allows_another_request()
    {
        var setup = await Setup();
        var purchase = await Posted(setup);
        var reversal = await RequestReversal(setup, purchase.Id);
        var cancel = await Post(setup.Maker, $"{setup.Purchase(purchase.Id)}/reversals/{reversal.Id}/cancel", new CancelPostingRequest(6, "Wrong correction date"));
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        var restored = (await cancel.Content.ReadFromJsonAsync<PurchaseResponse>())!;
        Assert.Equal("Posted", restored.Status);
        Assert.Equal(7, restored.Version);
        Assert.Equal(5, restored.PostedVersion);
        Assert.True((await setup.Maker.GetFromJsonAsync<ReconciliationResponse>($"{setup.Purchase(purchase.Id)}/reconciliation"))!.IsReconciled);
        var next = await RequestReversal(setup, purchase.Id, version: 7);
        Assert.Equal(HttpStatusCode.OK, (await Post(setup.Checker, $"{setup.Purchase(purchase.Id)}/reversals/{next.Id}/approve", new TransitionRequest(8))).StatusCode);
    }

    [Fact]
    public async Task Insufficient_stock_and_failure_after_decrement_leave_a_retryable_request()
    {
        var setup = await Setup();
        var purchase = await Posted(setup);
        var reversal = await RequestReversal(setup, purchase.Id);
        var path = $"{setup.Purchase(purchase.Id)}/reversals/{reversal.Id}/approve";
        await factory.Inspect(db => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE inventory.stock_balances SET \"Quantity\" = 1 WHERE \"CompanyId\" = {setup.Company}"));
        Assert.Equal(HttpStatusCode.Conflict, (await Post(setup.Checker, path, new TransitionRequest(6))).StatusCode);
        await factory.Inspect(db => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE inventory.stock_balances SET \"Quantity\" = 2.5 WHERE \"CompanyId\" = {setup.Company}"));
        var interceptor = new FailFirstCorrection();
        using var faultHost = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddDbContext<Vat.Infrastructure.Persistence.VatDbContext>(options => options.AddInterceptors(interceptor))));
        using var client = faultHost.CreateClient();
        client.DefaultRequestHeaders.Authorization = setup.Checker.DefaultRequestHeaders.Authorization;
        var key = Guid.NewGuid().ToString();
        Assert.Equal(HttpStatusCode.InternalServerError, (await Post(client, path, new TransitionRequest(6), key)).StatusCode);
        Assert.Equal(2.5m, await factory.Inspect(db => db.Set<StockBalance>().Where(x => x.CompanyId == setup.Company).Select(x => x.Quantity).SingleAsync()));
        Assert.Equal(1, await factory.Inspect(db => db.Set<StockMovement>().CountAsync(x => x.PurchaseId == purchase.Id)));
        Assert.Equal(1, await factory.Inspect(db => db.Set<TaxLedgerEntry>().CountAsync(x => x.PurchaseId == purchase.Id)));
        Assert.Equal("ReversalPending", (await setup.Maker.GetFromJsonAsync<PurchaseResponse>(setup.Purchase(purchase.Id)))!.Status);
        Assert.Equal(HttpStatusCode.OK, (await Post(client, path, new TransitionRequest(6), key)).StatusCode);
    }

    [Fact]
    public async Task Close_rejects_damaged_ledgers_and_damaged_balance_projections()
    {
        var setup = await Setup();
        var purchase = await Posted(setup);
        await Post(setup.Maker, $"{setup.Root}/periods/{setup.Period}/request-close", new PeriodActionRequest(1, "Close"));
        await factory.Inspect(db => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE tax.tax_ledger SET \"TaxAmount\" = \"TaxAmount\" + 1 WHERE \"PurchaseId\" = {purchase.Id}"));
        var path = $"{setup.Root}/periods/{setup.Period}/close";
        Assert.Equal(HttpStatusCode.Conflict, (await Post(setup.Checker, path, new PeriodActionRequest(2, "Review"))).StatusCode);
        await factory.Inspect(db => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE tax.tax_ledger SET \"TaxAmount\" = \"TaxAmount\" - 1 WHERE \"PurchaseId\" = {purchase.Id}"));
        await factory.Inspect(db => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE inventory.stock_balances SET \"Quantity\" = 1 WHERE \"CompanyId\" = {setup.Company}"));
        Assert.Equal(HttpStatusCode.Conflict, (await Post(setup.Checker, path, new PeriodActionRequest(2, "Review"))).StatusCode);
        Assert.Equal("Closing", (await setup.Maker.GetFromJsonAsync<PeriodResponse>($"{setup.Root}/periods/{setup.Period}"))!.Status);
    }

    [Fact]
    public async Task Concurrent_close_request_and_purchase_admission_cannot_both_succeed()
    {
        var setup = await Setup();
        var purchase = await Submitted(setup);
        var responses = await Task.WhenAll(
            Post(setup.Maker, $"{setup.Root}/periods/{setup.Period}/request-close", new PeriodActionRequest(1, "Close")),
            Post(setup.Checker, $"{setup.Purchase(purchase.Id)}/approve", new TransitionRequest(2)));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Concurrent_close_request_and_reversal_admission_cannot_both_succeed()
    {
        var setup = await Setup();
        var purchase = await Posted(setup);
        var responses = await Task.WhenAll(
            Post(setup.Maker, $"{setup.Root}/periods/{setup.Period}/request-close", new PeriodActionRequest(1, "Close")),
            Post(setup.Maker, $"{setup.Purchase(purchase.Id)}/reversals", new RequestReversalRequest(5, new(2026, 10, 24), "Correction")));
        Assert.Single(responses, x => x.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Tenant_company_and_permissions_protect_periods_and_reversals()
    {
        var setup = await Setup();
        var purchase = await Posted(setup);
        var reversal = await RequestReversal(setup, purchase.Id);
        using var wrongTenant = factory.Client(Guid.NewGuid(), setup.Company, "checker");
        using var wrongCompany = factory.Client(setup.Tenant, Guid.NewGuid(), "checker");
        using var noPermission = factory.Client(setup.Tenant, setup.Company, "checker", [Permissions.PurchaseRead]);
        var periodPath = $"{setup.Root}/periods/{setup.Period}";
        var reversalPath = $"{setup.Purchase(purchase.Id)}/reversals/{reversal.Id}";
        Assert.Equal(HttpStatusCode.NotFound, (await wrongTenant.GetAsync(periodPath)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await wrongCompany.GetAsync(periodPath)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await wrongTenant.GetAsync(reversalPath)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(wrongCompany, $"{reversalPath}/approve", new TransitionRequest(6))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(noPermission, $"{periodPath}/request-close", new PeriodActionRequest(1, "Close"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(noPermission, $"{reversalPath}/approve", new TransitionRequest(6))).StatusCode);
    }

    [Fact]
    public async Task Legacy_posting_and_reversal_backfill_rebuild_reports_without_changing_financial_ledgers()
    {
        var setup = await Setup();
        var purchase = await Posted(setup);
        var reversal = await RequestReversal(setup, purchase.Id);
        Assert.Equal(HttpStatusCode.OK, (await Post(setup.Checker, $"{setup.Purchase(purchase.Id)}/reversals/{reversal.Id}/approve", new TransitionRequest(6))).StatusCode);
        var reportPath = $"{setup.Root}/reports/financial-summary?from=2026-10-01&until=2026-11-01";
        Assert.False((await setup.Maker.GetFromJsonAsync<FinancialReportResponse>(reportPath))!.Freshness.IsCaughtUp);
        // Emulate transactions posted by the previous release, before financial projection events existed.
        await factory.Inspect(db => db.Outbox.Where(x => x.CompanyId == setup.Company && x.EventType == nameof(FinancialEntryRecordedV1)).ExecuteDeleteAsync());
        Assert.False((await setup.Maker.GetFromJsonAsync<FinancialReportResponse>(reportPath))!.Freshness.IsCaughtUp);
        var backfill = await Post(setup.Maker, $"{setup.Root}/reports/backfill", new RetryMessageRequest("Backfill prior release"));
        Assert.Equal(HttpStatusCode.OK, backfill.StatusCode);
        Assert.Equal(2, (await backfill.Content.ReadFromJsonAsync<BackfillResponse>())!.EventsScheduled);
        var messages = await factory.Inspect(db => db.Outbox.Where(x => x.CompanyId == setup.Company && x.EventType == nameof(FinancialEntryRecordedV1))
            .OrderByDescending(x => x.AggregateVersion).ToArrayAsync());
        async Task Apply()
        {
            foreach (var message in messages)
            {
                using var scope = factory.Services.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IIncomingEventProcessor>().Process(
                    new(Encoding.UTF8.GetBytes(message.Payload), message.Id.ToString(), message.EventType, "application/json"), default);
            }
        }
        await Apply();
        var report = (await setup.Maker.GetFromJsonAsync<FinancialReportResponse>(reportPath))!;
        Assert.True(report.Freshness.IsCaughtUp);
        Assert.Equal(2, report.TransactionCount);
        Assert.Equal(0m, report.NetAmount);
        var repeated = await Post(setup.Maker, $"{setup.Root}/reports/backfill", new RetryMessageRequest("Already scheduled"));
        Assert.Equal(0, (await repeated.Content.ReadFromJsonAsync<BackfillResponse>())!.EventsScheduled);
        Assert.Equal(HttpStatusCode.OK, (await Post(setup.Maker, $"{setup.Root}/reports/rebuild", new RetryMessageRequest("Rebuild projection"))).StatusCode);
        Assert.False((await setup.Maker.GetFromJsonAsync<FinancialReportResponse>(reportPath))!.Freshness.IsCaughtUp);
        await Apply();
        Assert.True((await setup.Maker.GetFromJsonAsync<FinancialReportResponse>(reportPath))!.Freshness.IsCaughtUp);
        Assert.Equal(2, await factory.Inspect(db => db.Set<TaxLedgerEntry>().CountAsync(x => x.PurchaseId == purchase.Id)));
        Assert.Equal(2, await factory.Inspect(db => db.Set<StockMovement>().CountAsync(x => x.PurchaseId == purchase.Id)));
    }

    private sealed class FailFirstCorrection : SaveChangesInterceptor
    {
        private int _failed;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<TaxLedgerEntry>().Any(x => x.State == EntityState.Added && x.Entity.ReversalId != null) &&
                Interlocked.Exchange(ref _failed, 1) == 0)
                throw new InvalidOperationException("Injected failure after stock decrement, before correcting-ledger commit.");
            return ValueTask.FromResult(result);
        }
    }
}
