using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vat.Application;
using Vat.Application.Security;
using Vat.Domain;

namespace Vat.Infrastructure.Persistence;

public sealed class CommandExecutor(VatDbContext db, TimeProvider clock) : ICommandExecutor
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

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Serialize retries across API replicas; released automatically on rollback/commit.
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({lockId})", ct);
        var previous = await db.Idempotency.SingleOrDefaultAsync(x => x.TenantId == actor.TenantId &&
            x.CompanyId == companyId && x.Operation == operation && x.Key == key, ct);
        if (previous is not null)
        {
            if (previous.ActorId != actor.Subject)
                throw new DomainException("idempotency_owner_conflict", "This key belongs to a different actor.", 409);
            if (previous.RequestHash != hash)
                throw new DomainException("idempotency_payload_conflict", "This key was already used with a different request.", 409);
            await transaction.CommitAsync(ct);
            return new(previous.StatusCode, previous.ResponseJson, previous.Location, true);
        }

        var outcome = await action();
        var response = JsonSerializer.Serialize(outcome.Body, outcome.Body.GetType(), Json);
        db.Idempotency.Add(new IdempotencyRecord
        {
            TenantId = actor.TenantId, CompanyId = companyId, Operation = operation, Key = key,
            ActorId = actor.Subject, RequestHash = hash, StatusCode = outcome.StatusCode,
            ResponseJson = response, Location = outcome.Location, CreatedAt = clock.GetUtcNow()
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(outcome.StatusCode, response, outcome.Location);
    }
}
