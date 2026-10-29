using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vat.Application.Security;
using Vat.Domain;
using Vat.Messaging.Contracts.V1;
using Vat.Services.Reporting.Application;
using Vat.Services.Reporting.Domain;

namespace Vat.Services.Reporting.Infrastructure;

public sealed class CustomerSnapshotGeneration
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid Id { get; set; }
    public Guid ExportId { get; set; }
    public string ExportJson { get; set; } = "";
    public string Fingerprint { get; set; } = "";
    public DateTimeOffset CapturedAt { get; set; }
    public DateTimeOffset ImportedAt { get; set; }
}
public sealed class CustomerSnapshotAudit
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid Id { get; set; }
    public Guid GenerationId { get; set; }
    public Guid CommandKey { get; set; }
    public string ActorId { get; set; } = "";
    public string Reason { get; set; } = "";
    public DateTimeOffset OccurredAt { get; set; }
}
public sealed class CustomerSnapshotStore(ReportingDbContext db, TimeProvider clock) : ICustomerSnapshotStore
{
    private const string Operation = "customer.snapshot.bootstrap-v1";
    public async Task<CustomerSnapshotResult?> Previous(Actor actor, Guid company, Guid customer, Guid key, BootstrapCustomerRequest request, CancellationToken ct)
    {
        actor.RequireCompany(company);
        var receipt = await db.Set<SnapshotReceipt>().AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == actor.TenantId
            && x.CompanyId == company && x.Operation == Operation && x.Key == key, ct);
        if (receipt is null) return null;
        if (receipt.ActorId != actor.Subject || receipt.RequestHash != Hash(customer, request))
            throw new DomainException("snapshot_command_conflict", "The command key belongs to another actor, customer or request.", 409);
        return JsonSerializer.Deserialize<CustomerSnapshotResult>(receipt.ResponseJson, ReportingSourceV1.Json)!;
    }
    public async Task<CustomerSnapshotResult> Import(Actor actor, Guid company, Guid customer, Guid key,
        BootstrapCustomerRequest request, VerifiedCustomerHistory history, CancellationToken ct)
    {
        actor.RequireCompany(company);
        var manifest = history.Manifest;
        if (manifest.TenantId != actor.TenantId || manifest.CompanyId != company || manifest.CustomerId != customer || manifest.ExportId != request.ExportId)
            throw new DomainException("invalid_customer_history", "Customer export scope differs from the command.", 409);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var lockKey = BinaryPrimitives.ReadInt64BigEndian(SHA256.HashData(Encoding.UTF8.GetBytes($"customer-snapshot:{actor.TenantId}:{company}")));
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({lockKey})", ct);
        var previous = await Previous(actor, company, customer, key, request, ct);
        if (previous is not null) { await transaction.CommitAsync(ct); return previous; }
        var json = JsonSerializer.Serialize(new CustomerHistoryExportV1(manifest, history.Data), ReportingSourceV1.Json);
        var generation = await db.Set<CustomerSnapshotGeneration>().SingleOrDefaultAsync(x => x.TenantId == actor.TenantId
            && x.CompanyId == company && x.ExportId == request.ExportId, ct);
        if (generation is not null && generation.ExportJson != json)
            throw new DomainException("source_export_conflict", "An immutable customer export changed.", 409);
        if (generation is null)
        {
            generation = new() { TenantId = actor.TenantId, CompanyId = company, CustomerId = customer, Id = Guid.NewGuid(),
                ExportId = manifest.ExportId, ExportJson = json, Fingerprint = manifest.FingerprintSha256,
                CapturedAt = manifest.CapturedAt.ToUniversalTime(), ImportedAt = clock.GetUtcNow() };
            db.Add(generation);
        }
        var result = Result(generation);
        db.Add(new CustomerSnapshotAudit { TenantId = actor.TenantId, CompanyId = company, CustomerId = customer,
            Id = Guid.NewGuid(), GenerationId = generation.Id, CommandKey = key, ActorId = actor.Subject,
            Reason = request.Reason.Trim(), OccurredAt = clock.GetUtcNow() });
        db.Add(new SnapshotReceipt { TenantId = actor.TenantId, CompanyId = company, Operation = Operation, Key = key,
            ActorId = actor.Subject, RequestHash = Hash(customer, request), ResponseJson = JsonSerializer.Serialize(result, ReportingSourceV1.Json) });
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return result;
    }
    public async Task<(CustomerSnapshotResult Snapshot, VerifiedCustomerHistory History)> Read(Actor actor, Guid company, Guid customer, Guid generation, CancellationToken ct)
    {
        actor.RequireCompany(company);
        var stored = await db.Set<CustomerSnapshotGeneration>().AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == actor.TenantId
            && x.CompanyId == company && x.CustomerId == customer && x.Id == generation, ct)
            ?? throw new DomainException("not_found", "Customer snapshot not found.", 404);
        var export = JsonSerializer.Deserialize<CustomerHistoryExportV1>(stored.ExportJson, ReportingSourceV1.Json)!;
        return (Result(stored), VerifiedCustomerHistory.Verify(export, actor.TenantId, company, customer));
    }
    private static CustomerSnapshotResult Result(CustomerSnapshotGeneration x) => new(x.Id, x.ExportId, x.CustomerId, x.Fingerprint, x.CapturedAt);
    private static string Hash(Guid customer, BootstrapCustomerRequest request) => Convert.ToHexString(SHA256.HashData(
        JsonSerializer.SerializeToUtf8Bytes(new { customer, request }, ReportingSourceV1.Json)));
}
