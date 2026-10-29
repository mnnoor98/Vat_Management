using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Vat.Services.Sales;

namespace Vat.Services.Sales.Infrastructure;

public sealed class SalesPeriodGuard : IPeriodGuard
{
    public Task RequireOpen(Guid tenantId, Guid companyId, DateOnly businessDate, CancellationToken ct) => Task.CompletedTask;
    public Task LockShared(Guid tenantId, Guid companyId, CancellationToken ct) => Task.CompletedTask;
}

public sealed class SalesCustomerStore(SalesDbContext db) : ICustomerStore
{
    public Task<Vat.Domain.Sales.Customer?> Find(Guid tenant, Guid company, Guid id, CancellationToken ct) =>
        db.Customers.SingleOrDefaultAsync(x => x.TenantId == tenant && x.CompanyId == company && x.Id == id, ct);

    public async Task<IReadOnlyList<Vat.Domain.Sales.Customer>> List(Guid tenant, Guid company, Guid? before, int take, CancellationToken ct)
    {
        var q = db.Customers.AsNoTracking().Where(x => x.TenantId == tenant && x.CompanyId == company);
        if (before is not null) q = q.Where(x => x.Id.CompareTo(before.Value) < 0);
        return await q.OrderByDescending(x => x.Id).Take(take).ToArrayAsync(ct);
    }

    public void Add(Vat.Domain.Sales.Customer customer) => db.Customers.Add(customer);
}

public sealed class SalesReferenceStore : IReferenceStore
{
    public Task<Vat.Domain.Catalog.Product?> ProductByReference(Guid tenantId, Guid companyId, string reference, CancellationToken ct)
        => Task.FromResult<Vat.Domain.Catalog.Product?>(null);

    public Task<Vat.Domain.Tax.TaxRule?> EffectiveSalesRule(Guid tenantId, Guid companyId, string taxCode, DateOnly date, CancellationToken ct)
        => Task.FromResult<Vat.Domain.Tax.TaxRule?>(null);

    public Task<WarehouseReference?> Warehouse(Guid tenantId, Guid companyId, Guid id, CancellationToken ct)
        => Task.FromResult<WarehouseReference?>(new WarehouseReference(id, Guid.Empty, "WH-MAIN", "Main Warehouse"));
}

public static class SalesPersistence
{
    public static void Configure(DbContextOptionsBuilder builder, string connectionString)
    {
        builder.UseNpgsql(connectionString);
    }

    public static IServiceCollection AddSalesPersistence(this IServiceCollection services, IConfiguration config)
    {
        var cs = config.GetConnectionString("SalesDatabase")
            ?? "Host=127.0.0.1;Port=5432;Database=vat_sales;Username=postgres;Password=123456;";
        services.AddDbContext<SalesDbContext>(options => Configure(options, cs));
        services.AddScoped<SalesStore>();
        services.AddScoped<ISalesInvoiceStore>(sp => sp.GetRequiredService<SalesStore>());
        services.AddScoped<SalesRecordWriters>();
        services.AddScoped<IAuditWriter>(sp => sp.GetRequiredService<SalesRecordWriters>());
        services.AddScoped<IEventWriter>(sp => sp.GetRequiredService<SalesRecordWriters>());
        services.AddScoped<ICommandExecutor, SalesCommandExecutor>();
        services.AddScoped<IPeriodGuard, SalesPeriodGuard>();
        services.AddScoped<ICustomerStore, SalesCustomerStore>();
        services.AddScoped<IReferenceStore, SalesReferenceStore>();
        services.AddSingleton(new PostingPolicy(true));

        services.AddScoped<SalesPostingStore>();
        services.AddScoped<ISalesPostingStore>(sp => sp.GetRequiredService<SalesPostingStore>());
        services.AddScoped<SalesCreditStore>();
        services.AddScoped<ISalesCreditStore>(sp => sp.GetRequiredService<SalesCreditStore>());
        services.AddScoped<SalesDebitStore>();
        services.AddScoped<ISalesDebitStore>(sp => sp.GetRequiredService<SalesDebitStore>());
        services.AddScoped<PaymentStore>();
        services.AddScoped<IPaymentStore>(sp => sp.GetRequiredService<PaymentStore>());
        services.AddScoped<PaymentCorrectionStore>();
        services.AddScoped<IPaymentCorrectionStore>(sp => sp.GetRequiredService<PaymentCorrectionStore>());

        services.AddScoped<SalesInvoiceService>();
        services.AddScoped<SalesPostingService>();
        services.AddScoped<SalesCreditService>();
        services.AddScoped<SalesDebitService>();
        services.AddScoped<PaymentService>();
        services.AddScoped<PaymentCorrectionService>();

        return services;
    }

    public static async Task ApplySalesMigrationsAsync(this IHost host, CancellationToken ct = default)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SalesDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<SalesDbContext>>();
        try
        {
            await db.Database.MigrateAsync(ct);
            logger.LogInformation("SalesDatabase migrations applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not apply automatic migrations for SalesDatabase.");
        }
    }
}

