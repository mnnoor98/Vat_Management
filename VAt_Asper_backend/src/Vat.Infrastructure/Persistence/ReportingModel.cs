using Microsoft.EntityFrameworkCore;

namespace Vat.Infrastructure.Persistence;

internal static class ReportingModel
{
    public static void Configure(ModelBuilder model)
    {
        model.Entity<OutboxMessage>(b =>
        {
            b.Property(x => x.LastError).HasMaxLength(100);
            b.HasIndex(x => new { x.NextAttemptAt, x.CreatedAt }).HasFilter("\"DispatchedAt\" IS NULL AND \"QuarantinedAt\" IS NULL");
        });
        model.Entity<InboxMessage>(b =>
        {
            b.ToTable("inbox_messages", "reporting");
            b.HasKey(x => new { x.Consumer, x.EventId });
            b.Property(x => x.Consumer).HasMaxLength(100);
            b.Property(x => x.PayloadHash).HasMaxLength(64);
            b.Property(x => x.Status).HasMaxLength(30);
            b.HasIndex(x => new { x.TenantId, x.CompanyId });
        });
        model.Entity<ReportingEntry>(b =>
        {
            b.ToTable("financial_entries", "reporting");
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.TransactionId });
            b.Property(x => x.TransactionId).ValueGeneratedNever();
            b.Property(x => x.Kind).HasMaxLength(30);
            b.Property(x => x.ExternalReference).HasMaxLength(100);
            b.Property(x => x.Currency).HasMaxLength(3);
            b.Property(x => x.NetAmount).HasPrecision(24, 4);
            b.Property(x => x.TaxAmount).HasPrecision(24, 4);
            b.Property(x => x.RecoverableTaxAmount).HasPrecision(24, 4);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.BusinessDate });
            // Reversal references intentionally have no FK: a reversal may arrive before its original.
        });
        model.Entity<ReportingCheckpoint>(b =>
        {
            b.ToTable("projection_checkpoints", "reporting");
            b.HasKey(x => new { x.TenantId, x.CompanyId });
        });
        model.Entity<InboxDeadLetter>(b =>
        {
            b.ToTable("dead_letters", "reporting");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Fingerprint).HasMaxLength(64);
            b.Property(x => x.MessageId).HasMaxLength(200);
            b.Property(x => x.EventType).HasMaxLength(200);
            b.Property(x => x.ContentType).HasMaxLength(100);
            b.Property(x => x.ErrorCode).HasMaxLength(100);
            b.HasIndex(x => x.Fingerprint).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.ResolvedAt });
        });
    }
}
