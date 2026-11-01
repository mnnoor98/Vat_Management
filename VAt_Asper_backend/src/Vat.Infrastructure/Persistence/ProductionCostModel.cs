using Microsoft.EntityFrameworkCore;
using Vat.Domain.Company;
namespace Vat.Infrastructure.Persistence;
public sealed class ProductionCostSession
{
    public Guid TenantId{get;set;} public Guid CompanyId{get;set;} public Guid Id{get;set;} public Guid OrderId{get;set;}
    public string SourceJson{get;set;}="";public string SourceSha256{get;set;}="";public string Reference{get;set;}="";
    public string State{get;set;}="Open";public int Version{get;set;}=1;public decimal WipValue{get;set;}public decimal ReceivedQuantity{get;set;}
    public string CreatedBy{get;set;}="";public DateTimeOffset CreatedAt{get;set;}
}
public sealed class ProductionCostOperation
{
    public Guid? ReversesOperationId{get;set;}
    public decimal? OutputInputQuantity{get;set;}public string? OutputUnitOfMeasure{get;set;}
    public Guid? OutputProductId{get;set;}public Guid? OutputWarehouseId{get;set;}
    public Guid TenantId{get;set;} public Guid CompanyId{get;set;} public Guid Id{get;set;} public Guid SessionId{get;set;}
    public string Kind{get;set;}="";public DateOnly BusinessDate{get;set;}public int SessionVersion{get;set;}public string Evidence{get;set;}="";
    public string Lines{get;set;}="[]";public decimal Labor{get;set;}public decimal Utility{get;set;}public decimal Overhead{get;set;}
    public decimal Amount{get;set;}public decimal OutputQuantity{get;set;}public string State{get;set;}="Draft";public int Version{get;set;}=1;
    public decimal PostedValue{get;set;}public string CreatedBy{get;set;}="";public DateTimeOffset CreatedAt{get;set;}public string? ApprovedBy{get;set;}public DateTimeOffset? PostedAt{get;set;}
}
public sealed class ProductionStockMovement
{
    public Guid TenantId{get;set;} public Guid CompanyId{get;set;} public Guid Id{get;set;} public Guid SessionId{get;set;}public Guid OperationId{get;set;}
    public string Kind{get;set;}="";public Guid WarehouseId{get;set;}public Guid ProductId{get;set;}public decimal Quantity{get;set;}public decimal Value{get;set;}
    public Guid? OriginalIssueId{get;set;}public DateOnly BusinessDate{get;set;}
}
public sealed class ProductionWipEntry
{
    public Guid TenantId{get;set;}public Guid CompanyId{get;set;}public Guid OperationId{get;set;}public Guid SessionId{get;set;}
    public int SessionVersion{get;set;}public string Kind{get;set;}="";public decimal Value{get;set;}public DateOnly BusinessDate{get;set;}
}
internal static class ProductionCostModel
{
    public static void Configure(ModelBuilder model)
    {
        model.Entity<ProductionCostSession>(b=>{
            b.ToTable("production_cost_sessions","inventory",t=>t.HasCheckConstraint("ck_production_cost_session","\"Version\">=1 AND \"WipValue\">=0 AND \"ReceivedQuantity\">=0 AND \"State\" IN ('Open','Closed')"));
            b.HasKey(x=>new{x.TenantId,x.CompanyId,x.Id});b.Property(x=>x.Id).ValueGeneratedNever();b.Property(x=>x.Version).IsConcurrencyToken();
            b.HasIndex(x=>new{x.TenantId,x.CompanyId,x.OrderId}).IsUnique();b.Property(x=>x.WipValue).HasPrecision(24,4);b.Property(x=>x.ReceivedQuantity).HasPrecision(24,6);
            b.Property(x=>x.SourceJson).HasColumnType("jsonb");b.Property(x=>x.SourceSha256).HasMaxLength(64);b.Property(x=>x.Reference).HasMaxLength(100);b.Property(x=>x.CreatedBy).HasMaxLength(200);
            b.HasOne<Company>().WithMany().HasForeignKey(x=>new{x.TenantId,x.CompanyId}).HasPrincipalKey(x=>new{x.TenantId,x.Id}).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<ProductionCostOperation>(b=>{
            b.ToTable("production_cost_operations","inventory",t=>{
                t.HasCheckConstraint("ck_production_operation_kind","\"Kind\" IN ('Issue','Return','Conversion','Receipt','ByProduct','Loss','Close','Reopen','Reverse')");
                t.HasCheckConstraint("ck_production_operation_reversal","(\"Kind\"='Reverse')=(\"ReversesOperationId\" IS NOT NULL)");
                t.HasCheckConstraint("ck_production_operation_state","(\"State\"='Draft' AND \"Version\"=1 AND \"ApprovedBy\" IS NULL AND \"PostedAt\" IS NULL AND \"PostedValue\"=0) OR (\"State\"='Cancelled' AND \"Version\"=2 AND \"ApprovedBy\" IS NULL AND \"PostedAt\" IS NULL AND \"PostedValue\"=0) OR (\"State\"='Posted' AND \"Version\"=2 AND \"ApprovedBy\" IS NOT NULL AND \"ApprovedBy\"<>\"CreatedBy\" AND \"PostedAt\" IS NOT NULL)");
                t.HasCheckConstraint("ck_production_operation_amounts","\"Amount\">=0 AND \"OutputQuantity\">=0 AND \"PostedValue\">=0 AND \"Labor\">=0 AND \"Utility\">=0 AND \"Overhead\">=0");
            });
            b.HasKey(x=>new{x.TenantId,x.CompanyId,x.Id});b.Property(x=>x.Id).ValueGeneratedNever();b.Property(x=>x.Version).IsConcurrencyToken();b.Property(x=>x.Lines).HasColumnType("jsonb");b.Property(x=>x.Evidence).HasMaxLength(1000);
            b.HasOne<ProductionCostOperation>().WithMany().HasForeignKey(x=>new{x.TenantId,x.CompanyId,Id=x.ReversesOperationId}).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x=>new{x.TenantId,x.CompanyId,x.ReversesOperationId}).HasFilter("\"State\"='Posted' AND \"ReversesOperationId\" IS NOT NULL");
            b.Property(x=>x.CreatedBy).HasMaxLength(200);b.Property(x=>x.ApprovedBy).HasMaxLength(200);
            foreach(var name in new[]{"Labor","Utility","Overhead","Amount","PostedValue"})b.Property<decimal>(name).HasPrecision(24,4);b.Property(x=>x.OutputQuantity).HasPrecision(24,6);b.Property(x=>x.OutputInputQuantity).HasPrecision(24,6);b.Property(x=>x.OutputUnitOfMeasure).HasMaxLength(20);
            b.HasOne<ProductionCostSession>().WithMany().HasForeignKey(x=>new{x.TenantId,x.CompanyId,Id=x.SessionId}).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<ProductionStockMovement>(b=>{
            b.ToTable("production_stock_movements","inventory",t=>t.HasCheckConstraint("ck_production_movement_sign","(\"Kind\" IN ('Issue','ReceiptReversal','ReturnReversal') AND \"Quantity\"<0 AND \"Value\"<=0) OR (\"Kind\" IN ('Return','Receipt') AND \"Quantity\">0 AND \"Value\">=0)"));
            b.HasKey(x=>new{x.TenantId,x.CompanyId,x.Id});b.Property(x=>x.Id).ValueGeneratedNever();b.Property(x=>x.Quantity).HasPrecision(24,6);b.Property(x=>x.Value).HasPrecision(24,4);
            b.HasIndex(x=>new{x.TenantId,x.CompanyId,x.WarehouseId,x.ProductId});b.HasOne<ProductionCostOperation>().WithMany().HasForeignKey(x=>new{x.TenantId,x.CompanyId,Id=x.OperationId}).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<ProductionWipEntry>(b=>{
            b.ToTable("production_wip_entries","inventory");b.HasKey(x=>new{x.TenantId,x.CompanyId,x.OperationId});b.Property(x=>x.OperationId).ValueGeneratedNever();b.Property(x=>x.Value).HasPrecision(24,4);
            b.HasIndex(x=>new{x.TenantId,x.CompanyId,x.SessionId,x.SessionVersion}).IsUnique();b.HasOne<ProductionCostOperation>().WithMany().HasForeignKey(x=>new{x.TenantId,x.CompanyId,Id=x.OperationId}).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
