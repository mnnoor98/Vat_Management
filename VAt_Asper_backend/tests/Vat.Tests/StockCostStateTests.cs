using Vat.Domain;
using Vat.Domain.Costing;
using Xunit;

namespace Vat.Tests;
public sealed class StockCostStateTests
{
    [Theory]
    [InlineData("FIFO",2000,1000)]
    [InlineData("WEIGHTED_AVERAGE",2250,750)]
    public void Persisted_costs_conserve_receipt_issue_return_and_transfer(string method,decimal issued,decimal closing)
    {
        var p=new StockCostState {Method=method};
        p.Add([new(Guid.NewGuid(),Guid.NewGuid(),10,1000),new(Guid.NewGuid(),Guid.NewGuid(),10,2000)]);
        var allocations=p.Take(15); Assert.Equal(issued,allocations.Sum(x=>x.Value)); Assert.Equal(closing,p.Value);
        var remaining=allocations.ToList(); var first=StockCostState.Consume(remaining,3); var rest=StockCostState.Consume(remaining,12);
        Assert.Equal(issued,first.Sum(x=>x.Value)+rest.Sum(x=>x.Value));
        p.Add(first); p.Add(rest); Assert.Equal(20,p.Quantity); Assert.Equal(3000,p.Value);
        var target=new StockCostState{Method=method}; var moved=p.Take(7); target.Add(moved);
        Assert.Equal(3000,p.Value+target.Value); Assert.Equal(20,p.Quantity+target.Quantity);
    }
    [Fact]
    public void Fifo_reversal_requires_original_receipt_cost_in_original_position()
    {
        var origin=Guid.NewGuid(); var p=new StockCostState();
        p.Add([new(Guid.NewGuid(),origin,10,1000),new(Guid.NewGuid(),Guid.NewGuid(),10,2000)]);
        var issued=p.Take(1); Assert.Throws<DomainException>(()=>p.ReverseReceipt(origin,10,1000));
        p.Add(issued); p.ReverseReceipt(origin,10,1000); Assert.Equal(10,p.Quantity); Assert.Equal(2000,p.Value);
    }
    [Theory]
    [InlineData("FIFO")]
    [InlineData("WEIGHTED_AVERAGE")]
    public void Fractional_allocations_and_partial_returns_preserve_last_money_residual(string method)
    {
        var p=new StockCostState{Method=method}; p.Add([new(Guid.NewGuid(),Guid.NewGuid(),0.000003m,0.0001m)]);
        var chunks=p.Take(0.000003m).ToList();
        var one=StockCostState.Consume(chunks,0.000001m); var two=StockCostState.Consume(chunks,0.000001m); var three=StockCostState.Consume(chunks,0.000001m);
        Assert.Equal(0.0001m,one.Concat(two).Concat(three).Sum(x=>x.Value)); p.Add(one);p.Add(two);p.Add(three);
        Assert.Equal(0.0001m,p.Take(0.000003m).Sum(x=>x.Value)); Assert.Equal(0,p.Value);
    }
}
