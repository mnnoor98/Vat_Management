using Microsoft.EntityFrameworkCore;
using Vat.Domain.Company;
using Vat.Domain.Costing;
using Vat.Domain.Sales;

namespace Vat.Infrastructure.Persistence;

internal static class CostSourceModel
{
    public static void Configure(ModelBuilder model)
    {
        model.Entity<CostSourceHead>(b =>
        {
            b.ToTable("cost_source_heads", "inventory", t => t.HasCheckConstraint("ck_cost_source_sequence", "\"Sequence\" >= 0"));
            b.HasKey(x => new { x.TenantId, x.CompanyId });
            b.Property(x => x.Sequence).IsConcurrencyToken();
            b.HasOne<Company>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<CostSourceEvent>(b =>
        {
            b.ToTable("cost_source_events", "inventory", t =>
            {
                t.HasCheckConstraint("ck_cost_source_kind", "\"Sequence\" > 0 AND ((\"ReceiptId\" IS NOT NULL AND \"SalesEntryId\" IS NULL AND \"Quantity\" > 0) OR (\"ReceiptId\" IS NULL AND \"SalesEntryId\" IS NOT NULL AND \"Quantity\" < 0))");
                t.HasCheckConstraint("ck_cost_source_policy", "(\"PolicyId\" IS NULL AND \"PolicyVersion\" IS NULL AND \"Method\" IS NULL) OR (\"PolicyId\" IS NOT NULL AND \"PolicyVersion\" IS NOT NULL AND \"Method\" IS NOT NULL AND \"PolicyVersion\" = 2 AND \"Method\" IN ('FIFO','WEIGHTED_AVERAGE'))");
            });
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Sequence }); b.Property(x => x.Sequence).ValueGeneratedNever();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.ReceiptId }).IsUnique().HasFilter("\"ReceiptId\" IS NOT NULL");
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.SalesEntryId }).IsUnique().HasFilter("\"SalesEntryId\" IS NOT NULL");
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.WarehouseId, x.ProductId, x.Sequence });
            b.Property(x => x.Quantity).HasPrecision(24, 6); b.Property(x => x.Method).HasMaxLength(30);
            b.HasOne<CostSourceHead>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<PurchaseCostReceipt>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.ReceiptId }).HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<SalesPostingEntry>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.SalesEntryId }).HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<CostingPolicy>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.PolicyId }).HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
