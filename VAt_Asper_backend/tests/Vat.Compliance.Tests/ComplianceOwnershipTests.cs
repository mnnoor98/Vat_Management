using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Vat.Application;
using Vat.Application.Security;
using Vat.Services.Compliance;
using Vat.Services.Compliance.Domain;
using Vat.Services.Compliance.Infrastructure;
using Xunit;

namespace Vat.Compliance.Tests;

public sealed class ComplianceOwnershipTests(ComplianceApiFactory factory) : IClassFixture<ComplianceApiFactory>
{
    private static async Task<Guid> Seed(ComplianceDbContext db, Guid tenant, Guid company, string evidence = "test filing")
    {
        var template = Guid.NewGuid(); var form = Guid.NewGuid(); var period = Guid.NewGuid(); var vatReturn = Guid.NewGuid(); var fiscal = Guid.NewGuid();
        db.Add(new FormTemplate { TenantId = tenant, CompanyId = company, Id = template, Code = template.ToString(), Name = "Form", CreatedBy = "maker" });
        db.Add(new FormVersion { TenantId = tenant, CompanyId = company, Id = form, TemplateId = template, Revision = 1, EffectiveFrom = new DateOnly(2026, 1, 1), Mapping = "{}", ApprovedBy = "checker", CreatedBy = "maker" });
        db.Add(new ReturnPeriod { TenantId = tenant, CompanyId = company, Id = period, FiscalPeriodId = fiscal, Code = period.ToString(), From = new DateOnly(2026, 1, 1), Until = new DateOnly(2026, 1, 31), CreatedBy = "maker" });
        db.Add(new VatReturn { TenantId = tenant, CompanyId = company, Id = vatReturn, ReturnPeriodId = period, FormVersionId = form, Revision = 1, State = "Pending", CreatedBy = "maker" });
        db.Add(new ReturnLine { TenantId = tenant, CompanyId = company, Id = Guid.NewGuid(), ReturnId = vatReturn, LineCode = "L1", Amount = 1234567890.123456m, SourceSnapshot = "{\"source\":\"frozen\"}", CreatedBy = "maker" });
        db.Add(new ReconciliationRun { TenantId = tenant, CompanyId = company, Id = Guid.NewGuid(), ReturnId = vatReturn, StartedAt = DateTimeOffset.UtcNow, State = "Completed", Differences = "[]", CreatedBy = "maker" });
        db.Add(new FilingEvidence { TenantId = tenant, CompanyId = company, Id = Guid.NewGuid(), ReturnId = vatReturn, ObjectKey = "private://evidence", Sha256 = new string('a', 64), FilingReference = evidence, FiledAt = DateTimeOffset.UtcNow, CreatedBy = "maker" });
        db.Add(new PeriodCloseRequest { TenantId = tenant, CompanyId = company, Id = Guid.NewGuid(), FiscalPeriodId = fiscal, ReturnId = vatReturn, State = "Pending", RequestedBy = "maker", CreatedBy = "maker" });
        await db.SaveChangesAsync(); return vatReturn;
    }

    [Fact]
    public async Task Standalone_reads_enforce_scope_permission_cursor_and_limits()
    {
        var tenant = Guid.NewGuid(); var company = Guid.NewGuid();
        await factory.Inspect(async db => { await Seed(db, tenant, company); await Seed(db, tenant, company); await Seed(db, Guid.NewGuid(), company); await Seed(db, tenant, Guid.NewGuid()); return 0; });
        using var client = factory.Client(tenant, company);
        var path = $"/api/v1/companies/{company}/compliance/returns";
        var rows = await client.GetFromJsonAsync<ModuleRecord[]>(path);
        Assert.Equal(2, rows!.Length); Assert.All(rows, x => { Assert.Equal("Pending", x.State); Assert.Null(x.Reference); });
        var body = await client.GetStringAsync(path); Assert.DoesNotContain("private://", body); Assert.DoesNotContain("SourceSnapshot", body, StringComparison.OrdinalIgnoreCase);
        var first = (await client.GetFromJsonAsync<ModuleRecord[]>(path + "?take=1"))!.Single();
        var next = (await client.GetFromJsonAsync<ModuleRecord[]>(path + $"?before={first.Id}&take=1"))!.Single();
        Assert.NotEqual(first.Id, next.Id);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.GetAsync(path + "?take=0")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.GetAsync(path + "?take=101")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/companies/{Guid.NewGuid()}/compliance/returns")).StatusCode);
        using var denied = factory.Client(tenant, company, permissions: []);
        Assert.Equal(HttpStatusCode.Forbidden, (await denied.GetAsync(path)).StatusCode);
        using var anonymous = factory.CreateClient(); Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
    }

    [Fact]
    public async Task Own_model_has_eight_tables_no_financial_dependencies_and_scoped_foreign_keys()
    {
        await factory.Inspect(async db =>
        {
            Assert.False(db.Database.HasPendingModelChanges());
            Assert.Equal(8, db.Model.GetEntityTypes().Count());
            Assert.All(db.Model.GetEntityTypes(), e => { Assert.Equal("compliance", e.GetSchema()); Assert.All(e.GetForeignKeys(), fk => { Assert.Equal("compliance", fk.PrincipalEntityType.GetSchema()); Assert.Equal(new[] { "TenantId", "CompanyId" }, fk.Properties.Take(2).Select(p => p.Name)); }); });
            await using var connection = new NpgsqlConnection(db.Database.GetConnectionString()); await connection.OpenAsync();
            await using var query = new NpgsqlCommand("SELECT count(*) FROM information_schema.tables WHERE table_schema NOT IN ('pg_catalog','information_schema','compliance') AND table_type='BASE TABLE'", connection);
            Assert.Equal(0L, (long)(await query.ExecuteScalarAsync())!);
            return 0;
        });
        var forbidden = new[] { "Vat.Hosting", "Vat.Infrastructure", "Vat.Application", "Vat.Domain", "Vat.Identity.Infrastructure", "Vat.Workflow.Infrastructure" };
        foreach (var assembly in new[] { typeof(Program).Assembly, typeof(ComplianceDbContext).Assembly, typeof(ComplianceReadService).Assembly, typeof(VatReturn).Assembly })
            Assert.DoesNotContain(assembly.GetReferencedAssemblies(), a => forbidden.Contains(a.Name));
    }

