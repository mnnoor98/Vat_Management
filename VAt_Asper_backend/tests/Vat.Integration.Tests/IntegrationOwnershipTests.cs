using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Vat.Application;
using Vat.Application.Security;
using Vat.Services.Integration;
using Vat.Services.Integration.Domain;
using Vat.Services.Integration.Infrastructure;
using Xunit;

namespace Vat.Integration.Tests;

public sealed class IntegrationOwnershipTests(IntegrationApiFactory factory) : IClassFixture<IntegrationApiFactory>
{
    private static async Task<Guid> Seed(IntegrationDbContext db, Guid tenant, Guid company, string? code = null)
    {
        var connection = Guid.NewGuid(); var mapping = Guid.NewGuid(); var batch = Guid.NewGuid(); var subscription = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        db.Add(new IntegrationConnection { TenantId = tenant, CompanyId = company, Id = connection, Code = code ?? connection.ToString(), Provider = "TEST", Endpoint = "https://private.example.invalid", SecretReference = "vault://private-secret", Enabled = true, CreatedBy = "maker" });
        db.Add(new FieldMapping { TenantId = tenant, CompanyId = company, Id = mapping, ConnectionId = connection, DocumentType = "Purchase", Revision = 1, Mapping = "{}", ApprovedBy = "checker", CreatedBy = "maker" });
        db.Add(new InboundBatch { TenantId = tenant, CompanyId = company, Id = batch, ConnectionId = connection, MappingId = mapping, ExternalId = batch.ToString(), State = "Pending", PayloadKey = "private://payload", PayloadDigest = new string('a', 64), ReceivedAt = now, CreatedBy = "maker" });
        db.Add(new InboundRow { TenantId = tenant, CompanyId = company, Id = Guid.NewGuid(), BatchId = batch, RowNumber = 1, ExternalId = "row-1", State = "Pending", CreatedBy = "maker" });
        db.Add(new ExternalReference { TenantId = tenant, CompanyId = company, Id = Guid.NewGuid(), ConnectionId = connection, SourceType = "Purchase", ExternalId = "external-1", SourceId = Guid.NewGuid(), CreatedBy = "maker" });
        db.Add(new WebhookSubscription { TenantId = tenant, CompanyId = company, Id = subscription, ConnectionId = connection, EventType = "PurchasePosted", Endpoint = "https://private.example.invalid", SigningSecretReference = "vault://webhook", Enabled = true, CreatedBy = "maker" });
        db.Add(new DeliveryAttempt { TenantId = tenant, CompanyId = company, Id = Guid.NewGuid(), SubscriptionId = subscription, EventId = Guid.NewGuid(), AttemptNumber = 1, State = "Pending", NextAttemptAt = now, CreatedBy = "maker" });
        db.Add(new IntegrationDeadLetter { TenantId = tenant, CompanyId = company, Id = Guid.NewGuid(), ConnectionId = connection, BatchId = batch, PayloadKey = "private://payload", Error = "metadata error", FailedAt = now, CreatedBy = "maker" });
        await db.SaveChangesAsync(); return connection;
    }

