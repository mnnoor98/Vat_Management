using Microsoft.EntityFrameworkCore;
using Vat.Domain.Company;
using Vat.Domain.Catalog;
using Vat.Domain.Costing;
using Vat.Domain.Posting;

namespace Vat.Infrastructure.Persistence;

internal static class CostingModel
{
    public static void Configure(ModelBuilder model)
    {
        model.Entity<CostingPolicy>(b =>
        {
            b.ToTable("costing_policies", "inventory", t =>
            {
                t.HasCheckConstraint("ck_cost_policy_method", "\"Method\" IN ('FIFO','WEIGHTED_AVERAGE')");
                t.HasCheckConstraint("ck_cost_policy_dates", "\"EffectiveFrom\" < \"EffectiveUntil\"");
                t.HasCheckConstraint("ck_cost_policy_state", "(\"State\" = 'Draft' AND \"Version\" = 1 AND \"ApprovedBy\" IS NULL AND \"ApprovedAt\" IS NULL) OR (\"State\" = 'Approved' AND \"Version\" = 2 AND \"ApprovedBy\" IS NOT NULL AND \"ApprovedBy\" <> \"CreatedBy\" AND \"ApprovedAt\" IS NOT NULL)");
            });
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Method).HasMaxLength(30); b.Property(x => x.State).HasMaxLength(20);
            b.Property(x => x.CreatedBy).HasMaxLength(200); b.Property(x => x.ApprovedBy).HasMaxLength(200);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.ProductId, x.EffectiveFrom });
            b.HasOne<Company>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Product>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.ProductId }).HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<PurchaseCostReceipt>(b =>
        {
            b.ToTable("purchase_cost_receipts", "inventory", t =>
            {
                t.HasCheckConstraint("ck_cost_receipt_amounts", "\"Quantity\" > 0 AND \"NetAmount\" >= 0 AND \"TaxAmount\" >= 0 AND \"RecoverableTaxAmount\" >= 0 AND \"RecoverableTaxAmount\" <= \"TaxAmount\"");
                t.HasCheckConstraint("ck_cost_receipt_policy", "(\"PolicyId\" IS NULL AND \"PolicyVersion\" IS NULL AND \"Method\" IS NULL) OR (\"PolicyId\" IS NOT NULL AND \"PolicyVersion\" IS NOT NULL AND \"Method\" IS NOT NULL AND \"PolicyVersion\" = 2 AND \"Method\" IN ('FIFO','WEIGHTED_AVERAGE'))");
            });
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id }); b.Property(x => x.Id).ValueGeneratedNever();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.SourceMovementId }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.ProductId, x.BusinessDate });
            b.Property(x => x.Method).HasMaxLength(30); b.Property(x => x.Currency).HasMaxLength(3);
            b.Property(x => x.Quantity).HasPrecision(24, 6);
            b.Property(x => x.NetAmount).HasPrecision(24, 4); b.Property(x => x.TaxAmount).HasPrecision(24, 4); b.Property(x => x.RecoverableTaxAmount).HasPrecision(24, 4);
            b.HasOne<CostingPolicy>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.PolicyId }).HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<StockMovement>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.SourceMovementId }).HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
