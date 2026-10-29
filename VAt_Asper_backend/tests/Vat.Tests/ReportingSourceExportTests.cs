using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vat.Application.Contracts;
using Vat.Application.Security;
using Vat.Application;
using Npgsql;
using Vat.Infrastructure.Persistence;
using Vat.Domain.Posting;
using Vat.Messaging.Contracts.V1;
using Xunit;

namespace Vat.Tests;

[Collection("PostgreSQL integration")]
public sealed class ReportingSourceExportTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> Host(bool reject = false) => factory.WithWebHostBuilder(builder =>
    {
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["ReportingSourceServer:Enabled"] = "true" }));
        if (reject) builder.ConfigureTestServices(services => services.AddDbContext<VatDbContext>(options => options.AddInterceptors(new RejectAudit())));
    });
    private HttpClient Client(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> host, Guid tenant, Guid company, string[]? permissions = null)
    {
        var client = host.CreateClient(); using var token = factory.Client(tenant, company, permissions: permissions);
        client.DefaultRequestHeaders.Authorization = token.DefaultRequestHeaders.Authorization; return client;
    }
    private static string Path(Guid company) => $"/internal/v1/reporting-source/companies/{company}/exports";
    private static async Task<HttpResponseMessage> Post(HttpClient client, string path, object body, Guid? key = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", (key ?? Guid.NewGuid()).ToString()); return await client.SendAsync(request);
    }
    private static async Task<T> Create<T>(HttpClient client, string path, object body)
    {
        using var response = await Post(client, path, body); response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
    private async Task<(Guid Tenant, Guid Company, Guid Purchase)> Seed()
    {
        var tenant = Guid.NewGuid(); var company = Guid.NewGuid(); var root = $"/api/v1/companies/{company}";
        using var maker = factory.Client(tenant, company); using var checker = factory.Client(tenant, company, "checker");
        await Create<CompanyResponse>(maker, "/api/v1/companies", new CreateCompanyRequest(company, "Export test", Guid.NewGuid().ToString()));
        await Create<PeriodResponse>(maker, root + "/periods", new CreatePeriodRequest("October", new(2026, 10, 1), new(2026, 11, 1)));
        var branch = await Create<BranchResponse>(maker, root + "/branches", new CreateBranchRequest("Test"));
        var warehouse = await Create<WarehouseResponse>(maker, root + "/warehouses", new CreateWarehouseRequest(branch.Id, "FROM", "Source"));
        await Create<ProductResponse>(maker, root + "/products", new CreateProductRequest("ITEM", "Product", "KG", "SYNTHETIC"));
        await Create<SupplierResponse>(maker, root + "/suppliers", new CreateSupplierRequest("SUP", "Test"));
        var rule = await Create<TaxRuleResponse>(maker, root + "/tax/rules", new CreateTaxRuleRequest("SYNTHETIC", new(2026, 1, 1), new(2027, 1, 1), 0, 0, "Synthetic fixture", "TEST"));
        await Create<TaxRuleResponse>(checker, root + $"/tax/rules/{rule.Id}/publish", new TransitionRequest(1));
        var purchase = await Create<PurchaseResponse>(maker, root + "/purchases", new CreatePurchaseRequest(branch.Id, Guid.NewGuid().ToString(), "SUP", new(2026, 10, 23), [new("ITEM", "Test", 100, 1)]));
        var path = root + $"/purchases/{purchase.Id}";
        await Create<PurchaseResponse>(maker, path + "/submit", new TransitionRequest(1));
        await Create<PurchaseResponse>(checker, path + "/approve", new TransitionRequest(2));
        var plan = await Create<PostingPlanResponse>(maker, path + "/posting-plans", new PreparePostingRequest(3, warehouse.Id));
        await Create<PurchaseResponse>(checker, path + $"/posting-plans/{plan.Id}/post", new TransitionRequest(4));
        return (tenant, company, purchase.Id);
    }

    [Fact]
    public async Task Journal_missing_committed_position_fails_closed_instead_of_returning_complete()
    {
        var s = await Seed();
        await factory.Inspect(async db =>
        {
            var actor = new Actor(s.Tenant, "reader", new HashSet<Guid> { s.Company }, new HashSet<string> { Permissions.ReportRead });
            var head = await db.Database.SqlQuery<ReportingSourceBoundaryV1>($"SELECT \"Epoch\", \"Sequence\", \"LegacyThroughSequence\" FROM platform.reporting_source_heads WHERE \"TenantId\"={s.Tenant} AND \"CompanyId\"={s.Company}").SingleAsync();
            await using var tx = await db.Database.BeginTransactionAsync();
            // Simulate owner-level evidence damage only inside this disposable DB transaction.
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE platform.reporting_source_journal DISABLE TRIGGER USER");
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM platform.reporting_source_journal WHERE \"TenantId\"={s.Tenant} AND \"CompanyId\"={s.Company}");
            var error = await Assert.ThrowsAsync<Vat.Domain.DomainException>(() => new ReportingSourceJournal(db).Read(actor, s.Company, head.Epoch, 0, 1, 100, default));
            Assert.Equal("source_sequence_gap", error.Code);
            await tx.RollbackAsync();
            Assert.Single((await new ReportingSourceJournal(db).Read(actor, s.Company, head.Epoch, 0, 1, 100, default)).Events);
            return true;
        });
    }

    [Fact]
    public async Task Journal_shortens_pages_by_bytes_and_never_skips_an_oversized_event()
    {
        var s = await Seed(); using var host = Host(); using var client = Client(host, s.Tenant, s.Company);
        var boundary = (await client.GetFromJsonAsync<ReportingSourceBoundaryV1>(Path(s.Company) + "/checkpoint"))!;
        async Task Add(int bytes)
        {
            await factory.Inspect(async db =>
            {
                var id = Guid.NewGuid(); var payload = JsonSerializer.Serialize(new { padding = new string('x', bytes) });
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO platform.outbox_messages ("Id", "TenantId", "CompanyId", "AggregateId", "AggregateVersion", "EventType", "Payload", "CreatedAt", "AttemptCount", "TotalAttempts")
                    VALUES ({id}, {s.Tenant}, {s.Company}, {id}, 1, 'FinancialEntryRecordedV1', {payload}::jsonb, now(), 0, 0)
                    """);
                return true;
            });
        }
        string Url(long after, long through) => Path(s.Company) + $"/journal?epoch={boundary.Epoch}&after={after}&through={through}";
        await Add(110 * 1024); await Add(110 * 1024);
        var first = (await client.GetFromJsonAsync<ReportingSourceJournalPageV1>(Url(0, 3)))!;
        Assert.Equal(2, first.Events.Count); Assert.False(first.Complete); Assert.Equal(2, first.NextAfterSequence);
        Assert.True(JsonSerializer.SerializeToUtf8Bytes(first, ReportingSourceV1.Json).Length <= ReportingSourceJournalV1.MaxPageBytes);
        var last = (await client.GetFromJsonAsync<ReportingSourceJournalPageV1>(Url(first.NextAfterSequence, 3)))!;
        Assert.True(last.Complete); Assert.Equal(3, Assert.Single(last.Events).Sequence);
        await Add(ReportingSourceJournalV1.MaxEventBytes + 1); await Add(10);
        var beforeOversized = (await client.GetFromJsonAsync<ReportingSourceJournalPageV1>(Url(2, 5)))!;
        Assert.Equal(3, beforeOversized.NextAfterSequence); Assert.False(beforeOversized.Complete);
        using var refused = await client.GetAsync(Url(beforeOversized.NextAfterSequence, 5));
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Contains("source_event_limit", await refused.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Journal_reads_fixed_boundary_original_envelopes_and_rejects_invalid_scope_or_cursor()
    {
        var s = await Seed(); using var host = Host(); using var client = Client(host, s.Tenant, s.Company);
        var boundary = (await client.GetFromJsonAsync<ReportingSourceBoundaryV1>(Path(s.Company) + "/checkpoint"))!;
        string Journal(Guid epoch, long after, long through, int take = 100) => Path(s.Company) + $"/journal?epoch={epoch}&after={after}&through={through}&take={take}";
        var url = Journal(boundary.Epoch, 0, 1);
        var page = (await client.GetFromJsonAsync<ReportingSourceJournalPageV1>(url))!;
        Assert.True(page.Complete); Assert.Equal(1, page.NextAfterSequence);
        Assert.Equal(s.Tenant, page.TenantId); Assert.Equal(s.Company, page.CompanyId); Assert.Equal(ReportingSourceV1.Scope, page.Scope);
        var original = await factory.Inspect(db => db.Outbox.AsNoTracking().SingleAsync(x => x.CompanyId == s.Company && x.EventType == nameof(FinancialEntryRecordedV1)));
        Assert.Equal(original.Id, Assert.Single(page.Events).EventId); Assert.Equal(original.Payload, page.Events[0].EnvelopeJson);
        Assert.Equal(1, page.Events[0].Sequence);
        var empty = (await client.GetFromJsonAsync<ReportingSourceJournalPageV1>(Journal(boundary.Epoch, 1, 1)))!;
        Assert.True(empty.Complete); Assert.Empty(empty.Events);
        Assert.Equal(HttpStatusCode.Conflict, (await client.GetAsync(Journal(Guid.NewGuid(), 0, 1))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.GetAsync(Journal(boundary.Epoch, 0, 2))).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.GetAsync(Journal(boundary.Epoch, -1, 1))).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.GetAsync(Journal(boundary.Epoch, 2, 1))).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.GetAsync(Journal(boundary.Epoch, 0, 1, 101))).StatusCode);
        using var other = Client(host, Guid.NewGuid(), s.Company); using var denied = Client(host, s.Tenant, s.Company, []);
        using var wrongCompany = Client(host, s.Tenant, Guid.NewGuid()); using var anonymous = host.CreateClient();
        Assert.Equal(HttpStatusCode.Conflict, (await other.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await denied.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await wrongCompany.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(url)).StatusCode);
        using var maker = factory.Client(s.Tenant, s.Company); using var checker = factory.Client(s.Tenant, s.Company, "checker");
        var reversals = $"/api/v1/companies/{s.Company}/purchases/{s.Purchase}/reversals";
        var reversal = await Create<ReversalResponse>(maker, reversals, new RequestReversalRequest(5, new(2026, 10, 24), "Journal paging"));
        await Create<PurchaseResponse>(checker, reversals + $"/{reversal.Id}/approve", new TransitionRequest(6));
        Assert.Equal(JsonSerializer.Serialize(page, ReportingSourceV1.Json),
            JsonSerializer.Serialize(await client.GetFromJsonAsync<ReportingSourceJournalPageV1>(url), ReportingSourceV1.Json));
        var first = (await client.GetFromJsonAsync<ReportingSourceJournalPageV1>(Journal(boundary.Epoch, 0, 2, 1)))!;
        Assert.False(first.Complete); Assert.Equal(1, first.NextAfterSequence);
        var last = (await client.GetFromJsonAsync<ReportingSourceJournalPageV1>(Journal(boundary.Epoch, first.NextAfterSequence, 2, 1)))!;
        Assert.True(last.Complete); Assert.Equal(2, Assert.Single(last.Events).Sequence);
    }

    [Fact]
    public async Task Source_sequence_serializes_concurrent_writers_and_reuses_rolled_back_position()
    {
        await factory.Inspect(async db =>
        {
            await using var first = new NpgsqlConnection(db.Database.GetConnectionString());
            await using var second = new NpgsqlConnection(db.Database.GetConnectionString());
            await first.OpenAsync(); await second.OpenAsync();
            var tenant = Guid.NewGuid(); var company = Guid.NewGuid();
            static async Task Insert(NpgsqlConnection connection, Guid tenant, Guid company)
            {
                await using var cmd = new NpgsqlCommand("""
                    INSERT INTO platform.outbox_messages ("Id", "TenantId", "CompanyId", "AggregateId", "AggregateVersion", "EventType", "Payload", "CreatedAt", "AttemptCount", "TotalAttempts")
                    VALUES (@id, @tenant, @company, @id, 1, 'FinancialEntryRecordedV1', '{}'::jsonb, now(), 0, 0)
                    """, connection);
                cmd.Parameters.AddWithValue("id", Guid.NewGuid()); cmd.Parameters.AddWithValue("tenant", tenant); cmd.Parameters.AddWithValue("company", company);
                await cmd.ExecuteNonQueryAsync();
            }
            await using var one = await first.BeginTransactionAsync();
            await Insert(first, tenant, company);
            await using var two = await second.BeginTransactionAsync();
            var waiting = Insert(second, tenant, company);
            try
            {
                var blocked = false;
                for (var i = 0; i < 100 && !waiting.IsCompleted; i++)
                {
                    await using var status = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM pg_locks WHERE pid = @pid AND NOT granted)", first);
                    status.Parameters.AddWithValue("pid", second.ProcessID);
                    if ((bool)(await status.ExecuteScalarAsync())!) { blocked = true; break; }
                    await Task.Delay(20);
                }
                Assert.True(blocked, "Second writer must wait for the first transaction to settle.");
            }
            finally { await one.CommitAsync(); }
            await waiting.WaitAsync(TimeSpan.FromSeconds(10));
            await two.RollbackAsync();
            await Insert(first, tenant, company);
            await using var count = new NpgsqlCommand("SELECT array_agg(\"Sequence\" ORDER BY \"Sequence\") FROM platform.reporting_source_journal WHERE \"TenantId\"=@tenant AND \"CompanyId\"=@company", first);
            count.Parameters.AddWithValue("tenant", tenant); count.Parameters.AddWithValue("company", company);
            Assert.Equal(new long[] { 1, 2 }, (long[])(await count.ExecuteScalarAsync())!);
            return true;
        });
    }

    [Fact]
    public async Task Source_checkpoint_is_durable_scoped_and_export_boundary_does_not_advance()
    {
        var s = await Seed(); using var host = Host(); using var client = Client(host, s.Tenant, s.Company);
        var manifest = await Create<ReportingExportManifestV1>(client, Path(s.Company), new PrepareReportingExportV1("Sequence boundary"));
        var boundary = (await client.GetFromJsonAsync<ReportingSourceBoundaryV1>(Path(s.Company) + $"/{manifest.ExportId}/boundary"))!;
        Assert.NotEqual(Guid.Empty, boundary.Epoch); Assert.Equal(1, boundary.Sequence); Assert.Equal(0, boundary.LegacyThroughSequence);
        Assert.Equal(boundary, await client.GetFromJsonAsync<ReportingSourceBoundaryV1>(Path(s.Company) + "/checkpoint"));
        await factory.Inspect(async db =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            var id = Guid.NewGuid();
            var payload = "{}";
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO platform.outbox_messages ("Id", "TenantId", "CompanyId", "AggregateId", "AggregateVersion", "EventType", "Payload", "CreatedAt", "AttemptCount", "TotalAttempts")
                VALUES ({id}, {s.Tenant}, {s.Company}, {id}, 1, 'FinancialEntryRecordedV1', {payload}::jsonb, now(), 0, 0)
                """);
            Assert.Equal(2, await db.Database.SqlQuery<long>($"SELECT \"Sequence\" AS \"Value\" FROM platform.reporting_source_heads WHERE \"TenantId\"={s.Tenant} AND \"CompanyId\"={s.Company}").SingleAsync());
            await tx.RollbackAsync(); return true;
        });
        Assert.Equal(boundary, await client.GetFromJsonAsync<ReportingSourceBoundaryV1>(Path(s.Company) + "/checkpoint"));
        using var stranger = Client(host, Guid.NewGuid(), s.Company);
        Assert.Equal(HttpStatusCode.Conflict, (await stranger.GetAsync(Path(s.Company) + "/checkpoint")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync(Path(s.Company) + $"/{manifest.ExportId}/boundary")).StatusCode);
        using var noPermission = Client(host, s.Tenant, s.Company, []);
        Assert.Equal(HttpStatusCode.Forbidden, (await noPermission.GetAsync(Path(s.Company) + "/checkpoint")).StatusCode);
        await factory.Inspect(async db =>
        {
            await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM platform.reporting_source_journal WHERE \"TenantId\"={s.Tenant} AND \"CompanyId\"={s.Company}"));
            return true;
        });
    }

    [Fact]
    public async Task Customer_export_requires_operator_permission_and_preserves_complete_empty_history()
    {
        var s = await Seed(); using var maker = factory.Client(s.Tenant, s.Company);
        var customer = await Create<CustomerResponse>(maker, $"/api/v1/companies/{s.Company}/customers", new CreateCustomerRequest("CUSTOMER", "Empty history"));
        using var host = Host(); using var client = Client(host, s.Tenant, s.Company);
        var path = $"/internal/v1/reporting-source/companies/{s.Company}/customers/{customer.Id}/exports";
        using var anonymous = host.CreateClient(); using var reader = Client(host, s.Tenant, s.Company, [Permissions.ReportRead]);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Post(anonymous, path, new PrepareReportingExportV1("Bootstrap"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(reader, path, new PrepareReportingExportV1("Bootstrap"))).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Post(client, path, new PrepareReportingExportV1(" "))).StatusCode);
        var manifest = await Create<CustomerHistoryManifestV1>(client, path, new PrepareReportingExportV1("Empty opening history"));
        var export = (await reader.GetFromJsonAsync<CustomerHistoryExportV1>(path + $"/{manifest.ExportId}"))!;
        Assert.True(export.Manifest.FullHistory); Assert.Equal(0, export.Manifest.RowCount);
        Assert.Equal(CustomerHistoryV1.Fingerprint(s.Tenant, s.Company, customer.Id, export.Data), manifest.FingerprintSha256);
        var verified = Vat.Services.Reporting.Domain.VerifiedCustomerHistory.Verify(export, s.Tenant, s.Company, customer.Id);
        Assert.Equal(0, Vat.Services.Reporting.Domain.CustomerHistoryProjection.Ageing(verified, new(2026, 10, 24)).Balance);
        using var forbidden = Client(host, s.Tenant, s.Company, []);
        Assert.Equal(HttpStatusCode.Forbidden, (await forbidden.GetAsync(path + $"/{manifest.ExportId}")).StatusCode);
        var financial = await Create<ReportingExportManifestV1>(client, Path(s.Company), new PrepareReportingExportV1("Financial scope"));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path + $"/{financial.ExportId}")).StatusCode);
    }

    [Fact]
    public async Task Customer_export_audit_failure_rolls_back_snapshot_and_command_receipt()
    {
        var s = await Seed(); using var maker = factory.Client(s.Tenant, s.Company);
        var customer = await Create<CustomerResponse>(maker, $"/api/v1/companies/{s.Company}/customers", new CreateCustomerRequest("CUSTOMER", "Atomic source"));
        var path = $"/internal/v1/reporting-source/companies/{s.Company}/customers/{customer.Id}/exports"; var key = Guid.NewGuid();
        using (var host = Host(true))
        using (var client = Client(host, s.Tenant, s.Company))
            Assert.Equal(HttpStatusCode.InternalServerError, (await Post(client, path, new PrepareReportingExportV1("Audit rollback"), key)).StatusCode);
        Assert.Equal(0, await factory.Inspect(db => db.Set<ReportingSourceExport>().CountAsync(x => x.CompanyId == s.Company)));
        Assert.Equal(0, await factory.Inspect(db => db.Idempotency.CountAsync(x => x.CompanyId == s.Company && x.Key == key.ToString())));
        using var good = Host(); using var authorized = Client(good, s.Tenant, s.Company);
        (await Post(authorized, path, new PrepareReportingExportV1("Audit rollback"), key)).EnsureSuccessStatusCode();
        Assert.Equal(1, await factory.Inspect(db => db.Set<ReportingSourceExport>().CountAsync(x => x.CompanyId == s.Company)));
    }

    [Fact]
    public async Task Snapshot_is_frozen_after_reversal_and_pages_reproduce_fingerprint_without_touching_delivery()
    {
        var s = await Seed(); using var host = Host(); using var client = Client(host, s.Tenant, s.Company);
        var original = await Create<ReportingExportManifestV1>(client, Path(s.Company), new PrepareReportingExportV1("Bootstrap"));
        var first = (await client.GetFromJsonAsync<ReportingExportPageV1>(Path(s.Company) + $"/{original.ExportId}/rows?take=1"))!;
        Assert.True(original.CoverageComplete); Assert.Equal(1, original.EventCount); Assert.Null(first.NextOffset);
        Assert.Equal(100m, Assert.Single(first.Rows).NetAmount);
        using var maker = factory.Client(s.Tenant, s.Company); using var checker = factory.Client(s.Tenant, s.Company, "checker");
        var path = $"/api/v1/companies/{s.Company}/purchases/{s.Purchase}/reversals";
        var reversal = await Create<ReversalResponse>(maker, path, new RequestReversalRequest(5, new(2026, 10, 24), "Return"));
        await Create<PurchaseResponse>(checker, path + $"/{reversal.Id}/approve", new TransitionRequest(6));
        var frozen = (await client.GetFromJsonAsync<ReportingExportPageV1>(Path(s.Company) + $"/{original.ExportId}/rows"))!;
        Assert.Equal(first.Rows, frozen.Rows);
        var next = await Create<ReportingExportManifestV1>(client, Path(s.Company), new PrepareReportingExportV1("After reversal"));
        var oldBoundary = (await client.GetFromJsonAsync<ReportingSourceBoundaryV1>(Path(s.Company) + $"/{original.ExportId}/boundary"))!;
        var newBoundary = (await client.GetFromJsonAsync<ReportingSourceBoundaryV1>(Path(s.Company) + $"/{next.ExportId}/boundary"))!;
        Assert.Equal(1, oldBoundary.Sequence); Assert.Equal(2, newBoundary.Sequence);
        Assert.Equal(oldBoundary.Epoch, newBoundary.Epoch);
        Assert.Equal(newBoundary, await client.GetFromJsonAsync<ReportingSourceBoundaryV1>(Path(s.Company) + "/checkpoint"));
        Assert.Equal(2, next.EventCount); Assert.Equal(1, next.ReconciledReversals); Assert.NotEqual(original.FingerprintSha256, next.FingerprintSha256);
        var page1 = (await client.GetFromJsonAsync<ReportingExportPageV1>(Path(s.Company) + $"/{next.ExportId}/rows?take=1"))!;
        var page2 = (await client.GetFromJsonAsync<ReportingExportPageV1>(Path(s.Company) + $"/{next.ExportId}/rows?offset={page1.NextOffset}&take=1"))!;
        var rows = page1.Rows.Concat(page2.Rows).ToArray(); Assert.Null(page2.NextOffset); Assert.Equal(0m, rows.Sum(x => x.NetAmount));
        Assert.Equal(next.FingerprintSha256, ReportingSourceV1.Fingerprint(s.Tenant, s.Company, rows));
        Assert.Equal(0, await factory.Inspect(db => db.Outbox.CountAsync(x => x.CompanyId == s.Company && (x.DispatchedAt != null || x.LeaseToken != null || x.TotalAttempts != 0))));
    }

    [Fact]
    public async Task Receipts_are_concurrent_safe_and_actor_payload_bound()
    {
        var s = await Seed(); using var host = Host(); using var client = Client(host, s.Tenant, s.Company); var key = Guid.NewGuid();
        var responses = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => Post(client, Path(s.Company), new PrepareReportingExportV1("Bootstrap"), key)));
        foreach (var response in responses) response.EnsureSuccessStatusCode();
        var manifests = await Task.WhenAll(responses.Select(x => x.Content.ReadFromJsonAsync<ReportingExportManifestV1>()));
        Assert.Single(manifests.Select(x => x!.ExportId).Distinct());
        Assert.Equal(2, responses.Count(x => x.Headers.GetValues("Idempotency-Replayed").Single() == "true"));
        Assert.Equal(HttpStatusCode.Conflict, (await Post(client, Path(s.Company), new PrepareReportingExportV1("Changed"), key)).StatusCode);
        Assert.Equal(1, await factory.Inspect(db => db.Set<ReportingSourceExport>().CountAsync(x => x.CompanyId == s.Company)));
        Assert.Equal(1, await factory.Inspect(db => db.Audit.CountAsync(x => x.CompanyId == s.Company && x.Action == "ReportingSourceExportPrepared")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Missing_or_conflicting_source_events_refuse_snapshot_and_receipt(bool corrupt)
    {
        var s = await Seed();
        var originalEvent = await factory.Inspect(db => db.Outbox.AsNoTracking().SingleAsync(x => x.CompanyId == s.Company && x.EventType == nameof(FinancialEntryRecordedV1)));
        await factory.Inspect(async db =>
        {
            var query = db.Outbox.Where(x => x.CompanyId == s.Company && x.EventType == nameof(FinancialEntryRecordedV1));
            if (corrupt) await query.ExecuteUpdateAsync(x => x.SetProperty(m => m.Payload, "{}")); else await query.ExecuteDeleteAsync();
            return 0;
        });
        using var host = Host(); using var client = Client(host, s.Tenant, s.Company);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(client, Path(s.Company), new PrepareReportingExportV1("Bootstrap"))).StatusCode);
        Assert.Equal(0, await factory.Inspect(db => db.Set<ReportingSourceExport>().CountAsync(x => x.CompanyId == s.Company)));
        Assert.Equal(0, await factory.Inspect(db => db.Idempotency.CountAsync(x => x.CompanyId == s.Company && x.Operation == "reporting-source.prepare-v1")));
        if (!corrupt)
        {
            using var owner = factory.Client(s.Tenant, s.Company);
            await Create<BackfillResponse>(owner, $"/api/v1/companies/{s.Company}/reports/backfill", new RetryMessageRequest("Verified missing legacy event"));
            var recovered = await Create<ReportingExportManifestV1>(client, Path(s.Company), new PrepareReportingExportV1("After backfill"));
            Assert.True(recovered.CoverageComplete); Assert.Equal(1, recovered.EventCount);
            var restoredEvent = await factory.Inspect(db => db.Outbox.AsNoTracking().SingleAsync(x => x.CompanyId == s.Company && x.EventType == nameof(FinancialEntryRecordedV1)));
            Assert.Equal(originalEvent.Id, restoredEvent.Id); Assert.Equal(originalEvent.Payload, restoredEvent.Payload);
            var boundary = (await client.GetFromJsonAsync<ReportingSourceBoundaryV1>(Path(s.Company) + $"/{recovered.ExportId}/boundary"))!;
            Assert.Equal(1, boundary.Sequence);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Ledger_gaps_or_oversized_source_refuse_export_before_writes(bool oversized)
    {
        var s = await Seed();
        await factory.Inspect(async db =>
        {
            if (oversized)
            {
                var payload = JsonSerializer.Serialize(new { padding = new string('x', ReportingSourceV1.MaxBytes / 4 + 1) });
                await db.Outbox.Where(x => x.CompanyId == s.Company && x.EventType == nameof(FinancialEntryRecordedV1))
                    .ExecuteUpdateAsync(x => x.SetProperty(m => m.Payload, payload));
            }
            else await db.Set<TaxLedgerEntry>().Where(x => x.CompanyId == s.Company).ExecuteDeleteAsync();
            return 0;
        });
        using var host = Host(); using var client = Client(host, s.Tenant, s.Company);
        var response = await Post(client, Path(s.Company), new PrepareReportingExportV1("Bootstrap"));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(oversized ? "export_limit" : "reconciliation_failed", problem.GetProperty("code").GetString());
        Assert.Equal(0, await factory.Inspect(db => db.Set<ReportingSourceExport>().CountAsync(x => x.CompanyId == s.Company)));
    }

    [Fact]
    public async Task Audit_failure_rolls_back_snapshot_and_can_retry()
    {
        var s = await Seed(); var key = Guid.NewGuid();
        using (var host = Host(true))
        using (var client = Client(host, s.Tenant, s.Company))
            Assert.Equal(HttpStatusCode.InternalServerError, (await Post(client, Path(s.Company), new PrepareReportingExportV1("Bootstrap"), key)).StatusCode);
        Assert.Equal(0, await factory.Inspect(db => db.Set<ReportingSourceExport>().CountAsync(x => x.CompanyId == s.Company)));
        using var good = Host(); using var authorized = Client(good, s.Tenant, s.Company);
        (await Post(authorized, Path(s.Company), new PrepareReportingExportV1("Bootstrap"), key)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Permissions_tenant_scope_page_and_reason_bounds_are_enforced()
    {
        var s = await Seed(); using var host = Host(); using var client = Client(host, s.Tenant, s.Company);
        using var anonymous = host.CreateClient(); using var forbidden = Client(host, s.Tenant, s.Company, []);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Post(anonymous, Path(s.Company), new PrepareReportingExportV1("Bootstrap"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(forbidden, Path(s.Company), new PrepareReportingExportV1("Bootstrap"))).StatusCode);
        using var wrongCompany = Client(host, s.Tenant, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(wrongCompany, Path(s.Company), new PrepareReportingExportV1("Bootstrap"))).StatusCode);
        var manifest = await Create<ReportingExportManifestV1>(client, Path(s.Company), new PrepareReportingExportV1("Bootstrap"));
        using var wrongTenant = Client(host, Guid.NewGuid(), s.Company);
        Assert.Equal(HttpStatusCode.NotFound, (await wrongTenant.GetAsync(Path(s.Company) + $"/{manifest.ExportId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await forbidden.GetAsync(Path(s.Company) + $"/{manifest.ExportId}")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.GetAsync(Path(s.Company) + $"/{manifest.ExportId}/rows?take=101")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.GetAsync(Path(s.Company) + $"/{manifest.ExportId}/rows?offset=-1")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Post(client, Path(s.Company), new PrepareReportingExportV1(new string(' ', 1001) + "x"))).StatusCode);
    }
    [Fact]
    public async Task Real_purchase_and_reversal_export_matches_an_independent_owned_snapshot_database()
    {
        var s = await Seed(); using var host = Host(); using var client = Client(host, s.Tenant, s.Company);
        using var maker = factory.Client(s.Tenant, s.Company); using var checker = factory.Client(s.Tenant, s.Company, "checker");
        var initialManifest = await Create<ReportingExportManifestV1>(client, Path(s.Company), new PrepareReportingExportV1("Baseline before live reversal"));
        var reversalPath = $"/api/v1/companies/{s.Company}/purchases/{s.Purchase}/reversals";
        var reversal = await Create<ReversalResponse>(maker, reversalPath, new RequestReversalRequest(5, new(2026, 10, 24), "Parity fixture"));
        await Create<PurchaseResponse>(checker, reversalPath + $"/{reversal.Id}/approve", new TransitionRequest(6));
        var manifest = await Create<ReportingExportManifestV1>(client, Path(s.Company), new PrepareReportingExportV1("Owned snapshot parity"));
        var messages = await factory.Inspect(db => db.Outbox.AsNoTracking().Where(x => x.CompanyId == s.Company && x.EventType == nameof(FinancialEntryRecordedV1)).ToArrayAsync());
        foreach (var message in messages)
        {
            using var scope = factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IIncomingEventProcessor>().Process(new(Encoding.UTF8.GetBytes(message.Payload), message.Id.ToString(), message.EventType, "application/json"), default);
        }

        var databaseName = "reporting_parity_" + Guid.NewGuid().ToString("N");
        var adminConnection = Environment.GetEnvironmentVariable("VAT_TEST_POSTGRES")!;
        await using var admin = new NpgsqlConnection(adminConnection); await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            var connection = new NpgsqlConnectionStringBuilder(adminConnection) { Database = databaseName, Pooling = false }.ConnectionString;
            var options = new DbContextOptionsBuilder<Vat.Services.Reporting.Infrastructure.ReportingDbContext>();
            Vat.Services.Reporting.Infrastructure.ReportingPersistence.Configure(options, connection);
            await using var owned = new Vat.Services.Reporting.Infrastructure.ReportingDbContext(options.Options); await owned.Database.MigrateAsync();
            var actor = new Actor(s.Tenant, "maker", new HashSet<Guid> { s.Company }, new HashSet<string> { Permissions.ReportRead, Permissions.ReportManage });
            var service = new Vat.Services.Reporting.Application.ReportingGenerationService(new Vat.Services.Reporting.Infrastructure.ReportingGenerationStore(owned, TimeProvider.System));
            using var source = new Vat.Services.Reporting.Infrastructure.ReportingExportHttpSource("http://localhost/", client.DefaultRequestHeaders.Authorization!.Parameter!, true, client);
            var candidate = await service.Bootstrap(actor, s.Company, Guid.NewGuid(), new(manifest.ExportId, "Compare reconciled history"), source, default);
            await service.ActivateSnapshot(actor, s.Company, Guid.NewGuid(), new(candidate.GenerationId, 0, "Verified snapshot only"), default);
            foreach (var dates in new[] { (From: new DateOnly(2026, 10, 23), Until: new DateOnly(2026, 10, 24)),
                         (From: new DateOnly(2026, 10, 24), Until: new DateOnly(2026, 10, 25)),
                         (From: new DateOnly(2026, 10, 1), Until: new DateOnly(2026, 11, 1)) })
            {
                var legacy = (await maker.GetFromJsonAsync<FinancialReportResponse>($"/api/v1/companies/{s.Company}/reports/financial-summary?from={dates.From:yyyy-MM-dd}&until={dates.Until:yyyy-MM-dd}"))!;
                var snapshot = await service.ReadSnapshot(actor, s.Company, dates.From, dates.Until, default); var totals = Assert.Single(snapshot.Totals);
                Assert.Equal(legacy.TransactionCount, totals.EntryCount); Assert.Equal(legacy.NetAmount, totals.NetAmount);
                Assert.Equal(legacy.TaxAmount, totals.TaxAmount); Assert.Equal(legacy.RecoverableTaxAmount, totals.RecoverableTaxAmount);
                Assert.False(snapshot.Snapshot.LiveCoverageVerified);
            }
            Assert.Equal(manifest.FingerprintSha256, candidate.FingerprintSha256);
            var liveInbox = new Vat.Services.Reporting.Infrastructure.FinancialGenerationInbox(owned, TimeProvider.System);
            await liveInbox.Open(actor, s.Company, Guid.NewGuid(), new(candidate.GenerationId, "Verify real envelope overlap"), default);
            foreach (var message in messages)
            {
                var delivery = InboxDeliveryV1.Create(new(Encoding.UTF8.GetBytes(message.Payload), message.Id.ToString(), message.EventType, "application/json"));
                var receipt = await liveInbox.Deliver(s.Tenant, s.Company, candidate.GenerationId, delivery, default);
                Assert.Equal("Duplicate", receipt.Status); Assert.True(receipt.IsCommittedFor(candidate.GenerationId, delivery));
            }
            var liveSummary = await liveInbox.Summary(actor, s.Company, candidate.GenerationId, new(2026, 10, 1), new(2026, 11, 1), default);
            Assert.Equal(0, Assert.Single(liveSummary.Totals).NetAmount); Assert.Equal(2, liveSummary.Totals[0].EntryCount);
            Assert.False(liveSummary.LiveCoverageVerified);
            // A second generation starts before reversal, then consumes the real later event.
            var streaming = await service.Bootstrap(actor, s.Company, Guid.NewGuid(), new(initialManifest.ExportId, "Earlier baseline"), source, default);
            await liveInbox.Open(actor, s.Company, Guid.NewGuid(), new(streaming.GenerationId, "Receive real later reversal"), default);
            foreach (var message in messages)
                await liveInbox.Deliver(s.Tenant, s.Company, streaming.GenerationId,
                    InboxDeliveryV1.Create(new(Encoding.UTF8.GetBytes(message.Payload), message.Id.ToString(), message.EventType, "application/json")), default);
            var coverage = new Vat.Services.Reporting.Application.FinancialCoverageService(new Vat.Services.Reporting.Infrastructure.FinancialCoverageStore(owned, TimeProvider.System));
            var proof = await coverage.Verify(actor, s.Company, Guid.NewGuid(), new(streaming.GenerationId, manifest.ExportId, 0, "Match complete source event set"), source, default);
            Assert.True(proof.ExportCoverageVerified); Assert.False(proof.LiveCoverageVerified); Assert.Equal(2, proof.RowCount);
            Assert.Equal(manifest.FingerprintSha256, proof.FingerprintSha256);
            Assert.True((await coverage.Status(actor, s.Company, streaming.GenerationId, default)).ProjectionMatchesVerifiedExport);
            Assert.Equal(3, await owned.Set<Vat.Services.Reporting.Infrastructure.SnapshotEntry>().CountAsync());
            Assert.Equal(1, await owned.Set<Vat.Services.Reporting.Infrastructure.FinancialLiveEntry>().CountAsync());
            Assert.Equal(0, await owned.Database.SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM information_schema.tables WHERE table_schema NOT IN ('reporting','public','pg_catalog','information_schema')").SingleAsync());
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\"", admin) { CommandTimeout = 120 }; await drop.ExecuteNonQueryAsync();
        }
    }
    private sealed class RejectAudit : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (data.Context!.ChangeTracker.Entries<AuditEntry>().Any(x => x.Entity.Action is "ReportingSourceExportPrepared" or "CustomerHistoryExportPrepared"))
                throw new InvalidOperationException("Injected export audit failure.");
            return base.SavingChangesAsync(data, result, ct);
        }
    }
}
