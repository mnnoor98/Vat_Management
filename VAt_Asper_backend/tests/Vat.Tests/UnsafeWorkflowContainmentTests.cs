using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Vat.Application.Security;
using Xunit;

namespace Vat.Tests;

[Collection("PostgreSQL integration")]
public sealed class UnsafeWorkflowContainmentTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    [Fact]
    public async Task Transfer_commands_enforce_auth_scope_and_validation_without_writes()
    {
        var tenant = Guid.NewGuid(); var company = Guid.NewGuid();
        var url = $"/api/v1/companies/{company}/transfers";
        Task<HttpResponseMessage> Send(HttpClient client)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(new { lines = Array.Empty<object>() }) };
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            return client.SendAsync(request);
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send(factory.CreateClient())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(factory.Client(tenant, company, permissions: []))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(factory.Client(tenant, Guid.NewGuid(), permissions: [Permissions.InventoryManage]))).StatusCode);
        var before = await factory.Inspect(db => db.Audit.CountAsync());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Send(factory.Client(tenant, company))).StatusCode);
        Assert.Equal(before, await factory.Inspect(db => db.Audit.CountAsync()));
    }
    [Theory]
    [InlineData("warehouses")]
    [InlineData("inventory/all-balances")]
    [InlineData("transfers")]
    public async Task Read_pages_are_bounded_before_query_execution(string path)
    {
        var tenant = Guid.NewGuid(); var company = Guid.NewGuid(); var client = factory.Client(tenant, company, permissions: [Permissions.InventoryRead]);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.GetAsync($"/api/v1/companies/{company}/{path}?take=101")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.GetAsync($"/api/v1/companies/{company}/{path}?skip=-1")).StatusCode);
    }
}
