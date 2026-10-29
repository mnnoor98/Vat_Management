using Vat.Application;

namespace Vat.Services.Manufacturing;

public interface IManufacturingOrderStore
{
    Task<IReadOnlyList<ModuleRecord>> List(Guid tenantId, Guid companyId, Guid? before, int take, CancellationToken ct);
}
