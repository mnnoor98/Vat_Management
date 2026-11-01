using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vat.Application.Contracts;
using Vat.Application;
using Vat.Domain.Costing;
using Npgsql;
using Vat.Application.Security;
using Vat.Domain.Posting;
using Vat.Domain.Purchasing;
using Xunit;

namespace Vat.Tests;

[Collection("PostgreSQL integration")]
public sealed class PostingApiTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    [Fact]
    public async Task Costing_policies_enforce_maker_checker_override_dates_overlap_scope_and_replay()
    {
        var setup = await Setup(); var root = setup.Root + "/inventory/costing-policies";
        var companyPolicy = await Created<CostingPolicyView>(setup.Maker, root, new CreateCostingPolicyRequest(null, "FIFO", new(2026, 1, 1), new(2028, 1, 1)));
        var approvalPath = root + $"/{companyPolicy.Id}/approve";
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(setup.Maker, approvalPath, new ApproveCostingPolicyRequest(1))).StatusCode);
        using var reader = factory.Client(setup.Tenant, setup.Company, permissions: [Permissions.CostingRead]);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(reader, approvalPath, new ApproveCostingPolicyRequest(1))).StatusCode);
        var key = Guid.NewGuid().ToString();
        Assert.Equal(HttpStatusCode.OK, (await Post(setup.Checker, approvalPath, new ApproveCostingPolicyRequest(1), key)).StatusCode);
        var replay = await Post(setup.Checker, approvalPath, new ApproveCostingPolicyRequest(1), key);
        Assert.Equal("true", replay.Headers.GetValues("Idempotency-Replayed").Single());
        var productPolicy = await Created<CostingPolicyView>(setup.Maker, root, new CreateCostingPolicyRequest(setup.Product, "WEIGHTED_AVERAGE", new(2026, 1, 1), new(2027, 1, 1)));
        Assert.Equal(HttpStatusCode.OK, (await Post(setup.Checker, root + $"/{productPolicy.Id}/approve", new ApproveCostingPolicyRequest(1))).StatusCode);
        var resolved = (await reader.GetFromJsonAsync<CostingPolicyView>(root + $"/resolve?productId={setup.Product}&businessDate=2026-10-23"))!;
        Assert.Equal(productPolicy.Id, resolved.Id); Assert.Equal(2, resolved.Version);
        Assert.Equal(companyPolicy.Id, (await reader.GetFromJsonAsync<CostingPolicyView>(root + $"/resolve?productId={setup.Product}&businessDate=2027-01-01"))!.Id);
        var overlap = await Created<CostingPolicyView>(setup.Maker, root, new CreateCostingPolicyRequest(setup.Product, "FIFO", new(2026, 6, 1), new(2027, 6, 1)));
        Assert.Equal(HttpStatusCode.Conflict, (await Post(setup.Checker, root + $"/{overlap.Id}/approve", new ApproveCostingPolicyRequest(1))).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await reader.GetAsync(root + "?take=101")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await reader.GetAsync(root + $"/resolve?productId={Guid.NewGuid()}&businessDate=2026-10-23")).StatusCode);
        using var foreignTenant = factory.Client(Guid.NewGuid(), setup.Company);
        Assert.Empty((await foreignTenant.GetFromJsonAsync<CostingPolicyView[]>(root))!);
        Assert.Equal(HttpStatusCode.NotFound, (await foreignTenant.GetAsync(root + $"/{companyPolicy.Id}")).StatusCode);
        using var foreignCompany = factory.Client(setup.Tenant, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Forbidden, (await foreignCompany.GetAsync(root)).StatusCode);
        await factory.Inspect(async db =>
        {
            await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE inventory.costing_policies SET \"Method\"='WEIGHTED_AVERAGE' WHERE \"TenantId\"={setup.Tenant} AND \"CompanyId\"={setup.Company} AND \"Id\"={companyPolicy.Id}"));
            return 0;
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Cost_receipts_commit_once_with_posting_and_freeze_policy_or_explicit_unassigned_history(bool configured)
    {
        var setup = await Setup(); var policies = setup.Root + "/inventory/costing-policies";
        CostingPolicyView? selected = null;
        if (configured)
        {
            selected = await Created<CostingPolicyView>(setup.Maker, policies, new CreateCostingPolicyRequest(setup.Product, "FIFO", new(2026, 1, 1), new(2027, 1, 1)));
            Assert.Equal(HttpStatusCode.OK, (await Post(setup.Checker, policies + $"/{selected.Id}/approve", new ApproveCostingPolicyRequest(1))).StatusCode);
        }
        var purchase = await Approve(setup); var plan = await Prepare(setup, purchase.Id);
        var path = setup.Purchase(purchase.Id) + $"/posting-plans/{plan.Id}/post"; var key = Guid.NewGuid().ToString();
        Assert.Equal(HttpStatusCode.OK, (await Post(setup.Checker, path, new TransitionRequest(4), key)).StatusCode);
        Assert.Equal("true", (await Post(setup.Checker, path, new TransitionRequest(4), key)).Headers.GetValues("Idempotency-Replayed").Single());
        await factory.Inspect(async db =>
        {
            var receipt = await db.Set<PurchaseCostReceipt>().SingleAsync(x => x.TenantId == setup.Tenant && x.CompanyId == setup.Company && x.PurchaseId == purchase.Id);
            Assert.Equal(selected?.Id, receipt.PolicyId); Assert.Equal(configured ? 2 : (int?)null, receipt.PolicyVersion);
            Assert.Equal(configured ? "FIFO" : null, receipt.Method);
            Assert.Equal(2.5m, receipt.Quantity); Assert.Equal(250m, receipt.NetAmount);
            Assert.Equal(18.75m, receipt.TaxAmount); Assert.Equal(7.5m, receipt.RecoverableTaxAmount);
            Assert.True(await db.Set<StockMovement>().AnyAsync(x => x.TenantId == receipt.TenantId && x.CompanyId == receipt.CompanyId && x.Id == receipt.SourceMovementId));
            await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE inventory.purchase_cost_receipts SET \"NetAmount\"=0 WHERE \"Id\"={receipt.Id}"));
            await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM inventory.purchase_cost_receipts WHERE \"Id\"={receipt.Id}"));
            return 0;
        });
        var laterPolicy = await Created<CostingPolicyView>(setup.Maker, policies, new CreateCostingPolicyRequest(null, "WEIGHTED_AVERAGE", new(2026, 1, 1), new(2027, 1, 1)));
        Assert.Equal(HttpStatusCode.Conflict, (await Post(setup.Checker, policies + $"/{laterPolicy.Id}/approve", new ApproveCostingPolicyRequest(1))).StatusCode);
        var receiptsPath = setup.Root + "/inventory/purchase-cost-receipts";
        Assert.Equal(HttpStatusCode.OK, (await setup.Checker.GetAsync(receiptsPath)).StatusCode);
        using var denied = factory.Client(setup.Tenant, setup.Company, permissions: [Permissions.InventoryRead]);
        Assert.Equal(HttpStatusCode.Forbidden, (await denied.GetAsync(receiptsPath)).StatusCode);
    }

    [Fact]
    public async Task Costing_approval_racing_receipt_capture_has_one_consistent_policy_boundary()
    {
        var setup = await Setup(); var root = setup.Root + "/inventory/costing-policies";
        var draft = await Created<CostingPolicyView>(setup.Maker, root, new CreateCostingPolicyRequest(null, "FIFO", new(2026, 1, 1), new(2027, 1, 1)));
        var purchase = await Approve(setup); var plan = await Prepare(setup, purchase.Id);
        var decisions = await Task.WhenAll(Post(setup.Checker, root + $"/{draft.Id}/approve", new ApproveCostingPolicyRequest(1)),
            Post(setup.Checker, setup.Purchase(purchase.Id) + $"/posting-plans/{plan.Id}/post", new TransitionRequest(4)));
        Assert.Equal(HttpStatusCode.OK, decisions[1].StatusCode);
        Assert.Contains(decisions[0].StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict });
        await factory.Inspect(async db =>
        {
            var receipt = await db.Set<PurchaseCostReceipt>().SingleAsync(x => x.TenantId == setup.Tenant && x.CompanyId == setup.Company);
            Assert.Equal(decisions[0].StatusCode == HttpStatusCode.OK ? draft.Id : (Guid?)null, receipt.PolicyId);
            return 0;
        });
    }

    [Fact]
    public async Task Costing_concurrent_overlapping_approvals_commit_only_one_policy()
    {
        var setup = await Setup(); var root = setup.Root + "/inventory/costing-policies";
        var a = await Created<CostingPolicyView>(setup.Maker, root, new CreateCostingPolicyRequest(null, "FIFO", new(2026, 1, 1), new(2027, 1, 1)));
        var b = await Created<CostingPolicyView>(setup.Maker, root, new CreateCostingPolicyRequest(null, "WEIGHTED_AVERAGE", new(2026, 1, 1), new(2027, 1, 1)));
        var outcomes = await Task.WhenAll(Post(setup.Checker, root + $"/{a.Id}/approve", new ApproveCostingPolicyRequest(1)),
            Post(setup.Checker, root + $"/{b.Id}/approve", new ApproveCostingPolicyRequest(1)));
        Assert.Single(outcomes, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Single(outcomes, x => x.StatusCode == HttpStatusCode.Conflict);
    }

    private sealed record SetupData(Guid Tenant, Guid Company, Guid Branch, Guid Warehouse, Guid Product,
        Guid Rule, HttpClient Maker, HttpClient Checker)
    {
        public string Root => $"/api/v1/companies/{Company}";
        public string Purchase(Guid id) => $"{Root}/purchases/{id}";
    }

    [Fact]
    public async Task TaxRuleListIsScopedBoundedAndRequiresReadPermission()
    {
        var setup = await Setup(publishRule: false);
        var rows = await setup.Maker.GetFromJsonAsync<TaxRuleResponse[]>($"{setup.Root}/tax/rules?take=25&skip=0");
        Assert.Contains(rows!, rule => rule.Id == setup.Rule && rule.PublishedAt == null);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await setup.Maker.GetAsync($"{setup.Root}/tax/rules?take=101")).StatusCode);
        using var foreign = factory.Client(Guid.NewGuid(), setup.Company);
        Assert.Empty((await foreign.GetFromJsonAsync<TaxRuleResponse[]>($"{setup.Root}/tax/rules"))!);
        using var denied = factory.Client(setup.Tenant, setup.Company, permissions: []);
        Assert.Equal(HttpStatusCode.Forbidden, (await denied.GetAsync($"{setup.Root}/tax/rules")).StatusCode);
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

    [Fact]
    public async Task Purchase_posts_stock_tax_audit_events_and_reconciles_from_frozen_snapshot()
    {
        var setup = await Setup();
        var purchase = await Approve(setup);
        var plan = await Prepare(setup, purchase.Id);
        Assert.Equal(4, plan.SourceVersion);
        Assert.Equal(250m, plan.Snapshot.NetAmount);
        Assert.Equal(18.75m, plan.Snapshot.TaxAmount);
        Assert.Equal(7.5m, plan.Snapshot.RecoverableTaxAmount);
        Assert.Equal(268.75m, plan.Snapshot.GrossAmount);
        Assert.Equal(setup.Rule, plan.Snapshot.Lines.Single().TaxRuleId);
        var key = Guid.NewGuid().ToString();
        var url = $"{setup.Purchase(purchase.Id)}/posting-plans/{plan.Id}/post";
        var posted = await Post(setup.Checker, url, new TransitionRequest(4), key);
        Assert.Equal(HttpStatusCode.OK, posted.StatusCode);
        var body = (await posted.Content.ReadFromJsonAsync<PurchaseResponse>())!;
        Assert.Equal("Posted", body.Status);
        Assert.Equal(5, body.Version);
        var replay = await Post(setup.Checker, url, new TransitionRequest(4), key);
        Assert.Equal(await posted.Content.ReadAsStringAsync(), await replay.Content.ReadAsStringAsync());
        Assert.Equal("true", replay.Headers.GetValues("Idempotency-Replayed").Single());
        var reconciliation = (await setup.Maker.GetFromJsonAsync<ReconciliationResponse>($"{setup.Purchase(purchase.Id)}/reconciliation"))!;
        Assert.True(reconciliation.IsReconciled);
        Assert.Empty(reconciliation.Differences);
        Assert.Equal(1, reconciliation.StockEntryCount);
        Assert.Equal(1, reconciliation.TaxEntryCount);
        var balances = (await setup.Maker.GetFromJsonAsync<StockBalanceResponse[]>($"{setup.Root}/inventory/balances?warehouseId={setup.Warehouse}"))!;
        Assert.Equal(2.5m, Assert.Single(balances).Quantity);
        var ledger = (await setup.Maker.GetFromJsonAsync<TaxLedgerResponse[]>($"{setup.Root}/tax/ledger?purchaseId={purchase.Id}"))!;
        Assert.Equal(7.5m, Assert.Single(ledger).RecoverableTaxAmount);
        Assert.Equal(5, await factory.Inspect(db => db.Outbox.CountAsync(x => x.AggregateId == purchase.Id)));
        Assert.Equal(5, await factory.Inspect(db => db.Audit.CountAsync(x => x.ResourceId == purchase.Id)));
    }

    [Fact]
    public async Task Missing_published_rule_fails_closed_and_same_key_can_retry_after_publication()
    {
        var setup = await Setup(publishRule: false);
        var purchase = await Approve(setup);
        var key = Guid.NewGuid().ToString();
        var url = $"{setup.Purchase(purchase.Id)}/posting-plans";
        var body = new PreparePostingRequest(3, setup.Warehouse);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(setup.Maker, url, body, key)).StatusCode);
        Assert.Equal(0, await factory.Inspect(db => db.Set<PurchasePostingPlan>().CountAsync(x => x.PurchaseId == purchase.Id)));
        Assert.Equal(HttpStatusCode.OK, (await Post(setup.Checker, $"{setup.Root}/tax/rules/{setup.Rule}/publish", new TransitionRequest(1))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await Post(setup.Maker, url, body, key)).StatusCode);
    }

    [Fact]
    public async Task Rule_publication_requires_checker_and_serializes_overlapping_intervals()
    {
        var setup = await Setup(publishRule: false);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(setup.Maker, $"{setup.Root}/tax/rules/{setup.Rule}/publish", new TransitionRequest(1))).StatusCode);
        var second = await Created<TaxRuleResponse>(setup.Maker, $"{setup.Root}/tax/rules", Rule());
        var results = await Task.WhenAll(
            Post(setup.Checker, $"{setup.Root}/tax/rules/{setup.Rule}/publish", new TransitionRequest(1)),
            Post(setup.Checker, $"{setup.Root}/tax/rules/{second.Id}/publish", new TransitionRequest(1)));
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.Conflict);
        var adjacent = await Created<TaxRuleResponse>(setup.Maker, $"{setup.Root}/tax/rules",
            Rule(from: new(2027, 1, 1), until: new(2028, 1, 1)));
        Assert.Equal(HttpStatusCode.OK, (await Post(setup.Checker, $"{setup.Root}/tax/rules/{adjacent.Id}/publish", new TransitionRequest(1))).StatusCode);
        var outside = await Approve(setup, date: new(2028, 1, 1));
        Assert.Equal(HttpStatusCode.Conflict, (await Post(setup.Maker, $"{setup.Purchase(outside.Id)}/posting-plans", new PreparePostingRequest(3, setup.Warehouse))).StatusCode);
    }

    [Fact]
    public async Task Missing_references_foreign_warehouse_and_self_posting_do_not_create_ledgers()
    {
        var setup = await Setup();
        var foreign = await Setup();
        var purchase = await Approve(setup);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Post(setup.Maker, $"{setup.Purchase(purchase.Id)}/posting-plans",
            new PreparePostingRequest(3, foreign.Warehouse))).StatusCode);
        var missingProduct = await Approve(setup, product: "UNKNOWN");
        Assert.Equal(HttpStatusCode.Conflict, (await Post(setup.Maker, $"{setup.Purchase(missingProduct.Id)}/posting-plans",
            new PreparePostingRequest(3, setup.Warehouse))).StatusCode);
        var missingSupplier = await Approve(setup, supplier: "UNKNOWN");
        Assert.Equal(HttpStatusCode.Conflict, (await Post(setup.Maker, $"{setup.Purchase(missingSupplier.Id)}/posting-plans",
            new PreparePostingRequest(3, setup.Warehouse))).StatusCode);
        var plan = await Prepare(setup, purchase.Id);
        var path = $"{setup.Purchase(purchase.Id)}/posting-plans/{plan.Id}/post";
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(setup.Maker, path, new TransitionRequest(4))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(setup.Checker, path, new TransitionRequest(3))).StatusCode);
        Assert.Equal(0, await factory.Inspect(db => db.Set<StockMovement>().CountAsync(x => x.CompanyId == setup.Company)));
        Assert.Equal(0, await factory.Inspect(db => db.Set<TaxLedgerEntry>().CountAsync(x => x.CompanyId == setup.Company)));
    }

    [Fact]
    public async Task Concurrent_posting_keys_create_exactly_one_stock_and_tax_effect()
    {
        var setup = await Setup();
        var purchase = await Approve(setup);
        var plan = await Prepare(setup, purchase.Id);
        var url = $"{setup.Purchase(purchase.Id)}/posting-plans/{plan.Id}/post";
        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Post(setup.Checker, url, new TransitionRequest(4))));
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.OK);
        Assert.All(results.Where(x => x.StatusCode != HttpStatusCode.OK), x => Assert.Equal(HttpStatusCode.Conflict, x.StatusCode));
        Assert.Equal(2.5m, await factory.Inspect(db => db.Set<StockBalance>().Where(x => x.CompanyId == setup.Company).Select(x => x.Quantity).SingleAsync()));
        Assert.Equal(1, await factory.Inspect(db => db.Set<TaxLedgerEntry>().CountAsync(x => x.PurchaseId == purchase.Id)));
    }

    [Fact]
    public async Task Concurrent_different_purchases_add_balances_without_lost_updates()
    {
        var setup = await Setup();
        var first = await Approve(setup, lines: [new("ITEM", "First", 1m, 100m), new("ITEM", "Second", 1.5m, 100m)]);
        var second = await Approve(setup);
        var plans = await Task.WhenAll(Prepare(setup, first.Id), Prepare(setup, second.Id));
        var results = await Task.WhenAll(
            Post(setup.Checker, $"{setup.Purchase(first.Id)}/posting-plans/{plans[0].Id}/post", new TransitionRequest(4)),
            Post(setup.Checker, $"{setup.Purchase(second.Id)}/posting-plans/{plans[1].Id}/post", new TransitionRequest(4)));
        Assert.All(results, x => Assert.Equal(HttpStatusCode.OK, x.StatusCode));
        Assert.Equal(5m, await factory.Inspect(db => db.Set<StockBalance>().Where(x => x.CompanyId == setup.Company).Select(x => x.Quantity).SingleAsync()));
        Assert.Equal(3, await factory.Inspect(db => db.Set<StockMovement>().CountAsync(x => x.CompanyId == setup.Company)));
    }

    [Fact]
    public async Task Failure_after_balance_upsert_rolls_everything_back_and_retry_recovers()
    {
        var setup = await Setup();
        var purchase = await Approve(setup);
        var plan = await Prepare(setup, purchase.Id);
        var interceptor = new FailFirstTaxCommit();
        using var failingHost = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddDbContext<Vat.Infrastructure.Persistence.VatDbContext>(options => options.AddInterceptors(interceptor))));
        using var client = failingHost.CreateClient();
        client.DefaultRequestHeaders.Authorization = setup.Checker.DefaultRequestHeaders.Authorization;
        var key = Guid.NewGuid().ToString();
        var path = $"{setup.Purchase(purchase.Id)}/posting-plans/{plan.Id}/post";
        Assert.Equal(HttpStatusCode.InternalServerError, (await Post(client, path, new TransitionRequest(4), key)).StatusCode);
        Assert.Equal(0, await factory.Inspect(db => db.Set<StockBalance>().CountAsync(x => x.CompanyId == setup.Company)));
        Assert.Equal(0, await factory.Inspect(db => db.Set<PurchaseCostReceipt>().CountAsync(x => x.CompanyId == setup.Company)));
        Assert.Equal(0, await factory.Inspect(db => db.Set<StockMovement>().CountAsync(x => x.PurchaseId == purchase.Id)));
        Assert.Equal(0, await factory.Inspect(db => db.Set<TaxLedgerEntry>().CountAsync(x => x.PurchaseId == purchase.Id)));
        Assert.Equal(1, await factory.Inspect(db => db.Outbox.CountAsync(x => x.AggregateId == purchase.Id)));
        var pending = (await setup.Maker.GetFromJsonAsync<PostingStatusResponse>($"{setup.Purchase(purchase.Id)}/posting-status"))!;
        Assert.Equal("PostingPrepared", pending.Status);
        Assert.Equal("Prepared", pending.LatestPlan!.Status);
        Assert.Equal(HttpStatusCode.OK, (await Post(client, path, new TransitionRequest(4), key)).StatusCode);
        Assert.True((await setup.Maker.GetFromJsonAsync<ReconciliationResponse>($"{setup.Purchase(purchase.Id)}/reconciliation"))!.IsReconciled);
    }

    [Fact]
    public async Task Posting_gate_and_permissions_prevent_ledger_writes()
    {
        var setup = await Setup();
        var purchase = await Approve(setup);
        var plan = await Prepare(setup, purchase.Id);
        var path = $"{setup.Purchase(purchase.Id)}/posting-plans/{plan.Id}/post";
        using var noPermission = factory.Client(setup.Tenant, setup.Company, "checker", [Permissions.PurchaseRead]);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(noPermission, path, new TransitionRequest(4))).StatusCode);
        using var disabledHost = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["Posting:Enabled"] = "false" })));
        using var disabled = disabledHost.CreateClient();
        disabled.DefaultRequestHeaders.Authorization = setup.Checker.DefaultRequestHeaders.Authorization;
        Assert.Equal(HttpStatusCode.Conflict, (await Post(disabled, path, new TransitionRequest(4))).StatusCode);
        Assert.Equal(0, await factory.Inspect(db => db.Set<StockMovement>().CountAsync(x => x.PurchaseId == purchase.Id)));
    }

    [Fact]
    public async Task Cancelled_plan_keeps_history_and_cannot_be_posted_after_reprepare()
    {
        var setup = await Setup();
        var purchase = await Approve(setup);
        var plan = await Prepare(setup, purchase.Id);
        var cancel = await Post(setup.Maker, $"{setup.Purchase(purchase.Id)}/posting-plans/{plan.Id}/cancel", new CancelPostingRequest(4, "Wrong warehouse selected"));
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        Assert.Equal(5, (await cancel.Content.ReadFromJsonAsync<PurchaseResponse>())!.Version);
        var history = (await setup.Maker.GetFromJsonAsync<PostingPlanResponse>($"{setup.Purchase(purchase.Id)}/posting-plans/{plan.Id}"))!;
        Assert.Equal("Cancelled", history.Status);
        Assert.Equal("Wrong warehouse selected", history.CancellationReason);
        var replacement = await Prepare(setup, purchase.Id, 5);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(setup.Checker, $"{setup.Purchase(purchase.Id)}/posting-plans/{plan.Id}/post", new TransitionRequest(4))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Post(setup.Checker, $"{setup.Purchase(purchase.Id)}/posting-plans/{replacement.Id}/post", new TransitionRequest(6))).StatusCode);
    }

    [Fact]
    public async Task New_reads_and_commands_are_scoped_to_tenant_and_company()
    {
        var setup = await Setup();
        var purchase = await Approve(setup);
        var plan = await Prepare(setup, purchase.Id);
        using var otherTenant = factory.Client(Guid.NewGuid(), setup.Company, "checker");
        using var otherCompany = factory.Client(setup.Tenant, Guid.NewGuid(), "checker");
        var planPath = $"{setup.Purchase(purchase.Id)}/posting-plans/{plan.Id}";
        foreach (var path in new[] { planPath, $"{setup.Purchase(purchase.Id)}/reconciliation", $"{setup.Root}/products/{setup.Product}",
            $"{setup.Root}/warehouses/{setup.Warehouse}", $"{setup.Root}/tax/rules/{setup.Rule}" })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await otherTenant.GetAsync(path)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await otherCompany.GetAsync(path)).StatusCode);
        }
        Assert.Equal(HttpStatusCode.NotFound, (await Post(otherTenant, $"{planPath}/post", new TransitionRequest(4))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(otherCompany, $"{planPath}/post", new TransitionRequest(4))).StatusCode);
        Assert.Empty((await otherTenant.GetFromJsonAsync<TaxLedgerResponse[]>($"{setup.Root}/tax/ledger"))!);
    }

    [Fact]
    public async Task Reconciliation_reports_an_actual_ledger_mismatch()
    {
        var setup = await Setup();
        var purchase = await Approve(setup);
        var plan = await Prepare(setup, purchase.Id);
        await Post(setup.Checker, $"{setup.Purchase(purchase.Id)}/posting-plans/{plan.Id}/post", new TransitionRequest(4));
        // Simulate externally damaged/restored data; there is no ledger editing API.
        await factory.Inspect(db => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE tax.tax_ledger SET \"TaxAmount\" = \"TaxAmount\" + 1 WHERE \"PurchaseId\" = {purchase.Id}"));
        var response = (await setup.Maker.GetFromJsonAsync<ReconciliationResponse>($"{setup.Purchase(purchase.Id)}/reconciliation"))!;
        Assert.False(response.IsReconciled);
        Assert.Contains(response.Differences, x => x.StartsWith("Tax posting mismatch", StringComparison.Ordinal));
    }

    private sealed class FailFirstTaxCommit : SaveChangesInterceptor
    {
        private int _failed;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<TaxLedgerEntry>().Any(x => x.State == EntityState.Added) &&
                Interlocked.Exchange(ref _failed, 1) == 0)
                throw new InvalidOperationException("Injected failure after stock-balance SQL, before ledger commit.");
            return ValueTask.FromResult(result);
        }
    }
}
