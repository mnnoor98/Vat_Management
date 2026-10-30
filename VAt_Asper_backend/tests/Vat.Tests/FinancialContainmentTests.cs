using Vat.Domain;
using Vat.Application.Security;
using Vat.Services.Purchase;
using Vat.Services.Purchase.Contracts;
using Vat.Services.Sales.Infrastructure;
using Vat.Services.Inventory.Infrastructure;
using Xunit;

namespace Vat.Tests;

public sealed class FinancialContainmentTests
{
    [Fact]
    public async Task Extracted_purchase_prepare_and_post_cannot_touch_stores_before_coordination_is_available()
    {
        var company = Guid.NewGuid();
        var actor = new Actor(Guid.NewGuid(), "maker", new HashSet<Guid> { company }, new HashSet<string>());
        var service = new PurchasePostingService(null!, null!, null!, null!, TimeProvider.System);
        var prepare = await Assert.ThrowsAsync<DomainException>(() => service.Prepare(actor, company, Guid.NewGuid(),
            new PreparePostingRequest(3, Guid.NewGuid()), "test", default));
        var post = await Assert.ThrowsAsync<DomainException>(() => service.Post(actor, company, Guid.NewGuid(), Guid.NewGuid(),
            new TransitionRequest(4), "test", default));
        Assert.Equal("financial_boundary_unavailable", prepare.Code);
        Assert.Equal(503, prepare.Status);
        Assert.Equal(503, post.Status);
    }

    [Fact]
    public async Task Incomplete_period_adapters_cannot_admit_financial_writes()
    {
        var sales = await Assert.ThrowsAsync<DomainException>(() => new SalesPeriodGuard().RequireOpen(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 10, 30), default));
        var inventory = await Assert.ThrowsAsync<DomainException>(() => new InventoryPeriodGuard().RequireOpen(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 10, 30), default));
        Assert.Equal(503, sales.Status);
        Assert.Equal(503, inventory.Status);
    }
}
