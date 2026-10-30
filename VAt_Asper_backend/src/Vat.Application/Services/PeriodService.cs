using Vat.Application.Security;
using Vat.Domain;
using Vat.Domain.Company;
using Vat.Application.Contracts;

namespace Vat.Application;

public sealed class PeriodService(IPeriodStore periods, ICompanyStore companies,
    IAuditWriter audit, IEventWriter events, TimeProvider clock, IPostingStore postings, ISalesPostingStore sales)
{
    public async Task<CommandOutcome> Create(Actor actor, Guid companyId, CreatePeriodRequest request, string correlationId, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        await periods.LockCompany(actor.TenantId, companyId, true, ct);
        if (await companies.FindCompany(actor.TenantId, companyId, ct) is null)
            throw new DomainException("not_found", "Company not found.", 404);
        var period = FiscalPeriod.Create(actor.TenantId, companyId, request.Name, request.From, request.Until);
        if (await periods.Overlaps(period, ct)) throw new DomainException("period_overlap", "Fiscal periods cannot overlap within a company.", 409);
        periods.Add(period);
        audit.Append(actor, companyId, period.Id, "FiscalPeriodCreated", correlationId, period.Version);
        return new(201, PeriodResponse.FromPeriod(period), $"/api/v1/companies/{companyId}/periods/{period.Id}");
    }

    public async Task<CommandOutcome> RequestClose(Actor actor, Guid companyId, Guid id, PeriodActionRequest request, string correlationId, CancellationToken ct)
    {
        var period = await LockedPeriod(actor, companyId, id, ct);
        var reason = Guard.Text(request.Reason, nameof(request.Reason), 1000);
        if (await periods.HasPending(period, ct))
            throw new DomainException("period_has_pending_operations", "Resolve pending approved purchases, sales invoices, posting plans and reversals before requesting close.", 409);
        period.RequestClose(actor.Subject, request.ExpectedVersion);
        audit.Append(actor, companyId, id, "FiscalPeriodCloseRequested", correlationId, period.Version, reason);
        return new(200, PeriodResponse.FromPeriod(period));
    }

    public async Task<CommandOutcome> Close(Actor actor, Guid companyId, Guid id, PeriodActionRequest request, string correlationId, CancellationToken ct)
    {
        var period = await LockedPeriod(actor, companyId, id, ct);
        var reason = Guard.Text(request.Reason, nameof(request.Reason), 1000);
        if (await periods.HasPending(period, ct))
            throw new DomainException("period_has_pending_operations", "Pending financial operations prevent closing.", 409);
        foreach (var purchaseId in await periods.FinancialPurchaseIds(period, ct))
        {
            var read = await postings.ReadReconciliation(actor.TenantId, companyId, purchaseId, ct);
            if (read is null || !PurchaseReconciler.Evaluate(read).IsReconciled)
                throw new DomainException("reconciliation_failed", "Purchase participants do not reconcile.", 409);
        }
        foreach (var invoiceId in await periods.FinancialSalesIds(period, ct))
        {
            var read = await sales.Read(actor.TenantId, companyId, invoiceId, ct);
            if (read is null || !SalesReconciler.Evaluate(read).IsReconciled)
                throw new DomainException("reconciliation_failed", "Sales participants do not reconcile.", 409);
        }
        if (!await periods.BalancesReconcile(actor.TenantId, companyId, ct))
            throw new DomainException("reconciliation_failed", "Stock, reservation or payment projections do not reconcile.", 409);
        var snapshot = await periods.Snapshot(period, clock.GetUtcNow(), ct);
        period.Close(actor.Subject, request.ExpectedVersion, snapshot, clock.GetUtcNow());
        audit.Append(actor, companyId, id, "FiscalPeriodClosed", correlationId, period.Version, reason);
        events.Append(actor.TenantId, companyId, period.Id, period.Version, nameof(PeriodClosedV1), correlationId,
            new PeriodClosedV1(period.Id, period.From, period.Until, snapshot.LedgerFingerprint,
                snapshot.NetAmount, snapshot.TaxAmount, snapshot.RecoverableTaxAmount));
        return new(200, PeriodResponse.FromPeriod(period));
    }

    public async Task<CommandOutcome> Reopen(Actor actor, Guid companyId, Guid id, PeriodActionRequest request, string correlationId, CancellationToken ct)
    {
        var period = await LockedPeriod(actor, companyId, id, ct);
        var reason = Guard.Text(request.Reason, nameof(request.Reason), 1000);
        period.CancelClose(request.ExpectedVersion);
        audit.Append(actor, companyId, id, "FiscalPeriodReopened", correlationId, period.Version, reason);
        return new(200, PeriodResponse.FromPeriod(period));
    }

    public async Task<PeriodResponse> Get(Actor actor, Guid companyId, Guid id, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        var period = await periods.Find(actor.TenantId, companyId, id, ct)
            ?? throw new DomainException("not_found", "Fiscal period not found.", 404);
        return PeriodResponse.FromPeriod(period);
    }

    public async Task<IReadOnlyList<PeriodResponse>> List(Actor actor, Guid companyId, Guid? before, int take, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        if (take is < 1 or > 100) throw new DomainException("validation_failed", "take must be between 1 and 100.");
        return (await periods.List(actor.TenantId, companyId, before, take, ct))
            .Select(PeriodResponse.FromPeriod).ToArray();
    }

    private async Task<FiscalPeriod> LockedPeriod(Actor actor, Guid companyId, Guid id, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        await periods.LockCompany(actor.TenantId, companyId, true, ct);
        return await periods.Find(actor.TenantId, companyId, id, ct)
            ?? throw new DomainException("not_found", "Fiscal period not found.", 404);
    }
}
