using Vat.Application.Security;
using Vat.Domain;
using Vat.Messaging.Contracts.V1;
using Vat.Services.Reporting.Domain;

namespace Vat.Services.Reporting.Application;

public sealed record BootstrapCustomerRequest(Guid ExportId, string Reason);
public sealed record CustomerSnapshotResult(Guid GenerationId, Guid ExportId, Guid CustomerId, string FingerprintSha256,
    DateTimeOffset CapturedAt, bool SnapshotOnly = true, bool LiveCoverageVerified = false);
public sealed record CustomerSnapshotReport<T>(CustomerSnapshotResult Snapshot, T Report);
public interface ICustomerHistorySource
{
    Task<CustomerHistoryExportV1> CustomerHistory(Guid company, Guid customer, Guid export, CancellationToken ct);
}
public interface ICustomerSnapshotStore
{
    Task<CustomerSnapshotResult?> Previous(Actor actor, Guid company, Guid customer, Guid key, BootstrapCustomerRequest request, CancellationToken ct);
    Task<CustomerSnapshotResult> Import(Actor actor, Guid company, Guid customer, Guid key, BootstrapCustomerRequest request, VerifiedCustomerHistory history, CancellationToken ct);
    Task<(CustomerSnapshotResult Snapshot, VerifiedCustomerHistory History)> Read(Actor actor, Guid company, Guid customer, Guid generation, CancellationToken ct);
}
public sealed class CustomerSnapshotService(ICustomerSnapshotStore store)
{
    public async Task<CustomerSnapshotResult> Bootstrap(Actor actor, Guid company, Guid customer, Guid key,
        BootstrapCustomerRequest request, ICustomerHistorySource source, CancellationToken ct)
    {
        Require(actor, company, customer, Permissions.ReportManage);
        if (key == Guid.Empty || request.ExportId == Guid.Empty || string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 1000)
            throw new DomainException("validation_failed", "Supply command/export UUIDs and a reason of at most 1000 characters.");
        var previous = await store.Previous(actor, company, customer, key, request, ct);
        if (previous is not null) return previous;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(TimeSpan.FromSeconds(20));
        CustomerHistoryExportV1 export;
        try { export = await source.CustomerHistory(company, customer, request.ExportId, deadline.Token).WaitAsync(deadline.Token); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { throw new DomainException("reporting_source_unavailable", "Customer history download exceeded its deadline.", 503); }
        if (export?.Manifest?.ExportId != request.ExportId)
            throw new DomainException("invalid_customer_history", "Source returned another export.", 409);
        var history = VerifiedCustomerHistory.Verify(export, actor.TenantId, company, customer);
        if (deadline.IsCancellationRequested)
        { ct.ThrowIfCancellationRequested(); throw new DomainException("reporting_source_unavailable", "Customer history verification exceeded its deadline.", 503); }
        return await store.Import(actor, company, customer, key, request, history, ct);
    }
    public async Task<CustomerSnapshotReport<CustomerSnapshotStatement>> Statement(Actor actor, Guid company, Guid customer,
        Guid generation, DateOnly from, DateOnly until, CancellationToken ct)
    {
        Require(actor, company, customer, Permissions.ReportRead);
        var stored = await store.Read(actor, company, customer, generation, ct);
        return new(stored.Snapshot, CustomerHistoryProjection.Statement(stored.History, from, until));
    }
    public async Task<CustomerSnapshotReport<CustomerSnapshotAgeing>> Ageing(Actor actor, Guid company, Guid customer,
        Guid generation, DateOnly asOf, CancellationToken ct)
    {
        Require(actor, company, customer, Permissions.ReportRead);
        var stored = await store.Read(actor, company, customer, generation, ct);
        return new(stored.Snapshot, CustomerHistoryProjection.Ageing(stored.History, asOf));
    }
    private static void Require(Actor actor, Guid company, Guid customer, string permission)
    {
        actor.RequireCompany(company);
        if (customer == Guid.Empty || actor.TenantId == Guid.Empty || string.IsNullOrWhiteSpace(actor.Subject) || actor.Subject.Length > 200)
            throw new DomainException("validation_failed", "A verified tenant, customer and operator identity are required.");
        if (!actor.Permissions.Contains(permission)) throw new DomainException("permission_denied", "Report permission is required.", 403);
    }
}
