using Vat.Domain;
using Vat.Domain.Catalog;
using Vat.Domain.Posting;
using Vat.Domain.Purchasing;
using Vat.Domain.Tax;
using Xunit;

namespace Vat.Tests;

public sealed class TaxCalculationTests
{
    [Fact]
    public void Output_tax_rule_cannot_be_used_for_purchase_calculation()
    {
        var tenant = Guid.NewGuid();
        var company = Guid.NewGuid();
        var purchase = Purchase.Create(tenant, company, Guid.NewGuid(), "REF", "SUP", Date, "maker", [new("P", "Item", 1, 100)], Now);
        var product = Product.Create(tenant, company, "P", "Item", "EA", "TEST");
        var rule = TaxRule.Create(tenant, company, "TEST", Date, Date.AddDays(1), 5, 0, "Synthetic test", "TEST", "maker", "Sale");
        rule.Publish("checker", 1, Now);
        Assert.Equal("tax_rule_unavailable", Assert.Throws<DomainException>(() =>
            PurchaseCalculator.Calculate(purchase.Lines.Single(), product, rule, Date)).Code);
    }

    private static readonly DateOnly Date = new(2026, 10, 23);
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Example_percentage_and_partial_recoverability_are_explicit_not_default_rates()
    {
        var tenant = Guid.NewGuid();
        var company = Guid.NewGuid();
        var purchase = Purchase.Create(tenant, company, Guid.NewGuid(), "REF", "SUP", Date, "maker",
            [new("ITEM", "Test goods", 2.5m, 100m)], Now);
        var product = Product.Create(tenant, company, "ITEM", "Test goods", "KG", "TEST-ONLY");
        var rule = TaxRule.Create(tenant, company, "TEST-ONLY", Date, Date.AddDays(1), 7.5m, 40m,
            "Synthetic test fixture, not a legal rate", "TEST-001", "rule-maker");
        rule.Publish("rule-reviewer", 1, Now);
        var line = PurchaseCalculator.Calculate(purchase.Lines.Single(), product, rule, Date);
        Assert.Equal(250m, line.NetAmount);
        Assert.Equal(18.75m, line.TaxAmount);
        Assert.Equal(7.5m, line.RecoverableTaxAmount);
        Assert.Equal(rule.Id, line.TaxRuleId);
        Assert.Equal(2, line.TaxRuleVersion);
        Assert.Equal("TEST-001", line.EvidenceReference);
    }

    [Fact]
    public void Rule_maker_cannot_publish_and_effective_until_is_exclusive()
    {
        var tenant = Guid.NewGuid();
        var company = Guid.NewGuid();
        var purchase = Purchase.Create(tenant, company, Guid.NewGuid(), "REF", "SUP", Date, "maker", [new("P", "Item", 1, 1)], Now);
        var product = Product.Create(tenant, company, "P", "Item", "EA", "TEST");
        var rule = TaxRule.Create(tenant, company, "TEST", Date.AddDays(-1), Date, 0, 0, "Test", "Test", "maker");
        Assert.Equal("tax_rule_unavailable", Assert.Throws<DomainException>(() => PurchaseCalculator.Calculate(purchase.Lines.Single(), product, rule, Date)).Code);
        Assert.Equal("self_approval_forbidden", Assert.Throws<DomainException>(() => rule.Publish("maker", 1, Now)).Code);
        rule.Publish("checker", 1, Now);
        Assert.Throws<DomainException>(() => PurchaseCalculator.Calculate(purchase.Lines.Single(), product, rule, Date));
        Assert.Equal(0, PurchaseCalculator.Calculate(purchase.Lines.Single(), product, rule, Date.AddDays(-1)).TaxAmount);
    }

    [Theory]
    [InlineData(-0.0001, 100)]
    [InlineData(100.0001, 100)]
    [InlineData(7.5, -1)]
    [InlineData(7.5, 101)]
    public void Invalid_rule_percentages_are_rejected(decimal rate, decimal recovery)
    {
        Assert.Throws<DomainException>(() => TaxRule.Create(Guid.NewGuid(), Guid.NewGuid(), "TEST", Date, Date.AddDays(1),
            rate, recovery, "Test", "Test", "maker"));
    }

    [Fact]
    public void Cross_company_product_or_rule_cannot_be_calculated()
    {
        var tenant = Guid.NewGuid();
        var company = Guid.NewGuid();
        var purchase = Purchase.Create(tenant, company, Guid.NewGuid(), "REF", "SUP", Date, "maker", [new("P", "Item", 1, 1)], Now);
        var product = Product.Create(tenant, Guid.NewGuid(), "P", "Item", "EA", "TEST");
        var rule = TaxRule.Create(tenant, company, "TEST", Date, Date.AddDays(1), 0, 0, "Test", "Test", "maker");
        rule.Publish("checker", 1, Now);
        Assert.Equal("reference_scope_mismatch", Assert.Throws<DomainException>(() =>
            PurchaseCalculator.Calculate(purchase.Lines.Single(), product, rule, Date)).Code);
    }
}
