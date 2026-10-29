using Vat.Application;

namespace Vat.Services.Compliance;

public interface IComplianceReturnStore
{
    Task<IReadOnlyList<ModuleRecord>> List(Guid tenantId, Guid companyId, Guid? before, int take, CancellationToken ct);
}
