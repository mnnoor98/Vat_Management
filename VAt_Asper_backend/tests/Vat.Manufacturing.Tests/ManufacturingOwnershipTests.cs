using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Vat.Application;
using Vat.Application.Security;
using Vat.Services.Manufacturing;
using Vat.Services.Manufacturing.Domain;
using Vat.Services.Manufacturing.Infrastructure;
using Xunit;

namespace Vat.Manufacturing.Tests;

public sealed class ManufacturingOwnershipTests(ManufacturingApiFactory factory) : IClassFixture<ManufacturingApiFactory>
{
    private static async Task<Guid> Seed(ManufacturingDbContext db, Guid tenant, Guid company, string? reference = null)
    {
        var bom = Guid.NewGuid(); var coefficient = Guid.NewGuid(); var order = Guid.NewGuid(); var product = Guid.NewGuid(); var component = Guid.NewGuid(); var warehouse = Guid.NewGuid(); var date = new DateOnly(2026, 1, 1);
        db.Add(new BomHeader { TenantId = tenant, CompanyId = company, Id = bom, ProductId = product, Code = bom.ToString(), Name = "BOM", CreatedBy = "maker" });
        db.Add(new CoefficientVersion { TenantId = tenant, CompanyId = company, Id = coefficient, BomId = bom, Revision = 1, OutputQuantity = 1.123456m, EffectiveFrom = date, State = "Approved", ApprovedBy = "checker", CreatedBy = "maker" });
        db.Add(new BomLine { TenantId = tenant, CompanyId = company, Id = Guid.NewGuid(), CoefficientVersionId = coefficient, ProductId = component, Quantity = 1234567890.123456m, WastagePercent = 12.345678m, CreatedBy = "maker" });
        db.Add(new ProductionOrder { TenantId = tenant, CompanyId = company, Id = order, CoefficientVersionId = coefficient, WarehouseId = warehouse, Reference = reference ?? order.ToString(), BusinessDate = date, PlannedQuantity = 1.123456m, State = "Pending", CreatedBy = "maker" });
        db.Add(new MaterialIssue { TenantId = tenant, CompanyId = company, Id = Guid.NewGuid(), ProductionOrderId = order, ProductId = component, WarehouseId = warehouse, Quantity = 1.123456m, BusinessDate = date, SourceReference = "issue-" + order, CreatedBy = "maker" });
        db.Add(new ProductionReceipt { TenantId = tenant, CompanyId = company, Id = Guid.NewGuid(), ProductionOrderId = order, ProductId = product, WarehouseId = warehouse, Quantity = 1.123456m, BusinessDate = date, SourceReference = "receipt-" + order, CreatedBy = "maker" });
        db.Add(new WastageRecord { TenantId = tenant, CompanyId = company, Id = Guid.NewGuid(), ProductionOrderId = order, ProductId = component, Quantity = 0.123456m, Reason = "private manufacturing reason", ApprovedBy = "checker", CreatedBy = "maker" });
        await db.SaveChangesAsync(); return order;
    }

    [Fact]
    public async Task Standalone_reads_enforce_scope_permission_cursor_and_limits()
    {
        var tenant = Guid.NewGuid(); var company = Guid.NewGuid();
        await factory.Inspect(async db => { await Seed(db, tenant, company); await Seed(db, tenant, company); await Seed(db, Guid.NewGuid(), company); await Seed(db, tenant, Guid.NewGuid()); return 0; });
        using var client = factory.Client(tenant, company);
        var path = $"/api/v1/companies/{company}/manufacturing/production-orders";
        var rows = await client.GetFromJsonAsync<ModuleRecord[]>(path);
        Assert.Equal(2, rows!.Length); Assert.All(rows, x => { Assert.Equal("Pending", x.State); Assert.NotNull(x.Reference); });
        var body = await client.GetStringAsync(path); Assert.DoesNotContain("private manufacturing reason", body); Assert.DoesNotContain("PlannedQuantity", body, StringComparison.OrdinalIgnoreCase);
        var first = (await client.GetFromJsonAsync<ModuleRecord[]>(path + "?take=1"))!.Single();
        var next = (await client.GetFromJsonAsync<ModuleRecord[]>(path + $"?before={first.Id}&take=1"))!.Single();
        Assert.NotEqual(first.Id, next.Id);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.GetAsync(path + "?take=0")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.GetAsync(path + "?take=101")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/companies/{Guid.NewGuid()}/manufacturing/production-orders")).StatusCode);
        using var denied = factory.Client(tenant, company, permissions: []);
        Assert.Equal(HttpStatusCode.Forbidden, (await denied.GetAsync(path)).StatusCode);
        using var anonymous = factory.CreateClient(); Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
    }

