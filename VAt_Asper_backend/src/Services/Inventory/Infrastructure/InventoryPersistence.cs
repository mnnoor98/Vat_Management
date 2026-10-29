using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Vat.Application.Security;

namespace Vat.Services.Inventory.Infrastructure;

public sealed class InventoryPeriodGuard : IPeriodGuard
{
    public Task RequireOpen(Guid tenantId, Guid companyId, DateOnly businessDate, CancellationToken ct) => Task.CompletedTask;
}

public sealed class InventoryRecordWriters(InventoryDbContext db, TimeProvider clock) : IAuditWriter, IEventWriter
{
    public void Append(Actor actor, Guid company, Guid resourceId, string action, string correlation, int version = 1, string? reason = null)
    {
        db.Audits.Add(new InventoryAudit
        {
            Id = Guid.NewGuid(),
            TenantId = actor.TenantId,
            CompanyId = company,
            ResourceId = resourceId,
            Actor = actor.Subject,
            Action = action,
            CorrelationId = correlation,
            Version = version,
            Reason = reason,
            OccurredAt = clock.GetUtcNow()
        });
    }

    public void Append<T>(Guid tenant, Guid company, Guid aggregateId, int version, string type, string correlation, T payload) where T : class
    {
    }
}

public static class InventoryPersistence
{
    public static void Configure(DbContextOptionsBuilder builder, string connectionString)
    {
        builder.UseNpgsql(connectionString);
    }

    public static IServiceCollection AddInventoryPersistence(this IServiceCollection services, IConfiguration config)
    {
        var cs = config.GetConnectionString("InventoryDatabase")
            ?? "Host=127.0.0.1;Port=5432;Database=vat_inventory;Username=postgres;Password=123456;";
        services.AddDbContext<InventoryDbContext>(options => Configure(options, cs));
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IPeriodGuard, InventoryPeriodGuard>();
        services.AddScoped<InventoryStore>();
        services.AddScoped<IWarehouseStore>(sp => sp.GetRequiredService<InventoryStore>());
        services.AddScoped<IStockStore>(sp => sp.GetRequiredService<InventoryStore>());
        services.AddScoped<ITransferStore>(sp => sp.GetRequiredService<InventoryStore>());
        services.AddScoped<IInventoryReadStore>(sp => sp.GetRequiredService<InventoryStore>());
        services.AddScoped<InventoryRecordWriters>();
        services.AddScoped<IAuditWriter>(sp => sp.GetRequiredService<InventoryRecordWriters>());
        services.AddScoped<IEventWriter>(sp => sp.GetRequiredService<InventoryRecordWriters>());
        services.AddScoped<ICommandExecutor, InventoryCommandExecutor>();
        services.AddScoped<WarehouseService>();
        services.AddScoped<StockService>();
        services.AddScoped<TransferService>();
        services.AddScoped<InventoryReadService>();
        return services;
    }

    public static async Task ApplyInventoryMigrationsAsync(this IHost host, CancellationToken ct = default)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<InventoryDbContext>>();
        try
        {
            await db.Database.MigrateAsync(ct);
            logger.LogInformation("InventoryDatabase migrations applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not apply automatic migrations for InventoryDatabase.");
        }
    }
}

