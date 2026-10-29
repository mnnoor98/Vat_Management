using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Vat.Domain.Company;
using Vat.Domain.Sales;

namespace Vat.Infrastructure.Persistence;

internal static class SalesModel
{
    internal static void Configure(ModelBuilder model)
    {
        model.Entity<Customer>(b =>
        {
            b.ToTable("customers", "sales");
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Code).HasMaxLength(100);
            b.Property(x => x.Name).HasMaxLength(200);
            b.Property(x => x.Contact).HasMaxLength(300);
            b.Property(x => x.Address).HasMaxLength(1000);
            b.Property(x => x.TaxRegistrationReference).HasMaxLength(100);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.Code }).IsUnique();
            b.HasOne<Company>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<SalesInvoice>(b =>
        {
            b.ToTable("sales_invoices", "sales", t =>
            {
                t.HasCheckConstraint("ck_sales_invoice_net", "\"NetAmount\" >= 0");
                t.HasCheckConstraint("ck_sales_invoice_due_date", "\"DueDate\" IS NULL OR \"DueDate\" >= \"BusinessDate\"");
            });
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.ExternalReference).HasMaxLength(100);
            b.Property(x => x.CustomerReference).HasMaxLength(100);
            b.Property(x => x.CustomerSnapshot).HasColumnType("jsonb").HasConversion(
                x => JsonSerializer.Serialize(x, (JsonSerializerOptions?)null),
                x => JsonSerializer.Deserialize<CustomerSnapshot>(x, (JsonSerializerOptions?)null));
            b.HasOne<Customer>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.CustomerId })
                .HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.Currency).HasMaxLength(3);
            b.Property(x => x.CreatedBy).HasMaxLength(200);
            b.Property(x => x.ApprovedBy).HasMaxLength(200);
            b.Property(x => x.RejectionReason).HasMaxLength(1000);
            b.Property(x => x.CancellationReason).HasMaxLength(1000);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.Property(x => x.NetAmount).HasPrecision(24, 4);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.ExternalReference }).IsUnique();
            b.HasOne<Branch>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.BranchId })
                .HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasMany(x => x.Lines).WithOne().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.SalesInvoiceId })
                .HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
        model.Entity<SalesInvoiceLine>(b =>
        {
            b.ToTable("sales_invoice_lines", "sales", t =>
            {
                t.HasCheckConstraint("ck_line_quantity", "\"Quantity\" > 0 AND \"Quantity\" <= 1000000");
                t.HasCheckConstraint("ck_line_price", "\"UnitPrice\" >= 0 AND \"UnitPrice\" <= 1000000000");
            });
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.SalesInvoiceId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.ProductReference).HasMaxLength(100);
            b.Property(x => x.Description).HasMaxLength(500);
            b.Property(x => x.Quantity).HasPrecision(18, 6);
            b.Property(x => x.UnitPrice).HasPrecision(20, 4);
            b.Property(x => x.NetAmount).HasPrecision(24, 4);
        });
    }
}
