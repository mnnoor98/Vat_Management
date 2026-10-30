using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Vat.Services.MasterData;

namespace Vat.Services.MasterData.Infrastructure;

public static class MasterDataPersistence
{
    public static void Configure(DbContextOptionsBuilder builder, string connectionString)
    {
        builder.UseNpgsql(connectionString);
    }

    public static IServiceCollection AddMasterDataPersistence(this IServiceCollection services, IConfiguration config)
    {
        var cs = config.GetConnectionString("MasterDataDatabase")
            ?? throw new InvalidOperationException("Configure ConnectionStrings:MasterDataDatabase.");
        if (string.IsNullOrWhiteSpace(cs)) throw new InvalidOperationException("Configure ConnectionStrings:MasterDataDatabase.");
        services.AddDbContext<MasterDataDbContext>(options => Configure(options, cs));
        services.AddScoped<MasterDataStore>();
        services.AddScoped<IMasterDataStore>(sp => sp.GetRequiredService<MasterDataStore>());
        services.AddScoped<IMasterDataCustomerStore>(sp => sp.GetRequiredService<MasterDataStore>());
        services.AddScoped<MasterDataRecordWriters>();
        services.AddScoped<IAuditWriter>(sp => sp.GetRequiredService<MasterDataRecordWriters>());
        services.AddScoped<IEventWriter>(sp => sp.GetRequiredService<MasterDataRecordWriters>());
        services.AddScoped<ICommandExecutor, MasterDataCommandExecutor>();
        services.AddScoped<MasterDataProductService>();
        services.AddScoped<MasterDataCustomerService>();
        return services;
    }

    public static async Task ApplyMasterDataMigrationsAsync(this IHost host, CancellationToken ct = default)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MasterDataDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<MasterDataDbContext>>();
        try
        {
            await db.Database.MigrateAsync(ct);
            logger.LogInformation("MasterDataDatabase migrations applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not apply automatic migrations for MasterDataDatabase.");
        }
    }
}

