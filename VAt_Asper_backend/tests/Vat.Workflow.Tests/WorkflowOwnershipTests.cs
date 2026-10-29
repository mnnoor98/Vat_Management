using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Vat.Application;
using Vat.Application.Security;
using Vat.Services.Workflow;
using Vat.Services.Workflow.Domain;
using Vat.Services.Workflow.Infrastructure;
using Xunit;

namespace Vat.Workflow.Tests;

public sealed class WorkflowOwnershipTests(WorkflowApiFactory factory) : IClassFixture<WorkflowApiFactory>
{
    private static async Task<Guid> Seed(WorkflowDbContext db, Guid tenant, Guid company, string assigned = "maker")
    {
        var definition = Guid.NewGuid(); var version = Guid.NewGuid(); var instance = Guid.NewGuid(); var task = Guid.NewGuid();
        db.Add(new WorkflowDefinition { TenantId = tenant, CompanyId = company, Id = definition, Code = definition.ToString(), Name = "Approval", CreatedBy = "maker" });
        db.Add(new WorkflowVersion { TenantId = tenant, CompanyId = company, Id = version, DefinitionId = definition, Revision = 1, State = "Approved", Definition = "{}", EffectiveFrom = new DateOnly(2026, 1, 1), ApprovedBy = "checker", CreatedBy = "maker" });
        db.Add(new ApprovalInstance { TenantId = tenant, CompanyId = company, Id = instance, WorkflowVersionId = version, SourceType = "Purchase", SourceId = Guid.NewGuid(), SourceVersion = 1, RequestedBy = "maker", State = "Pending", CreatedBy = "maker" });
        db.Add(new ApprovalTask { TenantId = tenant, CompanyId = company, Id = task, InstanceId = instance, AssignedTo = assigned, StepNumber = 1, State = "Pending", CreatedBy = "maker" });
        await db.SaveChangesAsync(); return task;
    }

    [Fact]
    public async Task Standalone_reads_enforce_scope_permission_cursor_and_limits()
    {
        var tenant = Guid.NewGuid(); var company = Guid.NewGuid();
        await factory.Inspect(async db => { await Seed(db, tenant, company); await Seed(db, tenant, company); await Seed(db, Guid.NewGuid(), company); await Seed(db, tenant, Guid.NewGuid()); return 0; });
        using var client = factory.Client(tenant, company);
        var path = $"/api/v1/companies/{company}/workflow/tasks";
        var rows = await client.GetFromJsonAsync<ModuleRecord[]>(path);
        Assert.Equal(2, rows!.Length); Assert.All(rows, x => Assert.Equal("Pending", x.State));
        var first = (await client.GetFromJsonAsync<ModuleRecord[]>(path + "?take=1"))!.Single();
        var next = (await client.GetFromJsonAsync<ModuleRecord[]>(path + $"?before={first.Id}&take=1"))!.Single();
        Assert.NotEqual(first.Id, next.Id);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.GetAsync(path + "?take=0")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.GetAsync(path + "?take=101")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/companies/{Guid.NewGuid()}/workflow/tasks")).StatusCode);
        using var denied = factory.Client(tenant, company, permissions: []);
        Assert.Equal(HttpStatusCode.Forbidden, (await denied.GetAsync(path)).StatusCode);
        using var anonymous = factory.CreateClient(); Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
        Assert.Equal(HttpStatusCode.Gone, (await client.PostAsync($"/api/v1/companies/{company}/workflows/tasks/{Guid.NewGuid()}/approve", null)).StatusCode);
    }

    [Fact]
    public async Task Own_model_has_seven_tables_no_financial_dependencies_and_scoped_foreign_keys()
    {
        await factory.Inspect(async db =>
        {
            Assert.False(db.Database.HasPendingModelChanges());
            Assert.Equal(7, db.Model.GetEntityTypes().Count());
            Assert.All(db.Model.GetEntityTypes(), e => { Assert.Equal("workflow", e.GetSchema()); Assert.All(e.GetForeignKeys(), fk => { Assert.Equal("workflow", fk.PrincipalEntityType.GetSchema()); Assert.Equal(new[] { "TenantId", "CompanyId" }, fk.Properties.Take(2).Select(p => p.Name)); }); });
            await using var connection = new NpgsqlConnection(db.Database.GetConnectionString()); await connection.OpenAsync();
            await using var query = new NpgsqlCommand("SELECT count(*) FROM information_schema.tables WHERE table_schema NOT IN ('pg_catalog','information_schema','workflow') AND table_type='BASE TABLE'", connection);
            Assert.Equal(0L, (long)(await query.ExecuteScalarAsync())!);
            return 0;
        });
        var forbidden = new[] { "Vat.Hosting", "Vat.Infrastructure", "Vat.Application", "Vat.Domain", "Vat.Identity.Infrastructure" };
        foreach (var assembly in new[] { typeof(Program).Assembly, typeof(WorkflowDbContext).Assembly, typeof(WorkflowReadService).Assembly, typeof(ApprovalTask).Assembly })
            Assert.DoesNotContain(assembly.GetReferencedAssemblies(), a => forbidden.Contains(a.Name));
    }

