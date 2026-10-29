using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Vat.Domain.Catalog;
using Vat.Domain.Sales;
using Vat.Domain.Tax;

namespace Vat.Infrastructure.Persistence;

internal static class SalesPostingModel
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static void Configure(ModelBuilder model)
    {
        model.Entity<SalesFulfillment>(b =>
        {
            b.ToTable("fulfillments", "sales");
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.InvoiceId });
            b.Property(x => x.InvoiceId).ValueGeneratedNever();
            b.Property(x => x.Status).HasMaxLength(20);
            b.Property(x => x.ReservedBy).HasMaxLength(200);
            b.Property(x => x.PostedBy).HasMaxLength(200);
            b.Property(x => x.DocumentNumber).HasMaxLength(100);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.DocumentNumber }).IsUnique();
            b.Property(x => x.Version).IsConcurrencyToken();
            b.Property(x => x.Snapshot).HasConversion(value => JsonSerializer.Serialize(value, Json),
                value => JsonSerializer.Deserialize<SalesCalculationSnapshot>(value, Json)!).HasColumnType("jsonb");
            b.HasOne<SalesInvoice>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.InvoiceId })
                .HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Warehouse>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.WarehouseId })
                .HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<SalesPostingEntry>(b =>
        {
            b.ToTable("posting_entries", "sales", t => t.HasCheckConstraint("ck_sales_issue_amounts",
                "(\"CreditNoteId\" IS NULL AND \"ReversesEntryId\" IS NULL AND \"DebitNoteId\" IS NULL AND \"AdjustsEntryId\" IS NULL AND \"EntryType\" = 'Goods' AND \"Quantity\" < 0 AND \"NetAmount\" >= 0 AND \"OutputTaxAmount\" >= 0) OR " +
                "(\"DebitNoteId\" IS NULL AND \"AdjustsEntryId\" IS NULL AND \"CreditNoteId\" IS NOT NULL AND \"ReversesEntryId\" IS NOT NULL AND ((\"EntryType\" = 'Goods' AND \"Quantity\" > 0) OR (\"EntryType\" = 'PriceCredit' AND \"Quantity\" = 0 AND \"NetAmount\" < 0)) AND \"NetAmount\" <= 0 AND \"OutputTaxAmount\" <= 0) OR " +
                "(\"CreditNoteId\" IS NULL AND \"ReversesEntryId\" IS NULL AND \"DebitNoteId\" IS NOT NULL AND \"AdjustsEntryId\" IS NOT NULL AND \"EntryType\" = 'PriceDebit' AND \"Quantity\" = 0 AND \"NetAmount\" > 0 AND \"OutputTaxAmount\" >= 0)"));
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Ignore(x => x.IsOriginal);
            b.Ignore(x => x.SourceEntryId);
            b.Ignore(x => x.AdjustmentId);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.DebitNoteId, x.AdjustsEntryId }).IsUnique().HasFilter("\"AdjustsEntryId\" IS NOT NULL");
            b.HasOne<SalesPostingEntry>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.InvoiceId, x.AdjustsEntryId })
                .HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.InvoiceId, x.Id }).HasConstraintName("fk_sales_entries_debit_original").OnDelete(DeleteBehavior.Restrict);
            b.HasOne<SalesDebitNote>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.InvoiceId, x.DebitNoteId })
                .HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.InvoiceId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.EntryType).HasMaxLength(20).HasDefaultValue("Goods");
            b.Property(x => x.Quantity).HasPrecision(24, 6);
            b.Property(x => x.NetAmount).HasPrecision(24, 4);
            b.Property(x => x.OutputTaxAmount).HasPrecision(24, 4);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.InvoiceId, x.LineId }).IsUnique().HasFilter("\"CreditNoteId\" IS NULL AND \"DebitNoteId\" IS NULL");
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.CreditNoteId, x.ReversesEntryId }).IsUnique().HasFilter("\"ReversesEntryId\" IS NOT NULL");
            b.HasOne<SalesPostingEntry>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.InvoiceId, x.ReversesEntryId })
                .HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.InvoiceId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<SalesCreditNote>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.InvoiceId, x.CreditNoteId })
                .HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.InvoiceId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.BusinessDate });
            b.HasOne<SalesFulfillment>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.InvoiceId }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<SalesInvoiceLine>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.InvoiceId, x.LineId })
                .HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.SalesInvoiceId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Warehouse>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.WarehouseId })
                .HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Product>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.ProductId })
                .HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<TaxRule>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.TaxRuleId })
                .HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<SalesCreditNote>(b =>
        {
            b.ToTable("credit_notes", "sales", t =>
            {
                t.HasCheckConstraint("ck_credit_amounts", "\"NetAmount\" <= 0 AND \"OutputTaxAmount\" <= 0");
                t.HasCheckConstraint("ck_credit_maker_checker", "\"ApprovedBy\" IS NULL OR \"ApprovedBy\" <> \"RequestedBy\"");
            });
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Version).IsConcurrencyToken();
            b.Property(x => x.Reason).HasMaxLength(1000);
            b.Property(x => x.ReturnReceiptReference).HasMaxLength(200);
            b.Property(x => x.AdjustmentEvidenceReference).HasMaxLength(1000);
            b.Property(x => x.RequestedBy).HasMaxLength(200);
            b.Property(x => x.ApprovedBy).HasMaxLength(200);
            b.Property(x => x.Status).HasMaxLength(20);
            b.Property(x => x.Mode).HasMaxLength(30).HasDefaultValue("FullInvoice");
            b.Property(x => x.Lines).HasConversion(value => JsonSerializer.Serialize(value, Json),
                value => JsonSerializer.Deserialize<IReadOnlyList<SalesReturnAllocation>>(value, Json)!).HasColumnType("jsonb").HasDefaultValueSql("'[]'::jsonb")
                .Metadata.SetValueComparer(new ValueComparer<IReadOnlyList<SalesReturnAllocation>>(
                    (left, right) => left == right || (left != null && right != null && left.SequenceEqual(right)),
                    value => value.Aggregate(0, (hash, item) => HashCode.Combine(hash, item)), value => value.ToArray()));
            b.Property(x => x.OriginalDocumentNumber).HasMaxLength(100);
            b.Property(x => x.DocumentNumber).HasMaxLength(100);
            b.Property(x => x.CancellationReason).HasMaxLength(1000);
            b.Property(x => x.NetAmount).HasPrecision(24, 4);
            b.Property(x => x.OutputTaxAmount).HasPrecision(24, 4);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.InvoiceId, x.SourceVersion }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.InvoiceId }).IsUnique().HasDatabaseName("ux_sales_pending_credit").HasFilter("\"Status\" = 'Pending'");
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.DocumentNumber }).IsUnique();
            b.HasOne<SalesInvoice>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.InvoiceId })
                .HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<SalesDebitNote>(b =>
        {
            b.ToTable("debit_notes", "sales", t =>
            {
                t.HasCheckConstraint("ck_debit_amounts", "\"NetAmount\" > 0 AND \"OutputTaxAmount\" >= 0");
                t.HasCheckConstraint("ck_debit_maker_checker", "\"ApprovedBy\" IS NULL OR \"ApprovedBy\" <> \"RequestedBy\"");
            });
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Version).IsConcurrencyToken();
            b.Property(x => x.Reason).HasMaxLength(1000);
            b.Property(x => x.EvidenceReference).HasMaxLength(1000);
            b.Property(x => x.RequestedBy).HasMaxLength(200);
            b.Property(x => x.ApprovedBy).HasMaxLength(200);
            b.Property(x => x.Status).HasMaxLength(20);
            b.Property(x => x.OriginalDocumentNumber).HasMaxLength(100);
            b.Property(x => x.DocumentNumber).HasMaxLength(100);
            b.Property(x => x.CancellationReason).HasMaxLength(1000);
            b.Property(x => x.NetAmount).HasPrecision(24, 4);
            b.Property(x => x.OutputTaxAmount).HasPrecision(24, 4);
            b.Property(x => x.Lines).HasConversion(value => JsonSerializer.Serialize(value, Json),
                value => JsonSerializer.Deserialize<IReadOnlyList<SalesDebitAllocation>>(value, Json)!).HasColumnType("jsonb")
                .Metadata.SetValueComparer(new ValueComparer<IReadOnlyList<SalesDebitAllocation>>(
                    (left, right) => left == right || (left != null && right != null && left.SequenceEqual(right)),
                    value => value.Aggregate(0, (hash, item) => HashCode.Combine(hash, item)), value => value.ToArray()));
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.InvoiceId, x.SourceVersion }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.InvoiceId }).IsUnique().HasFilter("\"Status\" = 'Pending'");
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.DocumentNumber }).IsUnique();
            b.HasOne<SalesInvoice>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.InvoiceId })
                .HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
