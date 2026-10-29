using Vat.Application.Security;
using Vat.Domain;
using Vat.Domain.Purchasing;
using Vat.Services.Purchase.Contracts;

namespace Vat.Services.Purchase;

public sealed class PurchaseService(IPurchaseStore purchases, IAuditWriter audit, TimeProvider clock)
{
    public async Task<PurchaseResponse> Get(Actor actor, Guid companyId, Guid id, CancellationToken ct)
        => PurchaseResponse.From(await RequirePurchase(actor, companyId, id, ct));

    public async Task<IReadOnlyList<PurchaseResponse>> List(Actor actor, Guid companyId, Guid? before, int take, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        if (take is < 1 or > 100)
            throw new DomainException("validation_failed", "take must be between 1 and 100.");
        return (await purchases.List(actor.TenantId, companyId, before, take, ct)).Select(PurchaseResponse.From).ToArray();
    }

    public Task<CommandOutcome> Create(Actor actor, Guid companyId, CreatePurchaseRequest request, string correlationId, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        var purchase = Vat.Domain.Purchasing.Purchase.Create(actor.TenantId, companyId, request.BranchId, request.ExternalReference,
            request.SupplierReference, request.BusinessDate, actor.Subject, request.Lines, clock.GetUtcNow());
        purchases.Add(purchase);
        audit.Append(actor, companyId, purchase.Id, "PurchaseCreated", correlationId, purchase.Version);
        return Task.FromResult(new CommandOutcome(201, PurchaseResponse.From(purchase), $"/api/v1/companies/{companyId}/purchases/{purchase.Id}"));
    }

    public async Task<CommandOutcome> Submit(Actor actor, Guid companyId, Guid id, TransitionRequest request, string correlationId, CancellationToken ct)
    {
        var purchase = await RequirePurchase(actor, companyId, id, ct);
        purchase.Submit(request.ExpectedVersion, clock.GetUtcNow());
        audit.Append(actor, companyId, id, "PurchaseSubmitted", correlationId, purchase.Version);
        return new(200, PurchaseResponse.From(purchase));
    }

    public async Task<CommandOutcome> Approve(Actor actor, Guid companyId, Guid id, TransitionRequest request, string correlationId, CancellationToken ct)
    {
        var purchase = await RequirePurchase(actor, companyId, id, ct);
        purchase.Approve(actor.Subject, request.ExpectedVersion, clock.GetUtcNow());
        audit.Append(actor, companyId, id, "PurchaseApproved", correlationId, purchase.Version);
        return new(200, PurchaseResponse.From(purchase));
    }

    public async Task<CommandOutcome> Reject(Actor actor, Guid companyId, Guid id, RejectPurchaseRequest request, string correlationId, CancellationToken ct)
    {
        var purchase = await RequirePurchase(actor, companyId, id, ct);
        purchase.Reject(actor.Subject, request.ExpectedVersion, request.Reason, clock.GetUtcNow());
        audit.Append(actor, companyId, id, "PurchaseRejected", correlationId, purchase.Version, request.Reason);
        return new(200, PurchaseResponse.From(purchase));
    }

    private async Task<Vat.Domain.Purchasing.Purchase> RequirePurchase(Actor actor, Guid companyId, Guid id, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        return await purchases.Find(actor.TenantId, companyId, id, ct)
            ?? throw new DomainException("not_found", "Purchase not found.", 404);
    }
}

