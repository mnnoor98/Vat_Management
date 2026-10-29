using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Vat.Services.Workflow.Infrastructure;

public sealed class WorkflowDatabaseSettings
{
    public string WorkflowDatabase { get; set; } = "";
}

public static class WorkflowPersistence
{
    public static IServiceCollection AddWorkflowPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<WorkflowDatabaseSettings>().Bind(configuration.GetSection("ConnectionStrings"))
            .Validate(x => !string.IsNullOrWhiteSpace(x.WorkflowDatabase), "Configure ConnectionStrings:WorkflowDatabase.").ValidateOnStart();
        services.AddDbContext<WorkflowDbContext>((provider, options) => Configure(options,
            provider.GetRequiredService<IOptions<WorkflowDatabaseSettings>>().Value.WorkflowDatabase));
        services.AddScoped<IWorkflowTaskStore, WorkflowTaskStore>();
        services.AddScoped<WorkflowReadService>();
        return services;
    }

    public static void Configure(DbContextOptionsBuilder options, string connection) =>
        options.UseNpgsql(connection, pg => pg.MigrationsHistoryTable("__WorkflowMigrationsHistory", "workflow"));
}

public sealed class WorkflowDesignTimeFactory : IDesignTimeDbContextFactory<WorkflowDbContext>
{
    public WorkflowDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__WorkflowDatabase")
            ?? throw new InvalidOperationException("Set ConnectionStrings__WorkflowDatabase before running Workflow migrations.");
        var options = new DbContextOptionsBuilder<WorkflowDbContext>();
        WorkflowPersistence.Configure(options, connection);
        return new(options.Options);
    }
}
