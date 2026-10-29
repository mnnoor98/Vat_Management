using Vat.Application.Security;
using Vat.Domain;

namespace Vat.Services.Manufacturing;

public sealed record CoefficientMaterialRequest(Guid ProductId, decimal Quantity, decimal WastagePercent, decimal UnitPrice);
public sealed record CoefficientCreateRequest(string DeclarationNumber, Guid FinishedProductId, decimal BatchSize,
    DateOnly EffectiveFrom, DateOnly? EffectiveUntil, decimal DirectLaborCost, decimal UtilityCost, decimal OverheadCost,
    List<CoefficientMaterialRequest> Materials);
public sealed record CoefficientActionRequest(int ExpectedVersion, string? Reason = null);
public sealed record ProductReference(Guid Id, string Reference, string Description, string UnitOfMeasure, string TaxCode);
public sealed record CoefficientMaterialSnapshot(ProductReference Product, decimal Quantity, decimal WastagePercent, decimal UnitPrice, decimal TotalCost);
public sealed record CoefficientSnapshot(ProductReference FinishedProduct, decimal BatchSize, IReadOnlyList<CoefficientMaterialSnapshot> Materials,
    decimal DirectLaborCost, decimal UtilityCost, decimal OverheadCost, decimal TotalCost, decimal UnitCost, DateTimeOffset ReferencesCheckedAt);
public sealed record CoefficientResult(Guid Id, Guid BomId, int Revision, string DeclarationNumber, string State, int Version,
    string CreatedBy, string? ApprovedBy, DateOnly EffectiveFrom, DateOnly? EffectiveUntil, CoefficientSnapshot? Snapshot);
public sealed record CoefficientResponse(int StatusCode, string Json, bool Replayed = false);

public interface ICoefficientReferences
{
    Task<ProductReference> Product(Actor actor, Guid company, Guid product, string bearerToken, CancellationToken ct);
}
public interface ICoefficientStore
{
    Task<CoefficientResponse> Create(Actor actor, Guid company, Guid key, CoefficientCreateRequest request,
        Func<Task<CoefficientSnapshot>> snapshot, string correlation, CancellationToken ct);
    Task<CoefficientResponse> Transition(Actor actor, Guid company, Guid id, Guid key, string action, CoefficientActionRequest request, string correlation, CancellationToken ct);
    Task<IReadOnlyList<CoefficientResult>> List(Guid tenant, Guid company, Guid? before, int take, CancellationToken ct);
}

public sealed class CoefficientService(ICoefficientStore store, ICoefficientReferences references, TimeProvider clock)
{
    public Task<IReadOnlyList<CoefficientResult>> List(Actor actor, Guid company, Guid? before, int take, CancellationToken ct)
    {
        Require(actor, company, Permissions.ManufacturingRead);
        if (take is < 1 or > 100) throw new DomainException("validation_failed", "take must be 1 to 100.");
        return store.List(actor.TenantId, company, before, take, ct);
    }
    public Task<CoefficientResponse> Create(Actor actor, Guid company, string key, CoefficientCreateRequest request, string token, string correlation, CancellationToken ct)
    {
        Require(actor, company, Permissions.ManufacturingRequest);
        Guard.Text(request.DeclarationNumber, "DeclarationNumber", 100); Guard.Id(request.FinishedProductId, "FinishedProductId");
        if (request.BatchSize <= 0 || !Number(request.BatchSize) || request.EffectiveFrom == default || request.EffectiveUntil < request.EffectiveFrom ||
            !Number(request.DirectLaborCost) || !Number(request.UtilityCost) || !Number(request.OverheadCost) ||
            request.Materials is null || request.Materials.Count is < 1 or > 100 ||
            request.Materials.Select(x => x.ProductId).Distinct().Count() != request.Materials.Count ||
            request.Materials.Any(x => x.ProductId == Guid.Empty || x.ProductId == request.FinishedProductId || x.Quantity <= 0 || !Number(x.Quantity) ||
                !Number(x.UnitPrice) || !Number(x.WastagePercent) || x.WastagePercent > 100))
            throw new DomainException("validation_failed", "Invalid dates, quantities, costs, wastage or material products.");
        return store.Create(actor, company, Key(key), request, async () =>
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(TimeSpan.FromSeconds(20));
            try
            {
                var finished = await references.Product(actor, company, request.FinishedProductId, token, deadline.Token);
                var lines = new List<CoefficientMaterialSnapshot>();
                foreach (var line in request.Materials.OrderBy(x => x.ProductId))
                {
                    var product = await references.Product(actor, company, line.ProductId, token, deadline.Token);
                    var cost = decimal.Round(line.Quantity * (1 + line.WastagePercent / 100m) * line.UnitPrice, 6);
                    lines.Add(new(product, line.Quantity, line.WastagePercent, line.UnitPrice, cost));
                }
                var total = lines.Sum(x => x.TotalCost) + request.DirectLaborCost + request.UtilityCost + request.OverheadCost;
                if (total >= 1000000000000000000m || total / request.BatchSize >= 1000000000000000000m)
                    throw new DomainException("validation_failed", "Calculated cost exceeds the supported range.");
                return new(finished, request.BatchSize, lines, request.DirectLaborCost, request.UtilityCost, request.OverheadCost,
                    total, decimal.Round(total / request.BatchSize, 6), clock.GetUtcNow());
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            { throw new DomainException("reference_service_unavailable", "Reference validation exceeded its time limit.", 503); }
        }, correlation, ct);
    }
    public Task<CoefficientResponse> Transition(Actor actor, Guid company, Guid id, string key, string action, CoefficientActionRequest request, string correlation, CancellationToken ct)
    {
        Require(actor, company, action == "approve" ? Permissions.ManufacturingApprove : Permissions.ManufacturingRequest);
        if (request.ExpectedVersion < 1) throw new DomainException("validation_failed", "ExpectedVersion must be positive.");
        if (request.Reason is { Length: > 1000 }) throw new DomainException("validation_failed", "Reason exceeds 1000 characters.");
        if (action == "cancel") Guard.Text(request.Reason ?? "", "Reason", 1000);
        return store.Transition(actor, company, id, Key(key), action, request, correlation, ct);
    }
    private static bool Number(decimal value) => value >= 0 && value < 1000000000m && decimal.Round(value, 6) == value;
    private static Guid Key(string key) => Guid.TryParseExact(key, "D", out var id) && id != Guid.Empty ? id :
        throw new DomainException("idempotency_key_required", "Idempotency-Key must be a non-empty UUID.", 400);
    private static void Require(Actor actor, Guid company, string permission)
    {
        actor.RequireCompany(company);
        if (!actor.Permissions.Contains(permission)) throw new DomainException("permission_denied", "The required permission is not granted.", 403);
    }
}
