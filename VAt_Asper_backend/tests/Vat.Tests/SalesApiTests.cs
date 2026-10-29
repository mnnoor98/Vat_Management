using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Vat.Application.Contracts;
using Vat.Application.Security;
using Vat.Domain.Sales;
using Xunit;

namespace Vat.Tests;

[Collection("PostgreSQL integration")]
public sealed class SalesInvoiceApiTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    [Fact]
    public async Task Period_close_request_and_sales_approval_cannot_both_succeed()
    {
        var s = await Setup();
        var root = $"/api/v1/companies/{s.Company}";
        var created = await Post(s.Maker, $"{root}/sales", Draft(s.Branch));
        var id = (await created.Content.ReadFromJsonAsync<SalesInvoiceResponse>())!.Id;
        Assert.Equal(HttpStatusCode.OK, (await Post(s.Maker, $"{root}/sales/{id}/submit", new TransitionRequest(1))).StatusCode);
        var period = (await s.Maker.GetFromJsonAsync<PeriodResponse[]>($"{root}/periods"))!.Single();
        var results = await Task.WhenAll(
            Post(s.Checker, $"{root}/sales/{id}/approve", new TransitionRequest(2)),
            Post(s.Maker, $"{root}/periods/{period.Id}/request-close", new PeriodActionRequest(1, "Close review")));
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData("0", "10")]
    [InlineData("1.0000001", "10")]
    [InlineData("1", "-1")]
    [InlineData("1", "0.00001")]
    public async Task Invalid_monetary_precision_or_quantity_rolls_back_creation(string quantity, string price)
    {
        var s = await Setup();
        var draft = Draft(s.Branch) with { Lines = [new SalesInvoiceLineInput("ITEM-001", "Goods",
            decimal.Parse(quantity, System.Globalization.CultureInfo.InvariantCulture),
            decimal.Parse(price, System.Globalization.CultureInfo.InvariantCulture))] };
        var response = await Post(s.Maker, $"/api/v1/companies/{s.Company}/sales", draft);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(0, await factory.Inspect(db => db.SalesInvoices.CountAsync(x => x.CompanyId == s.Company)));
        Assert.Equal(0, await factory.Inspect(db => db.Audit.CountAsync(x => x.CompanyId == s.Company && x.Action == "SalesInvoiceCreated")));
    }

    [Fact]
    public async Task Approved_invoice_blocks_close_until_cancelled_without_creating_financial_entries()
    {
        var s = await Setup();
        var root = $"/api/v1/companies/{s.Company}";
        var created = await Post(s.Maker, $"{root}/sales", Draft(s.Branch));
        var invoice = (await created.Content.ReadFromJsonAsync<SalesInvoiceResponse>())!;
        var url = $"{root}/sales/{invoice.Id}";
        Assert.Equal(HttpStatusCode.OK, (await Post(s.Maker, $"{url}/submit", new TransitionRequest(1))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Post(s.Checker, $"{url}/approve", new TransitionRequest(2))).StatusCode);
        var period = (await s.Maker.GetFromJsonAsync<PeriodResponse[]>($"{root}/periods"))!.Single();
        Assert.Equal(HttpStatusCode.Conflict, (await Post(s.Maker, $"{root}/periods/{period.Id}/request-close",
            new PeriodActionRequest(1, "Month end"))).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Post(s.Maker, $"{url}/cancel",
            new CancelSalesInvoiceRequest(3, " "))).StatusCode);
        var key = Guid.NewGuid().ToString();
        var cancelled = await Post(s.Maker, $"{url}/cancel", new CancelSalesInvoiceRequest(3, "Order withdrawn"), key);
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        var replay = await Post(s.Maker, $"{url}/cancel", new CancelSalesInvoiceRequest(3, "Order withdrawn"), key);
        Assert.Equal(await cancelled.Content.ReadAsStringAsync(), await replay.Content.ReadAsStringAsync());
        Assert.Equal("Cancelled", (await cancelled.Content.ReadFromJsonAsync<SalesInvoiceResponse>())!.Status);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(s.Maker, $"{url}/submit", new TransitionRequest(4))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Post(s.Maker, $"{root}/periods/{period.Id}/request-close",
            new PeriodActionRequest(1, "Month end"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Post(s.Checker, $"{root}/periods/{period.Id}/close",
            new PeriodActionRequest(2, "Reviewed"))).StatusCode);
        Assert.Equal(0, await factory.Inspect(db => db.Set<Vat.Domain.Posting.StockMovement>().CountAsync(x => x.CompanyId == s.Company)));
        Assert.Equal(0, await factory.Inspect(db => db.Set<Vat.Domain.Posting.TaxLedgerEntry>().CountAsync(x => x.CompanyId == s.Company)));
        var payload = await factory.Inspect(db => db.Outbox.Where(x => x.AggregateId == invoice.Id).Select(x => x.Payload).SingleAsync());
        var message = JsonSerializer.Deserialize<IntegrationEvent<JsonElement>>(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        // Approval is a notification, never a financial reporting entry.
        using var scope = factory.Services.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<Vat.Application.IIncomingEventProcessor>();
        var result = await processor.Process(new Vat.Application.ReceivedMessage(System.Text.Encoding.UTF8.GetBytes(payload), message.EventId.ToString(), message.EventType,
            "application/json"), default);
        Assert.Equal("Ignored", result.Status);
    }

    [Fact]
    public async Task Unregistered_product_and_closing_period_prevent_submission_and_approval()
    {
        var s = await Setup();
        var root = $"/api/v1/companies/{s.Company}";
        var invalid = Draft(s.Branch) with { Lines = [new SalesInvoiceLineInput("UNKNOWN", "Unknown", 1, 10)] };
        var created = await Post(s.Maker, $"{root}/sales", invalid);
        var id = (await created.Content.ReadFromJsonAsync<SalesInvoiceResponse>())!.Id;
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Post(s.Maker, $"{root}/sales/{id}/submit", new TransitionRequest(1))).StatusCode);
        created = await Post(s.Maker, $"{root}/sales", Draft(s.Branch));
        id = (await created.Content.ReadFromJsonAsync<SalesInvoiceResponse>())!.Id;
        Assert.Equal(HttpStatusCode.OK, (await Post(s.Maker, $"{root}/sales/{id}/submit", new TransitionRequest(1))).StatusCode);
        var period = (await s.Maker.GetFromJsonAsync<PeriodResponse[]>($"{root}/periods"))!.Single();
        Assert.Equal(HttpStatusCode.OK, (await Post(s.Maker, $"{root}/periods/{period.Id}/request-close", new PeriodActionRequest(1, "Closing"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(s.Checker, $"{root}/sales/{id}/approve", new TransitionRequest(2))).StatusCode);
        // Nonfinancial documents can still be cancelled during close.
        Assert.Equal(HttpStatusCode.OK, (await Post(s.Maker, $"{root}/sales/{id}/cancel", new CancelSalesInvoiceRequest(2, "Withdrawn"))).StatusCode);
        using var noSalesPermission = factory.Client(s.Tenant, s.Company, permissions: [Permissions.PurchaseApprove]);
        Assert.Equal(HttpStatusCode.Forbidden, (await noSalesPermission.GetAsync($"{root}/sales")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(noSalesPermission, $"{root}/sales", Draft(s.Branch))).StatusCode);
    }

    private static async Task<HttpResponseMessage> Post(HttpClient client, string url, object body, string? key = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString());
        return await client.SendAsync(request);
    }

    private async Task<(HttpClient Maker, HttpClient Checker, Guid Tenant, Guid Company, Guid Branch)> Setup()
    {
        var tenant = Guid.NewGuid();
        var company = Guid.NewGuid();
        var maker = factory.Client(tenant, company);
        var created = await Post(maker, "/api/v1/companies", new CreateCompanyRequest(company, "Pilot company", Guid.NewGuid().ToString()));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var period = await Post(maker, $"/api/v1/companies/{company}/periods",
            new CreatePeriodRequest("Test year", new(2026, 1, 1), new(2027, 1, 1)));
        Assert.Equal(HttpStatusCode.Created, period.StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await Post(maker, $"/api/v1/companies/{company}/products",
            new CreateProductRequest("ITEM-001", "Goods", "EA", "TEST"))).StatusCode);
        var branch = await Post(maker, $"/api/v1/companies/{company}/branches", new CreateBranchRequest("Dhaka"));
        Assert.Equal(HttpStatusCode.Created, branch.StatusCode);
        var branchBody = await branch.Content.ReadFromJsonAsync<BranchResponse>();
        return (maker, factory.Client(tenant, company, "checker"), tenant, company, branchBody!.Id);
    }

    private static CreateSalesInvoiceRequest Draft(Guid branch, string? reference = null) => new(branch,
        reference ?? Guid.NewGuid().ToString(), "SUPPLIER-001", new DateOnly(2026, 10, 23),
        [new SalesInvoiceLineInput("ITEM-001", "SalesInvoiced materials", 2.5m, 100m)]);

    [Fact]
    public async Task Approved_sale_commits_one_event_and_audit_with_same_transaction()
    {
        var setup = await Setup();
        var url = $"/api/v1/companies/{setup.Company}/sales";
        var created = await Post(setup.Maker, url, Draft(setup.Branch));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var sale = (await created.Content.ReadFromJsonAsync<SalesInvoiceResponse>())!;
        Assert.Equal(250m, sale.NetAmount);
        Assert.Equal(HttpStatusCode.OK, (await setup.Maker.GetAsync(created.Headers.Location)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Post(setup.Maker, $"{url}/{sale.Id}/submit", new TransitionRequest(1))).StatusCode);
        var key = Guid.NewGuid().ToString();
        var approved = await Post(setup.Checker, $"{url}/{sale.Id}/approve", new TransitionRequest(2), key);
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        var result = (await approved.Content.ReadFromJsonAsync<SalesInvoiceResponse>())!;
        Assert.Equal("ApprovedPendingPosting", result.Status);
        Assert.Equal(3, result.Version);
        var replay = await Post(setup.Checker, $"{url}/{sale.Id}/approve", new TransitionRequest(2), key);
        Assert.Equal(await approved.Content.ReadAsStringAsync(), await replay.Content.ReadAsStringAsync());
        Assert.Equal("true", replay.Headers.GetValues("Idempotency-Replayed").Single());
        Assert.Equal(1, await factory.Inspect(db => db.Outbox.CountAsync(x => x.AggregateId == sale.Id)));
        Assert.Equal(3, await factory.Inspect(db => db.Audit.CountAsync(x => x.ResourceId == sale.Id)));
        var payload = await factory.Inspect(db => db.Outbox.Where(x => x.AggregateId == sale.Id).Select(x => x.Payload).SingleAsync());
        using var envelope = JsonDocument.Parse(payload);
        Assert.Equal(setup.Tenant, envelope.RootElement.GetProperty("tenantId").GetGuid());
        Assert.Equal("SalesInvoiceApprovedV1", envelope.RootElement.GetProperty("eventType").GetString());
    }

    [Fact]
    public async Task Concurrent_same_key_returns_identical_resource_and_payload_change_conflicts()
    {
        var setup = await Setup();
        var url = $"/api/v1/companies/{setup.Company}/sales";
        var key = Guid.NewGuid().ToString();
        var draft = Draft(setup.Branch);
        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Post(setup.Maker, url, draft, key)));
        Assert.All(results, response => Assert.Equal(HttpStatusCode.Created, response.StatusCode));
        var bodies = await Task.WhenAll(results.Select(x => x.Content.ReadAsStringAsync()));
        Assert.Single(bodies.Distinct());
        Assert.Equal(1, await factory.Inspect(db => db.SalesInvoices.CountAsync(x => x.CompanyId == setup.Company)));
        var changed = await Post(setup.Maker, url, draft with { CustomerReference = "OTHER" }, key);
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        var anotherActor = await Post(setup.Checker, url, draft, key);
        Assert.Equal(HttpStatusCode.Conflict, anotherActor.StatusCode);
    }

    [Fact]
    public async Task Self_approval_and_stale_version_leave_no_approval_event()
    {
        var setup = await Setup();
        var url = $"/api/v1/companies/{setup.Company}/sales";
        var created = await Post(setup.Maker, url, Draft(setup.Branch));
        var id = (await created.Content.ReadFromJsonAsync<SalesInvoiceResponse>())!.Id;
        await Post(setup.Maker, $"{url}/{id}/submit", new TransitionRequest(1));
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(setup.Maker, $"{url}/{id}/approve", new TransitionRequest(2))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(setup.Checker, $"{url}/{id}/approve", new TransitionRequest(1))).StatusCode);
        Assert.Equal(0, await factory.Inspect(db => db.Outbox.CountAsync(x => x.AggregateId == id)));
        Assert.Equal(2, await factory.Inspect(db => db.Audit.CountAsync(x => x.ResourceId == id)));
    }

    [Fact]
    public async Task Concurrent_distinct_approval_keys_allow_only_one_state_transition()
    {
        var setup = await Setup();
        var url = $"/api/v1/companies/{setup.Company}/sales";
        var created = await Post(setup.Maker, url, Draft(setup.Branch));
        var id = (await created.Content.ReadFromJsonAsync<SalesInvoiceResponse>())!.Id;
        await Post(setup.Maker, $"{url}/{id}/submit", new TransitionRequest(1));
        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Post(setup.Checker, $"{url}/{id}/approve", new TransitionRequest(2))));
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.OK);
        Assert.All(results.Where(x => x.StatusCode != HttpStatusCode.OK), x => Assert.Equal(HttpStatusCode.Conflict, x.StatusCode));
        Assert.Equal(1, await factory.Inspect(db => db.Outbox.CountAsync(x => x.AggregateId == id)));
        Assert.Equal(3, await factory.Inspect(db => db.Audit.CountAsync(x => x.ResourceId == id)));
    }

    [Fact]
    public async Task Tenant_and_company_scope_are_enforced_on_reads_and_writes()
    {
        var setup = await Setup();
        var url = $"/api/v1/companies/{setup.Company}/sales";
        var created = await Post(setup.Maker, url, Draft(setup.Branch));
        var id = (await created.Content.ReadFromJsonAsync<SalesInvoiceResponse>())!.Id;
        using var wrongCompany = factory.Client(setup.Tenant, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Forbidden, (await wrongCompany.GetAsync($"{url}/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(wrongCompany, url, Draft(setup.Branch))).StatusCode);
        using var wrongTenant = factory.Client(Guid.NewGuid(), setup.Company);
        Assert.Equal(HttpStatusCode.NotFound, (await wrongTenant.GetAsync($"{url}/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Post(wrongTenant, url, Draft(setup.Branch))).StatusCode);
        var listed = await wrongTenant.GetFromJsonAsync<SalesInvoiceResponse[]>(url);
        Assert.Empty(listed!);
    }

    [Fact]
    public async Task Duplicate_external_reference_rolls_back_audit_and_idempotency()
    {
        var setup = await Setup();
        var url = $"/api/v1/companies/{setup.Company}/sales";
        var draft = Draft(setup.Branch);
        Assert.Equal(HttpStatusCode.Created, (await Post(setup.Maker, url, draft)).StatusCode);
        var auditBefore = await factory.Inspect(db => db.Audit.CountAsync(x => x.CompanyId == setup.Company));
        var keysBefore = await factory.Inspect(db => db.Idempotency.CountAsync(x => x.CompanyId == setup.Company));
        Assert.Equal(HttpStatusCode.Conflict, (await Post(setup.Maker, url, draft)).StatusCode);
        Assert.Equal(auditBefore, await factory.Inspect(db => db.Audit.CountAsync(x => x.CompanyId == setup.Company)));
        Assert.Equal(keysBefore, await factory.Inspect(db => db.Idempotency.CountAsync(x => x.CompanyId == setup.Company)));
    }

    [Fact]
    public async Task Foreign_company_branch_cannot_be_used_and_rejection_is_terminal()
    {
        var setup = await Setup();
        var other = await Setup();
        var url = $"/api/v1/companies/{setup.Company}/sales";
        Assert.Equal(HttpStatusCode.NotFound, (await Post(setup.Maker, url, Draft(other.Branch))).StatusCode);
        var created = await Post(setup.Maker, url, Draft(setup.Branch));
        var id = (await created.Content.ReadFromJsonAsync<SalesInvoiceResponse>())!.Id;
        await Post(setup.Maker, $"{url}/{id}/submit", new TransitionRequest(1));
        var rejected = await Post(setup.Checker, $"{url}/{id}/reject", new RejectSalesInvoiceRequest(2, "Missing evidence"));
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        Assert.Equal("Rejected", (await rejected.Content.ReadFromJsonAsync<SalesInvoiceResponse>())!.Status);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(setup.Checker, $"{url}/{id}/approve", new TransitionRequest(3))).StatusCode);
        Assert.Equal(0, await factory.Inspect(db => db.Outbox.CountAsync(x => x.AggregateId == id)));
    }

    [Fact]
    public async Task Missing_idempotency_and_unrecognized_tax_inputs_are_rejected()
    {
        var setup = await Setup();
        var url = $"/api/v1/companies/{setup.Company}/sales";
        var response = await setup.Maker.PostAsJsonAsync(url, Draft(setup.Branch));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var invalid = await Post(setup.Maker, url, new
        {
            branchId = setup.Branch, externalReference = "REF", customerReference = "S", businessDate = "2026-10-23",
            lines = new[] { new { productReference = "P", description = "Item", quantity = 1, unitPrice = 10 } },
            vatRate = 15
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(0, await factory.Inspect(db => db.SalesInvoices.CountAsync(x => x.CompanyId == setup.Company)));
    }

    [Fact]
    public async Task Cursor_pages_do_not_repeat_sales_rows()
    {
        var setup = await Setup();
        var url = $"/api/v1/companies/{setup.Company}/sales";
        for (var i = 0; i < 3; i++) await Post(setup.Maker, url, Draft(setup.Branch));
        var first = (await setup.Maker.GetFromJsonAsync<SalesInvoiceResponse[]>($"{url}?take=2"))!;
        var second = (await setup.Maker.GetFromJsonAsync<SalesInvoiceResponse[]>($"{url}?take=2&before={first[^1].Id}"))!;
        Assert.Equal(2, first.Length);
        Assert.Single(second);
        Assert.Empty(first.Select(x => x.Id).Intersect(second.Select(x => x.Id)));
    }
}
