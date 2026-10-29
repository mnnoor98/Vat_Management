using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Vat.Services.Identity.Infrastructure;

public sealed class IdentityDatabaseSettings
{
    public string IdentityDatabase { get; set; } = "";
}

public static class IdentityPersistence
{
    public static IServiceCollection AddIdentityPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<IdentityDatabaseSettings>().Bind(configuration.GetSection("ConnectionStrings"))
            .Validate(x => !string.IsNullOrWhiteSpace(x.IdentityDatabase), "Configure ConnectionStrings:IdentityDatabase.").ValidateOnStart();
        services.AddDbContext<IdentityDbContext>((provider, options) => Configure(options,
            provider.GetRequiredService<IOptions<IdentityDatabaseSettings>>().Value.IdentityDatabase));
        services.AddScoped<IIdentityRoleStore, IdentityRoleStore>();
        services.AddScoped<IdentityReadService>();
        return services;
    }

    public static void Configure(DbContextOptionsBuilder options, string connection) =>
        options.UseNpgsql(connection, pg => pg.MigrationsHistoryTable("__IdentityMigrationsHistory", "identity"));
}

public sealed class IdentityDesignTimeFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__IdentityDatabase")
            ?? throw new InvalidOperationException("Set ConnectionStrings__IdentityDatabase before running Identity migrations.");
        var options = new DbContextOptionsBuilder<IdentityDbContext>();
        IdentityPersistence.Configure(options, connection);
        return new(options.Options);
    }
}
