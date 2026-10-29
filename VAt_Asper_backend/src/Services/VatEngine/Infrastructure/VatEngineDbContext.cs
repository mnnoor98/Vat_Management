using Microsoft.EntityFrameworkCore;
using Vat.Domain.Tax;
using Vat.Services.VatEngine;

namespace Vat.Services.VatEngine.Infrastructure;

public sealed class VatEngineAudit
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

public sealed class VatEngineCommand
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

public sealed class TaxTreatmentRecord
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public string Code { get; set; } = "";
    public string Description { get; set; } = "";
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveUntil { get; set; }
    public string? ApprovedBy { get; set; }
    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public int Version { get; set; } = 1;
}

public sealed class TaxCalculationSnapshotRecord
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid TreatmentId { get; set; }
    public Guid RuleVersionId { get; set; }
    public string SourceType { get; set; } = "";
    public Guid SourceId { get; set; }
    public int SourceVersion { get; set; }
    public string Calculation { get; set; } = "{}";
    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public int Version { get; set; } = 1;
}

public sealed class TaxPostingBatchRecord
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public string SourceType { get; set; } = "";
    public Guid SourceId { get; set; }
    public int SourceVersion { get; set; }
    public DateOnly BusinessDate { get; set; }
    public string State { get; set; } = "Draft";
    public decimal ExpectedTax { get; set; }
    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public int Version { get; set; } = 1;
}

public sealed class VdsEntryRecord
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid BatchId { get; set; }
    public Guid SupplierId { get; set; }
    public DateOnly BusinessDate { get; set; }
    public decimal Amount { get; set; }
    public string EvidenceReference { get; set; } = "";
    public Guid? ReversalOfId { get; set; }
    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public int Version { get; set; } = 1;
}

public sealed class VatEngineDbContext(DbContextOptions<VatEngineDbContext> options) : DbContext(options)
{
    public DbSet<TaxRule> TaxRules => Set<TaxRule>();
    public DbSet<TaxLedgerEntry> TaxLedger => Set<TaxLedgerEntry>();
    public DbSet<TaxTreatmentRecord> TaxTreatments => Set<TaxTreatmentRecord>();
    public DbSet<TaxCalculationSnapshotRecord> CalculationSnapshots => Set<TaxCalculationSnapshotRecord>();
    public DbSet<TaxPostingBatchRecord> PostingBatches => Set<TaxPostingBatchRecord>();
    public DbSet<VdsEntryRecord> VdsEntries => Set<VdsEntryRecord>();
    public DbSet<VatEngineAudit> Audits => Set<VatEngineAudit>();
    public DbSet<VatEngineCommand> Commands => Set<VatEngineCommand>();

    protected override void OnModelCreating(ModelBuilder model)
    {
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
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.PlanId });
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.TaxRuleId });
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.ReversesEntryId });
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.ReversalId });
        });

        model.Entity<TaxTreatmentRecord>(b =>
        {
            b.ToTable("tax_treatments", "tax");
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Code).HasMaxLength(100);
            b.Property(x => x.Description).HasMaxLength(1000);
            b.Property(x => x.ApprovedBy).HasMaxLength(200);
            b.Property(x => x.CreatedBy).HasMaxLength(200);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.Code, x.EffectiveFrom });
        });

        model.Entity<TaxCalculationSnapshotRecord>(b =>
        {
            b.ToTable("tax_calculation_snapshots", "tax");
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.SourceType).HasMaxLength(80);
            b.Property(x => x.Calculation).HasColumnType("jsonb");
            b.Property(x => x.CreatedBy).HasMaxLength(200);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.SourceType, x.SourceId, x.SourceVersion });
        });

        model.Entity<TaxPostingBatchRecord>(b =>
        {
            b.ToTable("tax_posting_batches", "tax");
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.SourceType).HasMaxLength(80);
            b.Property(x => x.State).HasMaxLength(40);
            b.Property(x => x.ExpectedTax).HasPrecision(24, 6);
            b.Property(x => x.CreatedBy).HasMaxLength(200);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.SourceType, x.SourceId, x.SourceVersion });
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.BusinessDate });
        });

        model.Entity<VdsEntryRecord>(b =>
        {
            b.ToTable("vds_entries", "tax");
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Amount).HasPrecision(24, 6);
            b.Property(x => x.EvidenceReference).HasMaxLength(500);
            b.Property(x => x.CreatedBy).HasMaxLength(200);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.BatchId });
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.SupplierId });
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.BusinessDate });
        });

        model.Entity<VatEngineAudit>(b =>
        {
            b.ToTable("audit_trail", "tax");
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Action).HasMaxLength(100);
            b.Property(x => x.Actor).HasMaxLength(200);
            b.Property(x => x.CorrelationId).HasMaxLength(100);
            b.Property(x => x.Reason).HasMaxLength(1000);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.ResourceId });
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.OccurredAt });
        });

        model.Entity<VatEngineCommand>(b =>
        {
            b.ToTable("idempotent_commands", "tax");
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Operation).HasMaxLength(100);
            b.Property(x => x.Key).HasMaxLength(200);
            b.Property(x => x.Actor).HasMaxLength(200);
            b.Property(x => x.RequestHash).HasMaxLength(64);
            b.Property(x => x.Location).HasMaxLength(1000);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.Operation, x.Key }).IsUnique();
        });
    }
}

