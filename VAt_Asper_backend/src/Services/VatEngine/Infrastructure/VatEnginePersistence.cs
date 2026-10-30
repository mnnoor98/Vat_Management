using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Vat.Application.Security;

namespace Vat.Services.VatEngine.Infrastructure;

public sealed class VatEngineRecordWriters(VatEngineDbContext db, TimeProvider clock) : IAuditWriter, IEventWriter
{
    public void Append(Actor actor, Guid companyId, Guid resourceId, string action, string correlationId,
        long? version = null, string? reason = null)
    {
        db.Audits.Add(new VatEngineAudit
        {
            Id = Guid.NewGuid(),
            TenantId = actor.TenantId,
            CompanyId = companyId,
            ResourceId = resourceId,
            Actor = actor.Subject,
            Action = action,
            CorrelationId = correlationId,
            Version = version,
            Reason = reason,
            OccurredAt = clock.GetUtcNow()
        });
    }

    public void Append<T>(Guid tenantId, Guid companyId, Guid aggregateId, long version,
        string eventType, string correlationId, T payload)
    {
    }
}

public static class VatEnginePersistence
{
    public static void Configure(DbContextOptionsBuilder builder, string connectionString)
    {
        builder.UseNpgsql(connectionString);
    }

    public static IServiceCollection AddVatEnginePersistence(this IServiceCollection services, IConfiguration config)
    {
        var cs = config.GetConnectionString("VatEngineDatabase")
            ?? throw new InvalidOperationException("Configure ConnectionStrings:VatEngineDatabase.");
        if (string.IsNullOrWhiteSpace(cs)) throw new InvalidOperationException("Configure ConnectionStrings:VatEngineDatabase.");
        services.AddDbContext<VatEngineDbContext>(options => Configure(options, cs));
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<VatEngineStore>();
        services.AddScoped<ITaxRuleStore>(sp => sp.GetRequiredService<VatEngineStore>());
        services.AddScoped<ITaxLedgerStore>(sp => sp.GetRequiredService<VatEngineStore>());
        services.AddScoped<VatEngineRecordWriters>();
        services.AddScoped<IAuditWriter>(sp => sp.GetRequiredService<VatEngineRecordWriters>());
        services.AddScoped<IEventWriter>(sp => sp.GetRequiredService<VatEngineRecordWriters>());
        services.AddScoped<ICommandExecutor, VatEngineCommandExecutor>();
        services.AddScoped<TaxRuleService>();
        services.AddScoped<TaxLedgerService>();
        services.AddScoped<TaxCalculationService>();
        return services;
    }

    public static async Task ApplyVatEngineMigrationsAsync(this IHost host, CancellationToken ct = default)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VatEngineDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<VatEngineDbContext>>();
        try
        {
            await db.Database.MigrateAsync(ct);
            logger.LogInformation("VatEngineDatabase migrations applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not apply automatic migrations for VatEngineDatabase.");
        }
    }
}

