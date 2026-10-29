using Vat.Application;

namespace Vat.Services.Workflow;

public interface IWorkflowTaskStore
{
    Task<IReadOnlyList<ModuleRecord>> List(Guid tenantId, Guid companyId, Guid? before, int take, CancellationToken ct);
}
