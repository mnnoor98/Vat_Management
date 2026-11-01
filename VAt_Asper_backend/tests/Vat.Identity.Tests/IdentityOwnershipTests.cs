using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Vat.Application;
using Vat.Application.Security;
using Vat.Services.Identity.Domain;
using Vat.Services.Identity.Infrastructure;
using Xunit;

namespace Vat.Identity.Tests;

public sealed class IdentityOwnershipTests(IdentityApiFactory factory) : IClassFixture<IdentityApiFactory>
{
    [Fact]
    public async Task Ownership_copy_preserves_data_is_repeatable_and_refuses_to_overwrite_divergent_data()
    {
        var tenant = Guid.NewGuid(); var company = Guid.NewGuid(); var roleId = Guid.NewGuid();
        var source = await factory.Inspect(async db =>
        {
            db.Roles.Add(new IdentityRole { TenantId = tenant, CompanyId = company, Id = roleId,
                Code = "COPY-ROLE", Name = "বাংলা | quoted \"name\"\nnext line", CreatedBy = "copy-test" });
            await db.SaveChangesAsync();
            return db.Database.GetConnectionString()!;
        });
        var adminConnection = Environment.GetEnvironmentVariable("VAT_TEST_POSTGRES")!;
        var targetName = "identity_copy_" + Guid.NewGuid().ToString("N");
        var settings = new NpgsqlConnectionStringBuilder(adminConnection) { Database = targetName, Pooling = false };
        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{targetName}\"", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            var result = await IdentityDataTransfer.Transfer(source, settings.ConnectionString);
            Assert.False(result.AlreadyMatched);
            Assert.Equal(9, result.Tables.Count);
            Assert.True(result.Tables.Sum(x => x.Rows) > 0);
            Assert.True((await IdentityDataTransfer.Transfer(source, settings.ConnectionString)).AlreadyMatched);
            var options = new DbContextOptionsBuilder<IdentityDbContext>();
            IdentityPersistence.Configure(options, settings.ConnectionString);
            await using (var target = new IdentityDbContext(options.Options))
            {
                var role = await target.Roles.SingleAsync(x => x.TenantId == tenant && x.CompanyId == company && x.Id == roleId);
                Assert.Equal("বাংলা | quoted \"name\"\nnext line", role.Name);
                role.Name = "Target edited independently";
                await target.SaveChangesAsync();
            }
            await Assert.ThrowsAsync<InvalidOperationException>(() => IdentityDataTransfer.Transfer(source, settings.ConnectionString));
            await using (var untouched = new IdentityDbContext(options.Options))
                Assert.Equal("Target edited independently", await untouched.Roles.Where(x => x.TenantId == tenant && x.CompanyId == company && x.Id == roleId).Select(x => x.Name).SingleAsync());
            Assert.Equal("বাংলা | quoted \"name\"\nnext line", await factory.Inspect(db => db.Roles.Where(x => x.TenantId == tenant && x.CompanyId == company && x.Id == roleId).Select(x => x.Name).SingleAsync()));
        }
        finally
        {
            // Only an internally generated test database is removed.
            long connections = -1;
            for (var attempt = 0; attempt < 20; attempt++)
            {
                await using var sessions = new NpgsqlCommand("SELECT count(*) FROM pg_stat_activity WHERE datname = @database", admin);
                sessions.Parameters.AddWithValue("database", targetName);
                connections = (long)(await sessions.ExecuteScalarAsync())!;
                if (connections == 0) break;
                await Task.Delay(100);
            }
            Assert.Equal(0, connections);
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{targetName}\"", admin) { CommandTimeout = 60 };
            await drop.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task Role_reads_are_scoped_paginated_and_require_permissions_without_a_company_database()
    {
        var tenant = Guid.NewGuid(); var company = Guid.NewGuid();
        var otherTenant = Guid.NewGuid(); var otherCompany = Guid.NewGuid();
        var ids = new[] { Guid.Parse("00000000-0000-0000-0000-000000000001"), Guid.Parse("00000000-0000-0000-0000-000000000002") };
        await factory.Inspect(async db =>
        {
            foreach (var (t, c) in new[] { (tenant, company), (tenant, otherCompany), (otherTenant, company) })
                foreach (var id in ids)
                    db.Roles.Add(new IdentityRole { TenantId = t, CompanyId = c, Id = id, Code = id.ToString(), Name = "Reader", CreatedBy = "maker" });
            await db.SaveChangesAsync(); return 0;
        });
        var url = $"/api/v1/companies/{company}/identity/roles?take=1";
        var client = factory.Client(tenant, company, permissions: [Permissions.IdentityRead]);
        var first = Assert.Single((await client.GetFromJsonAsync<ModuleRecord[]>(url))!);
        Assert.Equal(ids[1], first.Id);
        var second = Assert.Single((await client.GetFromJsonAsync<ModuleRecord[]>(url + "&before=" + first.Id))!);
        Assert.Equal(ids[0], second.Id);
        Assert.Empty((await client.GetFromJsonAsync<ModuleRecord[]>(url + "&before=" + second.Id))!);
        Assert.Equal(HttpStatusCode.Forbidden, (await factory.Client(tenant, company, permissions: []).GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await factory.Client(tenant, otherCompany).GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.GetAsync(url.Replace("take=1", "take=101"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
    }

    [Fact]
    public async Task Only_identity_tables_and_identity_migrations_are_required()
    {
        await factory.Inspect(async db =>
        {
            Assert.Equal(10, db.Model.GetEntityTypes().Count());
            Assert.Equal("local_accounts", db.Model.FindEntityType(typeof(LocalAccount))?.GetTableName());
            Assert.All(db.Model.GetEntityTypes(), entity =>
            {
                Assert.Equal("identity", entity.GetSchema());
                Assert.All(entity.GetForeignKeys(), fk => Assert.Equal("identity", fk.PrincipalEntityType.GetSchema()));
            });
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.False(db.Database.HasPendingModelChanges());
            await using var command = db.Database.GetDbConnection().CreateCommand();
            await db.Database.OpenConnectionAsync();
            command.CommandText = "SELECT count(*) FROM information_schema.tables WHERE table_schema IN ('company','purchasing','sales','tax','inventory','platform')";
            Assert.Equal(0L, await command.ExecuteScalarAsync());
            return 0;
        });
        var forbidden = new[] { "Vat.Hosting", "Vat.Infrastructure", "Vat.Application", "Vat.Domain" };
        Assert.DoesNotContain(typeof(Program).Assembly.GetReferencedAssemblies(), name => forbidden.Contains(name.Name));
        Assert.DoesNotContain(typeof(IdentityDbContext).Assembly.GetReferencedAssemblies(), name => forbidden.Contains(name.Name));
    }

    [Fact]
    public async Task Grants_reject_duplicate_company_wide_roles_and_cross_tenant_references()
    {
        var tenant = Guid.NewGuid(); var company = Guid.NewGuid(); var user = Guid.NewGuid(); var role = Guid.NewGuid();
        await factory.Inspect(async db =>
        {
            db.Add(new IdentityUser { TenantId = tenant, CompanyId = company, Id = user, Issuer = "https://issuer.example", Subject = "user", DisplayName = "User", CreatedBy = "maker" });
            db.Roles.Add(new IdentityRole { TenantId = tenant, CompanyId = company, Id = role, Code = "READER", Name = "Reader", CreatedBy = "maker" });
            db.Add(new UserRole { TenantId = tenant, CompanyId = company, Id = Guid.NewGuid(), UserId = user, RoleId = role, CreatedBy = "maker" });
            await db.SaveChangesAsync(); return 0;
        });
        async Task Reject(Guid grantTenant, string state) => await factory.Inspect(async db =>
        {
            db.Add(new UserRole { TenantId = grantTenant, CompanyId = company, Id = Guid.NewGuid(), UserId = user, RoleId = role, CreatedBy = "maker" });
            try
            {
                var failure = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
                Assert.Equal(state, Assert.IsType<PostgresException>(failure.InnerException).SqlState);
            }
            finally { db.ChangeTracker.Clear(); }
            return 0;
        });
        await Reject(tenant, PostgresErrorCodes.UniqueViolation);
        await Reject(Guid.NewGuid(), PostgresErrorCodes.ForeignKeyViolation);
    }

    [Fact]
    public async Task Profile_uses_verified_identity_and_fixed_key_shortcut_stays_retired()
    {
        var client = factory.Client(Guid.NewGuid(), Guid.NewGuid(), "reader", []);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/identity/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Gone, (await factory.CreateClient().GetAsync("/api/v1/auth/dev-token")).StatusCode);
    }
}

