using Vat.Application;

namespace Vat.Services.Integration;

public interface IIntegrationConnectionStore
{
    Task<IReadOnlyList<ModuleRecord>> List(Guid tenantId, Guid companyId, Guid? before, int take, CancellationToken ct);
}
