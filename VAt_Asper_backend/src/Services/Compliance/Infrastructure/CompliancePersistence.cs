using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Vat.Services.Compliance.Infrastructure;

public sealed class ComplianceDatabaseSettings
{
    public string ComplianceDatabase { get; set; } = "";
}

public static class CompliancePersistence
{
    public static IServiceCollection AddCompliancePersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ComplianceDatabaseSettings>().Bind(configuration.GetSection("ConnectionStrings"))
            .Validate(x => !string.IsNullOrWhiteSpace(x.ComplianceDatabase), "Configure ConnectionStrings:ComplianceDatabase.").ValidateOnStart();
        services.AddDbContext<ComplianceDbContext>((provider, options) => Configure(options,
            provider.GetRequiredService<IOptions<ComplianceDatabaseSettings>>().Value.ComplianceDatabase));
        services.AddScoped<IComplianceReturnStore, ComplianceReturnStore>();
        services.AddScoped<ComplianceReadService>();
        return services;
    }

    public static void Configure(DbContextOptionsBuilder options, string connection) =>
        options.UseNpgsql(connection, pg => pg.MigrationsHistoryTable("__ComplianceMigrationsHistory", "compliance"));
}

public sealed class ComplianceDesignTimeFactory : IDesignTimeDbContextFactory<ComplianceDbContext>
{
    public ComplianceDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__ComplianceDatabase")
            ?? throw new InvalidOperationException("Set ConnectionStrings__ComplianceDatabase before running Compliance migrations.");
        var options = new DbContextOptionsBuilder<ComplianceDbContext>();
        CompliancePersistence.Configure(options, connection);
        return new(options.Options);
    }
}
