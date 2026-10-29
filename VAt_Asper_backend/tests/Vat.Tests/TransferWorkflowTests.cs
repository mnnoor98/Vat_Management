using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Vat.Application;
using Vat.Application.Contracts;
using Vat.Application.Security;
using Vat.Domain.Inventory;
using Vat.Domain.Posting;
using Vat.Infrastructure.Persistence;
using Xunit;

namespace Vat.Tests;

[Collection("PostgreSQL integration")]
public sealed class TransferWorkflowTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private sealed record Scope(Guid Tenant, Guid Company, Guid From, Guid To, Guid Product, Guid Period);
    private static async Task<T> Post<T>(HttpClient client, string path, object body)
    {
        var response = await Send(client, path, body); response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
    private static Task<HttpResponseMessage> Send(HttpClient client, string path, object body, Guid? key = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", (key ?? Guid.NewGuid()).ToString());
        return client.SendAsync(request);
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
        return new(tenant, company, from.Id, to.Id, product.Id, period.Id);
    }
    private static TransferCreateRequest Request(Scope scope, decimal quantity = 25) => new("TRANSFER", new(2026,10,23), "10:00", scope.From, scope.To, "Vehicle", "Driver", "Test", [new(scope.Product, quantity, 1, null)]);
    [Fact]
    public async Task Approval_posting_and_retries_are_atomic_scoped_and_reconcile()
    {
        var scope = await Seed(); var maker = factory.Client(scope.Tenant, scope.Company); var checker = factory.Client(scope.Tenant, scope.Company, "checker");
        var root = $"/api/v1/companies/{scope.Company}/transfers"; var key = Guid.NewGuid();
        var response = await Send(maker, root, Request(scope), key); Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var transfer = (await response.Content.ReadFromJsonAsync<TransferResult>())!;
        var replay = await Send(maker, root, Request(scope), key); Assert.Equal("true", replay.Headers.GetValues("Idempotency-Replayed").Single());
        Assert.Equal(HttpStatusCode.Conflict, (await Send(maker, root, Request(scope,26),key)).StatusCode);
        var path = root + $"/{transfer.Id}";
        Assert.Equal(HttpStatusCode.Conflict, (await Send(checker,path+"/post",new TransferActionRequest(1))).StatusCode);
        await Post<TransferResult>(maker,path+"/submit",new TransferActionRequest(1));
        Assert.Equal(HttpStatusCode.Conflict,(await Send(maker,path+"/approve",new TransferActionRequest(2))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await Send(checker,path+"/approve",new TransferActionRequest(1))).StatusCode);
        await Post<TransferResult>(checker,path+"/approve",new TransferActionRequest(2));
        using var denied = factory.Client(scope.Tenant,scope.Company,permissions:[Permissions.InventoryManage]);
        Assert.Equal(HttpStatusCode.Forbidden,(await Send(denied,path+"/post",new TransferActionRequest(3))).StatusCode);
        var postKey=Guid.NewGuid(); var posted=await Send(checker,path+"/post",new TransferActionRequest(3),postKey); posted.EnsureSuccessStatusCode();
        (await Send(checker,path+"/post",new TransferActionRequest(3),postKey)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict,(await Send(checker,path+"/post",new TransferActionRequest(3))).StatusCode);
        await factory.Inspect(async db =>
        {
            Assert.Equal(new[]{25m,75m},await db.Set<StockBalance>().Where(x=>x.TenantId==scope.Tenant&&x.CompanyId==scope.Company).Select(x=>x.Quantity).OrderBy(x=>x).ToArrayAsync());
            Assert.Single(await db.Set<TransferJournalEntry>().Where(x=>x.TransferId==transfer.Id).ToArrayAsync());
            Assert.Equal(4,await db.Audit.CountAsync(x=>x.ResourceId==transfer.Id));
            Assert.True(await new PeriodStore(db).BalancesReconcile(scope.Tenant,scope.Company,default));
            var notification = await db.Outbox.SingleAsync(x => x.AggregateId == transfer.Id);
            var receipt = await new Vat.Infrastructure.Messaging.ReportingInbox(db, TimeProvider.System).Process(
                new(System.Text.Encoding.UTF8.GetBytes(notification.Payload), notification.Id.ToString(), notification.EventType, "application/json"), default);
            Assert.Equal("Ignored", receipt.Status);
            var repair=await new LegacyTransferJournalRepair(db,TimeProvider.System).Run(scope.Tenant,scope.Company,"operator","Verify new posting evidence",false,default);
            Assert.Equal(0,repair.MissingEntries); return 0;
        });
        var history=(await maker.GetFromJsonAsync<TransferHistoryItem[]>(root))!.Single(); Assert.Equal(4,history.Version); Assert.Equal("checker",history.ApprovedBy);
        using var other=factory.Client(Guid.NewGuid(),scope.Company);
        Assert.Equal(HttpStatusCode.NotFound,(await Send(other,path+"/cancel",new TransferActionRequest(4,"test"))).StatusCode);
    }
    [Fact]
    public async Task Insufficient_or_reserved_stock_rolls_back_and_pending_transfers_block_close()
    {
        var scope=await Seed(); var maker=factory.Client(scope.Tenant,scope.Company); var checker=factory.Client(scope.Tenant,scope.Company,"checker");
        var root=$"/api/v1/companies/{scope.Company}/transfers";
        var transfer=await Post<TransferResult>(maker,root,Request(scope,80)); var path=root+$"/{transfer.Id}";
        await Post<TransferResult>(maker,path+"/submit",new TransferActionRequest(1));
        await Post<TransferResult>(checker,path+"/approve",new TransferActionRequest(2));
        await factory.Inspect(async db=>
        {
            await db.Set<StockBalance>().Where(x=>x.TenantId==scope.Tenant&&x.CompanyId==scope.Company).ExecuteUpdateAsync(x=>x.SetProperty(b=>b.ReservedQuantity,30));
            var period=(await db.Set<Vat.Domain.Company.FiscalPeriod>().FindAsync(scope.Tenant,scope.Company,scope.Period))!;
            Assert.True(await new PeriodStore(db).HasPending(period,default)); return 0;
        });
        Assert.Equal(HttpStatusCode.Conflict,(await Send(checker,path+"/post",new TransferActionRequest(3))).StatusCode);
        await factory.Inspect(async db=> { Assert.False(await db.Set<TransferJournalEntry>().AnyAsync(x=>x.TransferId==transfer.Id)); Assert.Equal(100,await db.Set<StockBalance>().Where(x=>x.TenantId==scope.Tenant&&x.CompanyId==scope.Company).SumAsync(x=>x.Quantity)); return 0; });
        var cancelled=await Post<TransferResult>(maker,path+"/cancel",new TransferActionRequest(3,"Source stock reserved")); Assert.Equal("Cancelled",cancelled.State);
    }
    [Fact]
    public async Task Concurrent_posts_cannot_spend_the_same_available_stock_twice()
    {
        var scope=await Seed(); using var maker=factory.Client(scope.Tenant,scope.Company); using var checker=factory.Client(scope.Tenant,scope.Company,"checker");
        var root=$"/api/v1/companies/{scope.Company}/transfers";
        async Task<Guid> Approved(string reference)
        {
            var transfer=await Post<TransferResult>(maker,root,Request(scope,80) with { TransferNumber=reference });
            await Post<TransferResult>(maker,root+$"/{transfer.Id}/submit",new TransferActionRequest(1));
            await Post<TransferResult>(checker,root+$"/{transfer.Id}/approve",new TransferActionRequest(2)); return transfer.Id;
        }
        var first=await Approved("FIRST"); var second=await Approved("SECOND");
        var results=await Task.WhenAll(Send(checker,root+$"/{first}/post",new TransferActionRequest(3)),Send(checker,root+$"/{second}/post",new TransferActionRequest(3)));
        Assert.Single(results,x=>x.StatusCode==HttpStatusCode.OK); Assert.Single(results,x=>x.StatusCode==HttpStatusCode.Conflict);
        await factory.Inspect(async db=>
        {
            Assert.Equal(new[]{20m,80m},await db.Set<StockBalance>().Where(x=>x.TenantId==scope.Tenant&&x.CompanyId==scope.Company).Select(x=>x.Quantity).OrderBy(x=>x).ToArrayAsync());
            Assert.Single(await db.Set<TransferJournalEntry>().Where(x=>x.CompanyId==scope.Company).ToArrayAsync());
            Assert.True(await new PeriodStore(db).BalancesReconcile(scope.Tenant,scope.Company,default)); return 0;
        });
    }
}
