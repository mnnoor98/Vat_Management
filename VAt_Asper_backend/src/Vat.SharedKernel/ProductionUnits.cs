using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
using Vat.Domain;
namespace Vat.Contracts.Manufacturing;

public static class ProductionUnits
{
    public static string Unit(string value){var unit=(value??"").Trim().ToUpperInvariant();if(!Regex.IsMatch(unit,@"^[\p{L}\p{N}][\p{L}\p{N}._/-]{0,19}$"))throw new DomainException("validation_failed","Use a unit code of 1–20 letters, digits, dot, slash, dash or underscore.");return unit;}
    public static decimal Factor(string value){if(!decimal.TryParse(value,NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var factor)||factor<=0||factor>=1000000000000m||decimal.Round(factor,12)!=factor)throw new DomainException("validation_failed","Require a positive stock-units-per-input-unit decimal string at scale 12.");return factor;}
    public static IReadOnlyList<ProductionUomConversion>? Freeze(ProductionOrderEvidence source,IReadOnlyList<ProductionUomConversion>? requested)
    {
        if(requested is null||requested.Count==0)return null;
        if(requested.Count>100||requested.Any(x=>x is null))throw new DomainException("validation_failed","At most 100 explicit product unit conversions are supported.");
        var products=source.Materials.Select(x=>x.Product).Append(source.FinishedProduct).Concat(source.ByProducts?.Select(x=>x.Product)??[]).ToDictionary(x=>x.Id);
        var result=requested.Select(x=>{
            if(!products.TryGetValue(x.ProductId,out var product))throw new DomainException("validation_failed","Unit conversions must identify a product on the frozen order BOM.");
            var unit=Unit(x.InputUnitOfMeasure??"");if(unit==product.UnitOfMeasure.Trim().ToUpperInvariant())throw new DomainException("validation_failed","The registered stock unit always has factor one; do not override it.");
            return new ProductionUomConversion(x.ProductId,unit,Factor(x.StockUnitsPerInputUnit).ToString("0.############",CultureInfo.InvariantCulture));
        }).OrderBy(x=>x.ProductId).ThenBy(x=>x.InputUnitOfMeasure,StringComparer.Ordinal).ToArray();
        if(result.Select(x=>(x.ProductId,x.InputUnitOfMeasure)).Distinct().Count()!=result.Length)throw new DomainException("validation_failed","Each product/input unit must have exactly one reviewed conversion.");
        return result;
    }
    public static decimal ToStock(ProductionOrderEvidence source,ProductionProduct product,decimal quantity,string? inputUnit)
    {
        if(quantity<=0||quantity>=1000000000000m||decimal.Round(quantity,6)!=quantity)throw new DomainException("validation_failed","Require positive input quantity at scale 6.");
        if(inputUnit is null)return quantity; // Existing registered stock UOM labels remain compatible.
        var unit=Unit(inputUnit);
        var factor=unit==product.UnitOfMeasure.Trim().ToUpperInvariant()?1m:Factor(source.UomConversions?.SingleOrDefault(x=>x.ProductId==product.Id&&x.InputUnitOfMeasure==unit)?.StockUnitsPerInputUnit??throw new DomainException("production_uom_unapproved","Input unit is not frozen in this approved production order.",409));
        var numerator=new BigInteger(quantity*1000000m)*new BigInteger(factor*1000000000000m);
        var stock=BigInteger.DivRem(numerator,new BigInteger(1000000000000m),out var residual);
        if(residual!=0||stock<=0||stock>=BigInteger.Pow(10,18))throw new DomainException("production_uom_precision","Converted stock quantity must be exact at scale 6 and below one trillion; rounding is not permitted.",422);
        return (decimal)stock/1000000m;
    }
}
