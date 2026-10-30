using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Vat.Services.Purchase;

namespace Vat.Services.Purchase.Infrastructure;

public static class PurchasePersistence
{
    public static void Configure(DbContextOptionsBuilder builder, string connectionString)
    {
        builder.UseNpgsql(connectionString);
    }

    public static IServiceCollection AddPurchasePersistence(this IServiceCollection services, IConfiguration config)
    {
        var cs = config.GetConnectionString("PurchaseDatabase")
            ?? throw new InvalidOperationException("Configure ConnectionStrings:PurchaseDatabase.");
        if (string.IsNullOrWhiteSpace(cs)) throw new InvalidOperationException("Configure ConnectionStrings:PurchaseDatabase.");
        services.AddDbContext<PurchaseDbContext>(options => Configure(options, cs));
        services.AddScoped<PurchaseStore>();
        services.AddScoped<IPurchaseStore>(sp => sp.GetRequiredService<PurchaseStore>());
        services.AddScoped<IPurchasePostingStore>(sp => sp.GetRequiredService<PurchaseStore>());
        services.AddScoped<IPurchaseReversalStore>(sp => sp.GetRequiredService<PurchaseStore>());
        services.AddScoped<PurchaseRecordWriters>();
        services.AddScoped<IAuditWriter>(sp => sp.GetRequiredService<PurchaseRecordWriters>());
        services.AddScoped<IEventWriter>(sp => sp.GetRequiredService<PurchaseRecordWriters>());
        services.AddScoped<ICommandExecutor, PurchaseCommandExecutor>();
        services.AddScoped<PurchaseService>();
        services.AddScoped<PurchasePostingService>();
        services.AddScoped<PurchaseReversalService>();
        return services;
    }

    public static async Task ApplyPurchaseMigrationsAsync(this IHost host, CancellationToken ct = default)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PurchaseDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<PurchaseDbContext>>();
        try
        {
            await db.Database.MigrateAsync(ct);
            logger.LogInformation("PurchaseDatabase migrations applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not apply automatic migrations for PurchaseDatabase.");
        }
    }
}
