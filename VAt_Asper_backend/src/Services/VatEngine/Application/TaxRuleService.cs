using Vat.Application.Security;
using Vat.Domain;

namespace Vat.Services.VatEngine;

public sealed class TaxRuleService(ITaxRuleStore store, IAuditWriter audit)
{
    public async Task<CommandOutcome> Create(Actor actor, Guid companyId, CreateTaxRuleRequest request, string correlationId, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        if (!actor.Permissions.Contains(VatEnginePermissions.TaxRuleManage))
            throw new DomainException("permission_denied", "Tax rule manage permission is required.", 403);

        var rule = await store.Create(actor, companyId, request, correlationId, ct);
        audit.Append(actor, companyId, rule.Id, "TaxRuleCreated", correlationId, rule.Version);
        return new(201, rule, $"/api/v1/companies/{companyId}/tax/rules/{rule.Id}");
    }

    public async Task<CommandOutcome> Publish(Actor actor, Guid companyId, Guid id, TransitionRequest request, string correlationId, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        if (!actor.Permissions.Contains(VatEnginePermissions.TaxRulePublish))
            throw new DomainException("permission_denied", "Tax rule publish permission is required.", 403);

        var rule = await store.Publish(actor, companyId, id, request, correlationId, ct);
        audit.Append(actor, companyId, id, "TaxRulePublished", correlationId, rule.Version);
        return new(200, rule);
    }

    public async Task<CommandOutcome> Get(Actor actor, Guid companyId, Guid id, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        if (!actor.Permissions.Contains(VatEnginePermissions.TaxRead))
            throw new DomainException("permission_denied", "Tax read permission is required.", 403);

        var rule = await store.Get(actor, companyId, id, ct);
        if (rule is null)
            throw new DomainException("tax_rule_not_found", "Tax rule not found.", 404);

        return new(200, rule);
    }
}

