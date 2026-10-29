using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Vat.Application;
using Vat.Application.Security;
using Vat.Domain.Company;
using Xunit;

namespace Vat.Tests;

[Collection("PostgreSQL integration")]
public sealed class ModuleApiTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    [Theory]
    [InlineData("customers/00000000-0000-0000-0000-000000000001/statement?from=2026-10-01")]
    [InlineData("customers/00000000-0000-0000-0000-000000000001/statement?until=2026-10-02")]
    [InlineData("customers/00000000-0000-0000-0000-000000000001/ageing")]
    [InlineData("reports/financial-summary?from=2026-10-01")]
    [InlineData("reports/financial-summary?until=2026-10-02")]
    [InlineData("reports/financial-entries?from=2026-10-01")]
    [InlineData("reports/financial-entries?until=2026-10-02")]
    [InlineData("sales/summary?from=2026-10-01")]
    [InlineData("sales/summary?until=2026-10-02")]
    [InlineData("inventory/balances")]
    public async Task Missing_required_query_parameters_preserve_the_400_contract(string path)
    {
        var company = Guid.NewGuid();
        var response = await factory.Client(Guid.NewGuid(), company).GetAsync($"/api/v1/companies/{company}/{path}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("identity/roles", Permissions.IdentityRead)]
    [InlineData("manufacturing/production-orders", Permissions.ManufacturingRead)]
    [InlineData("compliance/returns", Permissions.ComplianceRead)]
    [InlineData("workflow/tasks", Permissions.WorkflowRead)]
    [InlineData("integration/connections", Permissions.IntegrationRead)]
    public async Task Foundation_read_requires_permission_and_company_scope(string path, string permission)
    {
        var tenant = Guid.NewGuid(); var company = Guid.NewGuid();
        var url = $"/api/v1/companies/{company}/{path}";
        var allowed = factory.Client(tenant, company, permissions: [permission]);
        Assert.Equal(HttpStatusCode.OK, (await allowed.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await factory.Client(tenant, company, permissions: []).GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await factory.Client(tenant, Guid.NewGuid(), permissions: [permission]).GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await allowed.GetAsync(url + "?take=101")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task Connection_reads_filter_tenant_company_page_and_never_expose_secret_metadata()
    {
        var tenant = Guid.NewGuid(); var otherTenant = Guid.NewGuid();
        var company = Guid.NewGuid(); var otherCompany = Guid.NewGuid();
        var ids = new[] { Guid.Parse("00000000-0000-0000-0000-000000000001"), Guid.Parse("00000000-0000-0000-0000-000000000002") };
        await factory.Inspect(async db =>
        {
            foreach (var (t, c) in new[] { (tenant, company), (tenant, otherCompany), (otherTenant, company) })
            {
                db.Companies.Add(Company.Create(t, c, "Read test", c.ToString(), DateTimeOffset.UtcNow));
                foreach (var id in ids)
                    db.Set<Dictionary<string, object>>("integration.integration_connections").Add(new()
                    {
                        ["TenantId"] = t, ["CompanyId"] = c, ["Id"] = id, ["CreatedBy"] = "maker",
                        ["Code"] = id.ToString(), ["Provider"] = "TEST", ["Endpoint"] = "https://example.invalid",
                        ["SecretReference"] = "vault://must-not-leak", ["Enabled"] = false
                    });
            }
            await db.SaveChangesAsync(); return 0;
        });
        var client = factory.Client(tenant, company, permissions: [Permissions.IntegrationRead]);
        var url = $"/api/v1/companies/{company}/integration/connections?take=1";
        var firstResponse = await client.GetAsync(url);
        firstResponse.EnsureSuccessStatusCode();
        var body = await firstResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("vault", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("endpoint", body, StringComparison.OrdinalIgnoreCase);
        var first = Assert.Single((await firstResponse.Content.ReadFromJsonAsync<ModuleRecord[]>())!);
        Assert.Equal(ids[1], first.Id);
        var second = Assert.Single((await client.GetFromJsonAsync<ModuleRecord[]>(url + "&before=" + first.Id))!);
        Assert.Equal(ids[0], second.Id);
        Assert.Empty((await client.GetFromJsonAsync<ModuleRecord[]>(url + "&before=" + second.Id))!);
    }

    [Fact]
    public async Task Identity_profile_uses_verified_claims_and_fixed_key_token_endpoint_is_retired()
    {
        var tenant = Guid.NewGuid(); var company = Guid.NewGuid();
        var client = factory.Client(tenant, company, "reader", []);
        var profile = await client.GetAsync("/api/v1/identity/me");
        profile.EnsureSuccessStatusCode();
        Assert.Contains(tenant.ToString(), await profile.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Gone, (await factory.CreateClient().GetAsync("/api/v1/auth/dev-token")).StatusCode);
        Assert.Equal(HttpStatusCode.Gone, (await factory.Client(tenant, company).PostAsync($"/api/v1/companies/{company}/purchases/{Guid.NewGuid()}/post-ledger", null)).StatusCode);
    }
}
