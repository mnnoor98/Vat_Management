using Vat.Application.Security;
using Vat.Domain.Company;

namespace Vat.Services.Company
{
    public sealed record CommandOutcome(int StatusCode, object Body, string? Location = null);
    public sealed record StoredResponse(int StatusCode, string Json, string? Location, bool Replayed);

    public interface ICommandExecutor
    {
        Task<StoredResponse> Execute(Actor actor, Guid companyId, string operation, string key,
            object request, Func<Task<CommandOutcome>> action, CancellationToken ct);
    }

    public interface IAuditWriter
    {
        void Append(Actor actor, Guid companyId, Guid resourceId, string action, string correlationId,
            long? version = null, string? reason = null);
    }

    public interface IEventWriter
    {
        void Append<T>(Guid tenantId, Guid companyId, Guid aggregateId, long version,
            string eventType, string correlationId, T payload);
    }

    public interface ICompanyStore
    {
        Task<Vat.Domain.Company.Company?> FindCompany(Guid tenantId, Guid companyId, CancellationToken ct);
        Task<Branch?> FindBranch(Guid tenantId, Guid companyId, Guid branchId, CancellationToken ct);
        Task<IReadOnlyList<Branch>> ListBranches(Guid tenantId, Guid companyId, CancellationToken ct);
        void Add(Vat.Domain.Company.Company company);
        void Add(Branch branch);
    }

    public interface IPeriodStore
    {
        Task LockCompany(Guid tenantId, Guid companyId, bool exclusive, CancellationToken ct);
        Task<FiscalPeriod?> Find(Guid tenantId, Guid companyId, Guid id, CancellationToken ct);
        Task<FiscalPeriod?> ForDate(Guid tenantId, Guid companyId, DateOnly date, CancellationToken ct);
        Task<bool> Overlaps(FiscalPeriod period, CancellationToken ct);
        Task<bool> HasPending(FiscalPeriod period, CancellationToken ct);
        Task<IReadOnlyList<FiscalPeriod>> List(Guid tenantId, Guid companyId, Guid? before, int take, CancellationToken ct);
        void Add(FiscalPeriod period);
    }

    public interface IPeriodGuard
    {
        Task<FiscalPeriod> RequireOpen(Guid tenantId, Guid companyId, DateOnly date, CancellationToken ct);
        Task LockShared(Guid tenantId, Guid companyId, CancellationToken ct);
    }
}

namespace Vat.Services.Company.Contracts
{
    public sealed record CreateCompanyRequest(Guid Id, string Name, string RegistrationReference);
    public sealed record CreateBranchRequest(string Name);
    public sealed record CompanyResponse(Guid Id, string Name, string RegistrationReference);
    public sealed record BranchResponse(Guid Id, Guid CompanyId, string Name);

    public sealed record CreatePeriodRequest(string Name, DateOnly From, DateOnly Until);
    public sealed record PeriodActionRequest(long ExpectedVersion, string Reason);
    public sealed record PeriodResponse(Guid Id, Guid CompanyId, string Name, DateOnly From, DateOnly Until, string Status, long Version)
    {
        public static PeriodResponse FromPeriod(FiscalPeriod period) =>
            new(period.Id, period.CompanyId, period.Name, period.From, period.Until, period.Status.ToString(), period.Version);
    }
}
