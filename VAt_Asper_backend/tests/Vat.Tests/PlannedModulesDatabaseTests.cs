using Microsoft.EntityFrameworkCore;
using Npgsql;
using Vat.Domain.Company;
using Vat.Infrastructure.Persistence;
using Xunit;

namespace Vat.Tests;

[Collection("PostgreSQL integration")]
public sealed class PlannedModulesDatabaseTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private async Task<(Guid Tenant, Guid Company)> CompanyScope()
    {
        var tenant = Guid.NewGuid(); var company = Guid.NewGuid();
        await factory.Inspect(async db =>
        {
            db.Companies.Add(Company.Create(tenant, company, "Schema test", company.ToString(), DateTimeOffset.UtcNow));
            await db.SaveChangesAsync(); return 0;
        });
        return (tenant, company);
    }

    private async Task Insert(string table, Guid tenant, Guid company, Guid id, params (string Name, object Value)[] values)
    {
        await factory.Inspect(async db =>
        {
            var row = new Dictionary<string, object>
            {
                ["TenantId"] = tenant, ["CompanyId"] = company, ["Id"] = id, ["CreatedBy"] = "maker"
            };
            foreach (var (name, value) in values) row[name] = value;
            db.Set<Dictionary<string, object>>(table).Add(row);
            try { await db.SaveChangesAsync(); }
            finally { db.ChangeTracker.Clear(); }
            return 0;
        });
    }

    private static async Task Rejected(string state, Func<Task> action)
    {
        var failure = await Assert.ThrowsAsync<DbUpdateException>(action);
        Assert.Equal(state, Assert.IsType<PostgresException>(failure.InnerException).SqlState);
    }

    [Fact]
    public async Task Directory_grants_reject_cross_company_links_duplicates_and_missing_company()
    {
        var (tenant, company) = await CompanyScope();
        var (_, otherCompany) = await CompanyScope();
        // Another company in the same tenant is the strongest company-fence test.
        await factory.Inspect(async db => { db.Companies.Add(Company.Create(tenant, otherCompany, "Other", otherCompany.ToString(), DateTimeOffset.UtcNow)); await db.SaveChangesAsync(); return 0; });
        var user = Guid.NewGuid(); var role = Guid.NewGuid();
        await Insert("identity.users", tenant, company, user, ("Issuer", "https://issuer.example"), ("Subject", "subject"), ("DisplayName", "User"), ("Disabled", false));
        await Insert("identity.roles", tenant, company, role, ("Code", "reviewer"), ("Name", "Reviewer"));
        await Insert("identity.user_roles", tenant, company, Guid.NewGuid(), ("UserId", user), ("RoleId", role));
        await Rejected(PostgresErrorCodes.UniqueViolation, () => Insert("identity.user_roles", tenant, company, Guid.NewGuid(), ("UserId", user), ("RoleId", role)));
        await Rejected(PostgresErrorCodes.ForeignKeyViolation, () => Insert("identity.user_roles", tenant, otherCompany, Guid.NewGuid(), ("UserId", user), ("RoleId", role)));
        await Rejected(PostgresErrorCodes.ForeignKeyViolation, () => Insert("identity.roles", tenant, Guid.NewGuid(), Guid.NewGuid(), ("Code", "x"), ("Name", "X")));
    }

    [Fact]
    public async Task Effective_dates_quantities_and_checker_are_database_constraints()
    {
        var (tenant, company) = await CompanyScope();
        await Rejected(PostgresErrorCodes.CheckViolation, () => Insert("masterdata.hs_codes", tenant, company, Guid.NewGuid(),
            ("Code", "TEST"), ("Description", "Test only"), ("EffectiveFrom", new DateOnly(2026, 2, 1)), ("EffectiveUntil", new DateOnly(2026, 1, 1))));
        await Rejected(PostgresErrorCodes.CheckViolation, () => Insert("masterdata.uoms", tenant, company, Guid.NewGuid(), ("Code", "EA"), ("Name", "Each"), ("DecimalPlaces", 7)));
        await Rejected(PostgresErrorCodes.CheckViolation, () => Insert("tax.tax_treatments", tenant, company, Guid.NewGuid(),
            ("Code", "TEST"), ("Description", "Test only"), ("EffectiveFrom", new DateOnly(2026, 1, 1)), ("ApprovedBy", "maker")));
        await Rejected(PostgresErrorCodes.CheckViolation, () => Insert("workflow.delegations", tenant, company, Guid.NewGuid(),
            ("Delegator", "maker"), ("Delegate", "maker"), ("StartsAt", DateTimeOffset.UtcNow), ("ExpiresAt", DateTimeOffset.UtcNow.AddDays(1))));
    }

    [Fact]
    public async Task Voided_challan_number_requires_a_nonempty_reason()
    {
        var (tenant, company) = await CompanyScope();
        var branch = Branch.Create(tenant, company, "Test branch", DateTimeOffset.UtcNow);
        await factory.Inspect(async db => { db.Add(branch); await db.SaveChangesAsync(); return 0; });
        (string, object)[] Number(string? reason)
        {
            List<(string, object)> values = [("BranchId", branch.Id), ("FiscalYear", 2026),
                ("Series", "TEST"), ("Number", 1L), ("VoidedAt", DateTimeOffset.UtcNow)];
            if (reason is not null) values.Add(("VoidReason", reason));
            return values.ToArray();
        }
        await Rejected(PostgresErrorCodes.CheckViolation, () => Insert("sales.challan_numbers", tenant, company, Guid.NewGuid(), Number(null)));
        await Rejected(PostgresErrorCodes.CheckViolation, () => Insert("sales.challan_numbers", tenant, company, Guid.NewGuid(), Number("   ")));
        await Insert("sales.challan_numbers", tenant, company, Guid.NewGuid(), Number("Allocation cancelled"));
    }

    [Fact]
    public async Task Manufacturing_coefficients_require_positive_output_and_bound_wastage()
    {
        var (tenant, company) = await CompanyScope();
        var product = Vat.Domain.Catalog.Product.Create(tenant, company, "FINISHED", "Test", "EA", "TEST");
        await factory.Inspect(async db => { db.Add(product); await db.SaveChangesAsync(); return 0; });
        var bom = Guid.NewGuid(); var coefficient = Guid.NewGuid();
        await Insert("manufacturing.bom_headers", tenant, company, bom, ("ProductId", product.Id), ("Code", "BOM-1"), ("Name", "Test"));
        (string, object)[] Coefficient(decimal quantity) => [("BomId", bom), ("Revision", 1), ("OutputQuantity", quantity),
            ("EffectiveFrom", new DateOnly(2026, 1, 1)), ("State", "Draft")];
        await Rejected(PostgresErrorCodes.CheckViolation, () => Insert("manufacturing.coefficient_versions", tenant, company, Guid.NewGuid(), Coefficient(0)));
        await Insert("manufacturing.coefficient_versions", tenant, company, coefficient, Coefficient(1));
        await Rejected(PostgresErrorCodes.CheckViolation, () => Insert("manufacturing.bom_lines", tenant, company, Guid.NewGuid(),
            ("CoefficientVersionId", coefficient), ("ProductId", product.Id), ("Quantity", 1m), ("WastagePercent", 101m)));
    }

    [Fact]
    public async Task Inbound_batch_cannot_use_another_connections_mapping_and_external_id_is_idempotent()
    {
        var (tenant, company) = await CompanyScope();
        var first = Guid.NewGuid(); var second = Guid.NewGuid(); var mapping = Guid.NewGuid();
        foreach (var id in new[] { first, second })
            await Insert("integration.integration_connections", tenant, company, id, ("Code", id.ToString()), ("Provider", "TEST"),
                ("Endpoint", "https://example.invalid"), ("SecretReference", "vault://test"), ("Enabled", false));
        await Insert("integration.field_mappings", tenant, company, mapping, ("ConnectionId", first), ("DocumentType", "Purchase"), ("Revision", 1), ("Mapping", "{}"));
        (string, object)[] Batch(Guid connection) => [("ConnectionId", connection), ("MappingId", mapping), ("ExternalId", "external-1"),
            ("State", "Pending"), ("PayloadKey", "test/empty"), ("PayloadDigest", new string('a', 64)), ("ReceivedAt", DateTimeOffset.UtcNow)];
        await Rejected(PostgresErrorCodes.ForeignKeyViolation, () => Insert("integration.inbound_batches", tenant, company, Guid.NewGuid(), Batch(second)));
        await Insert("integration.inbound_batches", tenant, company, Guid.NewGuid(), Batch(first));
        await Rejected(PostgresErrorCodes.UniqueViolation, () => Insert("integration.inbound_batches", tenant, company, Guid.NewGuid(), Batch(first)));
    }

    [Fact]
    public async Task Full_migration_is_current_and_all_foundation_foreign_keys_validate()
    {
        await factory.Inspect(async db =>
        {
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.False(db.Database.HasPendingModelChanges());
            var foundations = db.Model.GetEntityTypes().Where(x => x.ClrType == typeof(Dictionary<string, object>)).ToArray();
            Assert.Equal(63, foundations.Length);
            foreach (var entity in foundations)
            {
                Assert.Contains(entity.GetForeignKeys(), fk => fk.PrincipalEntityType.ClrType == typeof(Company));
                // Query each physical table, catching migration/catalog mismatches.
                await db.Set<Dictionary<string, object>>(entity.Name).Take(1).ToListAsync();
            }
            return 0;
        });
    }
}