    [Fact]
    public async Task Database_rejects_cross_tenant_line_invalid_hash_and_self_approval()
    {
        var tenant = Guid.NewGuid(); var company = Guid.NewGuid(); var id = await factory.Inspect(db => Seed(db, tenant, company));
        await factory.Inspect(async db =>
        {
            db.Add(new ReturnLine { TenantId = Guid.NewGuid(), CompanyId = company, Id = Guid.NewGuid(), ReturnId = id, LineCode = "foreign", Amount = 1m, SourceSnapshot = "{}", CreatedBy = "maker" });
            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, Assert.IsType<PostgresException>(ex.InnerException).SqlState); return 0;
        });
        await factory.Inspect(async db =>
        {
            db.Add(new FilingEvidence { TenantId = tenant, CompanyId = company, Id = Guid.NewGuid(), ReturnId = id, ObjectKey = "invalid", Sha256 = "short", FilingReference = "test", FiledAt = DateTimeOffset.UtcNow, CreatedBy = "maker" });
            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); Assert.Equal(PostgresErrorCodes.CheckViolation, Assert.IsType<PostgresException>(ex.InnerException).SqlState); return 0;
        });
        await factory.Inspect(async db =>
        {
            var item = await db.Returns.SingleAsync(x => x.Id == id); item.ApprovedBy = "maker";
            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); Assert.Equal(PostgresErrorCodes.CheckViolation, Assert.IsType<PostgresException>(ex.InnerException).SqlState); return 0;
        });
    }

    [Fact]
    public async Task Transfer_copies_nonempty_graph_and_preserves_divergent_target()
    {
        var tenant = Guid.NewGuid(); var company = Guid.NewGuid(); const string text = "\u09ac\u09be\u0982\u09b2\u09be | quoted \u0022name\u0022\nnext line";
        var source = await factory.Inspect(async db =>
        {
            var id = await Seed(db, tenant, company, text);
            var original = await db.Returns.SingleAsync(x => x.Id == id);
            db.Add(new VatReturn { TenantId = tenant, CompanyId = company, Id = Guid.NewGuid(), ReturnPeriodId = original.ReturnPeriodId, FormVersionId = original.FormVersionId, Revision = 2, CorrectsId = id, State = "Draft", CreatedBy = "maker" });
            await db.SaveChangesAsync(); return db.Database.GetConnectionString()!;
        });
        var adminConnection = Environment.GetEnvironmentVariable("VAT_TEST_POSTGRES")!;
        var targetName = "compliance_copy_" + Guid.NewGuid().ToString("N");
        var settings = new NpgsqlConnectionStringBuilder(adminConnection) { Database = targetName, Pooling = false };
        await using var admin = new NpgsqlConnection(adminConnection); await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{targetName}\"", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            var report = await ComplianceDataTransfer.Transfer(source, settings.ConnectionString); Assert.False(report.AlreadyMatched); Assert.Equal(8, report.Tables.Count); Assert.All(report.Tables, table => Assert.True(table.Rows > 0));
            Assert.True((await ComplianceDataTransfer.Transfer(source, settings.ConnectionString)).AlreadyMatched);
            var options = new DbContextOptionsBuilder<ComplianceDbContext>(); CompliancePersistence.Configure(options, settings.ConnectionString);
            await using (var target = new ComplianceDbContext(options.Options))
            {
                Assert.Equal(1234567890.123456m, await target.Set<ReturnLine>().Where(x => x.TenantId == tenant && x.CompanyId == company).Select(x => x.Amount).SingleAsync());
                Assert.Equal(2, await target.Returns.CountAsync(x => x.TenantId == tenant && x.CompanyId == company));
                Assert.NotNull(await target.Returns.Where(x => x.TenantId == tenant && x.CompanyId == company && x.Revision == 2).Select(x => x.CorrectsId).SingleAsync());
                var task = await target.Set<FilingEvidence>().SingleAsync(x => x.TenantId == tenant && x.CompanyId == company); Assert.Equal(text, task.FilingReference); task.FilingReference = "independent"; await target.SaveChangesAsync();
            }
            await Assert.ThrowsAsync<InvalidOperationException>(() => ComplianceDataTransfer.Transfer(source, settings.ConnectionString));
            await using (var target = new ComplianceDbContext(options.Options)) Assert.Equal("independent", await target.Set<FilingEvidence>().Where(x => x.TenantId == tenant && x.CompanyId == company).Select(x => x.FilingReference).SingleAsync());
            Assert.Equal(text, await factory.Inspect(db => db.Set<FilingEvidence>().Where(x => x.TenantId == tenant && x.CompanyId == company).Select(x => x.FilingReference).SingleAsync()));
        }
        finally
        {
            await using var sessions = new NpgsqlCommand("SELECT count(*) FROM pg_stat_activity WHERE datname=@db", admin); sessions.Parameters.AddWithValue("db", targetName); Assert.Equal(0L, (long)(await sessions.ExecuteScalarAsync())!);
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{targetName}\"", admin) { CommandTimeout = 60 }; await drop.ExecuteNonQueryAsync();
        }
    }
}
