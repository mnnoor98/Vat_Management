using Vat.Domain.Company;
using Vat.Domain.Posting;

namespace Vat.Application;

public interface IPeriodStore
{
    Task LockCompany(Guid tenantId, Guid companyId, bool exclusive, CancellationToken ct);
    Task<FiscalPeriod?> Find(Guid tenantId, Guid companyId, Guid id, CancellationToken ct);
    Task<FiscalPeriod?> ForDate(Guid tenantId, Guid companyId, DateOnly date, CancellationToken ct);
    Task<bool> Overlaps(FiscalPeriod period, CancellationToken ct);
    Task<bool> HasPending(FiscalPeriod period, CancellationToken ct);
    Task<IReadOnlyList<Guid>> FinancialPurchaseIds(FiscalPeriod period, CancellationToken ct);
    Task<IReadOnlyList<Guid>> FinancialSalesIds(FiscalPeriod period, CancellationToken ct);
    Task<bool> BalancesReconcile(Guid tenantId, Guid companyId, CancellationToken ct);
    Task<PeriodCloseSnapshot> Snapshot(FiscalPeriod period, DateTimeOffset now, CancellationToken ct);
    Task<IReadOnlyList<FiscalPeriod>> List(Guid tenantId, Guid companyId, Guid? before, int take, CancellationToken ct);
    void Add(FiscalPeriod period);
}

public interface IPeriodGuard
{
    Task<FiscalPeriod> RequireOpen(Guid tenantId, Guid companyId, DateOnly date, CancellationToken ct);
    Task LockShared(Guid tenantId, Guid companyId, CancellationToken ct);
}

public sealed class PeriodGuard(IPeriodStore periods) : IPeriodGuard
{
    public async Task<FiscalPeriod> RequireOpen(Guid tenantId, Guid companyId, DateOnly date, CancellationToken ct)
    {
        await periods.LockCompany(tenantId, companyId, false, ct);
        var period = await periods.ForDate(tenantId, companyId, date, ct)
            ?? throw new Vat.Domain.DomainException("period_not_found", $"No fiscal period contains {date:yyyy-MM-dd}.", 409);
        if (period.Status != FiscalPeriodStatus.Open)
            throw new Vat.Domain.DomainException("period_closed", $"Fiscal period '{period.Name}' is {period.Status.ToString().ToLowerInvariant()}.", 409);
        return period;
    }

    public Task LockShared(Guid tenantId, Guid companyId, CancellationToken ct) => periods.LockCompany(tenantId, companyId, false, ct);
}

public interface IReversalStore
{
    Task<PurchaseReversal?> Find(Guid tenantId, Guid companyId, Guid purchaseId, Guid reversalId, CancellationToken ct);
    void Add(PurchaseReversal reversal);
    Task Correct(PurchaseReversal reversal, PostingReadSnapshot original, DateTimeOffset now, CancellationToken ct);
}
