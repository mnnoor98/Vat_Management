using System.Numerics;

namespace Vat.Domain.Costing;

public sealed record CostInput(long Sequence, Guid SourceId, string Kind, decimal Quantity, decimal ReceiptValue);
public sealed record CostAllocation(Guid? ReceiptId, decimal Quantity, decimal Value);
public sealed record CostIssue(Guid SourceId, decimal Quantity, decimal Value, IReadOnlyList<CostAllocation> Allocations);
public sealed record CostLayer(Guid ReceiptId, decimal Quantity, decimal Value);
public sealed record CostReplayResult(string Method, decimal ReceivedQuantity, decimal ReceivedValue, decimal IssuedQuantity,
    decimal IssuedValue, decimal ClosingQuantity, decimal ClosingValue, IReadOnlyList<CostIssue> Issues, IReadOnlyList<CostLayer> Layers);

// Exact integer units avoid floating-point drift and intermediate decimal multiplication overflow.
// Quantity uses 6 places; value uses 4 places; partial allocations round half away from zero.
public static class CostReplay
{
    private sealed class Layer(Guid id, BigInteger quantity, BigInteger value)
    { public Guid Id = id; public BigInteger Quantity = quantity; public BigInteger Value = value; }
    public static CostReplayResult Calculate(string method, IReadOnlyList<CostInput> inputs)
    {
        if (method is not ("FIFO" or "WEIGHTED_AVERAGE") || inputs.Count > 10000)
            throw new DomainException("cost_input_invalid", "Require a supported method and at most 10000 source events.");
        var layers = new Queue<Layer>(); var issues = new List<CostIssue>(); var seen = new HashSet<(string, Guid)>();
        BigInteger quantity = 0, value = 0, receivedQuantity = 0, receivedValue = 0, issuedQuantity = 0, issuedValue = 0;
        long previous = 0;
        foreach (var input in inputs)
        {
            if (input.Sequence <= previous || input.SourceId == Guid.Empty || !seen.Add((input.Kind, input.SourceId)) ||
                input.Kind is not ("Receipt" or "Issue") || input.Quantity <= 0 || input.Quantity >= 1000000000000000000m ||
                decimal.Round(input.Quantity, 6) != input.Quantity || input.ReceiptValue < 0 || input.ReceiptValue >= 100000000000000000000m ||
                decimal.Round(input.ReceiptValue, 4) != input.ReceiptValue || (input.Kind == "Issue" && input.ReceiptValue != 0))
                throw new DomainException("cost_input_invalid", "Sources must be unique, strictly ordered and within quantity/value precision bounds.");
            previous = input.Sequence;
            var q = new BigInteger(input.Quantity * 1000000m); var v = new BigInteger(input.ReceiptValue * 10000m);
            if (input.Kind == "Receipt")
            {
                quantity += q; value += v; receivedQuantity += q; receivedValue += v;
                if (method == "FIFO") layers.Enqueue(new(input.SourceId, q, v));
                continue;
            }
            if (q > quantity) throw new DomainException("cost_insufficient_coverage", "An issue precedes sufficient costed stock.", 409);
            var allocations = new List<CostAllocation>(); BigInteger cost = 0;
            if (method == "WEIGHTED_AVERAGE")
            {
                cost = q == quantity ? value : RoundRatio(value * q, quantity);
                allocations.Add(new(null, Q(q), V(cost)));
            }
            else
            {
                var remaining = q;
                while (remaining > 0)
                {
                    var layer = layers.Peek(); var take = BigInteger.Min(remaining, layer.Quantity);
                    var allocated = take == layer.Quantity ? layer.Value : RoundRatio(layer.Value * take, layer.Quantity);
                    allocations.Add(new(layer.Id, Q(take), V(allocated))); cost += allocated;
                    layer.Quantity -= take; layer.Value -= allocated; remaining -= take;
                    if (layer.Quantity == 0) layers.Dequeue();
                }
            }
            quantity -= q; value -= cost; issuedQuantity += q; issuedValue += cost;
            issues.Add(new(input.SourceId, Q(q), V(cost), allocations));
        }
        if (receivedQuantity - issuedQuantity != quantity || receivedValue - issuedValue != value || value < 0 || (quantity == 0 && value != 0))
            throw new DomainException("cost_reconciliation_failed", "Cost replay did not conserve quantity and value.", 409);
        return new(method, Q(receivedQuantity), V(receivedValue), Q(issuedQuantity), V(issuedValue), Q(quantity), V(value),
            issues, layers.Select(x => new CostLayer(x.Id, Q(x.Quantity), V(x.Value))).ToArray());
    }
    private static BigInteger RoundRatio(BigInteger numerator, BigInteger denominator)
    { var result = BigInteger.DivRem(numerator, denominator, out var remainder); return result + (remainder * 2 >= denominator ? 1 : 0); }
    private static decimal Q(BigInteger value) => (decimal)value / 1000000m;
    private static decimal V(BigInteger value) => (decimal)value / 10000m;
}
