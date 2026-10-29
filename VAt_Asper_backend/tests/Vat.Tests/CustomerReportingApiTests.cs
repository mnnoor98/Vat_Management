using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Vat.Messaging.Contracts.V1;
using Vat.Application;
using Vat.Application.Contracts;
using Vat.Application.Security;
using Vat.Domain.Sales;
using Vat.Domain.Purchasing;
using Xunit;

namespace Vat.Tests;

[Collection("PostgreSQL integration")]
public sealed class CustomerReportingApiTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
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
            setup.Branch, Guid.NewGuid().ToString(), supplier, date ?? new(2026, 1, 1),
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


    private static Task<CustomerResponse> Customer(SetupData s) => Created<CustomerResponse>(s.Maker, s.Root+"/customers", new CreateCustomerRequest("CUSTOMER","Report customer"));
    private static async Task<SalesInvoiceResponse> Invoice(SetupData s, Guid customer, DateOnly date, DateOnly? due = null, decimal price = 100)
    {
        var invoice = await Created<SalesInvoiceResponse>(s.Maker,s.Root+"/sales", new CreateSalesInvoiceRequest(s.Branch,Guid.NewGuid().ToString(),"CUSTOMER",date,[new("ITEM","Goods",1,price)],customer,due));
        var url=s.Root+$"/sales/{invoice.Id}";
        Assert.Equal(HttpStatusCode.OK,(await Post(s.Maker,url+"/submit",new TransitionRequest(1))).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await Post(s.Checker,url+"/approve",new TransitionRequest(2))).StatusCode);
        await Created<SalesPostingResponse>(s.Maker,url+"/reserve",new ReserveSalesRequest(3,s.Warehouse));
        Assert.Equal(HttpStatusCode.OK,(await Post(s.Checker,url+"/post",new TransitionRequest(4))).StatusCode);
        return (await s.Maker.GetFromJsonAsync<SalesInvoiceResponse>(url))!;
    }
    private static async Task<PaymentResponse> Receipt(SetupData s, Guid customer, DateOnly date, decimal amount, IReadOnlyList<PaymentAllocationInput>? lines = null, bool approve = true)
    {
        var p=await Created<PaymentResponse>(s.Maker,s.Root+"/payments",new CreatePaymentRequest(customer,Guid.NewGuid().ToString(),"Cash","CASH-EVIDENCE",date,amount,lines));
        if (approve) Assert.Equal(HttpStatusCode.OK,(await Post(s.Checker,s.Root+$"/payments/{p.Payment.Id}/approve",new TransitionRequest(1))).StatusCode);
        return p;
    }
    private static Task<CustomerAgeing?> Ageing(SetupData s,Guid customer,DateOnly date) => s.Maker.GetFromJsonAsync<CustomerAgeing>(s.Root+$"/customers/{customer}/ageing?asOf={date:yyyy-MM-dd}");
    private static Task<CustomerStatement?> Statement(SetupData s,Guid customer,string from="2026-10-01",string until="2026-10-25") => s.Maker.GetFromJsonAsync<CustomerStatement>(s.Root+$"/customers/{customer}/statement?from={from}&until={until}");

    [Fact]
    public async Task Authoritative_customer_export_matches_independent_database_statements_ageing_and_frozen_history()
    {
        var (s, _) = await Stock(); var customer = await Customer(s);
        var invoice = await Invoice(s, customer.Id, new(2026, 10, 1), new(2026, 10, 20));
        var payment = await Receipt(s, customer.Id, new(2026, 10, 10), 100, [new(invoice.Id, 50)]);
        var line = (await s.Maker.GetFromJsonAsync<SalesReturnableResponse>(s.Root + $"/sales/{invoice.Id}/returnable-lines"))!.Lines.Single();
        var credit = await Created<SalesCreditResponse>(s.Maker, s.Root + $"/sales/{invoice.Id}/price-credits",
            new RequestSalesPriceCredit(5, new(2026, 10, 15), "Reduction", "AGREEMENT", [new(line.LineId, 20)]));
        Assert.Equal(HttpStatusCode.OK, (await Post(s.Checker, s.Root + $"/sales/{invoice.Id}/credit-notes/{credit.Id}/approve", new TransitionRequest(credit.SourceVersion))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Post(s.Maker, s.Root + $"/payments/{payment.Payment.Id}/allocate",
            new AllocatePaymentRequest(2, new(2026, 10, 20), [new(invoice.Id, 34)]))).StatusCode);
        var debit = await Created<SalesDebitResponse>(s.Maker, s.Root + $"/sales/{invoice.Id}/debit-notes",
            new RequestSalesDebit(7, new(2026, 10, 22), "Increase", "AGREEMENT", [new(line.LineId, 10)]));
        Assert.Equal(HttpStatusCode.OK, (await Post(s.Checker, s.Root + $"/sales/{invoice.Id}/debit-notes/{debit.Id}/approve", new TransitionRequest(debit.SourceVersion))).StatusCode);
        var refund = await Created<PaymentCorrectionResponse>(s.Maker, s.Root + $"/payments/{payment.Payment.Id}/refunds",
            new RequestCustomerRefund(3, new(2026, 10, 23), 5.5m, "Bank", "Return customer credit", "EVIDENCE"));
        Assert.Equal(HttpStatusCode.OK, (await Post(s.Checker, s.Root + $"/payments/{payment.Payment.Id}/refunds/{refund.Id}/approve",
            new TransitionRequest(refund.SourceVersion))).StatusCode);
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["ReportingSourceServer:Enabled"] = "true" })));
        using var client = host.CreateClient(); client.DefaultRequestHeaders.Authorization = s.Maker.DefaultRequestHeaders.Authorization;
        var path = $"/internal/v1/reporting-source/companies/{s.Company}/customers/{customer.Id}/exports";
        var key = Guid.NewGuid().ToString();
        using var prepared = await Post(client, path, new PrepareReportingExportV1("Customer parity"), key);
        Assert.Equal(HttpStatusCode.OK, prepared.StatusCode);
        var manifest = (await prepared.Content.ReadFromJsonAsync<CustomerHistoryManifestV1>())!;
        using var replay = await Post(client, path, new PrepareReportingExportV1("Customer parity"), key);
        Assert.Equal(await prepared.Content.ReadAsStringAsync(), await replay.Content.ReadAsStringAsync());
        var export = (await client.GetFromJsonAsync<CustomerHistoryExportV1>(path + $"/{manifest.ExportId}"))!;
        Assert.True(manifest.FullHistory); Assert.Single(export.Data.Corrections);
        using var wrongTenant = host.CreateClient(); using var wrongToken = factory.Client(Guid.NewGuid(), s.Company);
        wrongTenant.DefaultRequestHeaders.Authorization = wrongToken.DefaultRequestHeaders.Authorization;
        Assert.Equal(HttpStatusCode.NotFound, (await wrongTenant.GetAsync(path + $"/{manifest.ExportId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/internal/v1/reporting-source/companies/{s.Company}/exports/{manifest.ExportId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path.Replace(customer.Id.ToString(), Guid.NewGuid().ToString()) + $"/{manifest.ExportId}")).StatusCode);
        var databaseName = "customer_parity_" + Guid.NewGuid().ToString("N");
        var adminConnection = Environment.GetEnvironmentVariable("VAT_TEST_POSTGRES")!;
        await using var admin = new NpgsqlConnection(adminConnection); await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            var connection = new NpgsqlConnectionStringBuilder(adminConnection) { Database = databaseName, Pooling = false }.ConnectionString;
            var options = new DbContextOptionsBuilder<Vat.Services.Reporting.Infrastructure.ReportingDbContext>();
            Vat.Services.Reporting.Infrastructure.ReportingPersistence.Configure(options, connection);
            await using var owned = new Vat.Services.Reporting.Infrastructure.ReportingDbContext(options.Options); await owned.Database.MigrateAsync();
            var actor = new Actor(s.Tenant, "maker", new HashSet<Guid> { s.Company }, new HashSet<string> { Permissions.ReportRead, Permissions.ReportManage });
            var service = new Vat.Services.Reporting.Application.CustomerSnapshotService(new Vat.Services.Reporting.Infrastructure.CustomerSnapshotStore(owned, TimeProvider.System));
            using var source = new Vat.Services.Reporting.Infrastructure.ReportingExportHttpSource("http://localhost/", client.DefaultRequestHeaders.Authorization!.Parameter!, true, client);
            var snapshot = await service.Bootstrap(actor, s.Company, customer.Id, Guid.NewGuid(), new(manifest.ExportId, "Compare historical reports"), source, default);
            foreach (var dates in new[] { (From: new DateOnly(2026, 10, 1), Until: new DateOnly(2026, 10, 12)),
                         (From: new DateOnly(2026, 10, 12), Until: new DateOnly(2026, 10, 22)),
                         (From: new DateOnly(2026, 10, 12), Until: new DateOnly(2026, 10, 24)) })
            {
                var old = (await Statement(s, customer.Id, dates.From.ToString("yyyy-MM-dd"), dates.Until.ToString("yyyy-MM-dd")))!;
                var next = await service.Statement(actor, s.Company, customer.Id, snapshot.GenerationId, dates.From, dates.Until, default);
                var comparable = JsonSerializer.Deserialize<CustomerStatement>(JsonSerializer.Serialize(next.Report, ReportingSourceV1.Json), ReportingSourceV1.Json)!;
                Assert.Equal(old with { CapturedAt = comparable.CapturedAt, Lines = comparable.Lines }, comparable);
                Assert.Equal(old.Lines, comparable.Lines);
                var oldAgeing = (await Ageing(s, customer.Id, dates.Until.AddDays(-1)))!;
                var newAgeing = await service.Ageing(actor, s.Company, customer.Id, snapshot.GenerationId, dates.Until.AddDays(-1), default);
                var age = JsonSerializer.Deserialize<CustomerAgeing>(JsonSerializer.Serialize(newAgeing.Report, ReportingSourceV1.Json), ReportingSourceV1.Json)!;
                Assert.Equal(oldAgeing with { CapturedAt = age.CapturedAt, Invoices = age.Invoices }, age);
                Assert.Equal(oldAgeing.Invoices, age.Invoices); Assert.False(newAgeing.Snapshot.LiveCoverageVerified);
            }
            // Later committed financial activity must not change a frozen customer export.
            await Receipt(s, customer.Id, new(2026, 10, 24), 7);
            var frozen = (await client.GetFromJsonAsync<CustomerHistoryExportV1>(path + $"/{manifest.ExportId}"))!;
            Assert.Equal(JsonSerializer.Serialize(export, ReportingSourceV1.Json), JsonSerializer.Serialize(frozen, ReportingSourceV1.Json));
            Assert.Equal(1, await owned.Set<Vat.Services.Reporting.Infrastructure.CustomerSnapshotGeneration>().CountAsync());
        }
        finally
        {
            // Database DROP can wait on a Windows filesystem checkpoint after parity.
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\"", admin) { CommandTimeout = 120 };
            await drop.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task Due_date_is_frozen_optional_and_cannot_precede_invoice_date()
    {
        var (s,_)=await Stock(); var customer=await Customer(s);
        var invoice=await Invoice(s,customer.Id,new(2026,10,1),new(2026,10,20));
        Assert.Equal(new DateOnly(2026,10,20),invoice.DueDate);
        Assert.Equal(new DateOnly(2026,10,20),(await s.Maker.GetFromJsonAsync<SalesInvoiceResponse>(s.Root+$"/sales/{invoice.Id}"))!.DueDate);
        var unknown=await Invoice(s,customer.Id,new(2026,10,1)); Assert.Null(unknown.DueDate);
        var invalid=new CreateSalesInvoiceRequest(s.Branch,Guid.NewGuid().ToString(),"CUSTOMER",new(2026,10,1),[new("ITEM","Goods",1,100)],customer.Id,new(2026,9,30));
        Assert.Equal(HttpStatusCode.UnprocessableEntity,(await Post(s.Maker,s.Root+"/sales",invalid)).StatusCode);
        Assert.Equal(2,await factory.Inspect(db=>db.SalesInvoices.CountAsync(x=>x.CompanyId==s.Company)));
        var report=(await Ageing(s,customer.Id,new(2026,10,24)))!;
        Assert.Equal(105,report.Buckets.Overdue1To30); Assert.Equal(105,report.Buckets.UnknownDueDate);
    }
    [Fact]
    public async Task Statement_opening_running_totals_and_asof_ageing_handle_receipts_credits_debits_and_late_allocation()
    {
        var (s,_)=await Stock(); var customer=await Customer(s);
        var invoice=await Invoice(s,customer.Id,new(2026,10,1),new(2026,10,20));
        var p=await Receipt(s,customer.Id,new(2026,10,10),100,[new(invoice.Id,50)]);
        var line=(await s.Maker.GetFromJsonAsync<SalesReturnableResponse>(s.Root+$"/sales/{invoice.Id}/returnable-lines"))!.Lines.Single();
        var credit=await Created<SalesCreditResponse>(s.Maker,s.Root+$"/sales/{invoice.Id}/price-credits",new RequestSalesPriceCredit(5,new(2026,10,15),"Reduction","AGREEMENT",[new(line.LineId,20)]));
        Assert.Equal(HttpStatusCode.OK,(await Post(s.Checker,s.Root+$"/sales/{invoice.Id}/credit-notes/{credit.Id}/approve",new TransitionRequest(credit.SourceVersion))).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await Post(s.Maker,s.Root+$"/payments/{p.Payment.Id}/allocate",new AllocatePaymentRequest(2,new(2026,10,20),[new(invoice.Id,34)]))).StatusCode);
        var debit=await Created<SalesDebitResponse>(s.Maker,s.Root+$"/sales/{invoice.Id}/debit-notes",new RequestSalesDebit(7,new(2026,10,22),"Increase","AGREEMENT",[new(line.LineId,10)]));
        Assert.Equal(HttpStatusCode.OK,(await Post(s.Checker,s.Root+$"/sales/{invoice.Id}/debit-notes/{debit.Id}/approve",new TransitionRequest(debit.SourceVersion))).StatusCode);
        var before=(await Ageing(s,customer.Id,new(2026,10,14)))!;
        Assert.Equal(55,before.Buckets.NotDue); Assert.Equal(50,before.UnallocatedAmount); Assert.Equal(5,before.Balance);
        var afterCredit=(await Ageing(s,customer.Id,new(2026,10,16)))!;
        Assert.Equal(34,afterCredit.OutstandingDebitAmount); Assert.Equal(-16,afterCredit.Balance);
        var afterAllocation=(await Ageing(s,customer.Id,new(2026,10,21)))!;
        Assert.Equal(0,afterAllocation.OutstandingDebitAmount); Assert.Equal(16,afterAllocation.UnallocatedAmount); Assert.Equal(-16,afterAllocation.Balance);
        var statement=(await Statement(s,customer.Id,"2026-10-12","2026-10-24"))!;
        Assert.Equal(5,statement.OpeningBalance); Assert.Equal(10.5m,statement.TotalDebit); Assert.Equal(21,statement.TotalCredit); Assert.Equal(-5.5m,statement.ClosingBalance);
        Assert.Equal(new[]{"CreditNote","DebitNote"},statement.Lines.Select(x=>x.Type)); Assert.Equal(new[]{-16m,-5.5m},statement.Lines.Select(x=>x.RunningBalance));
        Assert.Equal(10.5m,statement.Lines[1].BalanceChange);
        var exclusive=(await Statement(s,customer.Id,"2026-10-12","2026-10-22"))!; Assert.Single(exclusive.Lines); Assert.Equal(-16,exclusive.ClosingBalance);
        Assert.Equal(statement.ClosingBalance,(await Ageing(s,customer.Id,new(2026,10,23)))!.Balance);
        var all=(await Statement(s,customer.Id))!; Assert.Equal(4,all.Lines.Count); Assert.Equal("Invoice",all.Lines[0].Type); Assert.Equal("Payment",all.Lines[1].Type);
    }
    [Fact]
    public async Task Ageing_uses_exact_day_boundaries_and_keeps_unknown_terms_separate()
    {
        var (s,_)=await Stock(); var customer=await Customer(s); var asOf=new DateOnly(2026,10,24);
        foreach(var days in new int?[]{-1,0,1,30,31,60,61,90,91,null})
            await Invoice(s,customer.Id,new(2026,1,2),days.HasValue?asOf.AddDays(-days.Value):null,1);
        var report=(await Ageing(s,customer.Id,asOf))!;
        Assert.Equal(new AgeingBuckets(2.1m,2.1m,2.1m,2.1m,1.05m,1.05m),report.Buckets);
        Assert.Equal(10.5m,report.OutstandingDebitAmount); Assert.Equal(10.5m,report.Balance); Assert.Equal(10,report.Invoices.Count);
        Assert.Equal("OverdueOver90",report.Invoices.Single(x=>x.DaysOverdue==91).Bucket);
        Assert.Null(report.Invoices.Single(x=>x.Bucket=="UnknownDueDate").DaysOverdue);
    }
    [Fact]
    public async Task Future_and_pending_financial_activity_are_excluded_and_invoice_credit_is_not_netted_into_overdue_buckets()
    {
        var (s,_)=await Stock(); var customer=await Customer(s);
        var invoice=await Invoice(s,customer.Id,new(2026,10,1),new(2026,10,2));
        await Receipt(s,customer.Id,new(2026,10,5),20,approve:false);
        var cancelled=await Receipt(s,customer.Id,new(2026,10,6),20,approve:false);
        Assert.Equal(HttpStatusCode.OK,(await Post(s.Maker,s.Root+$"/payments/{cancelled.Payment.Id}/cancel",new CancelSalesInvoiceRequest(1,"Duplicate"))).StatusCode);
        await Receipt(s,customer.Id,new(2026,10,25),20);
        await Invoice(s,customer.Id,new(2026,10,30),new(2026,10,31));
        var asOf=(await Ageing(s,customer.Id,new(2026,10,24)))!; Assert.Equal(0,asOf.CollectedAmount); Assert.Equal(105,asOf.Buckets.Overdue1To30); Assert.Single(asOf.Invoices);
        var statement=(await Statement(s,customer.Id))!; Assert.Single(statement.Lines); Assert.Equal(105,statement.ClosingBalance);
        await Receipt(s,customer.Id,new(2026,10,10),105,[new(invoice.Id,105)]);
        var line=(await s.Maker.GetFromJsonAsync<SalesReturnableResponse>(s.Root+$"/sales/{invoice.Id}/returnable-lines"))!.Lines.Single();
        var credit=await Created<SalesCreditResponse>(s.Maker,s.Root+$"/sales/{invoice.Id}/price-credits",new RequestSalesPriceCredit(5,new(2026,10,15),"Reduction","AGREEMENT",[new(line.LineId,20)]));
        Assert.Equal(HttpStatusCode.OK,(await Post(s.Checker,s.Root+$"/sales/{invoice.Id}/credit-notes/{credit.Id}/approve",new TransitionRequest(credit.SourceVersion))).StatusCode);
        var paid=(await Ageing(s,customer.Id,new(2026,10,24)))!; Assert.Equal(21,paid.InvoiceCreditAmount); Assert.Equal(0,paid.OutstandingDebitAmount); Assert.Equal(-21,paid.Balance); Assert.Equal("Credit",paid.Invoices.Single().Bucket);
    }
    [Fact]
    public async Task Reports_enforce_report_permission_scope_required_dates_and_bounded_statement_window()
    {
        var (s,_)=await Stock(); var customer=await Customer(s);
        var path=s.Root+$"/customers/{customer.Id}";
        using var reader=factory.Client(s.Tenant,s.Company,permissions:[Permissions.ReportRead]);
        using var paymentsOnly=factory.Client(s.Tenant,s.Company,permissions:[Permissions.PaymentRead]);
        using var wrongTenant=factory.Client(Guid.NewGuid(),s.Company);
        using var wrongCompany=factory.Client(s.Tenant,Guid.NewGuid());
        Assert.Equal(HttpStatusCode.OK,(await reader.GetAsync(path+"/ageing?asOf=2026-10-24")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await paymentsOnly.GetAsync(path+"/ageing?asOf=2026-10-24")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await wrongTenant.GetAsync(path+"/statement?from=2026-10-01&until=2026-10-25")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await wrongCompany.GetAsync(path+"/ageing?asOf=2026-10-24")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await reader.GetAsync(path+"/ageing")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,(await reader.GetAsync(path+"/ageing?asOf=0001-01-01")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,(await reader.GetAsync(path+"/statement?from=2026-10-01&until=2026-10-01")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,(await reader.GetAsync(path+"/statement?from=2025-01-01&until=2026-10-01")).StatusCode);
        var report=(await reader.GetFromJsonAsync<CustomerStatement>(path+"/statement?from=2026-10-01&until=2026-10-25"))!;
        Assert.Empty(report.Lines); Assert.Equal(0,report.OpeningBalance); Assert.Equal(0,report.ClosingBalance);
    }
    [Fact]
    public async Task Pre_due_date_idempotency_records_replay_without_conflict_and_changed_terms_are_rejected()
    {
        var (s,_)=await Stock(); var customer=await Customer(s); var key=Guid.NewGuid().ToString();
        var body=new CreateSalesInvoiceRequest(s.Branch,Guid.NewGuid().ToString(),"CUSTOMER",new(2026,10,1),[new("ITEM","Goods",1,100)],customer.Id);
        var first=await Post(s.Maker,s.Root+"/sales",body,key); Assert.Equal(HttpStatusCode.Created,first.StatusCode);
        var oldContract=new { body.BranchId,body.ExternalReference,body.CustomerReference,body.BusinessDate,body.Lines,body.CustomerId };
        var oldHash=Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(oldContract,new JsonSerializerOptions(JsonSerializerDefaults.Web))));
        await factory.Inspect(async db=>
        {
            var record=await db.Idempotency.SingleAsync(x=>x.CompanyId==s.Company && x.Operation=="sales.create" && x.Key==key);
            record.RequestHash=oldHash; return await db.SaveChangesAsync();
        });
        var retry=await Post(s.Maker,s.Root+"/sales",body,key); Assert.Equal(HttpStatusCode.Created,retry.StatusCode);
        Assert.Equal("true",retry.Headers.GetValues("Idempotency-Replayed").Single());
        Assert.Equal(await first.Content.ReadAsStringAsync(),await retry.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict,(await Post(s.Maker,s.Root+"/sales",body with { DueDate=new(2026,10,20) },key)).StatusCode);
        Assert.Equal(1,await factory.Inspect(db=>db.SalesInvoices.CountAsync(x=>x.CompanyId==s.Company)));
    }
    [Fact]
    public async Task Corrupt_current_history_blocks_even_historical_reports_without_writing_financial_records()
    {
        var (s,_)=await Stock(); var customer=await Customer(s); var invoice=await Invoice(s,customer.Id,new(2026,10,1));
        await Receipt(s,customer.Id,new(2026,10,10),50,[new(invoice.Id,50)]);
        var count=await factory.Inspect(db=>db.Audit.CountAsync(x=>x.CompanyId==s.Company));
        await Statement(s,customer.Id); await Ageing(s,customer.Id,new(2026,10,24));
        Assert.Equal(count,await factory.Inspect(db=>db.Audit.CountAsync(x=>x.CompanyId==s.Company)));
        await factory.Inspect(async db=>await db.Set<PaymentAllocation>().Where(x=>x.CompanyId==s.Company).ExecuteDeleteAsync());
        Assert.Equal(HttpStatusCode.Conflict,(await s.Maker.GetAsync(s.Root+$"/customers/{customer.Id}/statement?from=2026-01-01&until=2026-10-05")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await s.Maker.GetAsync(s.Root+$"/customers/{customer.Id}/ageing?asOf=2026-10-05")).StatusCode);
    }
}
