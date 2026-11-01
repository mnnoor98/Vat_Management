using Vat.Application.Security;
using Vat.Domain;
using Vat.Domain.Costing;

namespace Vat.Application;

public sealed record CreateCostingPolicyRequest(Guid? ProductId, string Method, DateOnly EffectiveFrom, DateOnly EffectiveUntil);
public sealed record ApproveCostingPolicyRequest(int ExpectedVersion);
public sealed record CostingPolicyView(Guid Id, Guid CompanyId, Guid? ProductId, string Method, DateOnly EffectiveFrom,
    DateOnly EffectiveUntil, string State, int Version, string CreatedBy, DateTimeOffset CreatedAt, string? ApprovedBy, DateTimeOffset? ApprovedAt)
{
    public static CostingPolicyView From(CostingPolicy p) => new(p.Id, p.CompanyId, p.ProductId, p.Method, p.EffectiveFrom,
        p.EffectiveUntil, p.State, p.Version, p.CreatedBy, p.CreatedAt, p.ApprovedBy, p.ApprovedAt);
}
public interface ICostingStore
{
    Task Lock(Guid tenant, Guid company, CancellationToken ct);
    void Add(CostingPolicy policy);
    Task<CostingPolicy?> Find(Guid tenant, Guid company, Guid id, CancellationToken ct);
    Task<IReadOnlyList<CostingPolicy>> List(Guid tenant, Guid company, Guid? before, int take, CancellationToken ct);
    Task<bool> Overlaps(CostingPolicy policy, CancellationToken ct);
    Task<bool> HasFrozenReceipts(CostingPolicy policy, CancellationToken ct);
    Task<CostingPolicy?> Resolve(Guid tenant, Guid company, Guid product, DateOnly date, CancellationToken ct);
    Task<IReadOnlyList<PurchaseCostReceipt>> Receipts(Guid tenant, Guid company, Guid? before, int take, CancellationToken ct);
}

public sealed class CostingPolicyService(ICostingStore store, ICompanyStore companies, IReferenceStore references,
    IPeriodGuard periods, IAuditWriter audit, TimeProvider clock)
{
    public async Task<CommandOutcome> Create(Actor actor, Guid company, CreateCostingPolicyRequest request, string correlation, CancellationToken ct)
    {
        actor.RequireCompany(company);
        if (await companies.FindCompany(actor.TenantId, company, ct) is null) throw new DomainException("not_found", "Company not found.", 404);
        if (request.ProductId is { } product && await references.ProductById(actor.TenantId, company, product, ct) is null)
            throw new DomainException("not_found", "Product not found in this company.", 404);
        var policy = CostingPolicy.Create(actor.TenantId, company, request.ProductId, request.Method,
            request.EffectiveFrom, request.EffectiveUntil, actor.Subject, clock.GetUtcNow());
        store.Add(policy); audit.Append(actor, company, policy.Id, "CostingPolicyCreated", correlation, policy.Version);
        return new(201, CostingPolicyView.From(policy), $"/api/v1/companies/{company}/inventory/costing-policies/{policy.Id}");
    }

    public async Task<CommandOutcome> Approve(Actor actor, Guid company, Guid id, ApproveCostingPolicyRequest request, string correlation, CancellationToken ct)
    {
        actor.RequireCompany(company);
        var policy = await Get(actor, company, id, ct);
        await periods.RequireOpen(actor.TenantId, company, policy.EffectiveFrom, ct);
        await store.Lock(actor.TenantId, company, ct);
        if (await store.Overlaps(policy, ct)) throw new DomainException("cost_policy_overlap", "An approved policy already covers this scope/date range.", 409);
        if (await store.HasFrozenReceipts(policy, ct)) throw new DomainException("cost_policy_history_frozen", "Policy approval cannot reassign already captured receipt history.", 409);
        policy.Approve(actor.Subject, request.ExpectedVersion, clock.GetUtcNow());
        audit.Append(actor, company, id, "CostingPolicyApproved", correlation, policy.Version);
        return new(200, CostingPolicyView.From(policy));
    }

    public async Task<CostingPolicy> Get(Actor actor, Guid company, Guid id, CancellationToken ct)
    {
        actor.RequireCompany(company);
        return await store.Find(actor.TenantId, company, id, ct) ?? throw new DomainException("not_found", "Costing policy not found.", 404);
    }
    public Task<IReadOnlyList<CostingPolicy>> List(Actor actor, Guid company, Guid? before, int take, CancellationToken ct)
    { actor.RequireCompany(company); Page(take); return store.List(actor.TenantId, company, before, take, ct); }
    public async Task<CostingPolicy?> Resolve(Actor actor, Guid company, Guid product, DateOnly date, CancellationToken ct)
    {
        actor.RequireCompany(company); Guard.Id(product, nameof(product));
        if (await references.ProductById(actor.TenantId, company, product, ct) is null) throw new DomainException("not_found", "Product not found in this company.", 404);
        return await store.Resolve(actor.TenantId, company, product, date, ct);
    }
    public Task<IReadOnlyList<PurchaseCostReceipt>> Receipts(Actor actor, Guid company, Guid? before, int take, CancellationToken ct)
    { actor.RequireCompany(company); Page(take); return store.Receipts(actor.TenantId, company, before, take, ct); }
    private static void Page(int take) { if (take is < 1 or > 100) throw new DomainException("validation_failed", "Require take 1-100."); }
}
