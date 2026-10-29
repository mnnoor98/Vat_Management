using Microsoft.EntityFrameworkCore;
using Vat.Domain.Company;
using Vat.Domain.Purchasing;

namespace Vat.Infrastructure.Persistence;

public sealed class VatDbContext(DbContextOptions<VatDbContext> options) : DbContext(options)
{
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Vat.Domain.Sales.SalesInvoice> SalesInvoices => Set<Vat.Domain.Sales.SalesInvoice>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();
    public DbSet<AuditEntry> Audit => Set<AuditEntry>();
    public DbSet<IdempotencyRecord> Idempotency => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        PostingModel.Configure(model);
        SalesModel.Configure(model);
        PaymentModel.Configure(model);
        PaymentCorrectionModel.Configure(model);
        SalesPostingModel.Configure(model);
        PeriodModel.Configure(model);
        ReportingModel.Configure(model);
        model.Entity<ReportingSourceExport>(b =>
        {
            b.ToTable("reporting_source_exports", "platform", t =>
            {
                t.HasCheckConstraint("CK_reporting_source_exports_size", "octet_length(\"RowsJson\") <= 8388608");
            });
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.ManifestJson).HasMaxLength(2000);
        });
        model.Entity<Company>(b =>
        {
            b.ToTable("companies", "company");
            b.HasKey(x => new { x.TenantId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Name).HasMaxLength(200);
            b.Property(x => x.RegistrationReference).HasMaxLength(100);
            b.HasIndex(x => new { x.TenantId, x.RegistrationReference }).IsUnique();
        });
        model.Entity<Branch>(b =>
        {
            b.ToTable("branches", "company");
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Name).HasMaxLength(200);
            b.HasOne<Company>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<Purchase>(b =>
        {
            b.ToTable("purchases", "purchasing", t => t.HasCheckConstraint("ck_purchase_net", "\"NetAmount\" >= 0"));
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.ExternalReference).HasMaxLength(100);
            b.Property(x => x.SupplierReference).HasMaxLength(100);
            b.Property(x => x.Currency).HasMaxLength(3);
            b.Property(x => x.CreatedBy).HasMaxLength(200);
            b.Property(x => x.ApprovedBy).HasMaxLength(200);
            b.Property(x => x.RejectionReason).HasMaxLength(1000);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.Property(x => x.NetAmount).HasPrecision(24, 4);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.ExternalReference }).IsUnique();
            b.HasOne<Branch>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.BranchId })
                .HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasMany(x => x.Lines).WithOne().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.PurchaseId })
                .HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
        model.Entity<PurchaseLine>(b =>
        {
            b.ToTable("purchase_lines", "purchasing", t =>
            {
                t.HasCheckConstraint("ck_line_quantity", "\"Quantity\" > 0 AND \"Quantity\" <= 1000000");
                t.HasCheckConstraint("ck_line_price", "\"UnitPrice\" >= 0 AND \"UnitPrice\" <= 1000000000");
            });
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.PurchaseId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.ProductReference).HasMaxLength(100);
            b.Property(x => x.Description).HasMaxLength(500);
            b.Property(x => x.Quantity).HasPrecision(18, 6);
            b.Property(x => x.UnitPrice).HasPrecision(20, 4);
            b.Property(x => x.NetAmount).HasPrecision(24, 4);
        });
        model.Entity<OutboxMessage>(b =>
        {
            b.ToTable("outbox_messages", "platform");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.EventType).HasMaxLength(200);
            b.Property(x => x.Payload).HasColumnType("jsonb");
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.AggregateId, x.AggregateVersion, x.EventType }).IsUnique();
            b.HasIndex(x => x.CreatedAt).HasFilter("\"DispatchedAt\" IS NULL");
        });
        model.Entity<AuditEntry>(b =>
        {
            b.ToTable("audit_entries", "platform");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.ActorId).HasMaxLength(200);
            b.Property(x => x.Action).HasMaxLength(100);
            b.Property(x => x.CorrelationId).HasMaxLength(100);
            b.Property(x => x.Reason).HasMaxLength(1000);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.ResourceId, x.OccurredAt });
        });
        model.Entity<IdempotencyRecord>(b =>
        {
            b.ToTable("idempotency_records", "platform");
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Operation, x.Key });
            b.Property(x => x.Operation).HasMaxLength(200);
            b.Property(x => x.Key).HasMaxLength(100);
            b.Property(x => x.ActorId).HasMaxLength(200);
            b.Property(x => x.RequestHash).HasMaxLength(64);
            // Text preserves exact response bytes for retries (jsonb normalizes formatting).
            b.Property(x => x.ResponseJson).HasColumnType("text");
            b.Property(x => x.Location).HasMaxLength(500);
        });
        PlannedModulesModel.Configure(model);
        InventoryTransferModel.Configure(model);
    }
}
