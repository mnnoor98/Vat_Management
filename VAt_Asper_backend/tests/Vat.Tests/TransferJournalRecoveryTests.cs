using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Vat.Application;
using Vat.Application.Contracts;
using Vat.Domain;
using Vat.Domain.Company;
using Vat.Domain.Inventory;
using Vat.Domain.Posting;
using Vat.Infrastructure.Persistence;
using Xunit;

namespace Vat.Tests;

[Collection("PostgreSQL integration")]
public sealed class TransferJournalRecoveryTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private sealed record Scope(Guid Tenant, Guid Company, Guid From, Guid To, Guid Product, Guid Transfer, Guid Period);
    private static async Task<T> Post<T>(HttpClient client, string path, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var response = await client.SendAsync(request); response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
    private async Task<Scope> Seed()
    {
        var tenant = Guid.NewGuid(); var company = Guid.NewGuid(); var root = $"/api/v1/companies/{company}";
        var maker = factory.Client(tenant, company); var checker = factory.Client(tenant, company, "checker");
        await Post<CompanyResponse>(maker, "/api/v1/companies", new CreateCompanyRequest(company, "Recovery test", Guid.NewGuid().ToString()));
        var period = await Post<PeriodResponse>(maker, root + "/periods", new CreatePeriodRequest("October", new(2026, 10, 1), new(2026, 11, 1)));
        var branch = await Post<BranchResponse>(maker, root + "/branches", new CreateBranchRequest("Test"));
        var from = await Post<WarehouseResponse>(maker, root + "/warehouses", new CreateWarehouseRequest(branch.Id, "FROM", "Source"));
        var to = await Post<WarehouseResponse>(maker, root + "/warehouses", new CreateWarehouseRequest(branch.Id, "TO", "Destination"));
        var product = await Post<ProductResponse>(maker, root + "/products", new CreateProductRequest("ITEM", "Owned product", "KG", "SYNTHETIC"));
        await Post<SupplierResponse>(maker, root + "/suppliers", new CreateSupplierRequest("SUP", "Test"));
        var rule = await Post<TaxRuleResponse>(maker, root + "/tax/rules", new CreateTaxRuleRequest("SYNTHETIC", new(2026, 1, 1), new(2027, 1, 1), 0, 0, "Synthetic fixture, not legal advice", "TEST"));
        await Post<TaxRuleResponse>(checker, root + $"/tax/rules/{rule.Id}/publish", new TransitionRequest(1));
        var purchase = await Post<PurchaseResponse>(maker, root + "/purchases", new CreatePurchaseRequest(branch.Id, Guid.NewGuid().ToString(), "SUP", new(2026, 10, 23), [new("ITEM", "Test", 100, 1)]));
        var path = root + $"/purchases/{purchase.Id}";
        await Post<PurchaseResponse>(maker, path + "/submit", new TransitionRequest(1));
        await Post<PurchaseResponse>(checker, path + "/approve", new TransitionRequest(2));
        var plan = await Post<PostingPlanResponse>(maker, path + "/posting-plans", new PreparePostingRequest(3, from.Id));
        await Post<PurchaseResponse>(checker, path + $"/posting-plans/{plan.Id}/post", new TransitionRequest(4));
        var transfer = Guid.NewGuid();
        await factory.Inspect(async db =>
        {
            db.Set<Dictionary<string, object>>("inventory.transfers").Add(new() { ["TenantId"] = tenant, ["CompanyId"] = company,
                ["Id"] = transfer, ["FromWarehouseId"] = from.Id, ["ToWarehouseId"] = to.Id, ["Reference"] = "LEGACY",
                ["BusinessDate"] = new DateOnly(2026, 10, 23), ["State"] = "Posted", ["ApprovedBy"] = "legacy-checker", ["CreatedBy"] = "legacy-maker" });
            db.Set<Dictionary<string, object>>("inventory.transfer_lines").Add(new() { ["TenantId"] = tenant, ["CompanyId"] = company,
                ["Id"] = Guid.NewGuid(), ["TransferId"] = transfer, ["ProductId"] = product.Id, ["Quantity"] = 25m, ["UnitPrice"] = 1m, ["CreatedBy"] = "legacy-maker" });
            await db.SaveChangesAsync();
            // Simulate only the documented old prototype's already-applied balance changes.
            await db.Set<StockBalance>().Where(x => x.TenantId == tenant && x.CompanyId == company && x.WarehouseId == from.Id)
                .ExecuteUpdateAsync(x => x.SetProperty(b => b.Quantity, 75m));
            var balance = new StockBalance(); db.Add(balance);
            db.Entry(balance).Property(x => x.TenantId).CurrentValue = tenant; db.Entry(balance).Property(x => x.CompanyId).CurrentValue = company;
            db.Entry(balance).Property(x => x.WarehouseId).CurrentValue = to.Id; db.Entry(balance).Property(x => x.ProductId).CurrentValue = product.Id;
            db.Entry(balance).Property(x => x.Quantity).CurrentValue = 25m;
            await db.SaveChangesAsync(); return 0;
        });
        return new(tenant, company, from.Id, to.Id, product.Id, transfer, period.Id);
    }

    [Fact]
    public async Task Preview_apply_and_repeat_are_proven_scoped_and_do_not_change_balances()
    {
        var scope = await Seed();
        await factory.Inspect(async db =>
        {
            Assert.False(await new PeriodStore(db).BalancesReconcile(scope.Tenant, scope.Company, default));
            var repair = new LegacyTransferJournalRepair(db, TimeProvider.System);
            var preview = await repair.Run(scope.Tenant, scope.Company, "operator", "Documented reconstruction", false, default);
            Assert.Equal(1, preview.MissingEntries); Assert.False(preview.Applied);
            Assert.Empty(await db.Set<TransferJournalEntry>().Where(x => x.CompanyId == scope.Company).ToArrayAsync());
            var result = await repair.Run(scope.Tenant, scope.Company, "operator", "Documented reconstruction", true, default);
            Assert.True(result.Applied); Assert.Equal(preview.EvidenceSha256, result.EvidenceSha256);
            Assert.True(await new PeriodStore(db).BalancesReconcile(scope.Tenant, scope.Company, default));
            var repeat = await repair.Run(scope.Tenant, scope.Company, "operator", "Documented reconstruction", true, default);
            Assert.False(repeat.Applied); Assert.Equal(0, repeat.MissingEntries);
            Assert.Equal(1, await db.Audit.CountAsync(x => x.ResourceId == scope.Transfer && x.Action == "LegacyTransferJournalReconstructed"));
            var quantities = await db.Set<StockBalance>().Where(x => x.TenantId == scope.Tenant && x.CompanyId == scope.Company).Select(x => x.Quantity).OrderBy(x => x).ToArrayAsync();
            Assert.Equal(new[] { 25m, 75m }, quantities);
            var period = (await db.Set<FiscalPeriod>().FindAsync(scope.Tenant, scope.Company, scope.Period))!;
            var snapshot = await new PeriodStore(db).Snapshot(period, DateTimeOffset.UtcNow, default);
            Assert.Single(snapshot.TransferEntryIds!); return 0;
        });
    }

    [Fact]
    public async Task Unexplained_quantity_differences_refuse_all_writes()
    {
        var scope = await Seed();
        await factory.Inspect(async db =>
        {
            await db.Set<StockBalance>().Where(x => x.TenantId == scope.Tenant && x.CompanyId == scope.Company && x.WarehouseId == scope.From)
                .ExecuteUpdateAsync(x => x.SetProperty(b => b.Quantity, 74m));
            var error = await Assert.ThrowsAsync<DomainException>(() => new LegacyTransferJournalRepair(db, TimeProvider.System)
                .Run(scope.Tenant, scope.Company, "operator", "Documented reconstruction", true, default));
            Assert.Equal("repair_does_not_reconcile", error.Code);
            Assert.False(await db.Set<TransferJournalEntry>().AnyAsync(x => x.CompanyId == scope.Company)); return 0;
        });
    }

    [Fact]
    public async Task Closing_period_refuses_historical_reconstruction()
    {
        var scope = await Seed();
        await factory.Inspect(async db =>
        {
            var period = (await db.Set<FiscalPeriod>().FindAsync(scope.Tenant, scope.Company, scope.Period))!;
            period.RequestClose("closer", period.Version); await db.SaveChangesAsync();
            var error = await Assert.ThrowsAsync<DomainException>(() => new LegacyTransferJournalRepair(db, TimeProvider.System)
                .Run(scope.Tenant, scope.Company, "operator", "Documented reconstruction", true, default));
            Assert.Equal("repair_period_unavailable", error.Code); return 0;
        });
    }

    [Fact]
    public async Task Save_failure_rolls_back_journal_and_audit_then_can_recover()
    {
        var scope = await Seed();
        var connection = await factory.Inspect(db => Task.FromResult(db.Database.GetConnectionString()));
        await using (var db = new VatDbContext(new DbContextOptionsBuilder<VatDbContext>().UseNpgsql(connection).AddInterceptors(new Reject()).Options))
            await Assert.ThrowsAsync<InvalidOperationException>(() => new LegacyTransferJournalRepair(db, TimeProvider.System)
                .Run(scope.Tenant, scope.Company, "operator", "Documented reconstruction", true, default));
        await factory.Inspect(async db =>
        {
            Assert.False(await db.Set<TransferJournalEntry>().AnyAsync(x => x.CompanyId == scope.Company));
            Assert.False(await db.Audit.AnyAsync(x => x.ResourceId == scope.Transfer));
            Assert.True((await new LegacyTransferJournalRepair(db, TimeProvider.System).Run(scope.Tenant, scope.Company, "operator", "Documented reconstruction", true, default)).Applied);
            return 0;
        });
    }

    [Fact]
    public async Task Changed_source_evidence_refuses_reconstruction_reruns()
    {
        var scope = await Seed();
        await factory.Inspect(async db =>
        {
            var repair = new LegacyTransferJournalRepair(db, TimeProvider.System);
            await repair.Run(scope.Tenant, scope.Company, "operator", "Documented reconstruction", true, default);
            await db.Set<Dictionary<string, object>>("inventory.transfer_lines").Where(x => EF.Property<Guid>(x, "TenantId") == scope.Tenant
                && EF.Property<Guid>(x, "CompanyId") == scope.Company).ExecuteUpdateAsync(x => x.SetProperty(l => EF.Property<decimal>(l, "Quantity"), 26m));
            var error = await Assert.ThrowsAsync<DomainException>(() => repair.Run(scope.Tenant, scope.Company, "operator", "Documented reconstruction", true, default));
            Assert.Equal("repair_evidence_conflict", error.Code); return 0;
        });
    }

    private sealed class Reject : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
            => throw new InvalidOperationException("Test repair commit failure.");
    }
}
