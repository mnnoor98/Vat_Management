using Vat.Domain;
using Vat.Domain.Costing;
using Xunit;

namespace Vat.Tests;

public sealed class CostReplayTests
{
    [Theory]
    [InlineData("FIFO", 2000, 1000)]
    [InlineData("WEIGHTED_AVERAGE", 2250, 750)]
    public void Methods_allocate_actual_receipt_value_and_conserve_closing_stock(string method, decimal issue, decimal closing)
    {
        var receipt1 = Guid.NewGuid(); var receipt2 = Guid.NewGuid(); var sale = Guid.NewGuid();
        var result = CostReplay.Calculate(method, [new(1, receipt1, "Receipt", 10, 1000), new(2, receipt2, "Receipt", 10, 2000), new(3, sale, "Issue", 15, 0)]);
        Assert.Equal(issue, Assert.Single(result.Issues).Value); Assert.Equal(closing, result.ClosingValue); Assert.Equal(5, result.ClosingQuantity);
        Assert.Equal(result.ReceivedValue, result.IssuedValue + result.ClosingValue);
        if (method == "FIFO") { Assert.Equal(receipt1, result.Issues[0].Allocations[0].ReceiptId); Assert.Equal(2, result.Issues[0].Allocations.Count); }
        else Assert.Null(Assert.Single(result.Issues[0].Allocations).ReceiptId);
    }

    [Fact]
    public void Moving_average_changes_after_each_receipt_instead_of_averaging_all_historical_purchases()
    {
        var result = CostReplay.Calculate("WEIGHTED_AVERAGE", [new(1, Guid.NewGuid(), "Receipt", 10, 1000), new(2, Guid.NewGuid(), "Issue", 5, 0),
            new(3, Guid.NewGuid(), "Receipt", 10, 2000), new(4, Guid.NewGuid(), "Issue", 5, 0)]);
        Assert.Equal(500, result.Issues[0].Value); Assert.Equal(833.3333m, result.Issues[1].Value);
        Assert.Equal(1666.6667m, result.ClosingValue);
    }

    [Theory]
    [InlineData("FIFO")]
    [InlineData("WEIGHTED_AVERAGE")]
    public void Fractional_money_and_zero_cost_stock_clear_without_lost_or_invented_residuals(string method)
    {
        var result = CostReplay.Calculate(method, [new(1, Guid.NewGuid(), "Receipt", 0.000003m, 0.0001m),
            new(2, Guid.NewGuid(), "Issue", 0.000001m, 0), new(3, Guid.NewGuid(), "Issue", 0.000001m, 0), new(4, Guid.NewGuid(), "Issue", 0.000001m, 0),
            new(5, Guid.NewGuid(), "Receipt", 2, 0), new(6, Guid.NewGuid(), "Issue", 2, 0)]);
        Assert.Equal(0.0001m, result.IssuedValue); Assert.Equal(0, result.ClosingQuantity); Assert.Equal(0, result.ClosingValue);
        Assert.Equal(0, result.Issues[^1].Value);
    }

    [Theory]
    [InlineData("FIFO")]
    [InlineData("WEIGHTED_AVERAGE")]
    public void Large_source_values_do_not_overflow_intermediate_allocation_multiplication(string method)
    {
        var result = CostReplay.Calculate(method, [new(1, Guid.NewGuid(), "Receipt", 999999999999999999m, 99999999999999999999m),
            new(2, Guid.NewGuid(), "Issue", 500000000000000000m, 0)]);
        Assert.Equal(result.ReceivedValue, result.IssuedValue + result.ClosingValue); Assert.True(result.ClosingValue > 0);
    }

    [Fact]
    public void Invalid_order_duplicates_unfunded_issues_and_precision_are_rejected()
    {
        var id = Guid.NewGuid();
        var invalid = new CostInput[][] {
            [new(0, id, "Receipt", 1, 1)], [new(2, id, "Receipt", 1, 1), new(1, Guid.NewGuid(), "Issue", 1, 0)],
            [new(1, id, "Receipt", 1, 1), new(2, id, "Receipt", 1, 1)], [new(1, id, "Issue", 1, 0)],
            [new(1, id, "Receipt", 0.0000001m, 1)], [new(1, id, "Receipt", 1, 0.00001m)],
            [new(1, id, "Receipt", 1, 1), new(2, Guid.NewGuid(), "Issue", 1, 1)]
        };
        foreach (var inputs in invalid) Assert.Throws<DomainException>(() => CostReplay.Calculate("FIFO", inputs));
        Assert.Throws<DomainException>(() => CostReplay.Calculate("LAST_PRICE", []));
    }
}
