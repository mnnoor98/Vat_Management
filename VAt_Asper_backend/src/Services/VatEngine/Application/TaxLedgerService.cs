using Vat.Application.Security;
using Vat.Domain;

namespace Vat.Services.VatEngine;

public sealed class TaxLedgerService(ITaxLedgerStore store)
{
    public async Task<IReadOnlyList<TaxLedgerResponse>> TaxLedger(Actor actor, Guid companyId, Guid? purchaseId, Guid? before, int take, CancellationToken ct)
    {
        actor.RequireCompany(companyId);
        if (!actor.Permissions.Contains(VatEnginePermissions.TaxRead))
            throw new DomainException("permission_denied", "Tax read permission is required.", 403);

        if (take is < 1 or > 100)
            throw new DomainException("validation_failed", "Take must be between 1 and 100.");

        return await store.List(actor, companyId, purchaseId, before, take, ct);
    }
}

