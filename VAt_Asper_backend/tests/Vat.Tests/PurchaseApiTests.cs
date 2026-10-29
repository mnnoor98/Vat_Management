using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Vat.Application.Contracts;
using Vat.Application.Security;
using Vat.Domain.Purchasing;
using Xunit;

namespace Vat.Tests;

[Collection("PostgreSQL integration")]
public sealed class PurchaseApiTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
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
        var branch = await Post(maker, $"/api/v1/companies/{company}/branches", new CreateBranchRequest("Dhaka"));
        Assert.Equal(HttpStatusCode.Created, branch.StatusCode);
        var branchBody = await branch.Content.ReadFromJsonAsync<BranchResponse>();
        return (maker, factory.Client(tenant, company, "checker"), tenant, company, branchBody!.Id);
    }

    private static CreatePurchaseRequest Draft(Guid branch, string? reference = null) => new(branch,
        reference ?? Guid.NewGuid().ToString(), "SUPPLIER-001", new DateOnly(2026, 10, 23),
        [new PurchaseLineInput("ITEM-001", "Purchased materials", 2.5m, 100m)]);

    [Fact]
    public async Task Approved_purchase_commits_one_event_and_audit_with_same_transaction()
    {
        var setup = await Setup();
        var url = $"/api/v1/companies/{setup.Company}/purchases";
        var created = await Post(setup.Maker, url, Draft(setup.Branch));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var purchase = (await created.Content.ReadFromJsonAsync<PurchaseResponse>())!;
        Assert.Equal(250m, purchase.NetAmount);
        Assert.Equal(HttpStatusCode.OK, (await setup.Maker.GetAsync(created.Headers.Location)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Post(setup.Maker, $"{url}/{purchase.Id}/submit", new TransitionRequest(1))).StatusCode);
        var key = Guid.NewGuid().ToString();
        var approved = await Post(setup.Checker, $"{url}/{purchase.Id}/approve", new TransitionRequest(2), key);
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        var result = (await approved.Content.ReadFromJsonAsync<PurchaseResponse>())!;
        Assert.Equal("ApprovedPendingPosting", result.Status);
        Assert.Equal(3, result.Version);
        var replay = await Post(setup.Checker, $"{url}/{purchase.Id}/approve", new TransitionRequest(2), key);
        Assert.Equal(await approved.Content.ReadAsStringAsync(), await replay.Content.ReadAsStringAsync());
        Assert.Equal("true", replay.Headers.GetValues("Idempotency-Replayed").Single());
        Assert.Equal(1, await factory.Inspect(db => db.Outbox.CountAsync(x => x.AggregateId == purchase.Id)));
        Assert.Equal(3, await factory.Inspect(db => db.Audit.CountAsync(x => x.ResourceId == purchase.Id)));
        var payload = await factory.Inspect(db => db.Outbox.Where(x => x.AggregateId == purchase.Id).Select(x => x.Payload).SingleAsync());
        using var envelope = JsonDocument.Parse(payload);
        Assert.Equal(setup.Tenant, envelope.RootElement.GetProperty("tenantId").GetGuid());
        Assert.Equal("PurchaseApprovedV1", envelope.RootElement.GetProperty("eventType").GetString());
    }

    [Fact]
    public async Task Concurrent_same_key_returns_identical_resource_and_payload_change_conflicts()
    {
        var setup = await Setup();
        var url = $"/api/v1/companies/{setup.Company}/purchases";
        var key = Guid.NewGuid().ToString();
        var draft = Draft(setup.Branch);
        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Post(setup.Maker, url, draft, key)));
        Assert.All(results, response => Assert.Equal(HttpStatusCode.Created, response.StatusCode));
        var bodies = await Task.WhenAll(results.Select(x => x.Content.ReadAsStringAsync()));
        Assert.Single(bodies.Distinct());
        Assert.Equal(1, await factory.Inspect(db => db.Purchases.CountAsync(x => x.CompanyId == setup.Company)));
        var changed = await Post(setup.Maker, url, draft with { SupplierReference = "OTHER" }, key);
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        var anotherActor = await Post(setup.Checker, url, draft, key);
        Assert.Equal(HttpStatusCode.Conflict, anotherActor.StatusCode);
    }

    [Fact]
    public async Task Self_approval_and_stale_version_leave_no_approval_event()
    {
        var setup = await Setup();
        var url = $"/api/v1/companies/{setup.Company}/purchases";
        var created = await Post(setup.Maker, url, Draft(setup.Branch));
        var id = (await created.Content.ReadFromJsonAsync<PurchaseResponse>())!.Id;
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
        var url = $"/api/v1/companies/{setup.Company}/purchases";
        var created = await Post(setup.Maker, url, Draft(setup.Branch));
        var id = (await created.Content.ReadFromJsonAsync<PurchaseResponse>())!.Id;
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
        var url = $"/api/v1/companies/{setup.Company}/purchases";
        var created = await Post(setup.Maker, url, Draft(setup.Branch));
        var id = (await created.Content.ReadFromJsonAsync<PurchaseResponse>())!.Id;
        using var wrongCompany = factory.Client(setup.Tenant, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Forbidden, (await wrongCompany.GetAsync($"{url}/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(wrongCompany, url, Draft(setup.Branch))).StatusCode);
        using var wrongTenant = factory.Client(Guid.NewGuid(), setup.Company);
        Assert.Equal(HttpStatusCode.NotFound, (await wrongTenant.GetAsync($"{url}/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Post(wrongTenant, url, Draft(setup.Branch))).StatusCode);
        var listed = await wrongTenant.GetFromJsonAsync<PurchaseResponse[]>(url);
        Assert.Empty(listed!);
    }

    [Fact]
    public async Task Duplicate_external_reference_rolls_back_audit_and_idempotency()
    {
        var setup = await Setup();
        var url = $"/api/v1/companies/{setup.Company}/purchases";
        var draft = Draft(setup.Branch);
        Assert.Equal(HttpStatusCode.Created, (await Post(setup.Maker, url, draft)).StatusCode);
        var auditBefore = await factory.Inspect(db => db.Audit.CountAsync(x => x.CompanyId == setup.Company));
        var keysBefore = await factory.Inspect(db => db.Idempotency.CountAsync(x => x.CompanyId == setup.Company));
        Assert.Equal(HttpStatusCode.Conflict, (await Post(setup.Maker, url, draft)).StatusCode);
        Assert.Equal(auditBefore, await factory.Inspect(db => db.Audit.CountAsync(x => x.CompanyId == setup.Company)));
        Assert.Equal(keysBefore, await factory.Inspect(db => db.Idempotency.CountAsync(x => x.CompanyId == setup.Company)));
    }

    [Fact]
    public async Task Real_bearer_validation_rejects_wrong_issuer_audience_and_expired_token()
    {
        var tenant = Guid.NewGuid();
        var company = Guid.NewGuid();
        var path = $"/api/v1/companies/{company}";
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(path)).StatusCode);
        using var wrongIssuer = factory.Client(tenant, company, issuer: "https://wrong.example");
        using var wrongAudience = factory.Client(tenant, company, audience: "other-api");
        using var expired = factory.Client(tenant, company, expires: DateTime.UtcNow.AddHours(-1));
        Assert.Equal(HttpStatusCode.Unauthorized, (await wrongIssuer.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await wrongAudience.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await expired.GetAsync(path)).StatusCode);
        using var untrustedRsa = RSA.Create(2048);
        using var forged = factory.Client(tenant, company,
            signingKey: new RsaSecurityKey(untrustedRsa) { KeyId = "integration-test-key" });
        Assert.Equal(HttpStatusCode.Unauthorized, (await forged.GetAsync(path)).StatusCode);
        using var noPermission = factory.Client(tenant, company, permissions: []);
        Assert.Equal(HttpStatusCode.Forbidden, (await noPermission.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task Foreign_company_branch_cannot_be_used_and_rejection_is_terminal()
    {
        var setup = await Setup();
        var other = await Setup();
        var url = $"/api/v1/companies/{setup.Company}/purchases";
        Assert.Equal(HttpStatusCode.NotFound, (await Post(setup.Maker, url, Draft(other.Branch))).StatusCode);
        var created = await Post(setup.Maker, url, Draft(setup.Branch));
        var id = (await created.Content.ReadFromJsonAsync<PurchaseResponse>())!.Id;
        await Post(setup.Maker, $"{url}/{id}/submit", new TransitionRequest(1));
        var rejected = await Post(setup.Checker, $"{url}/{id}/reject", new RejectPurchaseRequest(2, "Missing evidence"));
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        Assert.Equal("Rejected", (await rejected.Content.ReadFromJsonAsync<PurchaseResponse>())!.Status);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(setup.Checker, $"{url}/{id}/approve", new TransitionRequest(3))).StatusCode);
        Assert.Equal(0, await factory.Inspect(db => db.Outbox.CountAsync(x => x.AggregateId == id)));
    }

    [Fact]
    public async Task Missing_idempotency_and_unrecognized_tax_inputs_are_rejected()
    {
        var setup = await Setup();
        var url = $"/api/v1/companies/{setup.Company}/purchases";
        var response = await setup.Maker.PostAsJsonAsync(url, Draft(setup.Branch));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var invalid = await Post(setup.Maker, url, new
        {
            branchId = setup.Branch, externalReference = "REF", supplierReference = "S", businessDate = "2026-10-23",
            lines = new[] { new { productReference = "P", description = "Item", quantity = 1, unitPrice = 10 } },
            vatRate = 15
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(0, await factory.Inspect(db => db.Purchases.CountAsync(x => x.CompanyId == setup.Company)));
    }

    [Fact]
    public async Task Readiness_and_openapi_are_available_and_cursor_pages_do_not_repeat_rows()
    {
        Assert.Equal(HttpStatusCode.OK, (await factory.CreateClient().GetAsync("/health/ready")).StatusCode);
        var document = await factory.CreateClient().GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, document.StatusCode);
        using var schema = JsonDocument.Parse(await document.Content.ReadAsStringAsync());
        Assert.True(schema.RootElement.GetProperty("paths").EnumerateObject().Any());
        var setup = await Setup();
        var url = $"/api/v1/companies/{setup.Company}/purchases";
        for (var i = 0; i < 3; i++) await Post(setup.Maker, url, Draft(setup.Branch));
        var first = (await setup.Maker.GetFromJsonAsync<PurchaseResponse[]>($"{url}?take=2"))!;
        var second = (await setup.Maker.GetFromJsonAsync<PurchaseResponse[]>($"{url}?take=2&before={first[^1].Id}"))!;
        Assert.Equal(2, first.Length);
        Assert.Single(second);
        Assert.Empty(first.Select(x => x.Id).Intersect(second.Select(x => x.Id)));
    }
}
