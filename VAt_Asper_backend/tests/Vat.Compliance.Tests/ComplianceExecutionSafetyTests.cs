using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Vat.Application.Security;
using Xunit;

namespace Vat.Compliance.Tests;

public sealed class ComplianceExecutionSafetyTests(ComplianceApiFactory factory) : IClassFixture<ComplianceApiFactory>
{
    [Theory]
    [InlineData("returns/9-1/submit", Permissions.ComplianceSubmit)]
    [InlineData("vds-certificates", Permissions.ComplianceManage)]
    [InlineData("vds-certificates/11111111-1111-1111-1111-111111111111/deposit", Permissions.ComplianceManage)]
    [InlineData("vat-ledger/deposit", Permissions.ComplianceManage)]
    [InlineData("credit-notes", Permissions.ComplianceManage)]
    [InlineData("debit-notes", Permissions.ComplianceManage)]
    public async Task Read_grants_never_authorize_writes_and_authorized_unavailable_work_never_mutates_data(string route, string permission)
    {
        var tenant = Guid.NewGuid(); var company = Guid.NewGuid();
        var path = $"/api/v1/companies/{company}/compliance/{route}";
        var count = await factory.Inspect(db => db.Returns.CountAsync());
        using var reader = factory.Client(tenant, company, permissions: [Permissions.ComplianceRead]);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.PostAsJsonAsync(path, new { })).StatusCode);
        using var writer = factory.Client(tenant, company, permissions: [permission]);
        var unavailable = await writer.PostAsJsonAsync(path, new { });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
        Assert.Contains("compliance_execution_unavailable", await unavailable.Content.ReadAsStringAsync());
        var wrongCompany = await writer.PostAsJsonAsync($"/api/v1/companies/{Guid.NewGuid()}/compliance/{route}", new { });
        Assert.Equal(HttpStatusCode.Forbidden, wrongCompany.StatusCode);
        Assert.Equal(count, await factory.Inspect(db => db.Returns.CountAsync()));
    }
}