    [Fact]
    public async Task Own_model_has_ten_tables_no_financial_dependencies_and_scoped_foreign_keys()
    {
        await factory.Inspect(async db =>
        {
            Assert.False(db.Database.HasPendingModelChanges());
            Assert.Equal(10, db.Model.GetEntityTypes().Count());
            Assert.All(db.Model.GetEntityTypes(), e => { Assert.Equal("manufacturing", e.GetSchema()); Assert.All(e.GetForeignKeys(), fk => { Assert.Equal("manufacturing", fk.PrincipalEntityType.GetSchema()); Assert.Equal(new[] { "TenantId", "CompanyId" }, fk.Properties.Take(2).Select(p => p.Name)); }); });
            await using var connection = new NpgsqlConnection(db.Database.GetConnectionString()); await connection.OpenAsync();
            await using var query = new NpgsqlCommand("SELECT count(*) FROM information_schema.tables WHERE table_schema NOT IN ('pg_catalog','information_schema','manufacturing') AND table_type='BASE TABLE'", connection);
            Assert.Equal(0L, (long)(await query.ExecuteScalarAsync())!);
            return 0;
        });
        var forbidden = new[] { "Vat.Hosting", "Vat.Infrastructure", "Vat.Application", "Vat.Domain", "Vat.Identity.Infrastructure", "Vat.Workflow.Infrastructure", "Vat.Compliance.Infrastructure" };
        foreach (var assembly in new[] { typeof(Program).Assembly, typeof(ManufacturingDbContext).Assembly, typeof(ManufacturingReadService).Assembly, typeof(ProductionOrder).Assembly })
            Assert.DoesNotContain(assembly.GetReferencedAssemblies(), a => forbidden.Contains(a.Name));
    }

