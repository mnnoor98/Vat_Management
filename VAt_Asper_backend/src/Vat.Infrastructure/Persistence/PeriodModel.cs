using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vat.Domain.Company;
using Vat.Domain.Posting;
using Vat.Domain.Purchasing;

namespace Vat.Infrastructure.Persistence;

internal static class PeriodModel
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static void Configure(ModelBuilder model)
    {
        model.Entity<Purchase>().Property(x => x.CancellationReason).HasMaxLength(1000);
        model.Entity<FiscalPeriod>(b =>
        {
            b.ToTable("fiscal_periods", "company", t => t.HasCheckConstraint("ck_period_interval", "\"Until\" > \"From\""));
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Name).HasMaxLength(100);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.Property(x => x.CloseRequestedBy).HasMaxLength(200);
            b.Property(x => x.ClosedBy).HasMaxLength(200);
            b.Property(x => x.Snapshot).HasConversion(
                snapshot => JsonSerializer.Serialize(snapshot, Json),
                json => JsonSerializer.Deserialize<PeriodCloseSnapshot>(json, Json)).HasColumnType("jsonb");
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.From });
            b.HasOne<Company>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<PurchaseReversal>(b =>
        {
            b.ToTable("purchase_reversals", "purchasing");
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.Property(x => x.Reason).HasMaxLength(1000);
            b.Property(x => x.RequestedBy).HasMaxLength(200);
            b.Property(x => x.ApprovedBy).HasMaxLength(200);
            b.Property(x => x.CancellationReason).HasMaxLength(1000);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.PurchaseId, x.SourceVersion }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.PurchaseId }).IsUnique().HasFilter("\"Status\" = 'Pending'");
            b.HasOne<Purchase>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.PurchaseId })
                .HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<PurchasePostingPlan>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.PlanId })
                .HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<StockMovement>(b =>
        {
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.ReversesEntryId }).IsUnique().HasFilter("\"ReversesEntryId\" IS NOT NULL");
            b.HasOne<StockMovement>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.ReversesEntryId })
                .HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<PurchaseReversal>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.ReversalId })
                .HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<TaxLedgerEntry>(b =>
        {
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.ReversesEntryId }).IsUnique().HasFilter("\"ReversesEntryId\" IS NOT NULL");
            b.HasOne<TaxLedgerEntry>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.ReversesEntryId })
                .HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<PurchaseReversal>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.ReversalId })
                .HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
    }
}

