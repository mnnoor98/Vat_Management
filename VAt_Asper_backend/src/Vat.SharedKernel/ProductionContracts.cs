namespace Vat.Contracts.Manufacturing;

public sealed record ProductionUomConversion(Guid ProductId,string InputUnitOfMeasure,string StockUnitsPerInputUnit);
public sealed record ProductionProduct(Guid Id, string Reference, string UnitOfMeasure);
public sealed record ProductionMaterial(ProductionProduct Product, decimal QuantityPerBatch, decimal WastagePercent);
public sealed record ProductionByProduct(ProductionProduct Product,string Classification);
public sealed record ProductionOrderEvidence(Guid CoefficientId, int Revision, int CoefficientVersion, string CoefficientSha256,
    Guid WarehouseId, string WarehouseName, DateOnly BusinessDate, ProductionProduct FinishedProduct, decimal BatchQuantity,
    decimal PlannedQuantity, IReadOnlyList<ProductionMaterial> Materials,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<ProductionUomConversion>? UomConversions=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<ProductionByProduct>? ByProducts=null);
public sealed record ProductionOrderSource(Guid TenantId, Guid CompanyId, Guid OrderId, int Version, string State,
    string Reference, string CreatedBy, string ApprovedBy, string EvidenceSha256, ProductionOrderEvidence Evidence);
public sealed record ProductionExecutionAcceptedV1(Guid SessionId,Guid OrderId,string SourceSha256);
public sealed record ProductionCostPostedV1(Guid SessionId,Guid OrderId,Guid OperationId,string Kind,int SessionVersion,string WipValue,string ReceivedQuantity);
