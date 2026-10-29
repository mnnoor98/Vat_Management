using Vat.Domain.Tax;

namespace Vat.Services.VatEngine;

public sealed class TaxCalculationService(ITaxRuleStore store)
{
    public async Task<TaxCalculationResult> Calculate(Guid tenantId, Guid companyId, TaxCalculationRequest request, CancellationToken ct)
    {
        var lineResults = new List<TaxCalculationItemResult>();
        decimal totalNet = 0;
        decimal totalVat = 0;
        decimal totalSd = 0;

        foreach (var item in request.Items)
        {
            var net = decimal.Round(item.Quantity * item.UnitPrice, 2);
            var rule = await store.EffectiveRule(tenantId, companyId, item.TaxCode, "Sale", request.Date, ct);
            var vatRate = rule?.RatePercent ?? 15.0m;
            var vat = decimal.Round(net * (vatRate / 100m), 2);
            var sdRate = 0m;
            var sd = 0m;
            var total = net + vat + sd;

            totalNet += net;
            totalVat += vat;
            totalSd += sd;

            lineResults.Add(new TaxCalculationItemResult(item.ProductId, net, vatRate, vat, sdRate, sd, total));
        }

        return new TaxCalculationResult(totalNet, totalVat, totalSd, totalNet + totalVat + totalSd, lineResults);
    }
}

