using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Vat.Api.Security;
using Vat.Application;
using Vat.Application.Security;
using Vat.Infrastructure.Persistence;
using Vat.Messaging.Contracts.V1;

namespace Vat.Hosting;

public static class ReportingSourceEndpoints
{
    public static WebApplication MapReportingSourceV1(this WebApplication app)
    {
        var group = app.MapGroup(ReportingSourceV1.Prefix);
        group.MapGet("/journal", async (HttpContext context, Guid companyId, Guid epoch,
            long after, long through, int? take, ReportingSourceJournal source, CancellationToken ct) =>
            Results.Ok(await source.Read(context.GetActor(), companyId, epoch,
                after, through, take ?? 100, ct)))
            .RequireAuthorization(Permissions.ReportRead);
        group.MapPost("", (HttpContext context, Guid companyId, PrepareReportingExportV1 request,
            ReportingSourceExportService source, ICommandExecutor commands, CancellationToken ct) =>
            ApiCommands.Execute(context, commands, companyId, "reporting-source.prepare-v1", request,
                () => source.Prepare(context.GetActor(), companyId, request.Reason, context.TraceIdentifier, ct), ct))
            .RequireAuthorization(Permissions.ReportManage);
        group.MapGet("/{id:guid}", async (HttpContext context, Guid companyId, Guid id,
            ReportingSourceExportService source, CancellationToken ct) =>
            Results.Ok(await source.Manifest(context.GetActor(), companyId, id, ct)))
            .RequireAuthorization(Permissions.ReportRead);
        group.MapGet("/{id:guid}/rows", async (HttpContext context, Guid companyId, Guid id, int? offset, int? take,
            ReportingSourceExportService source, CancellationToken ct) =>
            Results.Ok(await source.Page(context.GetActor(), companyId, id, offset ?? 0, take ?? 100, ct)))
            .RequireAuthorization(Permissions.ReportRead);
        var customers = app.MapGroup(CustomerHistoryV1.Prefix);
        group.MapGet("/checkpoint", async (HttpContext context, Guid companyId,
            ReportingSourceExportService source, CancellationToken ct) =>
            Results.Ok(await source.Checkpoint(context.GetActor(), companyId, ct)))
            .RequireAuthorization(Permissions.ReportRead);
        group.MapGet("/{id:guid}/boundary", async (HttpContext context, Guid companyId, Guid id,
            ReportingSourceExportService source, CancellationToken ct) =>
            Results.Ok(await source.Boundary(context.GetActor(), companyId, id, ct)))
            .RequireAuthorization(Permissions.ReportRead);
        customers.MapPost("", (HttpContext context, Guid companyId, Guid customerId, PrepareReportingExportV1 request,
            CustomerHistoryExportService source, ICommandExecutor commands, CancellationToken ct) =>
            ApiCommands.Execute(context, commands, companyId, $"customer-history.prepare-v1:{customerId}", request,
                () => source.Prepare(context.GetActor(), companyId, customerId, request.Reason, context.TraceIdentifier, ct), ct))
            .RequireAuthorization(Permissions.ReportManage);
        customers.MapGet("/{id:guid}", async (HttpContext context, Guid companyId, Guid customerId, Guid id,
            CustomerHistoryExportService source, CancellationToken ct) =>
            Results.Ok(await source.Read(context.GetActor(), companyId, customerId, id, ct)))
            .RequireAuthorization(Permissions.ReportRead);
        return app;
    }
}
