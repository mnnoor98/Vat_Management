using Vat.Domain;
using Vat.Domain.Company;

namespace Vat.Services.Company;

public sealed class PeriodGuard(IPeriodStore periods) : IPeriodGuard
{
    public Task LockShared(Guid tenantId, Guid companyId, CancellationToken ct) => periods.LockCompany(tenantId, companyId, false, ct);

    public async Task<FiscalPeriod> RequireOpen(Guid tenantId, Guid companyId, DateOnly date, CancellationToken ct)
    {
        await LockShared(tenantId, companyId, ct);
        var period = await periods.ForDate(tenantId, companyId, date, ct)
            ?? throw new DomainException("period_unavailable", "Create a fiscal period covering the business date before admitting this operation.", 409);
        if (period.Status != FiscalPeriodStatus.Open)
            throw new DomainException("period_not_open", "The operation's fiscal period is closing or closed.", 409);
        return period;
    }
}
