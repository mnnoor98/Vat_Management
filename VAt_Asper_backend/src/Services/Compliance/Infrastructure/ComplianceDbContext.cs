using Microsoft.EntityFrameworkCore;
using Vat.Services.Compliance.Domain;
namespace Vat.Services.Compliance.Infrastructure;

public sealed class ComplianceDbContext(DbContextOptions<ComplianceDbContext> options) : DbContext(options)
{
    public DbSet<VatReturn> Returns => Set<VatReturn>();
    public DbSet<FormTemplate> FormTemplates => Set<FormTemplate>();
    public DbSet<FormVersion> FormVersions => Set<FormVersion>();
    public DbSet<ReturnPeriod> ReturnPeriods => Set<ReturnPeriod>();
    public DbSet<ReturnLine> ReturnLines => Set<ReturnLine>();
    public DbSet<FilingEvidence> FilingEvidences => Set<FilingEvidence>();
    public DbSet<PeriodCloseRequest> PeriodCloseRequests => Set<PeriodCloseRequest>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<FormTemplate>(b =>
        {
            b.ToTable("form_templates", "compliance", t =>
            {
                t.HasCheckConstraint("ck_form_templates_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
            });
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.CreatedBy).HasMaxLength(200).IsRequired();
            b.Property(x => x.Version).HasDefaultValue(1).IsConcurrencyToken();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.CreatedAt });
            b.Property(x => x.CreatedBy).IsRequired();
            b.Property(x => x.Code).IsRequired();
            b.Property(x => x.Name).IsRequired();
            b.Property(x => x.Code).HasMaxLength(100);
            b.Property(x => x.Name).HasMaxLength(200);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.Code }).IsUnique().AreNullsDistinct(false);
        });
        model.Entity<FormVersion>(b =>
        {
            b.ToTable("form_versions", "compliance", t =>
            {
                t.HasCheckConstraint("ck_form_versions_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                t.HasCheckConstraint("ck_form_versions_rules", "\"Revision\" > 0 AND (\"EffectiveUntil\" IS NULL OR \"EffectiveUntil\" >= \"EffectiveFrom\") AND (\"ApprovedBy\" IS NULL OR \"ApprovedBy\" <> \"CreatedBy\")");
            });
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.CreatedBy).HasMaxLength(200).IsRequired();
            b.Property(x => x.Version).HasDefaultValue(1).IsConcurrencyToken();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.CreatedAt });
            b.Property(x => x.CreatedBy).IsRequired();
            b.Property(x => x.Mapping).IsRequired();
            b.Property(x => x.Mapping).HasColumnType("jsonb");
            b.Property(x => x.ApprovedBy).HasMaxLength(200);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.TemplateId, x.Revision }).IsUnique().AreNullsDistinct(false);
            b.HasOne<FormTemplate>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.TemplateId }).HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.TemplateId });
        });
        model.Entity<ReturnPeriod>(b =>
        {
            // Fiscal periods belong to Company; retain the scoped lookup index without a cross-database FK.
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.FiscalPeriodId });
            b.ToTable("return_periods", "compliance", t =>
            {
                t.HasCheckConstraint("ck_return_periods_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                t.HasCheckConstraint("ck_return_periods_rules", "(\"Until\" >= \"From\")");
            });
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.CreatedBy).HasMaxLength(200).IsRequired();
            b.Property(x => x.Version).HasDefaultValue(1).IsConcurrencyToken();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.CreatedAt });
            b.Property(x => x.CreatedBy).IsRequired();
            b.Property(x => x.Code).IsRequired();
            b.Property(x => x.Code).HasMaxLength(100);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.Code }).IsUnique().AreNullsDistinct(false);
        });
        model.Entity<VatReturn>(b =>
        {
            b.ToTable("vat_returns", "compliance", t =>
            {
                t.HasCheckConstraint("ck_vat_returns_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                t.HasCheckConstraint("ck_vat_returns_rules", "(\"CorrectsId\" IS NULL OR \"CorrectsId\" <> \"Id\") AND \"Revision\" > 0 AND (\"ApprovedBy\" IS NULL OR \"ApprovedBy\" <> \"CreatedBy\") AND \"State\" IN ('Draft','Pending','Approved','Rejected','Cancelled','Running','Completed','Failed','Submitted','Posted')");
            });
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.CreatedBy).HasMaxLength(200).IsRequired();
            b.Property(x => x.Version).HasDefaultValue(1).IsConcurrencyToken();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.CreatedAt });
            b.Property(x => x.CreatedBy).IsRequired();
            b.Property(x => x.State).IsRequired();
            b.Property(x => x.State).HasMaxLength(40);
            b.Property(x => x.ApprovedBy).HasMaxLength(200);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.ReturnPeriodId, x.FormVersionId, x.Revision }).IsUnique().AreNullsDistinct(false);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.State, x.CreatedAt });
            b.HasOne<ReturnPeriod>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.ReturnPeriodId }).HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.ReturnPeriodId });
            b.HasOne<FormVersion>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.FormVersionId }).HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.FormVersionId });
            b.HasOne<VatReturn>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.CorrectsId }).HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.CorrectsId });
        });
        model.Entity<ReturnLine>(b =>
        {
            b.ToTable("return_lines", "compliance", t =>
            {
                t.HasCheckConstraint("ck_return_lines_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
            });
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.CreatedBy).HasMaxLength(200).IsRequired();
            b.Property(x => x.Version).HasDefaultValue(1).IsConcurrencyToken();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.CreatedAt });
            b.Property(x => x.CreatedBy).IsRequired();
            b.Property(x => x.LineCode).IsRequired();
            b.Property(x => x.SourceSnapshot).IsRequired();
            b.Property(x => x.LineCode).HasMaxLength(100);
            b.Property(x => x.Amount).HasPrecision(24, 6);
            b.Property(x => x.SourceSnapshot).HasColumnType("jsonb");
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.ReturnId, x.LineCode }).IsUnique().AreNullsDistinct(false);
            b.HasOne<VatReturn>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.ReturnId }).HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.ReturnId });
        });
        model.Entity<ReconciliationRun>(b =>
        {
            b.ToTable("reconciliation_runs", "compliance", t =>
            {
                t.HasCheckConstraint("ck_reconciliation_runs_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                t.HasCheckConstraint("ck_reconciliation_runs_rules", "\"State\" IN ('Draft','Pending','Approved','Rejected','Cancelled','Running','Completed','Failed','Submitted','Posted')");
            });
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.CreatedBy).HasMaxLength(200).IsRequired();
            b.Property(x => x.Version).HasDefaultValue(1).IsConcurrencyToken();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.CreatedAt });
            b.Property(x => x.CreatedBy).IsRequired();
            b.Property(x => x.State).IsRequired();
            b.Property(x => x.Differences).IsRequired();
            b.Property(x => x.State).HasMaxLength(40);
            b.Property(x => x.Differences).HasColumnType("jsonb");
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.State, x.CreatedAt });
            b.HasOne<VatReturn>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.ReturnId }).HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.ReturnId });
        });
        model.Entity<FilingEvidence>(b =>
        {
            b.ToTable("filing_evidence", "compliance", t =>
            {
                t.HasCheckConstraint("ck_filing_evidence_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                t.HasCheckConstraint("ck_filing_evidence_rules", "(length(\"Sha256\") = 64)");
            });
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.CreatedBy).HasMaxLength(200).IsRequired();
            b.Property(x => x.Version).HasDefaultValue(1).IsConcurrencyToken();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.CreatedAt });
            b.Property(x => x.CreatedBy).IsRequired();
            b.Property(x => x.ObjectKey).IsRequired();
            b.Property(x => x.Sha256).IsRequired();
            b.Property(x => x.FilingReference).IsRequired();
            b.Property(x => x.ObjectKey).HasMaxLength(1000);
            b.Property(x => x.Sha256).HasMaxLength(64);
            b.Property(x => x.FilingReference).HasMaxLength(200);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.ReturnId, x.Sha256 }).IsUnique().AreNullsDistinct(false);
            b.HasOne<VatReturn>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.ReturnId }).HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.ReturnId });
        });
        model.Entity<PeriodCloseRequest>(b =>
        {
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.FiscalPeriodId });
            b.ToTable("period_close_requests", "compliance", t =>
            {
                t.HasCheckConstraint("ck_period_close_requests_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                t.HasCheckConstraint("ck_period_close_requests_rules", "(\"ApprovedBy\" IS NULL OR \"ApprovedBy\" <> \"CreatedBy\") AND \"State\" IN ('Draft','Pending','Approved','Rejected','Cancelled','Running','Completed','Failed','Submitted','Posted')");
            });
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.CreatedBy).HasMaxLength(200).IsRequired();
            b.Property(x => x.Version).HasDefaultValue(1).IsConcurrencyToken();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.CreatedAt });
            b.Property(x => x.CreatedBy).IsRequired();
            b.Property(x => x.State).IsRequired();
            b.Property(x => x.RequestedBy).IsRequired();
            b.Property(x => x.State).HasMaxLength(40);
            b.Property(x => x.RequestedBy).HasMaxLength(200);
            b.Property(x => x.ApprovedBy).HasMaxLength(200);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.State, x.CreatedAt });
            b.HasOne<VatReturn>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.ReturnId }).HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.ReturnId });
        });
    }
}
