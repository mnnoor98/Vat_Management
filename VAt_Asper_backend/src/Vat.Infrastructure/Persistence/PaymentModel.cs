using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Vat.Domain.Sales;

namespace Vat.Infrastructure.Persistence;

internal static class PaymentModel
{
    public static void Configure(ModelBuilder model)
    {
        model.Entity<CustomerPayment>(b =>
        {
            b.ToTable("payments", "sales", t => t.HasCheckConstraint("ck_payment_amount", "\"Amount\" > 0 AND \"Amount\" <= 1000000000000000"));
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id }); b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Version).IsConcurrencyToken(); b.Property(x => x.Amount).HasPrecision(24, 4);
            b.Property(x => x.Reference).HasMaxLength(100); b.Property(x => x.Method).HasMaxLength(10);
            b.Property(x => x.EvidenceReference).HasMaxLength(1000); b.Property(x => x.Currency).HasMaxLength(3);
            b.Property(x => x.Status).HasMaxLength(20); b.Property(x => x.RequestedBy).HasMaxLength(200);
            b.Property(x => x.ApprovedBy).HasMaxLength(200); b.Property(x => x.CancellationReason).HasMaxLength(1000);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.Reference }).IsUnique();
            b.HasOne<Customer>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.CustomerId }).OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.RequestedAllocations).HasColumnType("jsonb").HasConversion(
                x => JsonSerializer.Serialize(x, (JsonSerializerOptions?)null),
                x => JsonSerializer.Deserialize<IReadOnlyList<PaymentAllocationInput>>(x, (JsonSerializerOptions?)null)!)
                .Metadata.SetValueComparer(new ValueComparer<IReadOnlyList<PaymentAllocationInput>>(
                    (a,b) => a != null && b != null && a.SequenceEqual(b),
                    x => x.Aggregate(0, (hash, line) => HashCode.Combine(hash, line.GetHashCode())), x => x.ToArray()));
        });
        model.Entity<PaymentAllocation>(b =>
        {
            b.ToTable("payment_allocations", "sales", t => t.HasCheckConstraint("ck_payment_allocation_amount", "\"Amount\" > 0 AND \"PaymentVersion\" >= 2"));
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id }); b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Amount).HasPrecision(24, 4); b.Property(x => x.AllocatedBy).HasMaxLength(200);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.PaymentId, x.PaymentVersion, x.InvoiceId }).IsUnique();
            b.HasOne<CustomerPayment>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.CustomerId, x.PaymentId })
                .HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.CustomerId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<SalesInvoice>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.InvoiceId }).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
