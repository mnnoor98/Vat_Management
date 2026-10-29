using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Vat.Services.Reporting.Application;

namespace Vat.Services.Reporting.Infrastructure;

public static class ReportingPersistence
{
    public static IServiceCollection AddReportingGenerationCore(this IServiceCollection services, string connection)
    {
        if (string.IsNullOrWhiteSpace(connection)) throw new ArgumentException("Configure the owned Reporting connection.", nameof(connection));
        services.AddDbContext<ReportingDbContext>(options => Configure(options, connection));
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IReportingGenerationStore, ReportingGenerationStore>();
        services.AddScoped<ReportingGenerationService>();
        services.AddScoped<ICustomerSnapshotStore, CustomerSnapshotStore>();
        services.AddScoped<CustomerSnapshotService>();
        services.AddScoped<IFinancialGenerationInbox, FinancialGenerationInbox>();
        services.AddScoped<IFinancialCoverageStore, FinancialCoverageStore>();
        services.AddScoped<FinancialCoverageService>();
        return services;
    }

    public static IServiceCollection AddReportingPersistence(this IServiceCollection services, IConfiguration config)
    {
        var cs = config.GetConnectionString("ReportingDatabase")
            ?? "Host=127.0.0.1;Port=5432;Database=vat_reporting;Username=postgres;Password=123456;";
        services.AddReportingGenerationCore(cs);
        return services;
    }

    public static void Configure(DbContextOptionsBuilder options, string connection) =>
        options.UseNpgsql(connection, pg => pg.MigrationsHistoryTable("__ReportingMigrationsHistory", "reporting"));

    public static async Task ApplyReportingMigrationsAsync(this IHost host, CancellationToken ct = default)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ReportingDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ReportingDbContext>>();
        try
        {
            await db.Database.MigrateAsync(ct);
            logger.LogInformation("ReportingDatabase migrations applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not apply automatic migrations for ReportingDatabase.");
        }
    }
}
