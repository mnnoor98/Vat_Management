using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vat.Domain.Catalog;
using Vat.Domain.Company;
using Vat.Domain.Posting;
using Vat.Domain.Purchasing;
using Vat.Domain.Tax;

namespace Vat.Infrastructure.Persistence;

internal static class PostingModel
{
    private static readonly JsonSerializerOptions SnapshotJson = new(JsonSerializerDefaults.Web);

    public static void Configure(ModelBuilder model)
    {
        model.Entity<Product>(b =>
        {
            b.ToTable("products", "masterdata");
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Reference).HasMaxLength(100);
            b.Property(x => x.Description).HasMaxLength(500);
            b.Property(x => x.UnitOfMeasure).HasMaxLength(30);
            b.Property(x => x.TaxCode).HasMaxLength(100);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.Reference }).IsUnique();
            b.HasOne<Company>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<Supplier>(b =>
        {
            b.ToTable("suppliers", "masterdata");
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Reference).HasMaxLength(100);
            b.Property(x => x.Name).HasMaxLength(200);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.Reference }).IsUnique();
            b.HasOne<Company>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<Warehouse>(b =>
        {
            b.ToTable("warehouses", "inventory");
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Code).HasMaxLength(100);
            b.Property(x => x.Name).HasMaxLength(200);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.Code }).IsUnique();
            b.HasOne<Branch>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.BranchId })
                .HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<TaxRule>(b =>
        {
            b.ToTable("tax_rule_versions", "tax", t =>
            {
                t.HasCheckConstraint("ck_rule_treatment", "\"Treatment\" IN ('Purchase', 'Sale') AND (\"Treatment\" <> 'Sale' OR \"RecoverablePercent\" = 0)");
                t.HasCheckConstraint("ck_rule_interval", "\"EffectiveUntil\" > \"EffectiveFrom\"");
                t.HasCheckConstraint("ck_rule_percentages", "\"RatePercent\" BETWEEN 0 AND 100 AND \"RecoverablePercent\" BETWEEN 0 AND 100");
                t.HasCheckConstraint("ck_rule_maker_checker", "\"PublishedBy\" IS NULL OR \"PublishedBy\" <> \"CreatedBy\"");
            });
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.TaxCode).HasMaxLength(100);
            b.Property(x => x.Treatment).HasMaxLength(20).HasDefaultValue("Purchase");
            b.Property(x => x.RatePercent).HasPrecision(7, 4);
            b.Property(x => x.RecoverablePercent).HasPrecision(7, 4);
            b.Property(x => x.LegalBasis).HasMaxLength(2000);
            b.Property(x => x.EvidenceReference).HasMaxLength(1000);
            b.Property(x => x.CreatedBy).HasMaxLength(200);
            b.Property(x => x.PublishedBy).HasMaxLength(200);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.TaxCode, x.EffectiveFrom });
            b.HasOne<Company>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<PurchasePostingPlan>(b =>
        {
            b.ToTable("purchase_posting_plans", "purchasing");
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.Property(x => x.PreparedBy).HasMaxLength(200);
            b.Property(x => x.PostedBy).HasMaxLength(200);
            b.Property(x => x.CancellationReason).HasMaxLength(1000);
            b.Property(x => x.Snapshot).HasConversion(
                snapshot => JsonSerializer.Serialize(snapshot, SnapshotJson),
                json => JsonSerializer.Deserialize<CalculationSnapshot>(json, SnapshotJson)!).HasColumnType("jsonb");
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.PurchaseId, x.SourceVersion }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.PurchaseId }).IsUnique().HasFilter("\"Status\" = 'Prepared'");
            b.HasOne<Purchase>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.PurchaseId })
                .HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Warehouse>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.WarehouseId })
                .HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<StockMovement>(b =>
        {
            b.ToTable("stock_movements", "inventory", t => t.HasCheckConstraint("ck_receipt_positive",
                "(\"ReversesEntryId\" IS NULL AND \"ReversalId\" IS NULL AND \"Quantity\" > 0) OR (\"ReversesEntryId\" IS NOT NULL AND \"ReversalId\" IS NOT NULL AND \"Quantity\" < 0)"));
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Quantity).HasPrecision(24, 6);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.PurchaseId, x.PurchaseLineId }).IsUnique().HasFilter("\"ReversesEntryId\" IS NULL");
            b.HasOne<PurchasePostingPlan>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.PlanId })
                .HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<PurchaseLine>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.PurchaseId, x.PurchaseLineId })
                .HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.PurchaseId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Warehouse>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.WarehouseId })
                .HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Product>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.ProductId })
                .HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<StockBalance>(b =>
        {
            b.ToTable("stock_balances", "inventory", t => { t.HasCheckConstraint("ck_balance_nonnegative", "\"Quantity\" >= 0"); t.HasCheckConstraint("ck_balance_reserved", "\"ReservedQuantity\" >= 0 AND \"ReservedQuantity\" <= \"Quantity\""); });
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.WarehouseId, x.ProductId });
            b.Property(x => x.ReservedQuantity).HasPrecision(24, 6).HasDefaultValue(0m);
            b.Ignore(x => x.AvailableQuantity);
            b.Property(x => x.Quantity).HasPrecision(24, 6);
            b.HasOne<Warehouse>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.WarehouseId })
                .HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Product>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.ProductId })
                .HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<TaxLedgerEntry>(b =>
        {
            b.ToTable("tax_ledger", "tax", t => t.HasCheckConstraint("ck_input_tax_amounts",
                "(\"ReversesEntryId\" IS NULL AND \"ReversalId\" IS NULL AND \"NetAmount\" >= 0 AND \"TaxAmount\" >= 0 AND \"RecoverableTaxAmount\" BETWEEN 0 AND \"TaxAmount\") OR " +
                "(\"ReversesEntryId\" IS NOT NULL AND \"ReversalId\" IS NOT NULL AND \"NetAmount\" <= 0 AND \"TaxAmount\" <= 0 AND \"RecoverableTaxAmount\" BETWEEN \"TaxAmount\" AND 0)"));
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.NetAmount).HasPrecision(24, 4);
            b.Property(x => x.TaxAmount).HasPrecision(24, 4);
            b.Property(x => x.RecoverableTaxAmount).HasPrecision(24, 4);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.PurchaseId, x.PurchaseLineId }).IsUnique().HasFilter("\"ReversesEntryId\" IS NULL");
            b.HasOne<PurchasePostingPlan>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.PlanId })
                .HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<PurchaseLine>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.PurchaseId, x.PurchaseLineId })
                .HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.PurchaseId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<TaxRule>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.TaxRuleId })
                .HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
