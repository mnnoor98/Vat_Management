using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Vat.Application.Security;
using Vat.Services.Manufacturing;
using Vat.Services.Manufacturing.Domain;
using Xunit;

namespace Vat.Manufacturing.Tests;

public sealed class CoefficientWorkflowTests(ManufacturingApiFactory factory) : IClassFixture<ManufacturingApiFactory>
{
    private (Guid Tenant, Guid Company, CoefficientCreateRequest Request) Setup()
    {
        var tenant = Guid.NewGuid(); var company = Guid.NewGuid(); var finished = Guid.NewGuid(); var material = Guid.NewGuid();
        factory.References.Products[(tenant, company, finished)] = new(finished, "FINISHED", new string('P', 500), "KG", "TEST");
        factory.References.Products[(tenant, company, material)] = new(material, "MATERIAL", "Verified material", "KG", "TEST");
        return (tenant, company, new("DECLARATION", finished, 10, new(2026, 10, 1), new(2026, 10, 31), 10, 20, 30, [new(material, 20, 5, 2)]));
    }
    private static Task<HttpResponseMessage> Send(HttpClient client, string path, object body, Guid? key = null, bool approve = false)
    {
        var message = new HttpRequestMessage(approve ? HttpMethod.Put : HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        message.Headers.Add("Idempotency-Key", (key ?? Guid.NewGuid()).ToString());
        return client.SendAsync(message);
    }
    [Fact]
    public async Task Owned_create_submit_approve_is_idempotent_scoped_calculated_and_audited()
    {
        var scope = Setup(); var root = $"/api/v1/companies/{scope.Company}/manufacturing/coefficients";
        using var maker = factory.Client(scope.Tenant, scope.Company); using var checker = factory.Client(scope.Tenant, scope.Company, "checker");
        var key = Guid.NewGuid(); var created = await Send(maker, root, scope.Request, key); Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var value = (await created.Content.ReadFromJsonAsync<CoefficientResult>())!;
        Assert.Equal("Draft", value.State); Assert.Equal(102m, value.Snapshot!.TotalCost); Assert.Equal(10.2m, value.Snapshot.UnitCost);
        Assert.Equal(500, value.Snapshot.FinishedProduct.Description.Length);
        var replay = await Send(maker, root, scope.Request, key); Assert.Equal("true", replay.Headers.GetValues("Idempotency-Replayed").Single());
        Assert.Equal(HttpStatusCode.Conflict, (await Send(maker, root, scope.Request with { BatchSize = 11 }, key)).StatusCode);
        var path = root + $"/{value.Id}";
        Assert.Equal(HttpStatusCode.Conflict, (await Send(checker, path + "/approve", new CoefficientActionRequest(1), approve: true)).StatusCode);
        (await Send(maker, path + "/submit", new CoefficientActionRequest(1))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await Send(maker, path + "/approve", new CoefficientActionRequest(2), approve: true)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Send(checker, path + "/approve", new CoefficientActionRequest(1), approve: true)).StatusCode);
        var approvalKey = Guid.NewGuid(); (await Send(checker, path + "/approve", new CoefficientActionRequest(2), approvalKey, true)).EnsureSuccessStatusCode();
        (await Send(checker, path + "/approve", new CoefficientActionRequest(2), approvalKey, true)).EnsureSuccessStatusCode();
        var result = (await maker.GetFromJsonAsync<CoefficientResult[]>(root))!.Single(); Assert.Equal(3, result.Version); Assert.Equal("checker", result.ApprovedBy);
        Assert.Empty((await factory.Client(Guid.NewGuid(), scope.Company).GetFromJsonAsync<CoefficientResult[]>(root))!);
        await factory.Inspect(async db =>
        {
            Assert.Equal(3, await db.Set<CoefficientAudit>().CountAsync(x => x.TenantId == scope.Tenant));
            Assert.Equal(3, await db.Set<CoefficientCommand>().CountAsync(x => x.TenantId == scope.Tenant));
            Assert.Single(await db.Set<CoefficientDocument>().Where(x => x.TenantId == scope.Tenant).ToArrayAsync()); return 0;
        });
    }
    [Fact]
    public async Task Missing_references_permissions_and_invalid_inputs_write_nothing()
    {
        var scope = Setup(); var root = $"/api/v1/companies/{scope.Company}/manufacturing/coefficients";
        using var maker = factory.Client(scope.Tenant, scope.Company);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send(factory.CreateClient(), root, scope.Request)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(factory.Client(scope.Tenant, scope.Company, permissions: []), root, scope.Request)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(factory.Client(scope.Tenant, Guid.NewGuid()), root, scope.Request)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Send(maker, root, scope.Request with { BatchSize = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Send(maker, root, scope.Request with { FinishedProductId = Guid.NewGuid() })).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await maker.GetAsync(root + "?take=101")).StatusCode);
        await factory.Inspect(async db => { Assert.False(await db.Set<CoefficientCommand>().AnyAsync(x => x.TenantId == scope.Tenant)); Assert.False(await db.Set<BomHeader>().AnyAsync(x => x.TenantId == scope.Tenant)); return 0; });
    }
    [Fact]
    public async Task Revisions_are_monotonic_and_overlapping_approvals_are_rejected()
    {
        var scope = Setup(); var root = $"/api/v1/companies/{scope.Company}/manufacturing/coefficients";
        using var maker = factory.Client(scope.Tenant, scope.Company); using var checker = factory.Client(scope.Tenant, scope.Company, "checker");
        for (var revision = 1; revision <= 2; revision++)
        {
            var created = await Send(maker, root, scope.Request); created.EnsureSuccessStatusCode();
            var value = (await created.Content.ReadFromJsonAsync<CoefficientResult>())!; Assert.Equal(revision, value.Revision);
            var path = root + $"/{value.Id}"; (await Send(maker, path + "/submit", new CoefficientActionRequest(1))).EnsureSuccessStatusCode();
            var approved = await Send(checker, path + "/approve", new CoefficientActionRequest(2), approve: true);
            Assert.Equal(revision == 1 ? HttpStatusCode.OK : HttpStatusCode.Conflict, approved.StatusCode);
        }
    }
    [Fact]
    public async Task Changed_snapshot_refuses_submission_without_audit_or_command_writes()
    {
        var scope=Setup(); var root=$"/api/v1/companies/{scope.Company}/manufacturing/coefficients"; using var maker=factory.Client(scope.Tenant,scope.Company);
        var created=await Send(maker,root,scope.Request); created.EnsureSuccessStatusCode();
        var value=(await created.Content.ReadFromJsonAsync<CoefficientResult>())!;
        await factory.Inspect(async db=> { await db.Set<CoefficientDocument>().Where(x=>x.TenantId==scope.Tenant&&x.CompanyId==scope.Company&&x.Id==value.Id).ExecuteUpdateAsync(x=>x.SetProperty(d=>d.SnapshotJson,"{}")); return 0; });
        Assert.Equal(HttpStatusCode.Conflict,(await Send(maker,root+$"/{value.Id}/submit",new CoefficientActionRequest(1))).StatusCode);
        await factory.Inspect(async db=>
        {
            Assert.Equal(1,await db.Set<CoefficientCommand>().CountAsync(x=>x.TenantId==scope.Tenant));
            Assert.Equal(1,await db.Set<CoefficientAudit>().CountAsync(x=>x.TenantId==scope.Tenant));
            Assert.Equal("Draft",(await db.Set<CoefficientVersion>().FindAsync(scope.Tenant,scope.Company,value.Id))!.State); return 0;
        });
    }
}
