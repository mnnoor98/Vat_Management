using Microsoft.Extensions.DependencyInjection;
using Vat.Application;
using Vat.Infrastructure.Persistence;
using Xunit;

namespace Vat.Tests;

[Collection("PostgreSQL integration")]
public sealed class FinancialCompositionTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    [Fact]
    public void Active_financial_host_resolves_coordinated_stores_and_never_registers_staged_owner_contexts()
    {
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        Assert.NotNull(services.GetRequiredService<VatDbContext>());
        Assert.IsType<CommandExecutor>(services.GetRequiredService<ICommandExecutor>());
        Assert.IsType<CompanyStore>(services.GetRequiredService<ICompanyStore>());
        Assert.IsType<PurchaseStore>(services.GetRequiredService<IPurchaseStore>());
        Assert.IsType<PostingStore>(services.GetRequiredService<IPostingStore>());
        Assert.IsType<PeriodStore>(services.GetRequiredService<IPeriodStore>());
        Assert.IsType<SalesPostingStore>(services.GetRequiredService<ISalesPostingStore>());
        Assert.IsType<RecordWriters>(services.GetRequiredService<IEventWriter>());
        Assert.Null(services.GetService<Vat.Services.Company.Infrastructure.CompanyDbContext>());
        Assert.Null(services.GetService<Vat.Services.Purchase.Infrastructure.PurchaseDbContext>());
        Assert.Null(services.GetService<Vat.Services.Sales.Infrastructure.SalesDbContext>());
        Assert.Null(services.GetService<Vat.Services.Inventory.Infrastructure.InventoryDbContext>());
    }
}
