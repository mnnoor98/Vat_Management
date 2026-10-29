using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vat.Application.Security;
using Vat.Domain;
using Vat.Services.Manufacturing.Domain;

namespace Vat.Services.Manufacturing.Infrastructure;

public sealed class CoefficientStore(ManufacturingDbContext db, TimeProvider clock) : ICoefficientStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public Task<CoefficientResponse> Create(Actor actor, Guid company, Guid key, CoefficientCreateRequest request,
        Func<Task<CoefficientSnapshot>> snapshot, string correlation, CancellationToken ct) =>
        Execute(actor, company, key, new { action = "create", request }, async () =>
        {
            await Lock($"bom:{actor.TenantId}:{company}:{request.DeclarationNumber.Trim()}", ct);
            var header = await db.Set<BomHeader>().SingleOrDefaultAsync(x => x.TenantId == actor.TenantId && x.CompanyId == company && x.Code == request.DeclarationNumber.Trim(), ct);
            // A declaration identifies one immutable product/BOM; subsequent requests are new revisions.
            if (header is not null && header.ProductId != request.FinishedProductId)
                throw new DomainException("declaration_product_conflict", "This declaration belongs to another product.", 409);
            var proof = await snapshot(); var now = clock.GetUtcNow();
            if (header is null)
            {
                header = new() { TenantId = actor.TenantId, CompanyId = company, Id = Guid.CreateVersion7(), Code = request.DeclarationNumber.Trim(),
                    ProductId = request.FinishedProductId,
                    Name = proof.FinishedProduct.Description.Length <= 200 ? proof.FinishedProduct.Description : proof.FinishedProduct.Reference,
                    CreatedBy = actor.Subject, CreatedAt = now, UpdatedAt = now, Version = 1 };
                db.Add(header);
            }
            var revision = await db.Set<CoefficientVersion>().Where(x => x.TenantId == actor.TenantId && x.CompanyId == company && x.BomId == header.Id)
                .Select(x => (int?)x.Revision).MaxAsync(ct) ?? 0;
            var value = new CoefficientVersion { TenantId = actor.TenantId, CompanyId = company, Id = Guid.CreateVersion7(), BomId = header.Id,
                Revision = revision + 1, OutputQuantity = request.BatchSize, EffectiveFrom = request.EffectiveFrom, EffectiveUntil = request.EffectiveUntil,
                State = "Draft", Version = 1, CreatedBy = actor.Subject, CreatedAt = now, UpdatedAt = now };
            db.Add(value);
            var proofJson = JsonSerializer.Serialize(proof, Json);
            db.Add(new CoefficientDocument { TenantId = actor.TenantId, CompanyId = company, Id = value.Id, SnapshotJson = proofJson,
                SnapshotSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(proofJson))) });
            foreach (var line in request.Materials) db.Add(new BomLine { TenantId = actor.TenantId, CompanyId = company, Id = Guid.CreateVersion7(),
                CoefficientVersionId = value.Id, ProductId = line.ProductId, Quantity = line.Quantity, WastagePercent = line.WastagePercent,
                CreatedBy = actor.Subject, CreatedAt = now, UpdatedAt = now, Version = 1 });
            Audit(actor, company, value, "create", null, correlation);
            return (201, Result(value, header.Code, proof));
        }, ct);

    public Task<CoefficientResponse> Transition(Actor actor, Guid company, Guid id, Guid key, string action, CoefficientActionRequest request, string correlation, CancellationToken ct) =>
        Execute(actor, company, key, new { id, action, request }, async () =>
        {
            await Lock($"coefficient:{actor.TenantId}:{company}:{id}", ct);
            var value = await db.Set<CoefficientVersion>().SingleOrDefaultAsync(x => x.TenantId == actor.TenantId && x.CompanyId == company && x.Id == id, ct)
                ?? throw new DomainException("not_found", "Coefficient was not found.", 404);
            if (value.Version != request.ExpectedVersion) throw new DomainException("version_conflict", "Reload the current coefficient version.", 409);
            var document = await db.Set<CoefficientDocument>().SingleOrDefaultAsync(x => x.TenantId == actor.TenantId && x.CompanyId == company && x.Id == id, ct)
                ?? throw new DomainException("coefficient_snapshot_required", "Legacy coefficients require a new verified revision before execution.", 409);
            if (Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(document.SnapshotJson))) != document.SnapshotSha256)
                throw new DomainException("coefficient_snapshot_conflict", "The verified snapshot has changed.", 409);
            if (action == "approve" && value.CreatedBy == actor.Subject)
                throw new DomainException("self_approval", "A different person must approve the coefficient.", 409);
            var next = (value.State, action) switch
            {
                ("Draft", "submit") => "Submitted",
                ("Submitted", "approve") => "Approved",
                ("Draft" or "Submitted", "cancel") => "Cancelled",
                _ => throw new DomainException("invalid_transition", "The coefficient is not in the required state.", 409)
            };
            if (action == "approve")
            {
                await Lock($"coefficient-approval:{actor.TenantId}:{company}:{value.BomId}", ct);
                if (await db.Set<CoefficientVersion>().AnyAsync(x => x.TenantId == actor.TenantId && x.CompanyId == company && x.BomId == value.BomId && x.Id != id && x.State == "Approved" &&
                    (x.EffectiveUntil == null || x.EffectiveUntil >= value.EffectiveFrom) && (value.EffectiveUntil == null || x.EffectiveFrom <= value.EffectiveUntil), ct))
                    throw new DomainException("coefficient_overlap", "An approved revision already covers part of this effective interval.", 409);
                value.ApprovedBy = actor.Subject;
            }
            value.State = next; value.Version++; value.UpdatedAt = clock.GetUtcNow();
            Audit(actor, company, value, action, request.Reason, correlation);
            var header = await db.Set<BomHeader>().SingleAsync(x => x.TenantId == actor.TenantId && x.CompanyId == company && x.Id == value.BomId, ct);
            return (200, Result(value, header.Code, JsonSerializer.Deserialize<CoefficientSnapshot>(document.SnapshotJson, Json)));
        }, ct);

    public async Task<IReadOnlyList<CoefficientResult>> List(Guid tenant, Guid company, Guid? before, int take, CancellationToken ct)
    {
        var query = from value in db.Set<CoefficientVersion>().AsNoTracking()
            join header in db.Set<BomHeader>().AsNoTracking() on new { value.TenantId, value.CompanyId, Id = value.BomId } equals new { header.TenantId, header.CompanyId, header.Id }
            join proof in db.Set<CoefficientDocument>().AsNoTracking() on new { value.TenantId, value.CompanyId, value.Id } equals new { proof.TenantId, proof.CompanyId, proof.Id } into proofs
            from proof in proofs.DefaultIfEmpty()
            where value.TenantId == tenant && value.CompanyId == company && (before == null || value.Id.CompareTo(before.Value) < 0)
            orderby value.Id descending
            select new { value, header.Code, Snapshot = proof == null ? null : proof.SnapshotJson };
        var rows = await query.Take(take).ToArrayAsync(ct);
        return rows.Select(x => Result(x.value, x.Code, x.Snapshot is null ? null : JsonSerializer.Deserialize<CoefficientSnapshot>(x.Snapshot, Json))).ToArray();
    }
    private async Task<CoefficientResponse> Execute(Actor actor, Guid company, Guid key, object request, Func<Task<(int Status, CoefficientResult Result)>> action, CancellationToken ct)
    {
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request, Json)));
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await Lock($"coefficient-command:{actor.TenantId}:{company}:{key}", ct);
        var prior = await db.Set<CoefficientCommand>().FindAsync([actor.TenantId, company, key], ct);
        if (prior is not null)
        {
            if (prior.Actor != actor.Subject || prior.RequestHash != hash) throw new DomainException("idempotency_conflict", "This key belongs to another actor or request.", 409);
            await tx.CommitAsync(ct); return new(prior.StatusCode, prior.ResponseJson, true);
        }
        var outcome = await action(); var json = JsonSerializer.Serialize(outcome.Result, Json);
        db.Add(new CoefficientCommand { TenantId = actor.TenantId, CompanyId = company, Id = key, Actor = actor.Subject, RequestHash = hash,
            ResponseJson = json, StatusCode = outcome.Status, CreatedAt = clock.GetUtcNow() });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return new(outcome.Status, json);
    }
    private void Audit(Actor actor, Guid company, CoefficientVersion value, string action, string? reason, string correlation) =>
        db.Add(new CoefficientAudit { TenantId = actor.TenantId, CompanyId = company, Id = Guid.CreateVersion7(), CoefficientId = value.Id,
            Actor = actor.Subject, Action = action, Version = value.Version, Reason = reason, CorrelationId = correlation, CreatedAt = clock.GetUtcNow() });
    private Task Lock(string value, CancellationToken ct) => db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({BinaryPrimitives.ReadInt64BigEndian(SHA256.HashData(Encoding.UTF8.GetBytes(value)))})", ct);
    private static CoefficientResult Result(CoefficientVersion value, string code, CoefficientSnapshot? proof) => new(value.Id, value.BomId, value.Revision,
        code, value.State, value.Version, value.CreatedBy, value.ApprovedBy, value.EffectiveFrom, value.EffectiveUntil, proof);
}
