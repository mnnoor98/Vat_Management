using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Vat.Services.Integration.Infrastructure;

public sealed class IntegrationDatabaseSettings
{
    public string IntegrationDatabase { get; set; } = "";
}

public static class IntegrationPersistence
{
    public static IServiceCollection AddIntegrationPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<IntegrationDatabaseSettings>().Bind(configuration.GetSection("ConnectionStrings"))
            .Validate(x => !string.IsNullOrWhiteSpace(x.IntegrationDatabase), "Configure ConnectionStrings:IntegrationDatabase.").ValidateOnStart();
        services.AddDbContext<IntegrationDbContext>((provider, options) => Configure(options,
            provider.GetRequiredService<IOptions<IntegrationDatabaseSettings>>().Value.IntegrationDatabase));
        services.AddScoped<IIntegrationConnectionStore, IntegrationConnectionStore>();
        services.AddScoped<IntegrationReadService>();
        return services;
    }

    public static void Configure(DbContextOptionsBuilder options, string connection) =>
        options.UseNpgsql(connection, pg => pg.MigrationsHistoryTable("__IntegrationMigrationsHistory", "integration"));
}

public sealed class IntegrationDesignTimeFactory : IDesignTimeDbContextFactory<IntegrationDbContext>
{
    public IntegrationDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__IntegrationDatabase")
            ?? throw new InvalidOperationException("Set ConnectionStrings__IntegrationDatabase before running Integration migrations.");
        var options = new DbContextOptionsBuilder<IntegrationDbContext>();
        IntegrationPersistence.Configure(options, connection);
        return new(options.Options);
    }
}
