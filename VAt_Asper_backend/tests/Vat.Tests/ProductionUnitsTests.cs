using System.Text.Json;
using Vat.Contracts.Manufacturing;
using Vat.Domain;
using Xunit;
namespace Vat.Tests;
public sealed class ProductionUnitsTests
{
    private static ProductionOrderEvidence Source(){var product=new ProductionProduct(Guid.NewGuid(),"RAW","KG");return new(Guid.NewGuid(),1,3,new string('A',64),Guid.NewGuid(),"Output",new(2026,10,31),new(Guid.NewGuid(),"FG","PCS"),10,10,[new(product,5,0)]);}
    [Theory]
    [InlineData("BAG","25","2",50)]
    [InlineData("G","0.001","250",0.25)]
    [InlineData("MICRO","0.000000001","1000",0.000001)]
    public void Frozen_ratio_converts_exactly_without_decimal_multiplication_loss(string unit,string factor,string input,decimal expected){var source=Source();var product=source.Materials[0].Product;source=source with{UomConversions=ProductionUnits.Freeze(source,[new(product.Id,unit,factor)])};Assert.Equal(expected,ProductionUnits.ToStock(source,product,decimal.Parse(input,System.Globalization.CultureInfo.InvariantCulture),unit));}
    [Fact]
    public void Unapproved_units_rounding_overflow_duplicate_and_identity_override_fail_closed(){
        var s=Source();var p=s.Materials[0].Product;
        Assert.Throws<DomainException>(()=>ProductionUnits.ToStock(s,p,1,"BAG"));
        Assert.Throws<DomainException>(()=>ProductionUnits.Freeze(s,[new(p.Id,"kg","2")]));
        Assert.Throws<DomainException>(()=>ProductionUnits.Freeze(s,[new(p.Id,"bag","2"),new(p.Id," BAG ","3")]));
        Assert.Throws<DomainException>(()=>ProductionUnits.Freeze(s,[new(Guid.NewGuid(),"BAG","2")]));
        s=s with{UomConversions=ProductionUnits.Freeze(s,[new(p.Id,"MICRO","0.000000001"),new(p.Id,"HUGE","999999999999")])};
        Assert.Equal("production_uom_precision",Assert.Throws<DomainException>(()=>ProductionUnits.ToStock(s,p,1,"MICRO")).Code);
        Assert.Equal("production_uom_precision",Assert.Throws<DomainException>(()=>ProductionUnits.ToStock(s,p,2,"HUGE")).Code);
    }
    [Fact]
    public void Legacy_evidence_omits_new_optional_fields_so_its_digest_stays_compatible(){var s=Source();var json=JsonSerializer.Serialize(s,new JsonSerializerOptions(JsonSerializerDefaults.Web));Assert.DoesNotContain("uomConversions",json);Assert.DoesNotContain("byProducts",json);var parsed=JsonSerializer.Deserialize<ProductionOrderEvidence>(json,new JsonSerializerOptions(JsonSerializerDefaults.Web))!;Assert.Null(parsed.UomConversions);Assert.Null(parsed.ByProducts);}
    [Fact]
    public void Existing_stock_labels_are_preserved_without_forcing_new_alias_code_rules(){var s=Source();var p=s.Materials[0].Product with{UnitOfMeasure="Metric Ton"};Assert.Equal(2m,ProductionUnits.ToStock(s,p,2,null));}
}
