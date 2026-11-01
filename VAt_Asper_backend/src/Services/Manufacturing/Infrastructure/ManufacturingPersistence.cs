using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Vat.Services.Manufacturing.Infrastructure;

public sealed class ManufacturingDatabaseSettings
{
    public string ManufacturingDatabase { get; set; } = "";
}

public static class ManufacturingPersistence
{
    public static IServiceCollection AddManufacturingPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ManufacturingDatabaseSettings>().Bind(configuration.GetSection("ConnectionStrings"))
            .Validate(x => !string.IsNullOrWhiteSpace(x.ManufacturingDatabase), "Configure ConnectionStrings:ManufacturingDatabase.").ValidateOnStart();
        services.AddDbContext<ManufacturingDbContext>((provider, options) => Configure(options,
            provider.GetRequiredService<IOptions<ManufacturingDatabaseSettings>>().Value.ManufacturingDatabase));
        services.AddScoped<IManufacturingOrderStore, ManufacturingOrderStore>();
        services.AddScoped<ManufacturingReadService>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<ICoefficientStore, CoefficientStore>();
        services.AddScoped<CoefficientService>();
        services.AddScoped<IProductionOrderWorkflow,ProductionOrderWorkflow>();
        services.AddHttpClient<IProductionWarehouseReferences,ProductionWarehouseReferenceClient>(client=>client.Timeout=TimeSpan.FromSeconds(5))
            .ConfigurePrimaryHttpMessageHandler(()=>new HttpClientHandler{AllowAutoRedirect=false,UseCookies=false});
        services.AddHttpClient<ICoefficientReferences, CoefficientReferenceClient>(client => client.Timeout = TimeSpan.FromSeconds(5))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
        return services;
    }

    public static void Configure(DbContextOptionsBuilder options, string connection) =>
        options.UseNpgsql(connection, pg => pg.MigrationsHistoryTable("__ManufacturingMigrationsHistory", "manufacturing"));
}

public sealed class ManufacturingDesignTimeFactory : IDesignTimeDbContextFactory<ManufacturingDbContext>
{
    public ManufacturingDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__ManufacturingDatabase")
            ?? throw new InvalidOperationException("Set ConnectionStrings__ManufacturingDatabase before running Manufacturing migrations.");
        var options = new DbContextOptionsBuilder<ManufacturingDbContext>();
        ManufacturingPersistence.Configure(options, connection);
        return new(options.Options);
    }
}
