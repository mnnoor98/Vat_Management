using System.Buffers.Binary;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vat.Application.Security;
using Vat.Domain;
using Vat.Domain.Sales;
using Vat.Services.Sales;

namespace Vat.Services.Sales.Infrastructure;

public sealed class SalesRecordWriters(SalesDbContext db, TimeProvider clock) : IAuditWriter, IEventWriter
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public void Append(Actor actor, Guid companyId, Guid resourceId, string action, string correlationId,
        long? version = null, string? reason = null)
    {
        db.Audits.Add(new SalesAudit
        {
            Id = Guid.NewGuid(),
            TenantId = actor.TenantId,
            CompanyId = companyId,
            ResourceId = resourceId,
            Actor = actor.Subject,
            Action = action,
            Reason = reason,
            CorrelationId = correlationId,
            Version = version,
            OccurredAt = clock.GetUtcNow()
        });
    }

    public void Append<T>(Guid tenantId, Guid companyId, Guid aggregateId, long version,
        string eventType, string correlationId, T payload)
    {
        db.Outbox.Add(new SalesOutbox
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            AggregateId = aggregateId,
            AggregateVersion = version,
            EventType = eventType,
            CorrelationId = correlationId,
            Payload = JsonSerializer.Serialize(payload, Json),
            CreatedAt = clock.GetUtcNow()
        });
    }
}

public sealed class SalesCommandExecutor(SalesDbContext db, TimeProvider clock) : ICommandExecutor
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
        db.Commands.Add(new SalesCommand
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

public sealed class SalesStore(SalesDbContext db) : ISalesInvoiceStore
{
    public Task<SalesInvoice?> Find(Guid tenantId, Guid companyId, Guid invoiceId, CancellationToken ct) =>
        db.Invoices.Include(x => x.Lines).SingleOrDefaultAsync(x =>
            x.TenantId == tenantId && x.CompanyId == companyId && x.Id == invoiceId, ct);

    public async Task<IReadOnlyList<SalesInvoice>> List(Guid tenantId, Guid companyId, Guid? before, int take, CancellationToken ct)
    {
        var query = db.Invoices.AsNoTracking().Where(x => x.TenantId == tenantId && x.CompanyId == companyId);
        if (before is not null) query = query.Where(x => x.Id.CompareTo(before.Value) < 0);
        return await query.OrderByDescending(x => x.Id).Take(take).Include(x => x.Lines).ToArrayAsync(ct);
    }

    public void Add(SalesInvoice invoice) => db.Invoices.Add(invoice);
}

