using Vat.Application.Contracts;
using Vat.Application.Security;
using Vat.Domain;
using Vat.Domain.Posting;
using Vat.Domain.Purchasing;

namespace Vat.Application;

public sealed class ReversalService(IPurchaseStore purchases, IPostingStore postings, IReversalStore reversals,
    IPeriodGuard periods, IAuditWriter audit, IEventWriter events, TimeProvider clock, PostingPolicy policy)
{
    public async Task<CommandOutcome> Request(Actor actor, Guid companyId, Guid purchaseId, RequestReversalRequest request, string correlationId, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        await periods.RequireOpen(actor.TenantId, companyId, request.CorrectionDate, ct);
        var purchase = await RequirePurchase(actor, companyId, purchaseId, ct);
        var original = await Reconciled(actor, companyId, purchaseId, ct);
        purchase.RequestReversal(request.ExpectedVersion, clock.GetUtcNow());
        var reversal = PurchaseReversal.Request(purchase, original.Plan!.Id, request.CorrectionDate, request.Reason, actor.Subject, clock.GetUtcNow());
        reversals.Add(reversal);
        audit.Append(actor, companyId, purchaseId, "PurchaseReversalRequested", correlationId, purchase.Version, reversal.Reason);
        return new(201, ReversalResponse.From(reversal), $"/api/v1/companies/{companyId}/purchases/{purchaseId}/reversals/{reversal.Id}");
    }

    public async Task<CommandOutcome> Authorize(Actor actor, Guid companyId, Guid purchaseId, Guid reversalId,
        TransitionRequest request, string correlationId, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        if (!policy.Enabled) throw new DomainException("posting_disabled", "Ledger posting is disabled in this environment.", 409);
        var reversal = await RequireReversal(actor, companyId, purchaseId, reversalId, ct);
        await periods.RequireOpen(actor.TenantId, companyId, reversal.CorrectionDate, ct);
        var purchase = await RequirePurchase(actor, companyId, purchaseId, ct);
        if (reversal.SourceVersion != request.ExpectedVersion) throw new DomainException("version_conflict", "Use the pending reversal's source version.", 409);
        var original = await Reconciled(actor, companyId, purchaseId, ct);
        var now = clock.GetUtcNow();
        reversal.Authorize(actor.Subject, purchase.CreatedBy, now);
        purchase.Reverse(request.ExpectedVersion, now);
        await reversals.Correct(reversal, original, now, ct);
        audit.Append(actor, companyId, purchaseId, "PurchaseReversed", correlationId, purchase.Version, reversal.Reason);
        events.Append(actor.TenantId, companyId, purchaseId, purchase.Version, nameof(PurchaseReversedV1), correlationId,
            new PurchaseReversedV1(purchaseId, reversal.Id, reversal.PlanId, reversal.CorrectionDate, reversal.Reason));
        var plan = original.Plan!;
        events.Append(actor.TenantId, companyId, purchaseId, purchase.Version, nameof(FinancialEntryRecordedV1), correlationId,
            new FinancialEntryRecordedV1(reversal.Id, purchaseId, purchase.BranchId, plan.WarehouseId, "PurchaseReversal", reversal.CorrectionDate,
                purchase.ExternalReference, purchase.Currency, -plan.Snapshot.NetAmount, -plan.Snapshot.TaxAmount,
                -plan.Snapshot.RecoverableTaxAmount, reversal.PlanId));
        return new(200, PurchaseResponse.From(purchase));
    }

    public async Task<CommandOutcome> Cancel(Actor actor, Guid companyId, Guid purchaseId, Guid reversalId,
        CancelPostingRequest request, string correlationId, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        await periods.LockShared(actor.TenantId, companyId, ct);
        var purchase = await RequirePurchase(actor, companyId, purchaseId, ct);
        var reversal = await RequireReversal(actor, companyId, purchaseId, reversalId, ct);
        if (reversal.SourceVersion != request.ExpectedVersion) throw new DomainException("version_conflict", "Use the pending reversal's source version.", 409);
        reversal.Cancel(request.Reason);
        purchase.CancelReversal(request.ExpectedVersion, clock.GetUtcNow());
        audit.Append(actor, companyId, purchaseId, "PurchaseReversalCancelled", correlationId, purchase.Version, reversal.CancellationReason);
        return new(200, PurchaseResponse.From(purchase));
    }

    public async Task<CommandOutcome> CancelUnposted(Actor actor, Guid companyId, Guid purchaseId, CancelPostingRequest request, string correlationId, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        await periods.LockShared(actor.TenantId, companyId, ct);
        var purchase = await RequirePurchase(actor, companyId, purchaseId, ct);
        purchase.CancelUnposted(request.ExpectedVersion, request.Reason, clock.GetUtcNow());
        audit.Append(actor, companyId, purchaseId, "PurchaseCancelledBeforePosting", correlationId, purchase.Version, purchase.CancellationReason);
        return new(200, PurchaseResponse.From(purchase));
    }

    public async Task<ReversalResponse> Get(Actor actor, Guid companyId, Guid purchaseId, Guid reversalId, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        return ReversalResponse.From(await RequireReversal(actor, companyId, purchaseId, reversalId, ct));
    }

    private async Task<PostingReadSnapshot> Reconciled(Actor actor, Guid companyId, Guid purchaseId, CancellationToken ct)
    {
        var original = await postings.ReadReconciliation(actor.TenantId, companyId, purchaseId, ct);
        if (original is null || !PurchaseReconciler.Evaluate(original).IsReconciled)
            throw new DomainException("reconciliation_failed", "The original purchase must reconcile before reversal.", 409);
        return original;
    }

    private async Task<Purchase> RequirePurchase(Actor actor, Guid companyId, Guid purchaseId, CancellationToken ct) =>
        await purchases.Find(actor.TenantId, companyId, purchaseId, ct) ?? throw new DomainException("not_found", "Purchase not found.", 404);

    private async Task<PurchaseReversal> RequireReversal(Actor actor, Guid companyId, Guid purchaseId, Guid id, CancellationToken ct) =>
        await reversals.Find(actor.TenantId, companyId, purchaseId, id, ct) ?? throw new DomainException("not_found", "Reversal not found.", 404);
}
