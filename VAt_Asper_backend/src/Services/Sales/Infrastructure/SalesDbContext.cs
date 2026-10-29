using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Vat.Domain.Catalog;
using Vat.Domain.Sales;

namespace Vat.Services.Sales.Infrastructure;

public sealed class SalesAudit
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid ResourceId { get; set; }
    public string Actor { get; set; } = "";
    public string Action { get; set; } = "";
    public string? Reason { get; set; }
    public string CorrelationId { get; set; } = "";
    public long? Version { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}

public sealed class SalesCommand
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public string Operation { get; set; } = "";
    public string Key { get; set; } = "";
    public string Actor { get; set; } = "";
    public string RequestHash { get; set; } = "";
    public string ResponseJson { get; set; } = "";
    public int StatusCode { get; set; }
    public string? Location { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class SalesOutbox
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid AggregateId { get; set; }
    public long AggregateVersion { get; set; }
    public string EventType { get; set; } = "";
    public string CorrelationId { get; set; } = "";
    public string Payload { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DispatchedAt { get; set; }
}

public sealed class SalesDbContext(DbContextOptions<SalesDbContext> options) : DbContext(options)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public DbSet<SalesInvoice> Invoices => Set<SalesInvoice>();
    public DbSet<SalesInvoice> SalesInvoices => Invoices;
    public DbSet<SalesInvoiceLine> InvoiceLines => Set<SalesInvoiceLine>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<SalesFulfillment> Fulfillments => Set<SalesFulfillment>();
    public DbSet<SalesPostingEntry> PostingEntries => Set<SalesPostingEntry>();
    public DbSet<SalesCreditNote> CreditNotes => Set<SalesCreditNote>();
    public DbSet<SalesDebitNote> DebitNotes => Set<SalesDebitNote>();
    public DbSet<CustomerPayment> Payments => Set<CustomerPayment>();
    public DbSet<PaymentAllocation> PaymentAllocations => Set<PaymentAllocation>();
    public DbSet<PaymentCorrection> PaymentCorrections => Set<PaymentCorrection>();
    public DbSet<PaymentCashAdjustment> PaymentCashAdjustments => Set<PaymentCashAdjustment>();
    public DbSet<PaymentAllocationRelease> PaymentAllocationReleases => Set<PaymentAllocationRelease>();
    public DbSet<SalesAudit> Audits => Set<SalesAudit>();
    public DbSet<SalesCommand> Commands => Set<SalesCommand>();
    public DbSet<SalesOutbox> Outbox => Set<SalesOutbox>();

    protected override void OnModelCreating(ModelBuilder model)
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
                x => JsonSerializer.Serialize(x, Json),
                x => JsonSerializer.Deserialize<CustomerSnapshot>(x, Json));
            b.Property(x => x.Currency).HasMaxLength(3);
            b.Property(x => x.CreatedBy).HasMaxLength(200);
            b.Property(x => x.ApprovedBy).HasMaxLength(200);
            b.Property(x => x.RejectionReason).HasMaxLength(1000);
            b.Property(x => x.CancellationReason).HasMaxLength(1000);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.Property(x => x.NetAmount).HasPrecision(24, 4);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.ExternalReference }).IsUnique();
            b.HasMany(x => x.Lines).WithOne().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.SalesInvoiceId })
                .HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        model.Entity<SalesInvoiceLine>(b =>
        {
            b.ToTable("sales_invoice_lines", "sales", t =>
            {
                t.HasCheckConstraint("ck_sales_line_quantity", "\"Quantity\" > 0 AND \"Quantity\" <= 1000000");
                t.HasCheckConstraint("ck_sales_line_price", "\"UnitPrice\" >= 0 AND \"UnitPrice\" <= 1000000000");
            });
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.SalesInvoiceId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.ProductReference).HasMaxLength(100);
            b.Property(x => x.Description).HasMaxLength(500);
            b.Property(x => x.Quantity).HasPrecision(18, 6);
            b.Property(x => x.UnitPrice).HasPrecision(20, 4);
            b.Property(x => x.NetAmount).HasPrecision(24, 4);
        });

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
            b.Property(x => x.Snapshot).HasConversion(
                value => JsonSerializer.Serialize(value, Json),
                value => JsonSerializer.Deserialize<SalesCalculationSnapshot>(value, Json)!).HasColumnType("jsonb");
            b.HasOne<SalesInvoice>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.InvoiceId })
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
            b.Property(x => x.EntryType).HasMaxLength(20).HasDefaultValue("Goods");
            b.Property(x => x.Quantity).HasPrecision(24, 6);
            b.Property(x => x.NetAmount).HasPrecision(24, 4);
            b.Property(x => x.OutputTaxAmount).HasPrecision(24, 4);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.InvoiceId, x.LineId }).IsUnique().HasFilter("\"CreditNoteId\" IS NULL AND \"DebitNoteId\" IS NULL");
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.CreditNoteId, x.ReversesEntryId }).IsUnique().HasFilter("\"ReversesEntryId\" IS NOT NULL");
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.BusinessDate });
            b.HasOne<SalesFulfillment>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.InvoiceId }).OnDelete(DeleteBehavior.Restrict);
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

        model.Entity<CustomerPayment>(b =>
        {
            b.ToTable("payments", "sales", t => t.HasCheckConstraint("ck_payment_amount", "\"Amount\" > 0 AND \"Amount\" <= 1000000000000000"));
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Version).IsConcurrencyToken();
            b.Property(x => x.Amount).HasPrecision(24, 4);
            b.Property(x => x.Reference).HasMaxLength(100);
            b.Property(x => x.Method).HasMaxLength(10);
            b.Property(x => x.EvidenceReference).HasMaxLength(1000);
            b.Property(x => x.Currency).HasMaxLength(3);
            b.Property(x => x.Status).HasMaxLength(20);
            b.Property(x => x.RequestedBy).HasMaxLength(200);
            b.Property(x => x.ApprovedBy).HasMaxLength(200);
            b.Property(x => x.CancellationReason).HasMaxLength(1000);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.Reference }).IsUnique();
            b.Property(x => x.RequestedAllocations).HasColumnType("jsonb").HasConversion(
                x => JsonSerializer.Serialize(x, Json),
                x => JsonSerializer.Deserialize<IReadOnlyList<PaymentAllocationInput>>(x, Json)!)
                .Metadata.SetValueComparer(new ValueComparer<IReadOnlyList<PaymentAllocationInput>>(
                    (a, b) => a != null && b != null && a.SequenceEqual(b),
                    x => x.Aggregate(0, (hash, line) => HashCode.Combine(hash, line.GetHashCode())), x => x.ToArray()));
        });

        model.Entity<PaymentAllocation>(b =>
        {
            b.ToTable("payment_allocations", "sales", t => t.HasCheckConstraint("ck_payment_allocation_amount", "\"Amount\" > 0 AND \"PaymentVersion\" >= 2"));
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Amount).HasPrecision(24, 4);
            b.Property(x => x.AllocatedBy).HasMaxLength(200);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.PaymentId, x.PaymentVersion, x.InvoiceId }).IsUnique();
            b.HasOne<SalesInvoice>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.InvoiceId }).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<PaymentCorrection>(b =>
        {
            b.ToTable("payment_corrections", "sales", t =>
            {
                t.HasCheckConstraint("ck_payment_correction_amount", "\"Amount\" > 0 AND \"Mode\" IN ('Reversal','Refund') AND \"Method\" IN ('Cash','Bank')");
                t.HasCheckConstraint("ck_payment_correction_checker", "\"ApprovedBy\" IS NULL OR \"ApprovedBy\" <> \"RequestedBy\"");
            });
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Version).IsConcurrencyToken();
            b.Property(x => x.Amount).HasPrecision(24, 4);
            b.Property(x => x.Mode).HasMaxLength(20);
            b.Property(x => x.Status).HasMaxLength(20);
            b.Property(x => x.Method).HasMaxLength(10);
            b.Property(x => x.Reason).HasMaxLength(1000);
            b.Property(x => x.EvidenceReference).HasMaxLength(1000);
            b.Property(x => x.CancellationReason).HasMaxLength(1000);
            b.Property(x => x.RequestedBy).HasMaxLength(200);
            b.Property(x => x.ApprovedBy).HasMaxLength(200);
            b.Property(x => x.DocumentNumber).HasMaxLength(100);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.PaymentId, x.SourceVersion }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.PaymentId }).IsUnique().HasFilter("\"Status\" = 'Pending'");
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.DocumentNumber }).IsUnique();
            b.Property(x => x.Releases).HasColumnType("jsonb").HasConversion(
                x => JsonSerializer.Serialize(x, Json),
                x => JsonSerializer.Deserialize<IReadOnlyList<FrozenPaymentRelease>>(x, Json)!)
                .Metadata.SetValueComparer(new ValueComparer<IReadOnlyList<FrozenPaymentRelease>>(
                    (a, b) => a != null && b != null && a.SequenceEqual(b),
                    x => x.Aggregate(0, (h, l) => HashCode.Combine(h, l.GetHashCode())), x => x.ToArray()));
        });

        model.Entity<PaymentCashAdjustment>(b =>
        {
            b.ToTable("payment_cash_adjustments", "sales", t => t.HasCheckConstraint("ck_payment_cash_negative", "\"Amount\" < 0"));
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Amount).HasPrecision(24, 4);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.CorrectionId }).IsUnique();
        });

        model.Entity<PaymentAllocationRelease>(b =>
        {
            b.ToTable("payment_allocation_releases", "sales", t => t.HasCheckConstraint("ck_payment_release_positive", "\"Amount\" > 0"));
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Amount).HasPrecision(24, 4);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.CorrectionId, x.AllocationId }).IsUnique();
        });

        model.Entity<SalesAudit>(b =>
        {
            b.ToTable("sales_audit", "sales");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Actor).HasMaxLength(200);
            b.Property(x => x.Action).HasMaxLength(100);
            b.Property(x => x.Reason).HasMaxLength(1000);
            b.Property(x => x.CorrelationId).HasMaxLength(100);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.ResourceId, x.OccurredAt });
        });

        model.Entity<SalesCommand>(b =>
        {
            b.ToTable("sales_commands", "sales");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Operation).HasMaxLength(100);
            b.Property(x => x.Key).HasMaxLength(200);
            b.Property(x => x.Actor).HasMaxLength(200);
            b.Property(x => x.RequestHash).HasMaxLength(128);
            b.Property(x => x.Location).HasMaxLength(500);
            b.Property(x => x.ResponseJson).HasColumnType("jsonb");
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.Operation, x.Key }).IsUnique();
        });

        model.Entity<SalesOutbox>(b =>
        {
            b.ToTable("sales_outbox", "sales");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.EventType).HasMaxLength(200);
            b.Property(x => x.CorrelationId).HasMaxLength(100);
            b.Property(x => x.Payload).HasColumnType("jsonb");
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.AggregateId, x.AggregateVersion, x.EventType }).IsUnique();
            b.HasIndex(x => x.CreatedAt).HasFilter("\"DispatchedAt\" IS NULL");
        });
    }
}

