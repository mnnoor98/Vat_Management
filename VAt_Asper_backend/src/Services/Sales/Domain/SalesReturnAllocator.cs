using System.Numerics;

namespace Vat.Domain.Sales;

public sealed record SalesReturnLineInput(Guid LineId, decimal Quantity);
public sealed record SalesReturnAllocation(Guid OriginalEntryId, Guid LineId, decimal Quantity,
    decimal NetAmount, decimal OutputTaxAmount);

public static class SalesReturnAllocator
{
    public static IReadOnlyList<SalesReturnAllocation> Allocate(IReadOnlyList<SalesPostingEntry> originals,
        IReadOnlyList<SalesPostingEntry> previousReturns, IReadOnlyList<SalesReturnLineInput>? requested, IReadOnlyList<SalesAdjustmentHistory>? history = null)
    {
        if (originals.Count == 0 || originals.Any(x => !x.IsOriginal || x.Quantity >= 0))
            throw new DomainException("reconciliation_failed", "Valid original invoice entries are required.", 409);
        var selected = requested ?? originals.Select(x => new SalesReturnLineInput(x.LineId,
            -x.Quantity - previousReturns.Where(r => r.SourceEntryId == x.Id).Sum(r => r.Quantity)))
            .Where(x => x.Quantity > 0).ToArray();
        if (selected.Count is < 1 or > 200 || selected.Select(x => x?.LineId).Distinct().Count() != selected.Count)
            throw new DomainException("validation_failed", "Select 1-200 distinct invoice lines.");
        var allocations = new List<SalesReturnAllocation>();
        foreach (var line in selected)
        {
            if (line is null || line.LineId == Guid.Empty || line.Quantity <= 0 || line.Quantity > 1_000_000 || decimal.Round(line.Quantity, 6) != line.Quantity)
                throw new DomainException("validation_failed", "Return quantities must be positive with at most six decimal places.");
            var original = originals.SingleOrDefault(x => x.LineId == line.LineId)
                ?? throw new DomainException("return_line_not_found", "Return line must belong to this posted invoice.");
            var prior = previousReturns.Where(x => x.SourceEntryId == original.Id).ToArray();
            var returned = prior.Sum(x => x.Quantity);
            var sold = -original.Quantity;
            if (returned < 0 || returned + line.Quantity > sold)
                throw new DomainException("return_quantity_exceeded", "Return exceeds the remaining quantity on this invoice line.", 409);
            // A price adjustment starts a new quantity allocation pool using the remaining values/quantity.
            var epoch = CreditEpoch.For(original, prior, history, priceCredit: false);
            var cumulative = epoch.After.Sum(x => x.Quantity) + line.Quantity;
            var quantityPool = sold - epoch.Before.Sum(x => x.Quantity);
            var targetNet = Target(original.NetAmount + epoch.Before.Sum(x => x.NetAmount), cumulative, quantityPool);
            var targetTax = Target(original.OutputTaxAmount + epoch.Before.Sum(x => x.OutputTaxAmount), cumulative, quantityPool);
            var net = targetNet + epoch.After.Sum(x => x.NetAmount);
            var tax = targetTax + epoch.After.Sum(x => x.OutputTaxAmount);
            if (net < 0 || tax < 0) throw new DomainException("reconciliation_failed", "Prior return allocation is inconsistent.", 409);
            allocations.Add(new(original.Id, original.LineId, line.Quantity, -net, -tax));
        }
        return Array.AsReadOnly(allocations.OrderBy(x => x.LineId).ToArray());
    }

    public static bool FullyReturned(IReadOnlyList<SalesPostingEntry> originals, IReadOnlyList<SalesPostingEntry> returns,
        IReadOnlyList<SalesReturnAllocation>? additional = null) => originals.All(x =>
        returns.Where(r => r.SourceEntryId == x.Id).Sum(r => r.Quantity) +
        (additional?.Where(a => a.OriginalEntryId == x.Id).Sum(a => a.Quantity) ?? 0) == -x.Quantity);

    // Exact integer arithmetic avoids intermediate decimal division changing a half-unit rounding boundary.
    internal static decimal Target(decimal amount, decimal cumulative, decimal sold)
    {
        if (cumulative == sold) return amount;
        var (amountUnits, amountScale) = Parts(amount);
        var (quantityUnits, quantityScale) = Parts(cumulative);
        var (soldUnits, soldScale) = Parts(sold);
        var numerator = amountUnits * quantityUnits * BigInteger.Pow(10, 4 + soldScale);
        var denominator = soldUnits * BigInteger.Pow(10, amountScale + quantityScale);
        var result = BigInteger.DivRem(numerator, denominator, out var remainder);
        if (remainder * 2 >= denominator) result++;
        return (decimal)result / 10000m;
    }

    private static (BigInteger Units, int Scale) Parts(decimal value)
    {
        var bits = decimal.GetBits(value);
        return (new BigInteger((uint)bits[0]) + (new BigInteger((uint)bits[1]) << 32) +
            (new BigInteger((uint)bits[2]) << 64), (bits[3] >> 16) & 0xff);
    }
}
