using Microsoft.EntityFrameworkCore;

namespace Vat.Services.Reporting.Infrastructure;

public sealed class FinancialInboxGeneration
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid GenerationId { get; set; }
    public string Status { get; set; } = "Open";
    public long Version { get; set; } = 1;
}
public sealed class FinancialInboxMessage
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid GenerationId { get; set; }
    public Guid EventId { get; set; }
    public string BodyHash { get; set; } = "";
    public string DeliveryJson { get; set; } = "";
    public string RowJson { get; set; } = "";
    public Guid TransactionId { get; set; }
    public Guid? ReversalOfTransactionId { get; set; }
    public string Status { get; set; } = "";
    public DateTimeOffset ReceivedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
public sealed class FinancialLiveEntry
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid GenerationId { get; set; }
    public Guid TransactionId { get; set; }
    public Guid EventId { get; set; }
    public Guid PurchaseId { get; set; }
    public Guid? ReversalOfTransactionId { get; set; }
    public string Kind { get; set; } = "";
    public string RowJson { get; set; } = "";
    public DateOnly BusinessDate { get; set; }
    public string Currency { get; set; } = "";
    public decimal NetAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal RecoverableTaxAmount { get; set; }
}
public sealed class FinancialInboxDeadLetter
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid GenerationId { get; set; }
    public Guid Id { get; set; }
    public string Fingerprint { get; set; } = "";
    public string DeliveryJson { get; set; } = "";
    public string ErrorCode { get; set; } = "";
    public DateTimeOffset ReceivedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
}
internal static class FinancialInboxModel
{
    public static void Configure(ModelBuilder model)
    {
        model.Entity<FinancialInboxGeneration>(b =>
        {
            b.ToTable("financial_inbox_generations", t => t.HasCheckConstraint("ck_financial_inbox_generation_state", "\"Status\" IN ('Open','Retired') AND \"Version\" > 0"));
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.GenerationId }); b.Property(x => x.GenerationId).ValueGeneratedNever();
            b.Property(x => x.Status).HasMaxLength(20); b.Property(x => x.Version).IsConcurrencyToken();
            b.HasOne<SnapshotGeneration>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, Id = x.GenerationId }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<FinancialInboxMessage>(b =>
        {
            b.ToTable("financial_inbox_messages", t => t.HasCheckConstraint("ck_financial_inbox_message_state", "\"Status\" IN ('Processed','PendingOriginal','Quarantined','BaselineDuplicate')"));
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.GenerationId, x.EventId }); b.Property(x => x.EventId).ValueGeneratedNever();
            b.Property(x => x.BodyHash).HasMaxLength(64); b.Property(x => x.RowJson).HasMaxLength(8192); b.Property(x => x.Status).HasMaxLength(24);
            b.Property(x => x.DeliveryJson).HasMaxLength(1500000);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.GenerationId, x.ReversalOfTransactionId, x.Status });
            b.HasOne<FinancialInboxGeneration>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.GenerationId }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<FinancialLiveEntry>(b =>
        {
            b.ToTable("financial_live_entries"); b.HasKey(x => new { x.TenantId, x.CompanyId, x.GenerationId, x.TransactionId });
            b.Property(x => x.TransactionId).ValueGeneratedNever(); b.Property(x => x.RowJson).HasMaxLength(8192);
            b.Property(x => x.Kind).HasMaxLength(24); b.Property(x => x.Currency).HasMaxLength(3);
            b.Property(x => x.NetAmount).HasPrecision(24, 4); b.Property(x => x.TaxAmount).HasPrecision(24, 4); b.Property(x => x.RecoverableTaxAmount).HasPrecision(24, 4);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.GenerationId, x.EventId }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.GenerationId, x.ReversalOfTransactionId }).IsUnique().HasFilter("\"ReversalOfTransactionId\" IS NOT NULL");
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.GenerationId, x.PurchaseId }).IsUnique().HasFilter("\"Kind\" = 'Purchase'");
            b.HasOne<FinancialInboxGeneration>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.GenerationId }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<FinancialInboxDeadLetter>(b =>
        {
            b.ToTable("financial_inbox_dead_letters"); b.HasKey(x => new { x.TenantId, x.CompanyId, x.GenerationId, x.Id }); b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Fingerprint).HasMaxLength(64); b.Property(x => x.ErrorCode).HasMaxLength(100); b.Property(x => x.DeliveryJson).HasMaxLength(1500000);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.GenerationId, x.Fingerprint }).IsUnique();
            b.HasOne<FinancialInboxGeneration>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.GenerationId }).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
