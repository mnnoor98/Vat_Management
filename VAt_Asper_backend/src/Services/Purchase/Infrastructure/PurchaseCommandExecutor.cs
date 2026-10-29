using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vat.Application.Security;
using Vat.Domain;
using Vat.Services.Purchase;

namespace Vat.Services.Purchase.Infrastructure;

public sealed class PurchaseCommandExecutor(PurchaseDbContext db, TimeProvider clock) : ICommandExecutor
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<StoredResponse> Execute(Actor actor, Guid companyId, string operation, string key,
        object request, Func<Task<CommandOutcome>> action, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        if (!Guid.TryParseExact(key, "D", out var keyId) || keyId == Guid.Empty)
            throw new DomainException("idempotency_key_required", "Idempotency-Key must be a non-empty UUID in D format.", 400);
        key = keyId.ToString("D");
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request, request.GetType(), Json)));
        var lockBytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{actor.TenantId}:{companyId}:{operation}:{key}"));
        var lockId = BinaryPrimitives.ReadInt64BigEndian(lockBytes);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({lockId})", ct);

        var existing = await db.Commands.SingleOrDefaultAsync(x =>
            x.TenantId == actor.TenantId && x.CompanyId == companyId && x.Operation == operation && x.Key == key, ct);

        if (existing is not null)
        {
            if (existing.RequestHash != hash)
                throw new DomainException("idempotency_key_reused", "Idempotency-Key has been used with a different request payload.", 422);
            await tx.RollbackAsync(ct);
            return new StoredResponse(existing.StatusCode, existing.ResponseJson, existing.Location, true);
        }

        var outcome = await action();
        var json = JsonSerializer.Serialize(outcome.Body, outcome.Body.GetType(), Json);
        db.Commands.Add(new PurchaseCommand
        {
            Id = Guid.NewGuid(),
            TenantId = actor.TenantId,
            CompanyId = companyId,
            Operation = operation,
            Key = key,
            Actor = actor.Subject,
            RequestHash = hash,
            StatusCode = outcome.StatusCode,
            ResponseJson = json,
            Location = outcome.Location,
            CreatedAt = clock.GetUtcNow()
        });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new StoredResponse(outcome.StatusCode, json, outcome.Location, false);
    }
}

public sealed class PurchaseRecordWriters(PurchaseDbContext db, TimeProvider clock) : IAuditWriter, IEventWriter
{
    public void Append(Actor actor, Guid companyId, Guid resourceId, string action, string correlationId,
        long? version = null, string? reason = null)
    {
        db.Audits.Add(new PurchaseAudit
        {
            Id = Guid.NewGuid(),
            TenantId = actor.TenantId,
            CompanyId = companyId,
            ResourceId = resourceId,
            Action = action,
            CorrelationId = correlationId,
            Version = version,
            Reason = reason,
            Actor = actor.Subject,
            OccurredAt = clock.GetUtcNow()
        });
    }

    public void Append<T>(Guid tenantId, Guid companyId, Guid aggregateId, long version,
        string eventType, string correlationId, T payload)
    {
        // Reserved for future messaging outbox
    }
}

