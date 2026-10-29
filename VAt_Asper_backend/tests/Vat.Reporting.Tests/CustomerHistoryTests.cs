using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Vat.Application.Security;
using Vat.Domain;
using Vat.Messaging.Contracts.V1;
using Vat.Services.Reporting.Application;
using Vat.Services.Reporting.Domain;
using Vat.Services.Reporting.Infrastructure;
using Xunit;

namespace Vat.Reporting.Tests;

public sealed class CustomerHistoryTests(ReportingDatabase database) : IClassFixture<ReportingDatabase>
{
    private static readonly DateOnly Start = new(2026, 10, 1);
    private static Actor Operator() => new(Guid.NewGuid(), "operator", new HashSet<Guid> { Guid.NewGuid() },
        new HashSet<string> { Permissions.ReportRead, Permissions.ReportManage });
    private static CustomerSnapshotService Service(ReportingDbContext db) => new(new CustomerSnapshotStore(db, TimeProvider.System));
    private sealed class Source(CustomerHistoryExportV1 export) : ICustomerHistorySource
    {
        public bool Offline { get; set; }
        public Task<CustomerHistoryExportV1> CustomerHistory(Guid company, Guid customer, Guid id, CancellationToken ct)
            => Offline ? throw new InvalidOperationException("Source offline") : Task.FromResult(export);
    }
    private static T[] Order<T>(IEnumerable<T> rows, Func<T, Guid> key) => rows.OrderBy(x => key(x).ToString("D"), StringComparer.Ordinal).ToArray();
    private static CustomerHistoryExportV1 Fixture(Actor actor)
    {
        var invoice = Guid.NewGuid(); var payment = Guid.NewGuid(); var allocation = Guid.NewGuid(); var correction = Guid.NewGuid();
        var data = new CustomerHistoryDataV1("CUSTOMER", "Snapshot customer", 1, "BDT",
            [new(invoice, "INV-REF", Start, Start.AddDays(19))],
            Order(new CustomerHistoryDocumentV1[] { new(invoice, invoice, "Invoice", Start, "INV-1", 100, 5),
                new(Guid.NewGuid(), invoice, "CreditNote", Start.AddDays(14), "CN-1", -40, -2),
                new(Guid.NewGuid(), invoice, "DebitNote", Start.AddDays(21), "DN-1", 10, .5m) }, x => x.Id),
            [new(payment, "CASH-1", Start.AddDays(9), 100)],
            [new(correction, payment, "PaymentRefund", Start.AddDays(22), "RF-1", -20)],
            Order(new CustomerHistoryAllocationV1[] { new(allocation, payment, invoice, Start.AddDays(9), 50),
                new(Guid.NewGuid(), payment, invoice, Start.AddDays(19), 34) }, x => x.Id),
            [new(Guid.NewGuid(), correction, allocation, Start.AddDays(22), 10)]);
        var customer = Guid.NewGuid(); var company = actor.CompanyIds.Single();
        return new(new(1, Guid.NewGuid(), actor.TenantId, company, customer, CustomerHistoryV1.Scope, DateTimeOffset.UtcNow,
            true, CustomerHistoryV1.Count(data), CustomerHistoryV1.Fingerprint(actor.TenantId, company, customer, data)), data);
    }
    private static CustomerHistoryExportV1 Rehash(CustomerHistoryExportV1 export, CustomerHistoryDataV1 data)
        => export with { Data = data, Manifest = export.Manifest with { RowCount = CustomerHistoryV1.Count(data),
            FingerprintSha256 = CustomerHistoryV1.Fingerprint(export.Manifest.TenantId, export.Manifest.CompanyId, export.Manifest.CustomerId, data) } };
    private static VerifiedCustomerHistory Verify(CustomerHistoryExportV1 export)
        => VerifiedCustomerHistory.Verify(export, export.Manifest.TenantId, export.Manifest.CompanyId, export.Manifest.CustomerId);

