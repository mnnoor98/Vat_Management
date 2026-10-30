using Vat.Application.Security;
using Vat.Domain;
using Vat.Domain.Posting;
using Vat.Domain.Purchasing;
using Vat.Services.Purchase.Contracts;

namespace Vat.Services.Purchase;

public sealed class PurchasePostingService(IPurchaseStore purchases, IPurchasePostingStore plans,
    IAuditWriter audit, IEventWriter events, TimeProvider clock)
{
    public async Task<CommandOutcome> Prepare(Actor actor, Guid companyId, Guid purchaseId,
        PreparePostingRequest request, string correlationId, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        Vat.Domain.FinancialBoundary.RequireAvailable();
        actor.RequireCompany(companyId);
        var purchase = await RequirePurchase(actor, companyId, purchaseId, ct);
        var now = clock.GetUtcNow();
        var calculatedLines = purchase.Lines.Select(l => new CalculatedLine(
            l.Id, Guid.Empty, l.ProductReference, l.Description, "PCS",
            l.Quantity, l.UnitPrice, l.NetAmount,
            Guid.Empty, 1, "STD", 15m, 100m,
            decimal.Round(l.NetAmount * 0.15m, 4),
            decimal.Round(l.NetAmount * 0.15m, 4),
            "Section 15", "NBR-VAT-STD")).ToList();

        var tax = calculatedLines.Sum(x => x.TaxAmount);
        var recoverable = calculatedLines.Sum(x => x.RecoverableTaxAmount);
        var snapshot = new CalculationSnapshot(
            PurchaseCalculator.Version, purchase.Currency, Guid.Empty, purchase.SupplierReference,
            purchase.SupplierReference, purchase.BusinessDate, calculatedLines,
            purchase.NetAmount, tax, recoverable, purchase.NetAmount + tax);

        purchase.PreparePosting(request.ExpectedVersion, now);
        var plan = PurchasePostingPlan.Create(purchase, request.WarehouseId, actor.Subject, snapshot, now);
        plans.AddPlan(plan);
        audit.Append(actor, companyId, purchaseId, "PurchasePostingPrepared", correlationId, purchase.Version);
        return new(201, PostingPlanResponse.From(plan), $"/api/v1/companies/{companyId}/purchases/{purchaseId}/posting-plans/{plan.Id}");
    }

    public async Task<PostingPlanResponse> GetPlan(Actor actor, Guid companyId, Guid purchaseId, Guid planId, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        var plan = await plans.FindPlan(actor.TenantId, companyId, purchaseId, planId, ct)
            ?? throw new DomainException("not_found", "Purchase posting plan not found.", 404);
        return PostingPlanResponse.From(plan);
    }

    public async Task<PostingStatusResponse> Status(Actor actor, Guid companyId, Guid purchaseId, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        var purchase = await RequirePurchase(actor, companyId, purchaseId, ct);
        var plan = await plans.LatestPlan(actor.TenantId, companyId, purchaseId, ct);
        return new(purchase.Id, purchase.Status.ToString(), purchase.Version, plan is not null ? PostingPlanResponse.From(plan) : null);
    }

    public async Task<CommandOutcome> Post(Actor actor, Guid companyId, Guid purchaseId, Guid planId,
        TransitionRequest request, string correlationId, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        Vat.Domain.FinancialBoundary.RequireAvailable();
        actor.RequireCompany(companyId);
        var purchase = await RequirePurchase(actor, companyId, purchaseId, ct);
        var plan = await plans.FindPlan(actor.TenantId, companyId, purchaseId, planId, ct)
            ?? throw new DomainException("not_found", "Purchase posting plan not found.", 404);
        if (plan.SourceVersion != request.ExpectedVersion)
            throw new DomainException("version_conflict", "The request must reference the prepared purchase version.", 409);
        var now = clock.GetUtcNow();
        plan.Post(actor.Subject, purchase.CreatedBy, now);
        purchase.Post(request.ExpectedVersion, now);
        audit.Append(actor, companyId, purchaseId, "PurchasePosted", correlationId, purchase.Version);
        events.Append(actor.TenantId, companyId, purchaseId, purchase.Version, "PurchasePostedV1", correlationId,
            new { purchaseId, planId, plan.WarehouseId, purchase.ExternalReference, purchase.Currency, plan.Snapshot.NetAmount, plan.Snapshot.TaxAmount, plan.Snapshot.RecoverableTaxAmount });
        return new(200, PurchaseResponse.From(purchase));
    }

    public async Task<CommandOutcome> Cancel(Actor actor, Guid companyId, Guid purchaseId, Guid planId,
        CancelPostingRequest request, string correlationId, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        var purchase = await RequirePurchase(actor, companyId, purchaseId, ct);
        var plan = await plans.FindPlan(actor.TenantId, companyId, purchaseId, planId, ct)
            ?? throw new DomainException("not_found", "Purchase posting plan not found.", 404);
        if (plan.SourceVersion != request.ExpectedVersion)
            throw new DomainException("version_conflict", "The request must reference the prepared purchase version.", 409);
        plan.Cancel(request.Reason);
        purchase.CancelPosting(request.ExpectedVersion, clock.GetUtcNow());
        audit.Append(actor, companyId, purchaseId, "PurchasePostingCancelled", correlationId, purchase.Version, request.Reason);
        return new(200, PurchaseResponse.From(purchase));
    }

    private async Task<Vat.Domain.Purchasing.Purchase> RequirePurchase(Actor actor, Guid companyId, Guid id, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        return await purchases.Find(actor.TenantId, companyId, id, ct)
            ?? throw new DomainException("not_found", "Purchase not found.", 404);
    }
}