    [Fact]
    public async Task Database_rejects_cross_tenant_task_and_invalid_decision()
    {
        var tenant = Guid.NewGuid(); var company = Guid.NewGuid();
        var task = await factory.Inspect(db => Seed(db, tenant, company));
        await factory.Inspect(async db =>
        {
            var instance = await db.Tasks.Where(x => x.Id == task).Select(x => x.InstanceId).SingleAsync();
            db.Add(new ApprovalTask { TenantId = Guid.NewGuid(), CompanyId = company, Id = Guid.NewGuid(), InstanceId = instance, AssignedTo = "other", StepNumber = 2, State = "Pending", CreatedBy = "maker" });
            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, Assert.IsType<PostgresException>(ex.InnerException).SqlState); return 0;
        });
        await factory.Inspect(async db =>
        {
            db.Add(new ApprovalAction { TenantId = tenant, CompanyId = company, Id = Guid.NewGuid(), TaskId = task, Actor = "checker", Decision = "Posted", Reason = "invalid", OccurredAt = DateTimeOffset.UtcNow, IdempotencyKey = Guid.NewGuid().ToString(), CreatedBy = "checker" });
            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); Assert.Equal(PostgresErrorCodes.CheckViolation, Assert.IsType<PostgresException>(ex.InnerException).SqlState); return 0;
        });
    }

    [Fact]
    public async Task Transfer_copies_nonempty_graph_and_preserves_divergent_target()
    {
        var tenant = Guid.NewGuid(); var company = Guid.NewGuid(); const string text = "বাংলা | quoted \"name\"\nnext line";
        var source = await factory.Inspect(async db => { await Seed(db, tenant, company, text); return db.Database.GetConnectionString()!; });
        var adminConnection = Environment.GetEnvironmentVariable("VAT_TEST_POSTGRES")!;
        var targetName = "workflow_copy_" + Guid.NewGuid().ToString("N");
        var settings = new NpgsqlConnectionStringBuilder(adminConnection) { Database = targetName, Pooling = false };
        await using var admin = new NpgsqlConnection(adminConnection); await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{targetName}\"", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            var report = await WorkflowDataTransfer.Transfer(source, settings.ConnectionString); Assert.False(report.AlreadyMatched); Assert.Equal(7, report.Tables.Count);
            Assert.True((await WorkflowDataTransfer.Transfer(source, settings.ConnectionString)).AlreadyMatched);
            var options = new DbContextOptionsBuilder<WorkflowDbContext>(); WorkflowPersistence.Configure(options, settings.ConnectionString);
            await using (var target = new WorkflowDbContext(options.Options))
            {
                var task = await target.Tasks.SingleAsync(x => x.TenantId == tenant && x.CompanyId == company); Assert.Equal(text, task.AssignedTo); task.AssignedTo = "independent"; await target.SaveChangesAsync();
            }
            await Assert.ThrowsAsync<InvalidOperationException>(() => WorkflowDataTransfer.Transfer(source, settings.ConnectionString));
            await using (var target = new WorkflowDbContext(options.Options)) Assert.Equal("independent", await target.Tasks.Where(x => x.TenantId == tenant && x.CompanyId == company).Select(x => x.AssignedTo).SingleAsync());
            Assert.Equal(text, await factory.Inspect(db => db.Tasks.Where(x => x.TenantId == tenant && x.CompanyId == company).Select(x => x.AssignedTo).SingleAsync()));
        }
        finally
        {
            await using var sessions = new NpgsqlCommand("SELECT count(*) FROM pg_stat_activity WHERE datname=@db", admin); sessions.Parameters.AddWithValue("db", targetName); Assert.Equal(0L, (long)(await sessions.ExecuteScalarAsync())!);
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{targetName}\"", admin) { CommandTimeout = 60 }; await drop.ExecuteNonQueryAsync();
        }
    }
}
