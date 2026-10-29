using System.Text.Json;
using Vat.Application;
using Vat.Application.Contracts;
using Vat.Application.Security;

namespace Vat.Infrastructure.Persistence;

public sealed class RecordWriters(VatDbContext db, TimeProvider clock) : IEventWriter, IAuditWriter
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public void Append<T>(Guid tenantId, Guid companyId, Guid aggregateId, long version,
        string eventType, string correlationId, T payload)
    {
        var id = Guid.CreateVersion7();
        var now = clock.GetUtcNow();
        db.Outbox.Add(new OutboxMessage
        {
            Id = id, TenantId = tenantId, CompanyId = companyId, AggregateId = aggregateId,
            AggregateVersion = version, EventType = eventType, CreatedAt = now,
            Payload = JsonSerializer.Serialize(new IntegrationEvent<T>(id, eventType, 1, now,
                tenantId, companyId, aggregateId, version, correlationId, payload), Json)
        });
    }

    public void Append(Actor actor, Guid companyId, Guid resourceId, string action,
        string correlationId, long? version = null, string? reason = null) => db.Audit.Add(new AuditEntry
    {
        Id = Guid.CreateVersion7(), TenantId = actor.TenantId, CompanyId = companyId,
        ResourceId = resourceId, ActorId = actor.Subject, Action = action,
        CorrelationId = correlationId, DocumentVersion = version, Reason = reason, OccurredAt = clock.GetUtcNow()
    });
}