    [Fact]
    public async Task Standalone_reads_enforce_scope_permission_cursor_and_limits()
    {
        var tenant = Guid.NewGuid(); var company = Guid.NewGuid();
        await factory.Inspect(async db => { await Seed(db, tenant, company); await Seed(db, tenant, company); await Seed(db, Guid.NewGuid(), company); await Seed(db, tenant, Guid.NewGuid()); return 0; });
        using var client = factory.Client(tenant, company);
        var path = $"/api/v1/companies/{company}/integration/connections";
        var rows = await client.GetFromJsonAsync<ModuleRecord[]>(path);
        Assert.Equal(2, rows!.Length); Assert.All(rows, x => { Assert.Null(x.State); Assert.NotNull(x.Reference); });
        var body = await client.GetStringAsync(path); Assert.DoesNotContain("vault://", body); Assert.DoesNotContain("Endpoint", body, StringComparison.OrdinalIgnoreCase);
        var first = (await client.GetFromJsonAsync<ModuleRecord[]>(path + "?take=1"))!.Single();
        var next = (await client.GetFromJsonAsync<ModuleRecord[]>(path + $"?before={first.Id}&take=1"))!.Single();
        Assert.NotEqual(first.Id, next.Id);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.GetAsync(path + "?take=0")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.GetAsync(path + "?take=101")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/companies/{Guid.NewGuid()}/integration/connections")).StatusCode);
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
            Assert.All(db.Model.GetEntityTypes(), e => { Assert.Equal("integration", e.GetSchema()); Assert.All(e.GetForeignKeys(), fk => { Assert.Equal("integration", fk.PrincipalEntityType.GetSchema()); Assert.Equal(new[] { "TenantId", "CompanyId" }, fk.Properties.Take(2).Select(p => p.Name)); }); });
            await using var connection = new NpgsqlConnection(db.Database.GetConnectionString()); await connection.OpenAsync();
            await using var query = new NpgsqlCommand("SELECT count(*) FROM information_schema.tables WHERE table_schema NOT IN ('pg_catalog','information_schema','integration') AND table_type='BASE TABLE'", connection);
            Assert.Equal(0L, (long)(await query.ExecuteScalarAsync())!);
            return 0;
        });
        var forbidden = new[] { "Vat.Hosting", "Vat.Infrastructure", "Vat.Application", "Vat.Domain", "Vat.Identity.Infrastructure", "Vat.Workflow.Infrastructure", "Vat.Compliance.Infrastructure", "Vat.Manufacturing.Infrastructure" };
        foreach (var assembly in new[] { typeof(Program).Assembly, typeof(IntegrationDbContext).Assembly, typeof(IntegrationReadService).Assembly, typeof(IntegrationConnection).Assembly })
            Assert.DoesNotContain(assembly.GetReferencedAssemblies(), a => forbidden.Contains(a.Name));
    }

    [Fact]
    public async Task Database_rejects_cross_tenant_row_wrong_connection_mapping_and_invalid_digest()
    {
        var tenant = Guid.NewGuid(); var company = Guid.NewGuid(); var first = await factory.Inspect(db => Seed(db, tenant, company)); var second = await factory.Inspect(db => Seed(db, tenant, company));
        await factory.Inspect(async db =>
        {
            var batch = await db.Set<InboundBatch>().Where(x => x.ConnectionId == first).Select(x => x.Id).SingleAsync();
            db.Add(new InboundRow { TenantId = Guid.NewGuid(), CompanyId = company, Id = Guid.NewGuid(), BatchId = batch, RowNumber = 2, ExternalId = "foreign", State = "Pending", CreatedBy = "maker" });
            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, Assert.IsType<PostgresException>(ex.InnerException).SqlState); return 0;
        });
        await factory.Inspect(async db =>
        {
            var mapping = await db.Set<FieldMapping>().Where(x => x.ConnectionId == second).Select(x => x.Id).SingleAsync();
            db.Add(new InboundBatch { TenantId = tenant, CompanyId = company, Id = Guid.NewGuid(), ConnectionId = first, MappingId = mapping, ExternalId = "wrong-mapping", State = "Pending", PayloadKey = "private", PayloadDigest = new string('a', 64), ReceivedAt = DateTimeOffset.UtcNow, CreatedBy = "maker" });
            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, Assert.IsType<PostgresException>(ex.InnerException).SqlState); return 0;
        });
        await factory.Inspect(async db =>
        {
            var batch = await db.Set<InboundBatch>().SingleAsync(x => x.ConnectionId == first); batch.PayloadDigest = "short";
            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); Assert.Equal(PostgresErrorCodes.CheckViolation, Assert.IsType<PostgresException>(ex.InnerException).SqlState); return 0;
        });
        await factory.Inspect(async db =>
        {
            var mapping = await db.Set<FieldMapping>().SingleAsync(x => x.ConnectionId == first); mapping.ApprovedBy = "maker";
            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); Assert.Equal(PostgresErrorCodes.CheckViolation, Assert.IsType<PostgresException>(ex.InnerException).SqlState); return 0;
        });
    }

    [Fact]
    public async Task Transfer_copies_nonempty_graph_and_preserves_divergent_target()
    {
        var tenant = Guid.NewGuid(); var company = Guid.NewGuid(); const string text = "\u09ac\u09be\u0982\u09b2\u09be | quoted \u0022name\u0022\nnext line";
        var source = await factory.Inspect(async db => { await Seed(db, tenant, company, text); return db.Database.GetConnectionString()!; });
        var adminConnection = Environment.GetEnvironmentVariable("VAT_TEST_POSTGRES")!;
        var targetName = "integration_copy_" + Guid.NewGuid().ToString("N");
        var settings = new NpgsqlConnectionStringBuilder(adminConnection) { Database = targetName, Pooling = false };
        await using var admin = new NpgsqlConnection(adminConnection); await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{targetName}\"", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            var report = await IntegrationDataTransfer.Transfer(source, settings.ConnectionString); Assert.False(report.AlreadyMatched); Assert.Equal(8, report.Tables.Count); Assert.All(report.Tables, table => Assert.True(table.Rows > 0));
            Assert.True((await IntegrationDataTransfer.Transfer(source, settings.ConnectionString)).AlreadyMatched);
            var options = new DbContextOptionsBuilder<IntegrationDbContext>(); IntegrationPersistence.Configure(options, settings.ConnectionString);
            await using (var target = new IntegrationDbContext(options.Options))
            {
                Assert.True(await target.Connections.Where(x => x.TenantId == tenant && x.CompanyId == company).Select(x => x.Enabled).SingleAsync());
                var task = await target.Connections.SingleAsync(x => x.TenantId == tenant && x.CompanyId == company); Assert.Equal(text, task.Code); task.Code = "independent"; await target.SaveChangesAsync();
            }
            await Assert.ThrowsAsync<InvalidOperationException>(() => IntegrationDataTransfer.Transfer(source, settings.ConnectionString));
            await using (var target = new IntegrationDbContext(options.Options)) Assert.Equal("independent", await target.Connections.Where(x => x.TenantId == tenant && x.CompanyId == company).Select(x => x.Code).SingleAsync());
            Assert.Equal(text, await factory.Inspect(db => db.Connections.Where(x => x.TenantId == tenant && x.CompanyId == company).Select(x => x.Code).SingleAsync()));
        }
        finally
        {
            await using var sessions = new NpgsqlCommand("SELECT count(*) FROM pg_stat_activity WHERE datname=@db", admin); sessions.Parameters.AddWithValue("db", targetName); Assert.Equal(0L, (long)(await sessions.ExecuteScalarAsync())!);
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{targetName}\"", admin) { CommandTimeout = 60 }; await drop.ExecuteNonQueryAsync();
        }
    }
}
