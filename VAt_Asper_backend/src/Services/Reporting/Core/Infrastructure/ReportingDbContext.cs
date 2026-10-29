using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Vat.Services.Reporting.Infrastructure;

public sealed class SnapshotGeneration
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid Id { get; set; }
    public Guid ExportId { get; set; }
    public string ManifestJson { get; set; } = "";
    public string Fingerprint { get; set; } = "";
    public DateTimeOffset CapturedAt { get; set; }
    public DateTimeOffset ImportedAt { get; set; }
    public int EventCount { get; set; }
}
public sealed class SnapshotEntry
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid GenerationId { get; set; }
    public Guid TransactionId { get; set; }
    public Guid EventId { get; set; }
    public DateOnly BusinessDate { get; set; }
    public string Currency { get; set; } = "";
    public decimal NetAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal RecoverableTaxAmount { get; set; }
    public string RowJson { get; set; } = "";
}
public sealed class SnapshotHead
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid GenerationId { get; set; }
    public long Version { get; set; }
}
public sealed class SnapshotReceipt
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public string Operation { get; set; } = "";
    public Guid Key { get; set; }
    public string ActorId { get; set; } = "";
    public string RequestHash { get; set; } = "";
    public string ResponseJson { get; set; } = "";
}
public sealed class SnapshotAudit
{
    public Guid CommandKey { get; set; }
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid Id { get; set; }
    public Guid GenerationId { get; set; }
    public string ActorId { get; set; } = "";
    public string Action { get; set; } = "";
    public string Reason { get; set; } = "";
    public DateTimeOffset OccurredAt { get; set; }
}
public sealed class ReportingDbContext(DbContextOptions<ReportingDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema("reporting");
        FinancialInboxModel.Configure(model);
        model.Entity<FinancialCoverageVerification>(b =>
        {
            b.ToTable("financial_coverage_verifications", t => t.HasCheckConstraint("ck_financial_coverage_bounds", "\"Version\" > 0 AND \"RowCount\" BETWEEN 0 AND 10000"));
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.GenerationId, x.Id }); b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.SourceManifestJson).HasMaxLength(2000); b.Property(x => x.Fingerprint).HasMaxLength(64);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.GenerationId, x.Version }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.GenerationId, x.ExportId }).IsUnique();
            b.HasOne<FinancialInboxGeneration>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.GenerationId }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<CustomerSnapshotGeneration>(b =>
        {
            b.ToTable("customer_snapshot_generations");
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.CustomerId, x.Id }); b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Fingerprint).HasMaxLength(64); b.Property(x => x.ExportJson).HasMaxLength(8500000);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.ExportId }).IsUnique();
        });
        model.Entity<CustomerSnapshotAudit>(b =>
        {
            b.ToTable("customer_snapshot_audit"); b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id }); b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.ActorId).HasMaxLength(200); b.Property(x => x.Reason).HasMaxLength(1000);
            b.HasOne<CustomerSnapshotGeneration>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.CustomerId, Id = x.GenerationId }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<SnapshotGeneration>(b =>
        {
            b.ToTable("snapshot_generations", t => t.HasCheckConstraint("ck_snapshot_generation_count", "\"EventCount\" BETWEEN 0 AND 10000"));
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id }); b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.ManifestJson).HasMaxLength(2000); b.Property(x => x.Fingerprint).HasMaxLength(64);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.ExportId }).IsUnique();
        });
        model.Entity<SnapshotEntry>(b =>
        {
            b.ToTable("snapshot_entries"); b.HasKey(x => new { x.TenantId, x.CompanyId, x.GenerationId, x.TransactionId });
            b.Property(x => x.TransactionId).ValueGeneratedNever(); b.Property(x => x.Currency).HasMaxLength(3);
            b.Property(x => x.RowJson).HasMaxLength(8192);
            b.Property(x => x.NetAmount).HasPrecision(24, 4); b.Property(x => x.TaxAmount).HasPrecision(24, 4); b.Property(x => x.RecoverableTaxAmount).HasPrecision(24, 4);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.GenerationId, x.EventId }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.GenerationId, x.BusinessDate, x.Currency });
            b.HasOne<SnapshotGeneration>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, Id = x.GenerationId }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<SnapshotHead>(b =>
        {
            b.ToTable("snapshot_heads", t => t.HasCheckConstraint("ck_snapshot_head_version", "\"Version\" > 0"));
            b.HasKey(x => new { x.TenantId, x.CompanyId }); b.Property(x => x.Version).IsConcurrencyToken();
            b.HasOne<SnapshotGeneration>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, Id = x.GenerationId }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<SnapshotReceipt>(b =>
        {
            b.ToTable("snapshot_command_receipts"); b.HasKey(x => new { x.TenantId, x.CompanyId, x.Operation, x.Key });
            b.Property(x => x.Operation).HasMaxLength(40); b.Property(x => x.ActorId).HasMaxLength(200);
            b.Property(x => x.RequestHash).HasMaxLength(64); b.Property(x => x.ResponseJson).HasMaxLength(2000);
        });
        model.Entity<SnapshotAudit>(b =>
        {
            b.ToTable("snapshot_operator_audit"); b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id }); b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.ActorId).HasMaxLength(200); b.Property(x => x.Action).HasMaxLength(40); b.Property(x => x.Reason).HasMaxLength(1000);
            b.HasOne<SnapshotGeneration>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, Id = x.GenerationId }).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
public sealed class ReportingDesignTimeFactory : IDesignTimeDbContextFactory<ReportingDbContext>
{
    public ReportingDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<ReportingDbContext>()
        .UseNpgsql(Environment.GetEnvironmentVariable("ConnectionStrings__ReportingDatabase")
            ?? throw new InvalidOperationException("Set ConnectionStrings__ReportingDatabase for Reporting EF tools."),
            options => options.MigrationsHistoryTable("__ReportingMigrationsHistory", "reporting")).Options);
}
