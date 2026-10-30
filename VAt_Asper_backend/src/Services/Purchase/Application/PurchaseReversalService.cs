using Vat.Application.Security;
using Vat.Domain;
using Vat.Domain.Posting;
using Vat.Domain.Purchasing;
using Vat.Services.Purchase.Contracts;

namespace Vat.Services.Purchase;

public sealed class PurchaseReversalService(IPurchaseStore purchases, IPurchasePostingStore plans,
    IPurchaseReversalStore reversals, IAuditWriter audit, TimeProvider clock)
{
    public async Task<CommandOutcome> Request(Actor actor, Guid companyId, Guid purchaseId, RequestReversalRequest request, string correlationId, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        Vat.Domain.FinancialBoundary.RequireAvailable();
        actor.RequireCompany(companyId);
        var purchase = await RequirePurchase(actor, companyId, purchaseId, ct);
        var plan = await plans.LatestPlan(actor.TenantId, companyId, purchaseId, ct)
            ?? throw new DomainException("not_found", "Purchase posting plan not found.", 404);
        purchase.RequestReversal(purchase.Version, clock.GetUtcNow());
        var reversal = PurchaseReversal.Request(purchase, plan.Id, request.CorrectionDate, request.Reason, actor.Subject, clock.GetUtcNow());
        reversals.Add(reversal);
        audit.Append(actor, companyId, purchaseId, "PurchaseReversalRequested", correlationId, purchase.Version, reversal.Reason);
        return new(201, PurchaseReversalResponse.From(reversal), $"/api/v1/companies/{companyId}/purchases/{purchaseId}/reversals/{reversal.Id}");
    }

    public async Task<CommandOutcome> Authorize(Actor actor, Guid companyId, Guid purchaseId, Guid reversalId,
        ApproveReversalRequest request, string correlationId, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        Vat.Domain.FinancialBoundary.RequireAvailable();
        actor.RequireCompany(companyId);
        var reversal = await reversals.Find(actor.TenantId, companyId, purchaseId, reversalId, ct)
            ?? throw new DomainException("not_found", "Reversal not found.", 404);
        var purchase = await RequirePurchase(actor, companyId, purchaseId, ct);
        var now = clock.GetUtcNow();
        reversal.Authorize(actor.Subject, purchase.CreatedBy, now);
        purchase.Reverse(request.ExpectedVersion, now);
        audit.Append(actor, companyId, purchaseId, "PurchaseReversed", correlationId, purchase.Version, reversal.Reason);
        return new(200, PurchaseReversalResponse.From(reversal));
    }

    public async Task<PurchaseReversalResponse> Get(Actor actor, Guid companyId, Guid purchaseId, Guid reversalId, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        var reversal = await reversals.Find(actor.TenantId, companyId, purchaseId, reversalId, ct)
            ?? throw new DomainException("not_found", "Reversal not found.", 404);
        return PurchaseReversalResponse.From(reversal);
    }

    public async Task<CommandOutcome> Cancel(Actor actor, Guid companyId, Guid purchaseId, Guid reversalId,
        CancelPostingRequest request, string correlationId, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        var purchase = await RequirePurchase(actor, companyId, purchaseId, ct);
        var reversal = await reversals.Find(actor.TenantId, companyId, purchaseId, reversalId, ct)
            ?? throw new DomainException("not_found", "Reversal not found.", 404);
        reversal.Cancel(request.Reason);
        purchase.CancelReversal(request.ExpectedVersion, clock.GetUtcNow());
        audit.Append(actor, companyId, purchaseId, "PurchaseReversalCancelled", correlationId, purchase.Version, reversal.CancellationReason);
        return new(200, PurchaseResponse.From(purchase));
    }

    public async Task<CommandOutcome> CancelUnposted(Actor actor, Guid companyId, Guid purchaseId,
        CancelPostingRequest request, string correlationId, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        var purchase = await RequirePurchase(actor, companyId, purchaseId, ct);
        purchase.CancelUnposted(request.ExpectedVersion, request.Reason, clock.GetUtcNow());
        audit.Append(actor, companyId, purchaseId, "PurchaseCancelled", correlationId, purchase.Version, request.Reason);
        return new(200, PurchaseResponse.From(purchase));
    }

    private async Task<Vat.Domain.Purchasing.Purchase> RequirePurchase(Actor actor, Guid companyId, Guid id, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        return await purchases.Find(actor.TenantId, companyId, id, ct)
            ?? throw new DomainException("not_found", "Purchase not found.", 404);
    }
}
