using Vat.Application.Security;
using Vat.Contracts.Manufacturing;
namespace Vat.Application;
public interface IProductionExecutionReferences
{ Task<ProductionOrderSource> Order(Actor actor,Guid company,Guid order,string bearer,CancellationToken ct); }
