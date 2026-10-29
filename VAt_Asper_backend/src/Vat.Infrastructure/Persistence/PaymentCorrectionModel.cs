using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Vat.Domain.Sales;

namespace Vat.Infrastructure.Persistence;

internal static class PaymentCorrectionModel
{
    public static void Configure(ModelBuilder model)
    {
        model.Entity<PaymentCorrection>(b=>
        {
            b.ToTable("payment_corrections","sales",t=>
            {
                t.HasCheckConstraint("ck_payment_correction_amount","\"Amount\" > 0 AND \"Mode\" IN ('Reversal','Refund') AND \"Method\" IN ('Cash','Bank')");
                t.HasCheckConstraint("ck_payment_correction_checker","\"ApprovedBy\" IS NULL OR \"ApprovedBy\" <> \"RequestedBy\"");
            });
            b.HasKey(x=>new{x.TenantId,x.CompanyId,x.Id}); b.Property(x=>x.Id).ValueGeneratedNever();
            b.Property(x=>x.Version).IsConcurrencyToken(); b.Property(x=>x.Amount).HasPrecision(24,4);
            b.Property(x=>x.Mode).HasMaxLength(20); b.Property(x=>x.Status).HasMaxLength(20); b.Property(x=>x.Method).HasMaxLength(10);
            b.Property(x=>x.Reason).HasMaxLength(1000); b.Property(x=>x.EvidenceReference).HasMaxLength(1000); b.Property(x=>x.CancellationReason).HasMaxLength(1000);
            b.Property(x=>x.RequestedBy).HasMaxLength(200); b.Property(x=>x.ApprovedBy).HasMaxLength(200); b.Property(x=>x.DocumentNumber).HasMaxLength(100);
            b.HasIndex(x=>new{x.TenantId,x.CompanyId,x.PaymentId,x.SourceVersion}).IsUnique();
            b.HasIndex(x=>new{x.TenantId,x.CompanyId,x.PaymentId}).IsUnique().HasFilter("\"Status\" = 'Pending'");
            b.HasIndex(x=>new{x.TenantId,x.CompanyId,x.DocumentNumber}).IsUnique();
            b.HasOne<CustomerPayment>().WithMany().HasForeignKey(x=>new{x.TenantId,x.CompanyId,x.CustomerId,x.PaymentId})
                .HasPrincipalKey(x=>new{x.TenantId,x.CompanyId,x.CustomerId,x.Id}).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_correction_receipt");
            b.Property(x=>x.Releases).HasColumnType("jsonb").HasConversion(x=>JsonSerializer.Serialize(x,(JsonSerializerOptions?)null),x=>JsonSerializer.Deserialize<IReadOnlyList<FrozenPaymentRelease>>(x,(JsonSerializerOptions?)null)!)
                .Metadata.SetValueComparer(new ValueComparer<IReadOnlyList<FrozenPaymentRelease>>((a,b)=>a!=null && b!=null && a.SequenceEqual(b),x=>x.Aggregate(0,(h,l)=>HashCode.Combine(h,l.GetHashCode())),x=>x.ToArray()));
        });
        model.Entity<PaymentCashAdjustment>(b=>
        {
            b.ToTable("payment_cash_adjustments","sales",t=>t.HasCheckConstraint("ck_payment_cash_negative","\"Amount\" < 0"));
            b.HasKey(x=>new{x.TenantId,x.CompanyId,x.Id}); b.Property(x=>x.Id).ValueGeneratedNever(); b.Property(x=>x.Amount).HasPrecision(24,4);
            b.HasIndex(x=>new{x.TenantId,x.CompanyId,x.CorrectionId}).IsUnique();
            b.HasOne<PaymentCorrection>().WithMany().HasForeignKey(x=>new{x.TenantId,x.CompanyId,x.CustomerId,x.PaymentId,x.CorrectionId})
                .HasPrincipalKey(x=>new{x.TenantId,x.CompanyId,x.CustomerId,x.PaymentId,x.Id}).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_cash_correction");
        });
        model.Entity<PaymentAllocationRelease>(b=>
        {
            b.ToTable("payment_allocation_releases","sales",t=>t.HasCheckConstraint("ck_payment_release_positive","\"Amount\" > 0"));
            b.HasKey(x=>new{x.TenantId,x.CompanyId,x.Id}); b.Property(x=>x.Id).ValueGeneratedNever(); b.Property(x=>x.Amount).HasPrecision(24,4);
            b.HasIndex(x=>new{x.TenantId,x.CompanyId,x.CorrectionId,x.AllocationId}).IsUnique();
            b.HasOne<PaymentCorrection>().WithMany().HasForeignKey(x=>new{x.TenantId,x.CompanyId,x.CustomerId,x.PaymentId,x.CorrectionId})
                .HasPrincipalKey(x=>new{x.TenantId,x.CompanyId,x.CustomerId,x.PaymentId,x.Id}).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_release_correction");
            b.HasOne<PaymentAllocation>().WithMany().HasForeignKey(x=>new{x.TenantId,x.CompanyId,x.CustomerId,x.PaymentId,x.InvoiceId,x.AllocationId})
                .HasPrincipalKey(x=>new{x.TenantId,x.CompanyId,x.CustomerId,x.PaymentId,x.InvoiceId,x.Id}).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_release_original_allocation");
        });
    }
}
