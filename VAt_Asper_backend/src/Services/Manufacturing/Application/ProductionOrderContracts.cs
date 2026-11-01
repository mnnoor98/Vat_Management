using Vat.Application.Security;
using Vat.Contracts.Manufacturing;

namespace Vat.Services.Manufacturing;
public sealed record CreateProductionOrderRequest(string Reference, Guid CoefficientId, Guid WarehouseId, DateOnly BusinessDate, string PlannedQuantity,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<ProductionUomConversion>? UomConversions=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<ProductionByProductRequest>? ByProducts=null);
public sealed record ProductionByProductRequest(Guid ProductId,string Classification);
public sealed record ProductionWarehouseReference(Guid Id,string Name);
public sealed record ProductionOrderView(Guid Id,Guid CompanyId,string Reference,string State,int Version,string CreatedBy,string? ApprovedBy,ProductionOrderEvidence Evidence,string PlannedQuantityExact);
public interface IProductionWarehouseReferences
{ Task<ProductionWarehouseReference> Warehouse(Actor actor,Guid company,Guid warehouse,string bearer,CancellationToken ct); }
public interface IProductionOrderWorkflow
{
    Task<CoefficientResponse> Create(Actor actor,Guid company,Guid key,CreateProductionOrderRequest request,string bearer,string correlation,CancellationToken ct);
    Task<CoefficientResponse> Transition(Actor actor,Guid company,Guid id,Guid key,string action,CoefficientActionRequest request,string correlation,CancellationToken ct);
    Task<ProductionOrderView> Get(Actor actor,Guid company,Guid id,CancellationToken ct);
    Task<ProductionOrderSource> Source(Actor actor,Guid company,Guid id,CancellationToken ct);
}
