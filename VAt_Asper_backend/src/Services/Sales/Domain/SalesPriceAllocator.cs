namespace Vat.Domain.Sales;

public sealed record SalesPriceLineInput(Guid LineId, decimal NetAmount);

public static class SalesPriceAllocator
{
    public static IReadOnlyList<SalesReturnAllocation> Allocate(IReadOnlyList<SalesPostingEntry> originals,
        IReadOnlyList<SalesPostingEntry> previousCredits, IReadOnlyList<SalesPriceLineInput>? requested,
        IReadOnlyList<SalesAdjustmentHistory> history)
    {
        if (requested is null || requested.Count is < 1 or > 200 || requested.Select(x => x?.LineId).Distinct().Count() != requested.Count)
            throw new DomainException("validation_failed", "Select 1-200 distinct invoice lines with positive net credit amounts.");
        var allocations = new List<SalesReturnAllocation>();
        foreach (var line in requested)
        {
            if (line is null || line.LineId == Guid.Empty || line.NetAmount <= 0 || decimal.Round(line.NetAmount, 4) != line.NetAmount)
                throw new DomainException("validation_failed", "Net credit amounts must be positive with at most four decimal places.");
            var original = originals.SingleOrDefault(x => x.LineId == line.LineId && x.IsOriginal && x.Quantity < 0)
                ?? throw new DomainException("credit_line_not_found", "Price credit line must belong to this posted invoice.");
            var prior = previousCredits.Where(x => x.SourceEntryId == original.Id).ToArray();
            var remaining = original.NetAmount + prior.Sum(x => x.NetAmount);
            if (line.NetAmount > remaining || -original.Quantity <= prior.Sum(x => x.Quantity))
                throw new DomainException("credit_amount_exceeded", "Price credit exceeds the remaining value on retained goods.", 409);
            // Goods returns start a new monetary pool. Consecutive price credits use cumulative net targets.
            var epoch = CreditEpoch.For(original, prior, history, priceCredit: true);
            var poolNet = original.NetAmount + epoch.Before.Sum(x => x.NetAmount);
            var poolTax = original.OutputTaxAmount + epoch.Before.Sum(x => x.OutputTaxAmount);
            var cumulativeNet = line.NetAmount - epoch.After.Sum(x => x.NetAmount);
            if (poolNet <= 0 || poolTax < 0 || cumulativeNet > poolNet)
                throw new DomainException("reconciliation_failed", "The remaining credit pool is inconsistent.", 409);
            var tax = SalesReturnAllocator.Target(poolTax, cumulativeNet, poolNet) + epoch.After.Sum(x => x.OutputTaxAmount);
            if (tax < 0) throw new DomainException("reconciliation_failed", "Prior price-tax allocation is inconsistent.", 409);
            allocations.Add(new(original.Id, original.LineId, 0, -line.NetAmount, -tax));
        }
        return Array.AsReadOnly(allocations.OrderBy(x => x.LineId).ToArray());
    }
}

internal sealed record CreditEpoch(IReadOnlyList<SalesPostingEntry> Before, IReadOnlyList<SalesPostingEntry> After)
{
    public static CreditEpoch For(SalesPostingEntry original, IReadOnlyList<SalesPostingEntry> prior,
        IReadOnlyList<SalesAdjustmentHistory>? history, bool priceCredit)
    {
        // The legacy pure goods-return calculation needs no history lookup.
        if (!priceCredit && prior.All(x => x.Quantity > 0)) return new([], prior);
        var versions = (history ?? []).Where(x => x.Status == "Posted").ToDictionary(x => x.Id, x => x.SourceVersion);
        if (prior.Any(x => x.AdjustmentId is null || !versions.ContainsKey(x.AdjustmentId.Value)))
            throw new DomainException("reconciliation_failed", "Posted credit history is incomplete.", 409);
        var boundary = prior.Where(x => priceCredit ? x.Quantity > 0 || x.EntryType == "PriceDebit" : x.Quantity == 0)
            .Select(x => versions[x.AdjustmentId!.Value]).DefaultIfEmpty(0).Max();
        var before = prior.Where(x => versions[x.AdjustmentId!.Value] <= boundary).ToArray();
        var after = prior.Where(x => versions[x.AdjustmentId!.Value] > boundary).ToArray();
        if (original.NetAmount + before.Sum(x => x.NetAmount) < 0 || original.OutputTaxAmount + before.Sum(x => x.OutputTaxAmount) < 0 ||
            -original.Quantity - before.Sum(x => x.Quantity) < 0)
            throw new DomainException("reconciliation_failed", "The credit allocation pool exceeds original values.", 409);
        return new(before, after);
    }
}
