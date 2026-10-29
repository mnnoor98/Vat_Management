using Vat.Application.Contracts;
using Vat.Application.Security;
using Vat.Domain;
using Vat.Domain.Posting;
using Vat.Domain.Purchasing;

namespace Vat.Application;

public sealed class PostingService(IPurchaseStore purchases, IReferenceStore references, IPostingStore postings,
    IAuditWriter audit, IEventWriter events, TimeProvider clock, PostingPolicy policy, IPeriodGuard periods)
{
    public async Task<CommandOutcome> Prepare(Actor actor, Guid companyId, Guid purchaseId, PreparePostingRequest request,
        string correlationId, CancellationToken ct)
    {
        var purchase = await RequirePurchase(actor, companyId, purchaseId, ct);
        await periods.RequireOpen(actor.TenantId, companyId, purchase.BusinessDate, ct);
        var warehouse = await references.Warehouse(actor.TenantId, companyId, request.WarehouseId, ct);
        if (warehouse is null || warehouse.BranchId != purchase.BranchId)
            throw new DomainException("warehouse_scope_mismatch", "A warehouse in the purchase branch is required.", 422);
        var supplier = await references.SupplierByReference(actor.TenantId, companyId, purchase.SupplierReference, ct)
            ?? throw new DomainException("supplier_unavailable", "The supplier reference must be registered before preparing posting.", 409);
        var lines = new List<CalculatedLine>();
        foreach (var line in purchase.Lines.OrderBy(x => x.Id))
        {
            var product = await references.ProductByReference(actor.TenantId, companyId, line.ProductReference, ct)
                ?? throw new DomainException("product_unavailable", $"Product {line.ProductReference} must be registered before preparing posting.", 409);
            var rule = await references.EffectiveRule(actor.TenantId, companyId, product.TaxCode, purchase.BusinessDate, ct)
                ?? throw new DomainException("tax_rule_unavailable", $"No published rule covers product {line.ProductReference} on the business date.", 409);
            lines.Add(PurchaseCalculator.Calculate(line, product, rule, purchase.BusinessDate));
        }
        purchase.PreparePosting(request.ExpectedVersion, clock.GetUtcNow());
        var plan = PurchasePostingPlan.Create(purchase, warehouse.Id, actor.Subject,
            PurchaseCalculator.Snapshot(supplier, purchase, lines), clock.GetUtcNow());
        postings.Add(plan);
        audit.Append(actor, companyId, purchaseId, "PurchasePostingPrepared", correlationId, purchase.Version);
        return new(201, PostingPlanResponse.From(plan), $"/api/v1/companies/{companyId}/purchases/{purchaseId}/posting-plans/{plan.Id}");
    }

    public async Task<CommandOutcome> Post(Actor actor, Guid companyId, Guid purchaseId, Guid planId,
        TransitionRequest request, string correlationId, CancellationToken ct)
    {
        if (!policy.Enabled)
            throw new DomainException("posting_disabled", "Ledger posting is disabled in this environment.", 409);
        var purchase = await RequirePurchase(actor, companyId, purchaseId, ct);
        var plan = await RequirePlan(actor, companyId, purchaseId, planId, ct);
        await periods.RequireOpen(actor.TenantId, companyId, purchase.BusinessDate, ct);
        if (plan.SourceVersion != request.ExpectedVersion)
            throw new DomainException("version_conflict", "The request must reference the prepared purchase version.", 409);
        var now = clock.GetUtcNow();
        plan.Post(actor.Subject, purchase.CreatedBy, now);
        purchase.Post(request.ExpectedVersion, now);
        // The command executor commits all participants in the same local transaction.
        await postings.Receive(plan, now, ct);
        postings.RecordTax(plan, now);
        audit.Append(actor, companyId, purchaseId, "PurchasePosted", correlationId, purchase.Version);
        events.Append(actor.TenantId, companyId, purchaseId, purchase.Version, nameof(PurchasePostedV1), correlationId,
            new PurchasePostedV1(purchaseId, plan.Id, plan.WarehouseId, purchase.Version, purchase.Currency,
                plan.Snapshot.NetAmount, plan.Snapshot.TaxAmount, plan.Snapshot.RecoverableTaxAmount));
        events.Append(actor.TenantId, companyId, purchaseId, purchase.Version, nameof(StockReceiptPostedV1), correlationId,
            new StockReceiptPostedV1(purchaseId, plan.Id, plan.WarehouseId, plan.Snapshot.Lines.Count));
        events.Append(actor.TenantId, companyId, purchaseId, purchase.Version, nameof(TaxPostingCompletedV1), correlationId,
            new TaxPostingCompletedV1(purchaseId, plan.Id, plan.Snapshot.TaxAmount, plan.Snapshot.RecoverableTaxAmount));
        events.Append(actor.TenantId, companyId, purchaseId, purchase.Version, nameof(FinancialEntryRecordedV1), correlationId,
            new FinancialEntryRecordedV1(plan.Id, purchaseId, purchase.BranchId, plan.WarehouseId, "Purchase", purchase.BusinessDate,
                purchase.ExternalReference, purchase.Currency, plan.Snapshot.NetAmount, plan.Snapshot.TaxAmount, plan.Snapshot.RecoverableTaxAmount, null));
        return new(200, PurchaseResponse.From(purchase));
    }

    public async Task<CommandOutcome> Cancel(Actor actor, Guid companyId, Guid purchaseId, Guid planId,
        CancelPostingRequest request, string correlationId, CancellationToken ct)
    {
        Guard.Text(request.Reason, nameof(request.Reason), 1000);
        actor.RequireCompany(companyId);
        await periods.LockShared(actor.TenantId, companyId, ct);
        var purchase = await RequirePurchase(actor, companyId, purchaseId, ct);
        var plan = await RequirePlan(actor, companyId, purchaseId, planId, ct);
        if (plan.SourceVersion != request.ExpectedVersion)
            throw new DomainException("version_conflict", "The request must reference the prepared purchase version.", 409);
        plan.Cancel(request.Reason);
        purchase.CancelPosting(request.ExpectedVersion, clock.GetUtcNow());
        audit.Append(actor, companyId, purchaseId, "PurchasePostingCancelled", correlationId, purchase.Version);
        return new(200, PurchaseResponse.From(purchase));
    }

    public async Task<PostingPlanResponse> GetPlan(Actor actor, Guid companyId, Guid purchaseId, Guid planId, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        return PostingPlanResponse.From(await RequirePlan(actor, companyId, purchaseId, planId, ct));
    }

    public async Task<PostingStatusResponse> Status(Actor actor, Guid companyId, Guid purchaseId, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        var snapshot = await postings.ReadReconciliation(actor.TenantId, companyId, purchaseId, ct)
            ?? throw new DomainException("not_found", "Purchase not found.", 404);
        return new(purchaseId, snapshot.Purchase.Status.ToString(), snapshot.Purchase.Version,
            snapshot.Plan is null ? null : PostingPlanResponse.From(snapshot.Plan));
    }

    public async Task<ReconciliationResponse> Reconcile(Actor actor, Guid companyId, Guid purchaseId, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        var snapshot = await postings.ReadReconciliation(actor.TenantId, companyId, purchaseId, ct)
            ?? throw new DomainException("not_found", "Purchase not found.", 404);
        return PurchaseReconciler.Evaluate(snapshot);
    }

    public async Task<IReadOnlyList<StockBalanceResponse>> Balances(Actor actor, Guid companyId, Guid warehouseId,
        Guid? beforeProduct, int take, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        PageSize(take);
        if (await references.Warehouse(actor.TenantId, companyId, warehouseId, ct) is null)
            throw new DomainException("not_found", "Warehouse not found.", 404);
        return (await postings.Balances(actor.TenantId, companyId, warehouseId, beforeProduct, take, ct))
            .Select(x => new StockBalanceResponse(x.WarehouseId, x.ProductId, x.Quantity, x.ReservedQuantity, x.AvailableQuantity)).ToArray();
    }

    public async Task<IReadOnlyList<TaxLedgerResponse>> TaxLedger(Actor actor, Guid companyId, Guid? purchaseId,
        Guid? before, int take, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        PageSize(take);
        return (await postings.TaxLedger(actor.TenantId, companyId, purchaseId, before, take, ct))
            .Select(x => new TaxLedgerResponse(x.Id, x.PurchaseId, x.PurchaseLineId, x.PlanId, x.TaxRuleId,
                x.BusinessDate, x.NetAmount, x.TaxAmount, x.RecoverableTaxAmount, x.ReversesEntryId, x.ReversalId)).ToArray();
    }

    private async Task<Purchase> RequirePurchase(Actor actor, Guid companyId, Guid purchaseId, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        return await purchases.Find(actor.TenantId, companyId, purchaseId, ct)
            ?? throw new DomainException("not_found", "Purchase not found.", 404);
    }

    private async Task<PurchasePostingPlan> RequirePlan(Actor actor, Guid companyId, Guid purchaseId, Guid planId, CancellationToken ct) =>
        await postings.FindPlan(actor.TenantId, companyId, purchaseId, planId, ct)
        ?? throw new DomainException("not_found", "Posting plan not found.", 404);

    private static void PageSize(int take)
    {
        if (take is < 1 or > 100) throw new DomainException("validation_failed", "take must be between 1 and 100.");
    }
}
