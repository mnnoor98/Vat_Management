using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vat.Application;
using Vat.Contracts.Manufacturing;
using Vat.Services.Manufacturing.Domain;
using Vat.Services.Manufacturing.Infrastructure;
using Xunit;

namespace Vat.Manufacturing.Tests;

public sealed class ProductionExecutionInboxTests(ManufacturingApiFactory factory):IClassFixture<ManufacturingApiFactory>
{
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
    private static async Task<(Guid Tenant,Guid Company,Guid Order)> Seed(ManufacturingDbContext db)
    {
        var tenant=Guid.NewGuid();var company=Guid.NewGuid();var bom=Guid.NewGuid();var coefficient=Guid.NewGuid();var order=Guid.NewGuid();
        db.Add(new BomHeader{TenantId=tenant,CompanyId=company,Id=bom,ProductId=Guid.NewGuid(),Code=bom.ToString(),Name="BOM",CreatedBy="maker"});
        db.Add(new CoefficientVersion{TenantId=tenant,CompanyId=company,Id=coefficient,BomId=bom,Revision=1,OutputQuantity=1,EffectiveFrom=new(2026,1,1),State="Approved",ApprovedBy="checker",CreatedBy="maker"});
        db.Add(new ProductionOrder{TenantId=tenant,CompanyId=company,Id=order,CoefficientVersionId=coefficient,WarehouseId=Guid.NewGuid(),Reference=order.ToString(),BusinessDate=new(2026,1,1),PlannedQuantity=10,State="Approved",ApprovedBy="checker",CreatedBy="maker"});
        await db.SaveChangesAsync();return(tenant,company,order);
    }
    private static ReceivedMessage Message(Guid tenant,Guid company,Guid session,Guid order,Guid eventId,ProductionCostPostedV1 payload)
    {
        var body=JsonSerializer.SerializeToUtf8Bytes(new{eventId,eventType=nameof(ProductionCostPostedV1),schemaVersion=1,occurredAt=DateTimeOffset.UtcNow,tenantId=tenant,companyId=company,aggregateId=session,aggregateVersion=payload.SessionVersion,correlationId=Guid.NewGuid().ToString(),payload},Json);
        return new(body,eventId.ToString(),nameof(ProductionCostPostedV1),"application/json");
    }
    [Fact]
    public async Task Deduplicates_recovers_out_of_order_and_completes_projection()
    {
        await factory.Inspect(async db=>{
            var(tenant,company,order)=await Seed(db);var session=Guid.NewGuid();var processor=new ProductionExecutionInboxProcessor(db,TimeProvider.System);
            var v2=Message(tenant,company,session,order,Guid.NewGuid(),new(session,order,Guid.NewGuid(),"Receipt",2,"4.0000","2.000000"));
            Assert.Equal("Processed",(await processor.Process(v2,default)).Status);db.ChangeTracker.Clear();
            Assert.Equal("Duplicate",(await processor.Process(v2,default)).Status);db.ChangeTracker.Clear();
            var v1=Message(tenant,company,session,order,Guid.NewGuid(),new(session,order,Guid.NewGuid(),"Issue",1,"6.0000","0.000000"));
            Assert.Equal("Ignored",(await processor.Process(v1,default)).Status);db.ChangeTracker.Clear();
            var close=Message(tenant,company,session,order,Guid.NewGuid(),new(session,order,Guid.NewGuid(),"Close",3,"0.0000","2.000000"));
            Assert.Equal("Processed",(await processor.Process(close,default)).Status);db.ChangeTracker.Clear();
            var checkpoint=await db.ExecutionCheckpoints.SingleAsync(x=>x.OrderId==order);Assert.Equal("Completed",checkpoint.ExecutionState);Assert.Equal(3,checkpoint.SessionVersion);
            var reopen=Message(tenant,company,session,order,Guid.NewGuid(),new(session,order,Guid.NewGuid(),"Reopen",4,"0.0000","2.000000"));
            Assert.Equal("Processed",(await processor.Process(reopen,default)).Status);db.ChangeTracker.Clear();checkpoint=await db.ExecutionCheckpoints.SingleAsync(x=>x.OrderId==order);Assert.Equal("Running",checkpoint.ExecutionState);Assert.Equal(4,checkpoint.SessionVersion);
            var reclose=Message(tenant,company,session,order,Guid.NewGuid(),new(session,order,Guid.NewGuid(),"Close",5,"0.0000","2.000000"));
            Assert.Equal("Processed",(await processor.Process(reclose,default)).Status);db.ChangeTracker.Clear();
            Assert.Equal("Completed",(await db.Orders.SingleAsync(x=>x.Id==order)).State);Assert.Equal(5,await db.ExecutionInbox.CountAsync(x=>x.OrderId==order));return 0;
        });
    }
    [Fact]
    public async Task Conflicting_redelivery_is_quarantined_without_changing_projection()
    {
        await factory.Inspect(async db=>{
            var(tenant,company,order)=await Seed(db);var session=Guid.NewGuid();var eventId=Guid.NewGuid();var processor=new ProductionExecutionInboxProcessor(db,TimeProvider.System);
            var original=Message(tenant,company,session,order,eventId,new(session,order,Guid.NewGuid(),"Issue",1,"5.0000","0.000000"));await processor.Process(original,default);db.ChangeTracker.Clear();
            var conflict=Message(tenant,company,session,order,eventId,new(session,order,Guid.NewGuid(),"Loss",1,"0.0000","0.000000"));
            var result=await processor.Process(conflict,default);Assert.Equal("Quarantined",result.Status);Assert.NotNull(result.DeadLetterId);db.ChangeTracker.Clear();
            Assert.Equal("5.0000",(await db.ExecutionCheckpoints.SingleAsync(x=>x.OrderId==order)).WipValue);Assert.Equal(1,await db.ExecutionDeadLetters.CountAsync(x=>x.EventId==eventId));return 0;
        });
    }
}
