using Vat.Domain;
using Vat.Domain.Purchasing;
using Xunit;

namespace Vat.Tests;

public sealed class PurchaseDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 0, 0, 0, TimeSpan.Zero);

    private static Purchase NewPurchase(params PurchaseLineInput[] lines) => Purchase.Create(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "REF-1", "SUPPLIER-1",
        new DateOnly(2026, 10, 23), "maker", lines, Now);

    [Fact]
    public void Approval_requires_submitted_document_and_distinct_checker()
    {
        var purchase = NewPurchase(new PurchaseLineInput("P-1", "Test item", 1, 100));
        Assert.Equal("invalid_state", Assert.Throws<DomainException>(() => purchase.Approve("checker", 1, Now)).Code);
        purchase.Submit(1, Now);
        Assert.Equal("self_approval_forbidden", Assert.Throws<DomainException>(() => purchase.Approve("maker", 2, Now)).Code);
        Assert.Equal(PurchaseStatus.Submitted, purchase.Status);
        purchase.Approve("checker", 2, Now);
        Assert.Equal(PurchaseStatus.ApprovedPendingPosting, purchase.Status);
        Assert.Equal(3, purchase.Version);
    }

    [Fact]
    public void Stale_approval_does_not_mutate_document()
    {
        var purchase = NewPurchase(new PurchaseLineInput("P-1", "Test item", 1, 100));
        purchase.Submit(1, Now);
        Assert.Equal("version_conflict", Assert.Throws<DomainException>(() => purchase.Approve("checker", 1, Now)).Code);
        Assert.Null(purchase.ApprovedBy);
        Assert.Equal(2, purchase.Version);
    }

    [Fact]
    public void Line_amount_uses_explicit_rounding_and_document_is_sum_of_lines()
    {
        var purchase = NewPurchase(new("P-1", "First", 0.000001m, 50m), new("P-2", "Second", 2m, 0.1234m));
        Assert.Equal(0.0001m, purchase.Lines.First().NetAmount);
        Assert.Equal(0.2469m, purchase.NetAmount);
        // This is a net-value policy, not an approved VAT calculation.
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(1000001, 1)]
    [InlineData(1, -1)]
    [InlineData(1, 1000000001)]
    public void Invalid_quantities_and_prices_are_rejected(decimal quantity, decimal price)
    {
        Assert.Throws<DomainException>(() => NewPurchase(new PurchaseLineInput("P-1", "Item", quantity, price)));
    }

    [Fact]
    public void Rejected_document_cannot_be_approved()
    {
        var purchase = NewPurchase(new PurchaseLineInput("P-1", "Item", 1, 1));
        purchase.Submit(1, Now);
        purchase.Reject("checker", 2, "Source invoice missing", Now);
        Assert.Equal("invalid_state", Assert.Throws<DomainException>(() => purchase.Approve("checker", 3, Now)).Code);
    }
}
