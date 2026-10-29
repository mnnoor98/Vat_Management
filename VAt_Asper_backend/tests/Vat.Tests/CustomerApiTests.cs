using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Vat.Application;
using Vat.Application.Contracts;
using Vat.Application.Security;
using Vat.Domain.Sales;
using Xunit;

namespace Vat.Tests;

[Collection("PostgreSQL integration")]
public sealed class CustomerApiTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
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


    private static async Task<CustomerResponse> Customer(HttpClient client, string root, string code = "cus-001")
    {
        var result = await Post(client, root + "/customers", new CreateCustomerRequest(code, "Original name", "01700000000", "Dhaka", "REG-001"));
        Assert.Equal(HttpStatusCode.Created, result.StatusCode);
        return (await result.Content.ReadFromJsonAsync<CustomerResponse>())!;
    }
    [Fact]
    public async Task Invoice_freezes_customer_details_and_legacy_invoice_stays_unlinked()
    {
        var s = await Setup(); var root = $"/api/v1/companies/{s.Company}";
        var c = await Customer(s.Maker, root);
        Assert.Equal("CUS-001", c.Code);
        var draft = Draft(s.Branch) with { CustomerId = c.Id, CustomerReference = "cus-001" };
        var response = await Post(s.Maker, root + "/sales", draft);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var invoice = (await response.Content.ReadFromJsonAsync<SalesInvoiceResponse>())!;
        Assert.Equal(c.Id, invoice.CustomerId); Assert.Equal("Original name", invoice.CustomerSnapshot!.Name);
        var changed = await Post(s.Maker, root + $"/customers/{c.Id}/update", new UpdateCustomerRequest(1, "Changed name", null, "Other address", null, false));
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        var read = (await s.Maker.GetFromJsonAsync<SalesInvoiceResponse>(root + $"/sales/{invoice.Id}"))!;
        Assert.Equal(invoice.CustomerSnapshot, read.CustomerSnapshot); Assert.Equal("CUS-001", read.CustomerReference);
        Assert.Equal(HttpStatusCode.OK, (await Post(s.Maker, root + $"/sales/{invoice.Id}/submit", new TransitionRequest(1))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Post(s.Checker, root + $"/sales/{invoice.Id}/approve", new TransitionRequest(2))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(s.Maker, root + "/sales", draft with { ExternalReference = Guid.NewGuid().ToString() })).StatusCode);
        var legacy = await Post(s.Maker, root + "/sales", Draft(s.Branch));
        Assert.Equal(HttpStatusCode.Created, legacy.StatusCode);
        Assert.Null((await legacy.Content.ReadFromJsonAsync<SalesInvoiceResponse>())!.CustomerSnapshot);
    }
    [Fact]
    public async Task Scope_permissions_and_selected_customer_reference_are_enforced()
    {
        var s = await Setup(); var root = $"/api/v1/companies/{s.Company}"; var c = await Customer(s.Maker, root);
        using var wrongTenant = factory.Client(Guid.NewGuid(), s.Company);
        using var wrongCompany = factory.Client(s.Tenant, Guid.NewGuid());
        using var reader = factory.Client(s.Tenant, s.Company, permissions: [Permissions.MasterDataRead]);
        using var salesOnly = factory.Client(s.Tenant, s.Company, permissions: [Permissions.SalesInvoiceCreate]);
        Assert.Equal(HttpStatusCode.NotFound, (await wrongTenant.GetAsync(root + $"/customers/{c.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await wrongCompany.GetAsync(root + $"/customers/{c.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await reader.GetAsync(root + $"/customers/{c.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(reader, root + "/customers", new CreateCustomerRequest("X", "X"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await salesOnly.GetAsync(root + "/customers")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Post(s.Maker, root + "/sales", Draft(s.Branch) with { CustomerId = c.Id })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Post(s.Maker, root + "/sales", Draft(s.Branch) with { CustomerId = Guid.NewGuid() })).StatusCode);
        var other = await Setup();
        Assert.Equal(HttpStatusCode.NotFound, (await Post(other.Maker, $"/api/v1/companies/{other.Company}/sales", Draft(other.Branch) with { CustomerId = c.Id, CustomerReference = c.Code })).StatusCode);
    }
    [Fact]
    public async Task Customer_creation_is_idempotent_and_code_unique_per_company()
    {
        var s = await Setup(); var root = $"/api/v1/companies/{s.Company}"; var key = Guid.NewGuid().ToString();
        var body = new CreateCustomerRequest(" cus-001 ", "Customer");
        var results = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => Post(s.Maker, root + "/customers", body, key)));
        Assert.All(results, x => Assert.Equal(HttpStatusCode.Created, x.StatusCode));
        var ids = await Task.WhenAll(results.Select(async x => (await x.Content.ReadFromJsonAsync<CustomerResponse>())!.Id));
        Assert.Single(ids.Distinct());
        Assert.Equal(HttpStatusCode.Conflict, (await Post(s.Maker, root + "/customers", body with { Code = "CUS-001" })).StatusCode);
        Assert.Equal(1, await factory.Inspect(db => db.Set<Customer>().CountAsync(x => x.CompanyId == s.Company)));
        Assert.Equal(1, await factory.Inspect(db => db.Audit.CountAsync(x => x.CompanyId == s.Company && x.Action == "CustomerCreated")));
        var other = await Setup(); await Customer(other.Maker, $"/api/v1/companies/{other.Company}");
    }
    [Fact]
    public async Task Concurrent_updates_have_one_winner_and_retries_replay_the_committed_result()
    {
        var s = await Setup(); var root = $"/api/v1/companies/{s.Company}"; var c = await Customer(s.Maker, root);
        var url = root + $"/customers/{c.Id}/update";
        var changes = new UpdateCustomerRequest(1, "New name", "", "", "", true);
        var results = await Task.WhenAll(Post(s.Maker, url, changes), Post(s.Maker, url, changes with { Name = "Other name" }));
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.OK); Assert.Single(results, x => x.StatusCode == HttpStatusCode.Conflict);
        var key = Guid.NewGuid().ToString(); var next = changes with { ExpectedVersion = 2, IsActive = false };
        var first = await Post(s.Maker, url, next, key); var retry = await Post(s.Maker, url, next, key);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode); Assert.Equal(await first.Content.ReadAsStringAsync(), await retry.Content.ReadAsStringAsync());
        Assert.Equal(3, (await s.Maker.GetFromJsonAsync<CustomerResponse>(root + $"/customers/{c.Id}"))!.Version);
        Assert.Equal(2, await factory.Inspect(db => db.Audit.CountAsync(x => x.CompanyId == s.Company && x.Action == "CustomerUpdated")));
    }
    [Fact]
    public async Task Invalid_fields_roll_back_and_pagination_is_scoped_and_bounded()
    {
        var s = await Setup(); var root = $"/api/v1/companies/{s.Company}";
        foreach (var body in new[] { new CreateCustomerRequest("", "Name"), new CreateCustomerRequest("C", " "), new CreateCustomerRequest("C", "Name", Address: new string('x', 1001)) })
            Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Post(s.Maker, root + "/customers", body)).StatusCode);
        Assert.Equal(0, await factory.Inspect(db => db.Set<Customer>().CountAsync(x => x.CompanyId == s.Company)));
        for (var i = 0; i < 3; i++) await Customer(s.Maker, root, $"CUS-{i}");
        var page = (await s.Maker.GetFromJsonAsync<CustomerResponse[]>(root + "/customers?take=2"))!;
        Assert.Equal(2, page.Length);
        var rest = (await s.Maker.GetFromJsonAsync<CustomerResponse[]>(root + $"/customers?take=2&before={page.Last().Id}"))!;
        Assert.Single(rest); Assert.DoesNotContain(rest[0].Id, page.Select(x => x.Id));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await s.Maker.GetAsync(root + "/customers?take=101")).StatusCode);
    }
}
