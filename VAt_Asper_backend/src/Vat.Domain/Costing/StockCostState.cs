using System.Numerics;

namespace Vat.Domain.Costing;

public sealed record CostChunk(Guid Id, Guid Origin, decimal Quantity, decimal Value);

// Persisted FIFO layers / moving average pool. Values are BDT at scale 4.
public sealed class StockCostState
{
    public string Method { get; set; } = "FIFO";
    public decimal Quantity { get; set; }
    public decimal Value { get; set; }
    public List<CostChunk> Layers { get; set; } = [];

    public void Add(IReadOnlyList<CostChunk> chunks)
    {
        foreach (var c in chunks)
        {
            Valid(c.Quantity, c.Value);
            Quantity += c.Quantity; Value += c.Value;
            if (Method == "FIFO") Layers.Add(c with { Id = Guid.NewGuid() });
        }
        Check();
    }

    public IReadOnlyList<CostChunk> Take(decimal quantity)
    {
        Valid(quantity, 0);
        if (quantity > Quantity) throw new DomainException("cost_insufficient_stock", "Costed stock is insufficient.", 409);
        List<CostChunk> chunks;
        if (Method == "FIFO") chunks = Consume(Layers, quantity);
        else chunks = [new(Guid.NewGuid(), Guid.Empty, quantity, Share(Value, quantity, Quantity))];
        Quantity -= quantity; Value -= chunks.Sum(x => x.Value); Check(); return chunks;
    }

    public IReadOnlyList<CostChunk> ReverseReceipt(Guid origin, decimal quantity, decimal value)
    {
        Valid(quantity, value);
        List<CostChunk> chunks;
        if (Method == "FIFO")
        {
            chunks = Layers.Where(x => x.Origin == origin).ToList();
            if (chunks.Sum(x => x.Quantity) != quantity || chunks.Sum(x => x.Value) != value)
                throw new DomainException("cost_receipt_consumed", "The original receipt is consumed or held in another warehouse; return/transfer it before reversal.", 409);
            Layers.RemoveAll(x => x.Origin == origin);
        }
        else chunks = [new(Guid.NewGuid(), origin, quantity, value)];
        if (Quantity < quantity || Value < value || (Quantity == quantity && Value != value))
            throw new DomainException("cost_reversal_reconciliation_required", "The original receipt value cannot be removed from the remaining cost pool.", 409);
        Quantity -= quantity; Value -= value; Check(); return chunks;
    }

    public IReadOnlyList<CostChunk> ReverseReturn(IReadOnlyList<CostChunk> returned)
    {
        var quantity=returned.Sum(x=>x.Quantity);var value=returned.Sum(x=>x.Value);Valid(quantity,value);
        if(Quantity<quantity||Value<value||(Quantity==quantity&&Value!=value))throw new DomainException("cost_return_consumed","Returned material has been consumed, transferred or mixed beyond exact reversal.",409);
        if(Method=="WEIGHTED_AVERAGE")
        {
            Quantity-=quantity;Value-=value;Check();return[new(Guid.NewGuid(),Guid.Empty,quantity,value)];
        }
        var removed=new List<CostChunk>();
        foreach(var group in returned.GroupBy(x=>x.Origin))
        {
            var needed=group.Sum(x=>x.Quantity);var expected=group.Sum(x=>x.Value);var candidates=Layers.Where(x=>x.Origin==group.Key).ToList();
            if(candidates.Sum(x=>x.Quantity)<needed)throw new DomainException("cost_return_consumed","Returned material is no longer available in its original FIFO layers.",409);
            var taken=Consume(candidates,needed);if(taken.Sum(x=>x.Value)!=expected)throw new DomainException("cost_return_value_changed","Returned material no longer carries its original FIFO value.",409);
            foreach(var chunk in taken)
            {
                var index=Layers.FindIndex(x=>x.Id==chunk.Id);var layer=Layers[index];removed.Add(chunk);
                if(layer.Quantity==chunk.Quantity)Layers.RemoveAt(index);else Layers[index]=layer with{Quantity=layer.Quantity-chunk.Quantity,Value=layer.Value-chunk.Value};
            }
        }
        Quantity-=quantity;Value-=value;Check();return removed;
    }

    public void Check()
    {
        if (Method is not ("FIFO" or "WEIGHTED_AVERAGE") || Quantity < 0 || Value < 0 || (Quantity == 0 && Value != 0) ||
            (Method == "WEIGHTED_AVERAGE" && Layers.Count != 0) || Layers.Any(x=>x.Quantity<=0 || x.Value<0 || decimal.Round(x.Quantity,6)!=x.Quantity || decimal.Round(x.Value,4)!=x.Value) ||
            (Method == "FIFO" && (Layers.Sum(x => x.Quantity) != Quantity || Layers.Sum(x => x.Value) != Value)))
            throw new DomainException("cost_state_invalid", "Cost layer/pool conservation failed.", 409);
    }

    public static List<CostChunk> Consume(List<CostChunk> layers, decimal quantity)
    {
        if (quantity <= 0 || layers.Sum(x => x.Quantity) < quantity)
            throw new DomainException("cost_return_exceeds_issue", "Returned quantity exceeds the original cost allocation remaining.", 409);
        var result = new List<CostChunk>(); var remaining = quantity;
        while (remaining > 0)
        {
            var layer = layers[0]; var take = Math.Min(remaining, layer.Quantity);
            var value = Share(layer.Value, take, layer.Quantity);
            result.Add(layer with { Quantity = take, Value = value });
            if (take == layer.Quantity) layers.RemoveAt(0);
            else layers[0] = layer with { Quantity = layer.Quantity - take, Value = layer.Value - value };
            remaining -= take;
        }
        return result;
    }

    public static decimal Share(decimal value, decimal quantity, decimal total)
    {
        if (quantity == total) return value;
        var numerator = new BigInteger(value * 10000m) * new BigInteger(quantity * 1000000m);
        var denominator = new BigInteger(total * 1000000m);
        var quotient = BigInteger.DivRem(numerator, denominator, out var remainder);
        return (decimal)(quotient + (remainder * 2 >= denominator ? 1 : 0)) / 10000m;
    }
    private static void Valid(decimal quantity, decimal value)
    {
        if (quantity <= 0 || decimal.Round(quantity, 6) != quantity || value < 0 || decimal.Round(value, 4) != value)
            throw new DomainException("cost_precision_invalid", "Require positive quantity at scale 6 and nonnegative cost at scale 4.");
    }
}