    [Fact]
    public void Statements_preserve_opening_signed_tax_running_totals_and_exclusive_end()
    {
        var history = Verify(Fixture(Operator()));
        var statement = CustomerHistoryProjection.Statement(history, Start.AddDays(11), Start.AddDays(23));
        Assert.Equal(5, statement.OpeningBalance); Assert.Equal(30.5m, statement.TotalDebit); Assert.Equal(42, statement.TotalCredit);
        Assert.Equal(-6.5m, statement.ClosingBalance); Assert.Equal(new[] { "CreditNote", "DebitNote", "PaymentRefund" }, statement.Lines.Select(x => x.Type));
        Assert.Equal(new[] { -37m, -26.5m, -6.5m }, statement.Lines.Select(x => x.RunningBalance));
        Assert.Equal(-37, CustomerHistoryProjection.Statement(history, Start.AddDays(11), Start.AddDays(21)).ClosingBalance);
        Assert.Equal(statement.ClosingBalance, CustomerHistoryProjection.Ageing(history, Start.AddDays(22)).Balance);
    }
    [Fact]
    public void Ageing_uses_dated_allocations_releases_and_cash_corrections()
    {
        var history = Verify(Fixture(Operator()));
        var before = CustomerHistoryProjection.Ageing(history, Start.AddDays(13));
        Assert.Equal(55, before.Buckets.NotDue); Assert.Equal(50, before.UnallocatedAmount); Assert.Equal(5, before.Balance);
        var after = CustomerHistoryProjection.Ageing(history, Start.AddDays(22));
        Assert.Equal(74, after.AllocatedAmount); Assert.Equal(20, after.ReturnedAmount); Assert.Equal(80, after.NetCollectedAmount);
        Assert.Equal(0, after.Buckets.Overdue1To30); Assert.Equal(.5m, after.InvoiceCreditAmount);
        Assert.Equal(6, after.UnallocatedAmount); Assert.Equal(-6.5m, after.Balance);
    }
    [Theory]
    [InlineData("scope")][InlineData("coverage")][InlineData("fingerprint")][InlineData("duplicate")]
    [InlineData("orphan")][InlineData("future-release")][InlineData("over-release")][InlineData("money")][InlineData("decimal-min")]
    public void Even_rehashed_invalid_history_fails_closed(string fault)
    {
        var export = Fixture(Operator()); var d = export.Data;
        export = fault switch
        {
            "scope" => export with { Manifest = export.Manifest with { Scope = ReportingSourceV1.Scope } },
            "coverage" => export with { Manifest = export.Manifest with { FullHistory = false } },
            "fingerprint" => export with { Manifest = export.Manifest with { FingerprintSha256 = new string('0', 64) } },
            "duplicate" => Rehash(export, d with { Documents = d.Documents.Concat([d.Documents[0]]).ToArray() }),
            "orphan" => Rehash(export, d with { Allocations = Order(d.Allocations.Select(x => x with { InvoiceId = Guid.NewGuid() }), x => x.Id) }),
            "future-release" => Rehash(export, d with { Releases = d.Releases.Select(x => x with { BusinessDate = x.BusinessDate.AddDays(1) }).ToArray() }),
            "over-release" => Rehash(export, d with { Releases = d.Releases.Select(x => x with { Amount = 51 }).ToArray() }),
            "decimal-min" => Rehash(export, d with { Corrections = d.Corrections.Select(x => x with { Amount = decimal.MinValue }).ToArray() }),
            _ => Rehash(export, d with { Payments = d.Payments.Select(x => x with { Amount = 100.00001m }).ToArray() })
        };
        Assert.Equal("invalid_customer_history", Assert.Throws<DomainException>(() => Verify(export)).Code);
    }
    [Fact]
    public void Ageing_keeps_unknown_terms_and_exact_day_buckets_without_netting_invoice_credit()
    {
        var export = Fixture(Operator()); var asOf = new DateOnly(2026, 10, 24);
        var invoices = new List<CustomerHistoryInvoiceV1>(); var documents = new List<CustomerHistoryDocumentV1>();
        foreach (var days in new int?[] { -1, 0, 1, 30, 31, 60, 61, 90, 91, null })
        {
            var id = Guid.NewGuid(); invoices.Add(new(id, "REF", new(2026, 1, 1), days.HasValue ? asOf.AddDays(-days.Value) : null));
            documents.Add(new(id, id, "Invoice", new(2026, 1, 1), "INV", 1, .05m));
        }
        var creditInvoice = Guid.NewGuid(); invoices.Add(new(creditInvoice, "CREDIT", Start, Start));
        documents.Add(new(creditInvoice, creditInvoice, "Invoice", Start, "INV-CREDIT", 100, 5));
        var payment = Guid.NewGuid();
        var data = export.Data with { Invoices = Order(invoices, x => x.Id), Documents = Order(documents, x => x.Id),
            Payments = [new(payment, "PAY", Start, 105)], Corrections = [], Releases = [],
            Allocations = [new(Guid.NewGuid(), payment, creditInvoice, Start, 105)] };
        // A later price credit creates invoice credit; retain the original paid allocation.
        data = data with { Documents = Order(data.Documents.Concat([new CustomerHistoryDocumentV1(Guid.NewGuid(), creditInvoice,
            "CreditNote", Start.AddDays(1), "CN", -20, -1)]), x => x.Id) };
        var report = CustomerHistoryProjection.Ageing(Verify(Rehash(export, data)), asOf);
        Assert.Equal(new CustomerSnapshotAgeingBuckets(2.1m, 2.1m, 2.1m, 2.1m, 1.05m, 1.05m), report.Buckets);
        Assert.Equal(21, report.InvoiceCreditAmount); Assert.Equal(10.5m, report.OutstandingDebitAmount);
        Assert.Equal(-10.5m, report.Balance); Assert.Single(report.Invoices, x => x.Bucket == "Credit");
        Assert.Null(report.Invoices.Single(x => x.Bucket == "UnknownDueDate").DaysOverdue);
    }
    [Fact]
    public async Task Import_restarts_offline_retries_and_customer_scope_are_persisted_without_financial_sql()
    {
        var actor = Operator(); var company = actor.CompanyIds.Single(); var export = Fixture(actor); var customer = export.Manifest.CustomerId;
        var source = new Source(export); var key = Guid.NewGuid(); var request = new BootstrapCustomerRequest(export.Manifest.ExportId, "Verified source");
        CustomerSnapshotResult imported;
        await using (var db = database.Open()) imported = await Service(db).Bootstrap(actor, company, customer, key, request, source, default);
        source.Offline = true;
        await using var reopened = database.Open(); var service = Service(reopened);
        Assert.Equal(imported, await service.Bootstrap(actor, company, customer, key, request, source, default));
        Assert.True(imported.SnapshotOnly); Assert.False(imported.LiveCoverageVerified);
        var statement = await service.Statement(actor, company, customer, imported.GenerationId, Start, Start.AddDays(24), default);
        Assert.Equal(-6.5m, statement.Report.ClosingBalance);
        Assert.Equal(export.Manifest.CapturedAt, statement.Report.CapturedAt);
        Assert.Equal(1, await reopened.Set<CustomerSnapshotGeneration>().CountAsync(x => x.CompanyId == company));
        Assert.Equal(1, await reopened.Set<CustomerSnapshotAudit>().CountAsync(x => x.CompanyId == company));
        Assert.Equal("snapshot_command_conflict", (await Assert.ThrowsAsync<DomainException>(() => service.Bootstrap(actor, company, customer,
            key, request with { Reason = "Changed" }, source, default))).Code);
        Assert.Equal("not_found", (await Assert.ThrowsAsync<DomainException>(() => service.Statement(actor, company, Guid.NewGuid(),
            imported.GenerationId, Start, Start.AddDays(24), default))).Code);
        var wrongTenant = actor with { TenantId = Guid.NewGuid() };
        Assert.Equal("not_found", (await Assert.ThrowsAsync<DomainException>(() => service.Ageing(wrongTenant, company, customer,
            imported.GenerationId, Start, default))).Code);
    }
    [Fact]
    public async Task Concurrent_bootstrap_commits_once_and_audit_failure_rolls_back_all_evidence()
    {
        var actor = Operator(); var company = actor.CompanyIds.Single(); var export = Fixture(actor); var customer = export.Manifest.CustomerId;
        var key = Guid.NewGuid(); var request = new BootstrapCustomerRequest(export.Manifest.ExportId, "Atomic import");
        async Task<CustomerSnapshotResult> Import()
        { await using var db = database.Open(); return await Service(db).Bootstrap(actor, company, customer, key, request, new Source(export), default); }
        var results = await Task.WhenAll(Import(), Import()); Assert.Equal(results[0], results[1]);
        var other = Fixture(actor); var failedKey = Guid.NewGuid();
        await using (var failing = database.Open(new RejectAudit()))
            await Assert.ThrowsAsync<InvalidOperationException>(() => Service(failing).Bootstrap(actor, company, other.Manifest.CustomerId,
                failedKey, new(other.Manifest.ExportId, "Fail audit"), new Source(other), default));
        await using var check = database.Open();
        Assert.Equal(0, await check.Set<CustomerSnapshotGeneration>().CountAsync(x => x.ExportId == other.Manifest.ExportId));
        Assert.Equal(0, await check.Set<SnapshotReceipt>().CountAsync(x => x.Key == failedKey));
        Assert.Equal(1, await check.Set<CustomerSnapshotAudit>().CountAsync(x => x.CompanyId == company));
    }
    [Fact]
    public async Task Evidence_is_append_only_and_read_requires_report_permission()
    {
        var actor = Operator(); var company = actor.CompanyIds.Single(); var export = Fixture(actor); var customer = export.Manifest.CustomerId;
        await using var db = database.Open(); var service = Service(db);
        var result = await service.Bootstrap(actor, company, customer, Guid.NewGuid(), new(export.Manifest.ExportId, "Evidence"), new Source(export), default);
        var denied = actor with { Permissions = new HashSet<string>() };
        Assert.Equal("permission_denied", (await Assert.ThrowsAsync<DomainException>(() => service.Ageing(denied, company, customer, result.GenerationId, Start, default))).Code);
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM reporting.customer_snapshot_generations WHERE \"CompanyId\" = {company}"));
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlRawAsync("TRUNCATE reporting.customer_snapshot_audit"));
    }
    private sealed class RejectAudit : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (data.Context!.ChangeTracker.Entries<CustomerSnapshotAudit>().Any()) throw new InvalidOperationException("Injected audit failure");
            return base.SavingChangesAsync(data, result, ct);
        }
    }
}
