using Microsoft.EntityFrameworkCore;
using Vat.Services.Manufacturing.Domain;
namespace Vat.Services.Manufacturing.Infrastructure;

public sealed class ManufacturingDbContext(DbContextOptions<ManufacturingDbContext> options) : DbContext(options)
{
    public DbSet<ProductionOrder> Orders => Set<ProductionOrder>();
    public DbSet<ProductionExecutionInbox> ExecutionInbox => Set<ProductionExecutionInbox>();
    public DbSet<ProductionExecutionCheckpoint> ExecutionCheckpoints => Set<ProductionExecutionCheckpoint>();
    public DbSet<ProductionExecutionDeadLetter> ExecutionDeadLetters => Set<ProductionExecutionDeadLetter>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<ProductionExecutionInbox>(b=>{
            b.ToTable("production_execution_inbox","manufacturing",t=>t.HasCheckConstraint("ck_production_execution_inbox","\"SessionVersion\" >= 0 AND \"Status\" IN ('Processed','Ignored')"));
            b.HasKey(x=>new{x.TenantId,x.CompanyId,x.EventId});b.Property(x=>x.EventId).ValueGeneratedNever();
            b.Property(x=>x.EventType).HasMaxLength(100);b.Property(x=>x.PayloadSha256).HasMaxLength(64);b.Property(x=>x.Status).HasMaxLength(20);
            b.HasIndex(x=>new{x.TenantId,x.CompanyId,x.OrderId,x.SessionVersion});
            b.HasOne<ProductionOrder>().WithMany().HasForeignKey(x=>new{x.TenantId,x.CompanyId,Id=x.OrderId}).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<ProductionExecutionCheckpoint>(b=>{
            b.ToTable("production_execution_checkpoints","manufacturing",t=>t.HasCheckConstraint("ck_production_execution_checkpoints","\"SessionVersion\" >= 0 AND \"ExecutionState\" IN ('Running','Completed')"));
            b.HasKey(x=>new{x.TenantId,x.CompanyId,x.OrderId});b.Property(x=>x.ExecutionState).HasMaxLength(20);b.Property(x=>x.WipValue).HasMaxLength(40);b.Property(x=>x.ReceivedQuantity).HasMaxLength(40);
            b.HasIndex(x=>new{x.TenantId,x.CompanyId,x.SessionId}).IsUnique();
            b.HasOne<ProductionOrder>().WithMany().HasForeignKey(x=>new{x.TenantId,x.CompanyId,Id=x.OrderId}).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<ProductionExecutionDeadLetter>(b=>{
            b.ToTable("production_execution_dead_letters","manufacturing");b.HasKey(x=>x.Id);b.Property(x=>x.Id).ValueGeneratedNever();
            b.Property(x=>x.Fingerprint).HasMaxLength(64);b.Property(x=>x.ErrorCode).HasMaxLength(100);b.Property(x=>x.Body).HasMaxLength(1048576);
            b.HasIndex(x=>x.Fingerprint).IsUnique();b.HasIndex(x=>new{x.TenantId,x.CompanyId,x.LastSeenAt});
        });
        model.Entity<ProductionOrderDocument>(b=>{
            b.ToTable("production_order_documents","manufacturing");b.HasKey(x=>new{x.TenantId,x.CompanyId,x.Id});b.Property(x=>x.Id).ValueGeneratedNever();b.Property(x=>x.Sha256).HasMaxLength(64);
            b.HasOne<ProductionOrder>().WithMany().HasForeignKey(x=>new{x.TenantId,x.CompanyId,x.Id}).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<ProductionOrderAudit>(b=>{
            b.ToTable("production_order_audit","manufacturing");b.HasKey(x=>new{x.TenantId,x.CompanyId,x.Id});b.Property(x=>x.Id).ValueGeneratedNever();
            b.Property(x=>x.Action).HasMaxLength(40);b.Property(x=>x.Actor).HasMaxLength(200);b.Property(x=>x.Reason).HasMaxLength(1000);b.Property(x=>x.CorrelationId).HasMaxLength(200);
            b.HasIndex(x=>new{x.TenantId,x.CompanyId,x.OrderId,x.Version}).IsUnique();b.HasOne<ProductionOrder>().WithMany().HasForeignKey(x=>new{x.TenantId,x.CompanyId,Id=x.OrderId}).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<CoefficientDocument>(b =>
        {
            b.ToTable("coefficient_documents", "manufacturing");
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.SnapshotJson).HasColumnType("text");
            b.Property(x => x.SnapshotSha256).HasMaxLength(64);
            b.HasOne<CoefficientVersion>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<CoefficientCommand>(b =>
        {
            b.ToTable("coefficient_commands", "manufacturing");
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Actor).HasMaxLength(200); b.Property(x => x.RequestHash).HasMaxLength(64);
        });
        model.Entity<CoefficientAudit>(b =>
        {
            b.ToTable("coefficient_audit", "manufacturing");
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Actor).HasMaxLength(200); b.Property(x => x.Action).HasMaxLength(40);
            b.Property(x => x.Reason).HasMaxLength(1000); b.Property(x => x.CorrelationId).HasMaxLength(200);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.CoefficientId, x.Version }).IsUnique();
            b.HasOne<CoefficientVersion>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.CoefficientId }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<BomHeader>(b =>
        {
            b.ToTable("bom_headers", "manufacturing", t =>
            {
                t.HasCheckConstraint("ck_bom_headers_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
            });
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.CreatedBy).HasMaxLength(200).IsRequired();
            b.Property(x => x.Version).HasDefaultValue(1).IsConcurrencyToken();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.CreatedAt });
            b.Property(x => x.CreatedBy).IsRequired();
            b.Property(x => x.Code).IsRequired();
            b.Property(x => x.Name).IsRequired();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.ProductId });
            b.Property(x => x.Code).HasMaxLength(100);
            b.Property(x => x.Name).HasMaxLength(200);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.Code }).IsUnique().AreNullsDistinct(false);
        });
        model.Entity<CoefficientVersion>(b =>
        {
            b.ToTable("coefficient_versions", "manufacturing", t =>
            {
                t.HasCheckConstraint("ck_coefficient_versions_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                t.HasCheckConstraint("ck_coefficient_versions_rules", "(\"OutputQuantity\" > 0) AND \"Revision\" > 0 AND (\"EffectiveUntil\" IS NULL OR \"EffectiveUntil\" >= \"EffectiveFrom\") AND (\"ApprovedBy\" IS NULL OR \"ApprovedBy\" <> \"CreatedBy\") AND \"State\" IN ('Draft','Pending','Approved','Rejected','Cancelled','Running','Completed','Failed','Submitted','Posted')");
            });
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.CreatedBy).HasMaxLength(200).IsRequired();
            b.Property(x => x.Version).HasDefaultValue(1).IsConcurrencyToken();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.CreatedAt });
            b.Property(x => x.CreatedBy).IsRequired();
            b.Property(x => x.State).IsRequired();
            b.Property(x => x.OutputQuantity).HasPrecision(24, 6);
            b.Property(x => x.State).HasMaxLength(40);
            b.Property(x => x.ApprovedBy).HasMaxLength(200);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.BomId, x.Revision }).IsUnique().AreNullsDistinct(false);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.State, x.CreatedAt });
            b.HasOne<BomHeader>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.BomId }).HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.BomId });
        });
        model.Entity<BomLine>(b =>
        {
            b.ToTable("bom_lines", "manufacturing", t =>
            {
                t.HasCheckConstraint("ck_bom_lines_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                t.HasCheckConstraint("ck_bom_lines_rules", "(\"Quantity\" > 0 AND \"WastagePercent\" BETWEEN 0 AND 100)");
            });
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.CreatedBy).HasMaxLength(200).IsRequired();
            b.Property(x => x.Version).HasDefaultValue(1).IsConcurrencyToken();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.CreatedAt });
            b.Property(x => x.CreatedBy).IsRequired();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.ProductId });
            b.Property(x => x.Quantity).HasPrecision(24, 6);
            b.Property(x => x.WastagePercent).HasPrecision(24, 6);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.CoefficientVersionId, x.ProductId }).IsUnique().AreNullsDistinct(false);
            b.HasOne<CoefficientVersion>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.CoefficientVersionId }).HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.CoefficientVersionId });
        });
        model.Entity<ProductionOrder>(b =>
        {
            b.ToTable("production_orders", "manufacturing", t =>
            {
                t.HasCheckConstraint("ck_production_orders_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                t.HasCheckConstraint("ck_production_orders_rules", "(\"PlannedQuantity\" > 0) AND (\"ApprovedBy\" IS NULL OR \"ApprovedBy\" <> \"CreatedBy\") AND \"State\" IN ('Draft','Pending','Approved','Rejected','Cancelled','Running','Completed','Failed','Submitted','Posted')");
            });
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.CreatedBy).HasMaxLength(200).IsRequired();
            b.Property(x => x.Version).HasDefaultValue(1).IsConcurrencyToken();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.CreatedAt });
            b.Property(x => x.CreatedBy).IsRequired();
            b.Property(x => x.Reference).IsRequired();
            b.Property(x => x.State).IsRequired();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.WarehouseId });
            b.Property(x => x.Reference).HasMaxLength(100);
            b.Property(x => x.PlannedQuantity).HasPrecision(24, 6);
            b.Property(x => x.State).HasMaxLength(40);
            b.Property(x => x.ApprovedBy).HasMaxLength(200);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.Reference }).IsUnique().AreNullsDistinct(false);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.BusinessDate });
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.State, x.CreatedAt });
            b.HasOne<CoefficientVersion>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.CoefficientVersionId }).HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.CoefficientVersionId });
        });
        model.Entity<MaterialIssue>(b =>
        {
            b.ToTable("material_issues", "manufacturing", t =>
            {
                t.HasCheckConstraint("ck_material_issues_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                t.HasCheckConstraint("ck_material_issues_rules", "\"Quantity\" > 0");
            });
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.CreatedBy).HasMaxLength(200).IsRequired();
            b.Property(x => x.Version).HasDefaultValue(1).IsConcurrencyToken();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.CreatedAt });
            b.Property(x => x.CreatedBy).IsRequired();
            b.Property(x => x.SourceReference).IsRequired();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.ProductId });
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.WarehouseId });
            b.Property(x => x.Quantity).HasPrecision(24, 6);
            b.Property(x => x.SourceReference).HasMaxLength(100);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.SourceReference }).IsUnique().AreNullsDistinct(false);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.BusinessDate });
            b.HasOne<ProductionOrder>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.ProductionOrderId }).HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.ProductionOrderId });
        });
        model.Entity<ProductionReceipt>(b =>
        {
            b.ToTable("production_receipts", "manufacturing", t =>
            {
                t.HasCheckConstraint("ck_production_receipts_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                t.HasCheckConstraint("ck_production_receipts_rules", "\"Quantity\" > 0");
            });
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.CreatedBy).HasMaxLength(200).IsRequired();
            b.Property(x => x.Version).HasDefaultValue(1).IsConcurrencyToken();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.CreatedAt });
            b.Property(x => x.CreatedBy).IsRequired();
            b.Property(x => x.SourceReference).IsRequired();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.ProductId });
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.WarehouseId });
            b.Property(x => x.Quantity).HasPrecision(24, 6);
            b.Property(x => x.SourceReference).HasMaxLength(100);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.SourceReference }).IsUnique().AreNullsDistinct(false);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.BusinessDate });
            b.HasOne<ProductionOrder>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.ProductionOrderId }).HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.ProductionOrderId });
        });
        model.Entity<WastageRecord>(b =>
        {
            b.ToTable("wastage_records", "manufacturing", t =>
            {
                t.HasCheckConstraint("ck_wastage_records_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                t.HasCheckConstraint("ck_wastage_records_rules", "(\"Quantity\" > 0) AND (\"ApprovedBy\" IS NULL OR \"ApprovedBy\" <> \"CreatedBy\")");
            });
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.CreatedBy).HasMaxLength(200).IsRequired();
            b.Property(x => x.Version).HasDefaultValue(1).IsConcurrencyToken();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.CreatedAt });
            b.Property(x => x.CreatedBy).IsRequired();
            b.Property(x => x.Reason).IsRequired();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.ProductId });
            b.Property(x => x.Quantity).HasPrecision(24, 6);
            b.Property(x => x.Reason).HasMaxLength(1000);
            b.Property(x => x.ApprovedBy).HasMaxLength(200);
            b.HasOne<ProductionOrder>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.ProductionOrderId }).HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.ProductionOrderId });
        });
    }
}
