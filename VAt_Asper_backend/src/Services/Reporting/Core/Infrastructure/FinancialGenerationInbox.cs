using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vat.Application.Security;
using Vat.Domain;
using Vat.Messaging.Contracts.V1;
using Vat.Services.Reporting.Application;
using Vat.Services.Reporting.Domain;

namespace Vat.Services.Reporting.Infrastructure;

// Staged financial-only inbox. Arrival time/count never implies source completeness.
public sealed class FinancialGenerationInbox(ReportingDbContext db, TimeProvider clock) : IFinancialGenerationInbox
{
    private static readonly JsonSerializerOptions Json = ReportingSourceV1.Json;
    public Task<FinancialInboxStateResult> Open(Actor actor, Guid company, Guid key, OpenFinancialInboxRequest request, CancellationToken ct)
        => StateCommand(actor, company, key, request.GenerationId, request.Reason, "financial.inbox.open-v1", request, null, ct);
    public Task<FinancialInboxStateResult> Retire(Actor actor, Guid company, Guid key, RetireFinancialInboxRequest request, CancellationToken ct)
        => StateCommand(actor, company, key, request.GenerationId, request.Reason, "financial.inbox.retire-v1", request, request.ExpectedVersion, ct);

    private async Task<FinancialInboxStateResult> StateCommand(Actor actor, Guid company, Guid key, Guid generation, string reason,
        string operation, object request, long? expected, CancellationToken ct)
    {
        Require(actor, company, key, reason, Permissions.ReportManage);
        if (generation == Guid.Empty || expected < 1) throw new DomainException("validation_failed", "Supply a generation and positive expected version.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct); await Lock(actor.TenantId, company, ct);
        var previous = await Previous<FinancialInboxStateResult>(actor, company, operation, key, request, ct);
        if (previous is not null) { await transaction.CommitAsync(ct); return previous; }
        if (!await db.Set<SnapshotGeneration>().AnyAsync(x => x.TenantId == actor.TenantId && x.CompanyId == company && x.Id == generation, ct))
            throw new DomainException("not_found", "Verified baseline generation not found.", 404);
        var state = await State(actor.TenantId, company, generation, ct);
        if (expected is null)
        {
            if (state?.Status == "Retired") throw new DomainException("generation_retired", "A retired inbox cannot reopen.", 409);
            if (state is null) { state = new() { TenantId = actor.TenantId, CompanyId = company, GenerationId = generation }; db.Add(state); }
        }
        else
        {
            if (state is null || state.Status != "Open" || state.Version != expected)
                throw new DomainException("generation_version_conflict", "Reload the inbox generation version.", 409);
            state.Status = "Retired"; state.Version = checked(state.Version + 1);
        }
        var result = new FinancialInboxStateResult(generation, state.Status, state.Version);
        Record(actor, company, generation, key, reason, operation, request, result);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return result;
    }

    public async Task<ReportingGenerationReceiptV1> Deliver(Guid tenant, Guid company, Guid generation, InboxDeliveryV1 delivery, CancellationToken ct)
    {
        Scope(tenant, company, generation);
        if (db.Database.CurrentTransaction is not null) throw new InvalidOperationException("Delivery requires its own commit boundary.");
        delivery = Copy(delivery);
        await using var transaction = await db.Database.BeginTransactionAsync(ct); await Lock(tenant, company, ct);
        await RequireOpen(tenant, company, generation, ct);
        var result = await Process(tenant, company, generation, delivery, ct);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return result;
    }

    public async Task<ReportingGenerationReceiptV1> Replay(Actor actor, Guid company, Guid generation, Guid deadLetter, Guid key, string reason, CancellationToken ct)
    {
        Require(actor, company, key, reason, Permissions.ReportManage); Scope(actor.TenantId, company, generation);
        var request = new { generation, deadLetter, reason }; const string operation = "financial.inbox.replay-v1";
        await using var transaction = await db.Database.BeginTransactionAsync(ct); await Lock(actor.TenantId, company, ct);
        var previous = await Previous<ReportingGenerationReceiptV1>(actor, company, operation, key, request, ct);
        if (previous is not null) { await transaction.CommitAsync(ct); return previous; }
        await RequireOpen(actor.TenantId, company, generation, ct);
        var dead = await db.Set<FinancialInboxDeadLetter>().SingleOrDefaultAsync(x => x.TenantId == actor.TenantId && x.CompanyId == company
            && x.GenerationId == generation && x.Id == deadLetter, ct) ?? throw new DomainException("not_found", "Dead letter not found.", 404);
        var delivery = Copy(JsonSerializer.Deserialize<InboxDeliveryV1>(dead.DeliveryJson, Json)!);
        if (delivery.OriginalBodyLength > InboxDeliveryV1.MaxBodyBytes) throw new DomainException("replay_unavailable", "Truncated evidence cannot be replayed.", 409);
        var result = await Process(actor.TenantId, company, generation, delivery, ct);
        if (result.Status is "Processed" or "Duplicate") dead.ResolvedAt = clock.GetUtcNow();
        Record(actor, company, generation, key, reason, operation, request, result);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return result;
    }

    private async Task<ReportingGenerationReceiptV1> Process(Guid tenant, Guid company, Guid generation, InboxDeliveryV1 delivery, CancellationToken ct)
    {
        var (row, error) = FinancialDelivery.Parse(delivery, tenant, company);
        if (error is not null) return await Quarantine(tenant, company, generation, delivery, error, ct);
        var message = await db.Set<FinancialInboxMessage>().SingleOrDefaultAsync(x => x.TenantId == tenant && x.CompanyId == company
            && x.GenerationId == generation && x.EventId == row!.EventId, ct);
        if (message is not null)
        {
            await db.Entry(message).ReloadAsync(ct);
            if (message.BodyHash != delivery.BodySha256 || message.DeliveryJson != JsonSerializer.Serialize(delivery, Json))
                return await Quarantine(tenant, company, generation, delivery, "event_id_payload_conflict", ct);
            if (message.Status == "Quarantined") return await Quarantine(tenant, company, generation, delivery, "financial_projection_conflict", ct);
            return new(generation, delivery.Fingerprint(), message.Status == "PendingOriginal" ? "PendingOriginal" : "Duplicate");
        }
        message = new() { TenantId = tenant, CompanyId = company, GenerationId = generation, EventId = row!.EventId,
            TransactionId = row.TransactionId, ReversalOfTransactionId = row.ReversalOfTransactionId,
            RowJson = JsonSerializer.Serialize(row, Json), DeliveryJson = JsonSerializer.Serialize(delivery, Json), BodyHash = delivery.BodySha256,
            ReceivedAt = clock.GetUtcNow(), UpdatedAt = clock.GetUtcNow(), Status = "PendingOriginal" };
        db.Add(message);
        var baseline = await Baseline(tenant, company, generation, ct);
        error = await Apply(message, row, baseline, ct);
        if (error is not null)
        {
            message.Status = "Quarantined";
            return await Quarantine(tenant, company, generation, delivery, error, ct);
        }
        if (message.Status == "Processed" && row.Kind == "Purchase")
        {
            // A missing original may arrive after its reversal. Drain only its dependants.
            var pending = await db.Set<FinancialInboxMessage>().Where(x => x.TenantId == tenant && x.CompanyId == company
                && x.GenerationId == generation && x.Status == "PendingOriginal" && x.ReversalOfTransactionId == row.TransactionId)
                .OrderBy(x => x.EventId).ToArrayAsync(ct);
            foreach (var waiting in pending)
            {
                var candidate = JsonSerializer.Deserialize<ReportingSourceRowV1>(waiting.RowJson, Json)!;
                var fault = await Apply(waiting, candidate, baseline, ct); waiting.UpdatedAt = clock.GetUtcNow();
                var originalDelivery = JsonSerializer.Deserialize<InboxDeliveryV1>(waiting.DeliveryJson, Json)!;
                if (fault is not null) { waiting.Status = "Quarantined"; await Quarantine(tenant, company, generation, originalDelivery, fault, ct); }
                else if (waiting.Status == "Processed")
                    await Resolve(tenant, company, generation, originalDelivery.Fingerprint(), ct);
            }
        }
        return new(generation, delivery.Fingerprint(), message.Status == "PendingOriginal" ? "PendingOriginal" : message.Status == "BaselineDuplicate" ? "Duplicate" : "Processed");
    }

    private async Task<string?> Apply(FinancialInboxMessage message, ReportingSourceRowV1 row, ReportingSourceRowV1[] baseline, CancellationToken ct)
    {
        var tenant = message.TenantId; var company = message.CompanyId; var generation = message.GenerationId;
        var existing = baseline.Where(x => x.EventId == row.EventId || x.TransactionId == row.TransactionId).ToArray();
        if (existing.Length != 0)
        {
            if (existing.Length != 1 || existing[0] != row) return "baseline_payload_conflict";
            message.Status = "BaselineDuplicate"; return null;
        }
        var local = db.Set<FinancialLiveEntry>().Local.Where(x => x.TenantId == tenant && x.CompanyId == company && x.GenerationId == generation).ToArray();
        var query = db.Set<FinancialLiveEntry>().Where(x => x.TenantId == tenant && x.CompanyId == company && x.GenerationId == generation);
        if (local.Any(x => x.TransactionId == row.TransactionId || x.EventId == row.EventId)
            || await query.AnyAsync(x => x.TransactionId == row.TransactionId || x.EventId == row.EventId, ct)) return "transaction_payload_conflict";
        if (row.Kind == "Purchase")
        {
            if (baseline.Any(x => x.Kind == "Purchase" && x.PurchaseId == row.PurchaseId)
                || local.Any(x => x.Kind == "Purchase" && x.PurchaseId == row.PurchaseId)
                || await query.AnyAsync(x => x.Kind == "Purchase" && x.PurchaseId == row.PurchaseId, ct)) return "purchase_identity_conflict";
        }
        else
        {
            var original = baseline.SingleOrDefault(x => x.TransactionId == row.ReversalOfTransactionId);
            var live = local.SingleOrDefault(x => x.TransactionId == row.ReversalOfTransactionId)
                ?? await query.SingleOrDefaultAsync(x => x.TransactionId == row.ReversalOfTransactionId, ct);
            if (original is null && live is not null) original = JsonSerializer.Deserialize<ReportingSourceRowV1>(live.RowJson, Json)!;
            if (original is null)
            {
                if (await db.Set<FinancialInboxMessage>().CountAsync(x => x.TenantId == tenant && x.CompanyId == company
                    && x.GenerationId == generation && x.Status == "PendingOriginal" && x.ReversalOfTransactionId == row.ReversalOfTransactionId, ct) >= 32)
                    return "pending_original_limit";
                message.Status = "PendingOriginal"; return null;
            }
            if (!FinancialDelivery.Reverses(row, original)) return "reversal_payload_conflict";
            if (baseline.Any(x => x.ReversalOfTransactionId == row.ReversalOfTransactionId)
                || local.Any(x => x.ReversalOfTransactionId == row.ReversalOfTransactionId)
                || await query.AnyAsync(x => x.ReversalOfTransactionId == row.ReversalOfTransactionId, ct)) return "duplicate_reversal";
        }
        db.Add(new FinancialLiveEntry { TenantId = tenant, CompanyId = company, GenerationId = generation,
            TransactionId = row.TransactionId, EventId = row.EventId, PurchaseId = row.PurchaseId, Kind = row.Kind,
            ReversalOfTransactionId = row.ReversalOfTransactionId, RowJson = message.RowJson, BusinessDate = row.BusinessDate,
            Currency = row.Currency, NetAmount = row.NetAmount, TaxAmount = row.TaxAmount, RecoverableTaxAmount = row.RecoverableTaxAmount });
        message.Status = "Processed"; return null;
    }

    public async Task<FinancialInboxSummary> Summary(Actor actor, Guid company, Guid generation, DateOnly from, DateOnly until, CancellationToken ct)
    {
        actor.RequireCompany(company);
        if (!actor.Permissions.Contains(Permissions.ReportRead)) throw new DomainException("permission_denied", "Report read permission is required.", 403);
        Scope(actor.TenantId, company, generation);
        if (from == default || until <= from) throw new DomainException("validation_failed", "Supply a nonempty [from, until) interval.");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        if (await State(actor.TenantId, company, generation, ct) is null) throw new DomainException("not_found", "Inbox generation not found.", 404);
        var baseline = await db.Set<SnapshotEntry>().AsNoTracking().Where(x => x.TenantId == actor.TenantId && x.CompanyId == company && x.GenerationId == generation
            && from <= x.BusinessDate && x.BusinessDate < until).GroupBy(x => x.Currency)
            .Select(g => new SnapshotCurrencyTotals(g.Key, g.Count(), g.Sum(x => x.NetAmount), g.Sum(x => x.TaxAmount), g.Sum(x => x.RecoverableTaxAmount))).ToArrayAsync(ct);
        var live = await db.Set<FinancialLiveEntry>().AsNoTracking().Where(x => x.TenantId == actor.TenantId && x.CompanyId == company && x.GenerationId == generation
            && from <= x.BusinessDate && x.BusinessDate < until).GroupBy(x => x.Currency)
            .Select(g => new SnapshotCurrencyTotals(g.Key, g.Count(), g.Sum(x => x.NetAmount), g.Sum(x => x.TaxAmount), g.Sum(x => x.RecoverableTaxAmount))).ToArrayAsync(ct);
        var totals = baseline.Concat(live).GroupBy(x => x.Currency).OrderBy(x => x.Key)
            .Select(g => new SnapshotCurrencyTotals(g.Key, g.Sum(x => x.EntryCount), g.Sum(x => x.NetAmount), g.Sum(x => x.TaxAmount), g.Sum(x => x.RecoverableTaxAmount))).ToArray();
        var messages = db.Set<FinancialInboxMessage>().Where(x => x.TenantId == actor.TenantId && x.CompanyId == company && x.GenerationId == generation);
        var pending = await messages.CountAsync(x => x.Status == "PendingOriginal", ct);
        var quarantined = await db.Set<FinancialInboxDeadLetter>().CountAsync(x => x.TenantId == actor.TenantId && x.CompanyId == company && x.GenerationId == generation && x.ResolvedAt == null, ct);
        var last = await messages.MaxAsync(x => (DateTimeOffset?)x.ReceivedAt, ct);
        var lastQuarantine = await db.Set<FinancialInboxDeadLetter>().Where(x => x.TenantId == actor.TenantId && x.CompanyId == company
            && x.GenerationId == generation).MaxAsync(x => (DateTimeOffset?)x.ReceivedAt, ct);
        if (lastQuarantine is not null && (last is null || lastQuarantine > last)) last = lastQuarantine;
        await transaction.CommitAsync(ct); return new(generation, from, until, totals, pending, quarantined, last);
    }

    private async Task<ReportingGenerationReceiptV1> Quarantine(Guid tenant, Guid company, Guid generation, InboxDeliveryV1 delivery, string error, CancellationToken ct)
    {
        var fingerprint = delivery.Fingerprint();
        var dead = db.Set<FinancialInboxDeadLetter>().Local.SingleOrDefault(x => x.TenantId == tenant && x.CompanyId == company && x.GenerationId == generation && x.Fingerprint == fingerprint)
            ?? await db.Set<FinancialInboxDeadLetter>().SingleOrDefaultAsync(x => x.TenantId == tenant && x.CompanyId == company && x.GenerationId == generation && x.Fingerprint == fingerprint, ct);
        if (dead is null)
        {
            dead = new() { TenantId = tenant, CompanyId = company, GenerationId = generation, Id = Guid.NewGuid(), Fingerprint = fingerprint,
                DeliveryJson = JsonSerializer.Serialize(delivery, Json), ErrorCode = error, ReceivedAt = clock.GetUtcNow() }; db.Add(dead);
        }
        else if (db.Entry(dead).State != EntityState.Added) await db.Entry(dead).ReloadAsync(ct);
        dead.ResolvedAt = null; return new(generation, fingerprint, "Quarantined", dead.Id);
    }
    private async Task Resolve(Guid tenant, Guid company, Guid generation, string fingerprint, CancellationToken ct)
    {
        var dead = await db.Set<FinancialInboxDeadLetter>().SingleOrDefaultAsync(x => x.TenantId == tenant && x.CompanyId == company && x.GenerationId == generation && x.Fingerprint == fingerprint, ct);
        if (dead is not null) dead.ResolvedAt = clock.GetUtcNow();
    }
    private async Task<ReportingSourceRowV1[]> Baseline(Guid tenant, Guid company, Guid generation, CancellationToken ct)
        => (await db.Set<SnapshotEntry>().AsNoTracking().Where(x => x.TenantId == tenant && x.CompanyId == company && x.GenerationId == generation)
            .Select(x => x.RowJson).ToArrayAsync(ct)).Select(x => JsonSerializer.Deserialize<ReportingSourceRowV1>(x, Json)!).ToArray();
    private async Task<FinancialInboxGeneration?> State(Guid tenant, Guid company, Guid generation, CancellationToken ct)
    {
        var state = await db.Set<FinancialInboxGeneration>().SingleOrDefaultAsync(x => x.TenantId == tenant && x.CompanyId == company && x.GenerationId == generation, ct);
        // A reused worker/operator scope must observe another process's terminal retirement.
        if (state is not null) await db.Entry(state).ReloadAsync(ct);
        return state;
    }
    private async Task RequireOpen(Guid tenant, Guid company, Guid generation, CancellationToken ct)
    {
        var state = await db.Set<FinancialInboxGeneration>().AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == tenant && x.CompanyId == company
            && x.GenerationId == generation, ct) ?? throw new DomainException("not_found", "Inbox generation not found.", 404);
        if (state.Status != "Open") throw new DomainException("generation_retired", "Stale delivery cannot write a retired generation.", 409);
    }
    private Task Lock(Guid tenant, Guid company, CancellationToken ct) => FinancialGenerationLock.Acquire(db, tenant, company, ct);
    private async Task<T?> Previous<T>(Actor actor, Guid company, string operation, Guid key, object request, CancellationToken ct) where T : class
    {
        var receipt = await db.Set<SnapshotReceipt>().AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == actor.TenantId && x.CompanyId == company && x.Operation == operation && x.Key == key, ct);
        if (receipt is null) return null;
        if (receipt.ActorId != actor.Subject || receipt.RequestHash != Hash(request)) throw new DomainException("snapshot_command_conflict", "Command key belongs to another actor or payload.", 409);
        return JsonSerializer.Deserialize<T>(receipt.ResponseJson, Json)!;
    }
    private void Record(Actor actor, Guid company, Guid generation, Guid key, string reason, string operation, object request, object response)
    {
        db.Add(new SnapshotAudit { TenantId = actor.TenantId, CompanyId = company, GenerationId = generation, Id = Guid.NewGuid(), CommandKey = key,
            ActorId = actor.Subject, Action = operation, Reason = reason.Trim(), OccurredAt = clock.GetUtcNow() });
        db.Add(new SnapshotReceipt { TenantId = actor.TenantId, CompanyId = company, Operation = operation, Key = key,
            ActorId = actor.Subject, RequestHash = Hash(request), ResponseJson = JsonSerializer.Serialize(response, response.GetType(), Json) });
    }
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value, value.GetType(), Json)));
    private static InboxDeliveryV1 Copy(InboxDeliveryV1 delivery)
    {
        if (delivery?.BodyPrefix is null) throw new DomainException("validation_failed", "A valid bounded delivery is required.");
        if (delivery.BodyPrefix.Length > InboxDeliveryV1.MaxBodyBytes) throw new DomainException("validation_failed", "Delivery prefix is oversized.");
        var copy = delivery with { BodyPrefix = delivery.BodyPrefix.ToArray() };
        if (!copy.IsValid()) throw new DomainException("validation_failed", "Delivery integrity is invalid."); return copy;
    }
    private static void Scope(Guid tenant, Guid company, Guid generation)
    { if (tenant == Guid.Empty || company == Guid.Empty || generation == Guid.Empty) throw new DomainException("validation_failed", "Tenant, company and generation UUIDs are required."); }
    private static void Require(Actor actor, Guid company, Guid key, string reason, string permission)
    {
        actor.RequireCompany(company);
        if (!actor.Permissions.Contains(permission)) throw new DomainException("permission_denied", "Report management permission is required.", 403);
        if (actor.TenantId == Guid.Empty || string.IsNullOrWhiteSpace(actor.Subject) || actor.Subject.Length > 200
            || key == Guid.Empty || string.IsNullOrWhiteSpace(reason) || reason.Length > 1000)
            throw new DomainException("validation_failed", "Supply a verified operator, command UUID and bounded reason.");
    }
}
