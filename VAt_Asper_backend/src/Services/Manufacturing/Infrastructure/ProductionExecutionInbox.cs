using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vat.Application;
using Vat.Application.Contracts;
using Vat.Contracts.Manufacturing;
using Vat.Services.Manufacturing.Domain;

namespace Vat.Services.Manufacturing.Infrastructure;

internal sealed record ProductionIntegrationEvent(Guid EventId,string EventType,int SchemaVersion,DateTimeOffset OccurredAt,Guid TenantId,Guid CompanyId,Guid AggregateId,long AggregateVersion,string CorrelationId,JsonElement Payload);

public sealed class ProductionExecutionInboxProcessor(ManufacturingDbContext db, TimeProvider clock) : IIncomingEventProcessor
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = false, MaxDepth = 32 };
    public async Task<ProcessedMessage> Process(ReceivedMessage message, CancellationToken ct)
    {
        var hash = Convert.ToHexString(SHA256.HashData(message.Body));
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{message.MessageId}|{message.EventType}|{message.ContentType}|{hash}")));
        ProductionIntegrationEvent? envelope = null; string? error = null;
        ProductionExecutionAcceptedV1? accepted = null; ProductionCostPostedV1? posted = null;
        try
        {
            if (message.Body.Length > 1_048_576) error = "message_too_large";
            else envelope = JsonSerializer.Deserialize<ProductionIntegrationEvent>(message.Body, Json);
            if (error is null && (envelope is null || envelope.EventId == Guid.Empty || envelope.TenantId == Guid.Empty || envelope.CompanyId == Guid.Empty || envelope.AggregateId == Guid.Empty || envelope.SchemaVersion != 1)) error = "invalid_envelope";
            if (error is null && (message.ContentType != "application/json" || message.MessageId != envelope!.EventId.ToString() || message.EventType != envelope.EventType)) error = "metadata_mismatch";
            if (error is null && envelope!.EventType == nameof(ProductionExecutionAcceptedV1)) accepted = envelope.Payload.Deserialize<ProductionExecutionAcceptedV1>(Json);
            else if (error is null && envelope!.EventType == nameof(ProductionCostPostedV1)) posted = envelope.Payload.Deserialize<ProductionCostPostedV1>(Json);
            else if (error is null) return new("Ignored");
            if (error is null && accepted is not null && (accepted.SessionId != envelope!.AggregateId || accepted.OrderId == Guid.Empty || accepted.SourceSha256.Length != 64)) error = "invalid_execution_payload";
            if (error is null && posted is not null && (posted.SessionId != envelope!.AggregateId || posted.OrderId == Guid.Empty || posted.OperationId == Guid.Empty || posted.SessionVersion < 1 || posted.Kind is not ("Issue" or "Return" or "Conversion" or "Receipt" or "ByProduct" or "Loss" or "Close" or "Reopen" or "Reverse") || !Exact(posted.WipValue,4) || !Exact(posted.ReceivedQuantity,6))) error = "invalid_execution_payload";
        }
        catch (JsonException) { error = "invalid_json"; }

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await Lock(error is null ? $"execution-event:{envelope!.EventId}" : $"execution-dead:{fingerprint}", ct);
        if (error is not null)
        {
            var dead = await db.ExecutionDeadLetters.SingleOrDefaultAsync(x=>x.Fingerprint==fingerprint,ct);
            if (dead is null) { dead=new(){Id=Guid.CreateVersion7(),EventId=envelope?.EventId,TenantId=envelope?.TenantId,CompanyId=envelope?.CompanyId,Fingerprint=fingerprint,Body=message.Body[..Math.Min(message.Body.Length,1_048_576)],FirstSeenAt=clock.GetUtcNow()};db.Add(dead); }
            dead.ErrorCode=error;dead.LastSeenAt=clock.GetUtcNow();dead.Occurrences++;await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return new("Quarantined",dead.Id);
        }
        var validEnvelope=envelope!;
        var prior=await db.ExecutionInbox.SingleOrDefaultAsync(x=>x.TenantId==validEnvelope.TenantId&&x.CompanyId==validEnvelope.CompanyId&&x.EventId==validEnvelope.EventId,ct);
        if(prior is not null){if(prior.PayloadSha256!=hash){await Quarantine(fingerprint,"event_id_payload_conflict",message,validEnvelope,ct);await tx.CommitAsync(ct);return new("Quarantined",(await db.ExecutionDeadLetters.SingleAsync(x=>x.Fingerprint==fingerprint,ct)).Id);}await tx.CommitAsync(ct);return new("Duplicate");}
        var orderId=accepted?.OrderId??posted!.OrderId;var order=await db.Orders.SingleOrDefaultAsync(x=>x.TenantId==validEnvelope.TenantId&&x.CompanyId==validEnvelope.CompanyId&&x.Id==orderId,ct);
        if(order is null){await Quarantine(fingerprint,"unknown_production_order",message,validEnvelope,ct);await tx.CommitAsync(ct);return new("Quarantined",(await db.ExecutionDeadLetters.SingleAsync(x=>x.Fingerprint==fingerprint,ct)).Id);}
        var checkpoint=await db.ExecutionCheckpoints.SingleOrDefaultAsync(x=>x.TenantId==validEnvelope.TenantId&&x.CompanyId==validEnvelope.CompanyId&&x.OrderId==orderId,ct);
        var session=accepted?.SessionId??posted!.SessionId;var version=posted?.SessionVersion??0L;var status="Processed";
        if(checkpoint is null){checkpoint=new(){TenantId=validEnvelope.TenantId,CompanyId=validEnvelope.CompanyId,OrderId=orderId,SessionId=session,SessionVersion=version,ExecutionState=posted?.Kind=="Close"?"Completed":"Running",WipValue=posted?.WipValue??"0.0000",ReceivedQuantity=posted?.ReceivedQuantity??"0.000000",UpdatedAt=clock.GetUtcNow()};db.Add(checkpoint);}
        else if(checkpoint.SessionId!=session){await Quarantine(fingerprint,"execution_session_conflict",message,validEnvelope,ct);await tx.CommitAsync(ct);return new("Quarantined",(await db.ExecutionDeadLetters.SingleAsync(x=>x.Fingerprint==fingerprint,ct)).Id);}
        else if(posted is not null && posted.SessionVersion>checkpoint.SessionVersion){checkpoint.SessionVersion=posted.SessionVersion;checkpoint.ExecutionState=posted.Kind=="Close"?"Completed":"Running";checkpoint.WipValue=posted.WipValue;checkpoint.ReceivedQuantity=posted.ReceivedQuantity;checkpoint.UpdatedAt=clock.GetUtcNow();}
        else if(posted is not null) status="Ignored";
        order.State=checkpoint.ExecutionState;order.UpdatedAt=clock.GetUtcNow();
        db.Add(new ProductionExecutionInbox{TenantId=validEnvelope.TenantId,CompanyId=validEnvelope.CompanyId,EventId=validEnvelope.EventId,OrderId=orderId,SessionId=session,OperationId=posted?.OperationId,EventType=validEnvelope.EventType,PayloadSha256=hash,SessionVersion=version,Status=status,OccurredAt=validEnvelope.OccurredAt,ProcessedAt=clock.GetUtcNow()});
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return new(status);
    }
    private async Task Quarantine(string fingerprint,string code,ReceivedMessage message,ProductionIntegrationEvent envelope,CancellationToken ct){var dead=await db.ExecutionDeadLetters.SingleOrDefaultAsync(x=>x.Fingerprint==fingerprint,ct);if(dead is null){dead=new(){Id=Guid.CreateVersion7(),EventId=envelope.EventId,TenantId=envelope.TenantId,CompanyId=envelope.CompanyId,Fingerprint=fingerprint,Body=message.Body[..Math.Min(message.Body.Length,1_048_576)],FirstSeenAt=clock.GetUtcNow()};db.Add(dead);}dead.ErrorCode=code;dead.LastSeenAt=clock.GetUtcNow();dead.Occurrences++;await db.SaveChangesAsync(ct);}
    private Task Lock(string scope,CancellationToken ct)=>db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({BinaryPrimitives.ReadInt64BigEndian(SHA256.HashData(Encoding.UTF8.GetBytes(scope)))})",ct);
    private static bool Exact(string value,int scale)=>decimal.TryParse(value,System.Globalization.NumberStyles.AllowDecimalPoint,System.Globalization.CultureInfo.InvariantCulture,out var number)&&number>=0&&decimal.Round(number,scale)==number;
}
