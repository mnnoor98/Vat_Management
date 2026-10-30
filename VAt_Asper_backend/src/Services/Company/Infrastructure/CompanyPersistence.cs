using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Vat.Services.Company;

namespace Vat.Services.Company.Infrastructure;

public static class CompanyPersistence
{
    public static void Configure(DbContextOptionsBuilder builder, string connectionString)
    {
        builder.UseNpgsql(connectionString);
    }

    public static IServiceCollection AddCompanyPersistence(this IServiceCollection services, IConfiguration config)
    {
        var cs = config.GetConnectionString("CompanyDatabase")
            ?? throw new InvalidOperationException("Configure ConnectionStrings:CompanyDatabase.");
        if (string.IsNullOrWhiteSpace(cs)) throw new InvalidOperationException("Configure ConnectionStrings:CompanyDatabase.");
        services.AddDbContext<CompanyDbContext>(options => Configure(options, cs));
        services.AddScoped<ICompanyStore, CompanyStore>();
        services.AddScoped<IPeriodStore, CompanyPeriodStore>();
        services.AddScoped<IPeriodGuard, PeriodGuard>();
        services.AddScoped<CompanyRecordWriters>();
        services.AddScoped<IAuditWriter>(sp => sp.GetRequiredService<CompanyRecordWriters>());
        services.AddScoped<IEventWriter>(sp => sp.GetRequiredService<CompanyRecordWriters>());
        services.AddScoped<ICommandExecutor, CompanyCommandExecutor>();
        services.AddScoped<CompanyService>();
        services.AddScoped<PeriodService>();
        return services;
    }

    public static async Task ApplyCompanyMigrationsAsync(this IHost host, CancellationToken ct = default)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CompanyDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<CompanyDbContext>>();
        try
        {
            await db.Database.MigrateAsync(ct);
            logger.LogInformation("CompanyDatabase migrations applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not apply automatic migrations for CompanyDatabase.");
        }
    }
}