    [Fact]
    public async Task Database_rejects_cross_tenant_issue_invalid_wastage_and_self_approval()
    {
        var tenant = Guid.NewGuid(); var company = Guid.NewGuid(); var id = await factory.Inspect(db => Seed(db, tenant, company));
        await factory.Inspect(async db =>
        {
            db.Add(new MaterialIssue { TenantId = Guid.NewGuid(), CompanyId = company, Id = Guid.NewGuid(), ProductionOrderId = id, ProductId = Guid.NewGuid(), WarehouseId = Guid.NewGuid(), Quantity = 1m, BusinessDate = new DateOnly(2026, 1, 1), SourceReference = "foreign", CreatedBy = "maker" });
            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, Assert.IsType<PostgresException>(ex.InnerException).SqlState); return 0;
        });
        await factory.Inspect(async db =>
        {
            var coefficient = await db.Orders.Where(x => x.Id == id).Select(x => x.CoefficientVersionId).SingleAsync();
            db.Add(new BomLine { TenantId = tenant, CompanyId = company, Id = Guid.NewGuid(), CoefficientVersionId = coefficient, ProductId = Guid.NewGuid(), Quantity = 1m, WastagePercent = 101m, CreatedBy = "maker" });
            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); Assert.Equal(PostgresErrorCodes.CheckViolation, Assert.IsType<PostgresException>(ex.InnerException).SqlState); return 0;
        });
        await factory.Inspect(async db =>
        {
            var order = await db.Orders.SingleAsync(x => x.Id == id); order.ApprovedBy = "maker";
            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); Assert.Equal(PostgresErrorCodes.CheckViolation, Assert.IsType<PostgresException>(ex.InnerException).SqlState); return 0;
        });
    }

    [Fact]
    public async Task Transfer_copies_nonempty_graph_and_preserves_divergent_target()
    {
        var tenant = Guid.NewGuid(); var company = Guid.NewGuid(); const string text = "\u09ac\u09be\u0982\u09b2\u09be | quoted \u0022name\u0022\nnext line";
        var source = await factory.Inspect(async db => { await Seed(db, tenant, company, text); return db.Database.GetConnectionString()!; });
        var adminConnection = Environment.GetEnvironmentVariable("VAT_TEST_POSTGRES")!;
        var targetName = "manufacturing_copy_" + Guid.NewGuid().ToString("N");
        var settings = new NpgsqlConnectionStringBuilder(adminConnection) { Database = targetName, Pooling = false };
        await using var admin = new NpgsqlConnection(adminConnection); await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{targetName}\"", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            var report = await ManufacturingDataTransfer.Transfer(source, settings.ConnectionString); Assert.False(report.AlreadyMatched); Assert.Equal(7, report.Tables.Count); Assert.All(report.Tables, table => Assert.True(table.Rows > 0));
            Assert.True((await ManufacturingDataTransfer.Transfer(source, settings.ConnectionString)).AlreadyMatched);
            var options = new DbContextOptionsBuilder<ManufacturingDbContext>(); ManufacturingPersistence.Configure(options, settings.ConnectionString);
            await using (var target = new ManufacturingDbContext(options.Options))
            {
                Assert.Equal(1234567890.123456m, await target.Set<BomLine>().Where(x => x.TenantId == tenant && x.CompanyId == company).Select(x => x.Quantity).SingleAsync());
                Assert.Equal(new DateOnly(2026, 1, 1), await target.Orders.Where(x => x.TenantId == tenant && x.CompanyId == company).Select(x => x.BusinessDate).SingleAsync());
                var task = await target.Orders.SingleAsync(x => x.TenantId == tenant && x.CompanyId == company); Assert.Equal(text, task.Reference); task.Reference = "independent"; await target.SaveChangesAsync();
            }
            await Assert.ThrowsAsync<InvalidOperationException>(() => ManufacturingDataTransfer.Transfer(source, settings.ConnectionString));
            await using (var target = new ManufacturingDbContext(options.Options)) Assert.Equal("independent", await target.Orders.Where(x => x.TenantId == tenant && x.CompanyId == company).Select(x => x.Reference).SingleAsync());
            Assert.Equal(text, await factory.Inspect(db => db.Orders.Where(x => x.TenantId == tenant && x.CompanyId == company).Select(x => x.Reference).SingleAsync()));
            // Even matching metadata must not overwrite or bypass an active workflow target.
            await using (var target = new ManufacturingDbContext(options.Options))
            {
                (await target.Orders.SingleAsync(x => x.TenantId == tenant && x.CompanyId == company)).Reference = text;
                target.Add(new CoefficientCommand { TenantId = tenant, CompanyId = company, Id = Guid.NewGuid(),
                    Actor = "maker", RequestHash = new string('a', 64), ResponseJson = "{}", StatusCode = 200, CreatedAt = DateTimeOffset.UtcNow });
                await target.SaveChangesAsync();
            }
            var active = await Assert.ThrowsAsync<InvalidOperationException>(() => ManufacturingDataTransfer.Transfer(source, settings.ConnectionString));
            Assert.Contains("active coefficient workflow", active.Message);
            await using (var target = new ManufacturingDbContext(options.Options)) Assert.Equal(1, await target.Set<CoefficientCommand>().CountAsync());
        }
        finally
        {
            await using var sessions = new NpgsqlCommand("SELECT count(*) FROM pg_stat_activity WHERE datname=@db", admin); sessions.Parameters.AddWithValue("db", targetName); Assert.Equal(0L, (long)(await sessions.ExecuteScalarAsync())!);
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{targetName}\"", admin) { CommandTimeout = 60 }; await drop.ExecuteNonQueryAsync();
        }
    }
}
