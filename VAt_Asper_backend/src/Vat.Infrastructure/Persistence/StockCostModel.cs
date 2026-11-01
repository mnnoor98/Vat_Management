using Microsoft.EntityFrameworkCore;
using Vat.Domain.Company;
using Vat.Domain.Posting;

namespace Vat.Infrastructure.Persistence;

public sealed class StockCostHead
{
    public Guid TenantId { get; set; } public Guid CompanyId { get; set; }
    public Guid AdoptionId { get; set; } public DateOnly OpeningDate { get; set; }
    public string Basis { get; set; } = "PurchaseNetOnly"; public long Sequence { get; set; }
}
public sealed class StockCostPosition
{
    public Guid TenantId { get; set; } public Guid CompanyId { get; set; }
    public Guid WarehouseId { get; set; } public Guid ProductId { get; set; }
    public string Method { get; set; } = "FIFO";
    public decimal Quantity { get; set; } public decimal Value { get; set; }
    public string Layers { get; set; } = "[]";
}
public sealed class StockCostEntry
{
    public Guid TenantId { get; set; } public Guid CompanyId { get; set; } public long Sequence { get; set; }
    public Guid WarehouseId { get; set; } public Guid ProductId { get; set; }
    public string Kind { get; set; } = ""; public Guid SourceId { get; set; } public Guid? OriginalSourceId { get; set; }
    public DateOnly BusinessDate { get; set; } public string Method { get; set; } = "";
    public decimal Quantity { get; set; } public decimal Value { get; set; }
    public string Allocations { get; set; } = "[]"; public DateTimeOffset CreatedAt { get; set; }
}
public sealed class StockCostAdoption
{
    public Guid TenantId { get; set; } public Guid CompanyId { get; set; } public Guid Id { get; set; }
    public DateOnly BusinessDate { get; set; } public string Basis { get; set; } = "PurchaseNetOnly";
    public string State { get; set; } = "Draft"; public int Version { get; set; } = 1;
    public string Evidence { get; set; } = ""; public string Snapshot { get; set; } = "";
    public string Lines { get; set; } = "[]"; public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } public string? ApprovedBy { get; set; } public DateTimeOffset? ApprovedAt { get; set; }
}
public sealed class StockLandedReview
{
    public Guid TenantId { get; set; } public Guid CompanyId { get; set; } public Guid Id { get; set; } public Guid PlanId { get; set; }
    public string State { get; set; } = "Draft"; public int Version { get; set; } = 1; public decimal TotalAmount { get; set; }
    public string Lines { get; set; } = "[]"; public string Evidence { get; set; } = ""; public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } public string? ApprovedBy { get; set; } public DateTimeOffset? ApprovedAt { get; set; }
}
internal static class StockCostModel
{
    public static void Configure(ModelBuilder model)
    {
        model.Entity<StockCostAdoption>(b => {
            b.ToTable("cost_adoptions", "inventory", t => {
                t.HasCheckConstraint("ck_cost_adoption_state", "(\"State\"='Draft' AND \"Version\"=1 AND \"ApprovedBy\" IS NULL AND \"ApprovedAt\" IS NULL) OR (\"State\"='Approved' AND \"Version\"=2 AND \"ApprovedBy\" IS NOT NULL AND \"ApprovedAt\" IS NOT NULL AND \"ApprovedBy\"<>\"CreatedBy\")");
                t.HasCheckConstraint("ck_cost_adoption_basis", "\"Basis\" IN ('PurchaseNetOnly','NetPlusNonrecoverableTaxAndLanded')");
            });
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id }); b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Version).IsConcurrencyToken(); b.Property(x => x.Lines).HasColumnType("jsonb");
            b.Property(x => x.Evidence).HasMaxLength(1000); b.Property(x => x.Snapshot).HasMaxLength(64);
            b.Property(x => x.CreatedBy).HasMaxLength(200); b.Property(x => x.ApprovedBy).HasMaxLength(200);
            b.HasOne<Company>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<StockCostHead>(b => {
            b.ToTable("cost_ledger_heads", "inventory", t => t.HasCheckConstraint("ck_cost_ledger_sequence", "\"Sequence\">=0"));
            b.HasKey(x => new { x.TenantId, x.CompanyId }); b.Property(x => x.Sequence).IsConcurrencyToken();
            b.HasOne<StockCostAdoption>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, Id = x.AdoptionId }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<StockCostPosition>(b => {
            b.ToTable("cost_positions", "inventory", t => {
                t.HasCheckConstraint("ck_cost_position_value", "\"Quantity\">=0 AND \"Value\">=0 AND (\"Quantity\">0 OR \"Value\"=0)");
                t.HasCheckConstraint("ck_cost_position_method", "\"Method\" IN ('FIFO','WEIGHTED_AVERAGE')");
            });
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.WarehouseId, x.ProductId });
            b.Property(x => x.Quantity).HasPrecision(24,6); b.Property(x => x.Value).HasPrecision(24,4); b.Property(x => x.Layers).HasColumnType("jsonb");
            b.HasOne<StockCostHead>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<StockCostEntry>(b => {
            b.ToTable("cost_entries", "inventory", t => {
                t.HasCheckConstraint("ck_cost_entry_kind", "\"Kind\" IN ('Opening','Receipt','Issue','Return','Reversal','TransferOut','TransferIn','ProductionIssue','ProductionReturn','ProductionReturnReversal','ProductionReceipt','ProductionReceiptReversal')");
                t.HasCheckConstraint("ck_cost_entry_sign", "(\"Kind\" IN ('Opening','Receipt','Return','TransferIn','ProductionReturn','ProductionReceipt') AND \"Quantity\">0 AND \"Value\">=0) OR (\"Kind\" IN ('Issue','Reversal','TransferOut','ProductionIssue','ProductionReturnReversal','ProductionReceiptReversal') AND \"Quantity\"<0 AND \"Value\"<=0)");
            });
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Sequence }); b.Property(x => x.Sequence).ValueGeneratedNever();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.Kind, x.SourceId, x.WarehouseId, x.ProductId }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.WarehouseId, x.ProductId, x.Sequence });
            b.Property(x => x.Quantity).HasPrecision(24,6); b.Property(x => x.Value).HasPrecision(24,4); b.Property(x => x.Allocations).HasColumnType("jsonb");
            b.HasOne<StockCostHead>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<StockLandedReview>(b => {
            b.ToTable("cost_landed_reviews", "inventory", t => {
                t.HasCheckConstraint("ck_landed_state", "(\"State\"='Draft' AND \"Version\"=1 AND \"ApprovedBy\" IS NULL AND \"ApprovedAt\" IS NULL) OR (\"State\"='Approved' AND \"Version\"=2 AND \"ApprovedBy\" IS NOT NULL AND \"ApprovedAt\" IS NOT NULL AND \"ApprovedBy\"<>\"CreatedBy\")");
                t.HasCheckConstraint("ck_landed_amount", "\"TotalAmount\">=0");
            });
            b.HasKey(x => new { x.TenantId,x.CompanyId,x.Id }); b.Property(x => x.Id).ValueGeneratedNever(); b.Property(x => x.Version).IsConcurrencyToken();
            b.Property(x => x.TotalAmount).HasPrecision(24,4); b.Property(x => x.Lines).HasColumnType("jsonb"); b.Property(x => x.Evidence).HasMaxLength(1000);
            b.Property(x => x.CreatedBy).HasMaxLength(200); b.Property(x => x.ApprovedBy).HasMaxLength(200);
            b.HasIndex(x => new { x.TenantId,x.CompanyId,x.PlanId }).IsUnique().HasFilter("\"State\"='Approved'");
            b.HasOne<PurchasePostingPlan>().WithMany().HasForeignKey(x => new { x.TenantId,x.CompanyId,x.PlanId }).HasPrincipalKey(x => new { x.TenantId,x.CompanyId,x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
