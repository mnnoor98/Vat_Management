using Microsoft.EntityFrameworkCore;
using Vat.Application.Security;
using Vat.Domain;
using Vat.Domain.Tax;

namespace Vat.Services.VatEngine.Infrastructure;

public sealed class VatEngineStore(VatEngineDbContext db, TimeProvider clock) : ITaxRuleStore, ITaxLedgerStore
{
    public async Task<TaxRuleResponse> Create(Actor actor, Guid companyId, CreateTaxRuleRequest request, string correlationId, CancellationToken ct)
    {
        var rule = TaxRule.Create(actor.TenantId, companyId, request.TaxCode, request.EffectiveFrom, request.EffectiveUntil,
            request.RatePercent, request.RecoverablePercent, request.LegalBasis, request.EvidenceReference, actor.Subject, request.Treatment);

        db.TaxRules.Add(rule);
        await db.SaveChangesAsync(ct);
        return TaxRuleResponse.From(rule);
    }

    public async Task<TaxRuleResponse> Publish(Actor actor, Guid companyId, Guid id, TransitionRequest request, string correlationId, CancellationToken ct)
    {
        var rule = await db.TaxRules.SingleOrDefaultAsync(x => x.TenantId == actor.TenantId && x.CompanyId == companyId && x.Id == id, ct)
            ?? throw new DomainException("tax_rule_not_found", "Tax rule not found.", 404);

        rule.Publish(actor.Subject, request.ExpectedVersion, clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return TaxRuleResponse.From(rule);
    }

    public async Task<TaxRuleResponse?> Get(Actor actor, Guid companyId, Guid id, CancellationToken ct)
    {
        var rule = await db.TaxRules.AsNoTracking()
            .SingleOrDefaultAsync(x => x.TenantId == actor.TenantId && x.CompanyId == companyId && x.Id == id, ct);
        return rule is null ? null : TaxRuleResponse.From(rule);
    }

    public async Task<TaxRule?> EffectiveRule(Guid tenantId, Guid companyId, string taxCode, string treatment, DateOnly date, CancellationToken ct)
    {
        return await db.TaxRules.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.CompanyId == companyId && x.TaxCode == taxCode
                && x.Treatment == treatment && x.PublishedAt != null
                && x.EffectiveFrom <= date && date < x.EffectiveUntil)
            .OrderByDescending(x => x.EffectiveFrom)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<TaxLedgerResponse>> List(Actor actor, Guid companyId, Guid? purchaseId, Guid? before, int take, CancellationToken ct)
    {
        var q = db.TaxLedger.AsNoTracking().Where(x => x.TenantId == actor.TenantId && x.CompanyId == companyId);
        if (purchaseId.HasValue)
            q = q.Where(x => x.PurchaseId == purchaseId.Value);
        if (before.HasValue)
            q = q.Where(x => x.Id.CompareTo(before.Value) < 0);

        return await q.OrderByDescending(x => x.Id).Take(take)
            .Select(x => new TaxLedgerResponse(
                x.Id,
                x.PurchaseId,
                x.PurchaseLineId,
                x.PlanId,
                x.TaxRuleId,
                x.BusinessDate,
                x.NetAmount,
                x.TaxAmount,
                x.RecoverableTaxAmount,
                x.ReversesEntryId,
                x.ReversalId
            )).ToListAsync(ct);
    }
}

