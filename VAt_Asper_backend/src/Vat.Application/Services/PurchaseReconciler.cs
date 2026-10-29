using Vat.Application.Contracts;
using Vat.Domain.Posting;
using Vat.Domain.Purchasing;

namespace Vat.Application;

public static class PurchaseReconciler
{
    public static ReconciliationResponse Evaluate(PostingReadSnapshot snapshot)
    {
        var differences = new List<string>();
        var purchase = snapshot.Purchase;
        var plan = snapshot.Plan;
        var reversed = purchase.Status == PurchaseStatus.Reversed;
        var originals = snapshot.StockEntries.Where(x => x.ReversesEntryId is null).ToArray();
        var originalTax = snapshot.TaxEntries.Where(x => x.ReversesEntryId is null).ToArray();
        var corrections = snapshot.StockEntries.Where(x => x.ReversesEntryId is not null).ToArray();
        var taxCorrections = snapshot.TaxEntries.Where(x => x.ReversesEntryId is not null).ToArray();
        var postedReversals = snapshot.Reversals.Where(x => x.Status == ReversalStatus.Posted).ToArray();
        if (purchase.Status is not (PurchaseStatus.Posted or PurchaseStatus.ReversalPending or PurchaseStatus.Reversed) || plan?.Status != PostingPlanStatus.Posted)
            differences.Add("Purchase and posting plan are not financially posted.");
        if (snapshot.Reversals.Count(x => x.Status == ReversalStatus.Pending) != (purchase.Status == PurchaseStatus.ReversalPending ? 1 : 0))
            differences.Add("Pending reversal state differs from the source.");
        if (postedReversals.Length != (reversed ? 1 : 0))
            differences.Add("Posted reversal count differs from the source.");
        if (plan is not null)
        {
            if (plan.Snapshot.NetAmount != purchase.NetAmount || plan.Snapshot.BusinessDate != purchase.BusinessDate ||
                plan.Snapshot.Currency != purchase.Currency || plan.Snapshot.SupplierReference != purchase.SupplierReference ||
                plan.Snapshot.Lines.Count != purchase.Lines.Count)
                differences.Add("Source document differs from the frozen plan.");
            if (purchase.PostedVersion != plan.SourceVersion + 1)
                differences.Add("Posted source version differs from the prepared plan.");
            var count = plan.Snapshot.Lines.Count;
            if (originals.Length != count || originalTax.Length != count || corrections.Length != (reversed ? count : 0) || taxCorrections.Length != (reversed ? count : 0))
                differences.Add("Ledger line counts differ from the frozen plan.");
            foreach (var line in plan.Snapshot.Lines)
            {
                var source = purchase.Lines.SingleOrDefault(x => x.Id == line.PurchaseLineId);
                if (source is null || source.ProductReference != line.ProductReference || source.Quantity != line.Quantity ||
                    source.UnitPrice != line.UnitPrice || source.NetAmount != line.NetAmount)
                    differences.Add($"Source line mismatch for line {line.PurchaseLineId}.");
                var stock = originals.SingleOrDefault(x => x.PurchaseLineId == line.PurchaseLineId);
                var tax = originalTax.SingleOrDefault(x => x.PurchaseLineId == line.PurchaseLineId);
                if (stock is null || stock.PlanId != plan.Id || stock.WarehouseId != plan.WarehouseId || stock.ProductId != line.ProductId ||
                    stock.Quantity != line.Quantity || stock.BusinessDate != plan.Snapshot.BusinessDate || stock.ReversalId is not null)
                    differences.Add($"Stock receipt mismatch for line {line.PurchaseLineId}.");
                if (tax is null || tax.PlanId != plan.Id || tax.TaxRuleId != line.TaxRuleId || tax.NetAmount != line.NetAmount ||
                    tax.TaxAmount != line.TaxAmount || tax.RecoverableTaxAmount != line.RecoverableTaxAmount ||
                    tax.BusinessDate != plan.Snapshot.BusinessDate || tax.ReversalId is not null)
                    differences.Add($"Tax posting mismatch for line {line.PurchaseLineId}.");
                if (reversed && stock is not null && tax is not null && postedReversals.Length == 1)
                {
                    var reversal = postedReversals[0];
                    var stockCorrection = corrections.SingleOrDefault(x => x.ReversesEntryId == stock.Id);
                    var taxCorrection = taxCorrections.SingleOrDefault(x => x.ReversesEntryId == tax.Id);
                    if (reversal.PlanId != plan.Id || stockCorrection is null || stockCorrection.ReversalId != reversal.Id ||
                        stockCorrection.PlanId != stock.PlanId || stockCorrection.PurchaseLineId != stock.PurchaseLineId ||
                        stockCorrection.WarehouseId != stock.WarehouseId || stockCorrection.ProductId != stock.ProductId ||
                        stockCorrection.Quantity != -stock.Quantity || stockCorrection.BusinessDate != reversal.CorrectionDate)
                        differences.Add($"Stock reversal mismatch for line {line.PurchaseLineId}.");
                    if (taxCorrection is null || taxCorrection.ReversalId != reversal.Id || taxCorrection.PlanId != tax.PlanId ||
                        taxCorrection.PurchaseLineId != tax.PurchaseLineId || taxCorrection.TaxRuleId != tax.TaxRuleId ||
                        taxCorrection.NetAmount != -tax.NetAmount || taxCorrection.TaxAmount != -tax.TaxAmount ||
                        taxCorrection.RecoverableTaxAmount != -tax.RecoverableTaxAmount || taxCorrection.BusinessDate != reversal.CorrectionDate)
                        differences.Add($"Tax reversal mismatch for line {line.PurchaseLineId}.");
                }
            }
        }
        return new(purchase.Id, purchase.Status.ToString(), differences.Count == 0, snapshot.StockEntries.Count,
            snapshot.TaxEntries.Count, snapshot.TaxEntries.Sum(x => x.NetAmount), snapshot.TaxEntries.Sum(x => x.TaxAmount),
            snapshot.TaxEntries.Sum(x => x.RecoverableTaxAmount), differences);
    }
}
