using Microsoft.EntityFrameworkCore;
using Vat.Services.Integration.Domain;
namespace Vat.Services.Integration.Infrastructure;

public sealed class IntegrationDbContext(DbContextOptions<IntegrationDbContext> options) : DbContext(options)
{
    public DbSet<IntegrationConnection> Connections => Set<IntegrationConnection>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<IntegrationConnection>(b =>
        {
            b.ToTable("integration_connections", "integration", t =>
            {
                t.HasCheckConstraint("ck_integration_connections_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
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
            b.Property(x => x.Provider).IsRequired();
            b.Property(x => x.Endpoint).IsRequired();
            b.Property(x => x.SecretReference).IsRequired();
            b.Property(x => x.Code).HasMaxLength(100);
            b.Property(x => x.Provider).HasMaxLength(100);
            b.Property(x => x.Endpoint).HasMaxLength(2000);
            b.Property(x => x.SecretReference).HasMaxLength(1000);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.Code }).IsUnique().AreNullsDistinct(false);
        });
        model.Entity<FieldMapping>(b =>
        {
            b.ToTable("field_mappings", "integration", t =>
            {
                t.HasCheckConstraint("ck_field_mappings_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                t.HasCheckConstraint("ck_field_mappings_rules", "\"Revision\" > 0 AND (\"ApprovedBy\" IS NULL OR \"ApprovedBy\" <> \"CreatedBy\")");
            });
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.CreatedBy).HasMaxLength(200).IsRequired();
            b.Property(x => x.Version).HasDefaultValue(1).IsConcurrencyToken();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.CreatedAt });
            b.Property(x => x.CreatedBy).IsRequired();
            b.Property(x => x.DocumentType).IsRequired();
            b.Property(x => x.Mapping).IsRequired();
            b.Property(x => x.DocumentType).HasMaxLength(100);
            b.Property(x => x.Mapping).HasColumnType("jsonb");
            b.Property(x => x.ApprovedBy).HasMaxLength(200);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.ConnectionId, x.DocumentType, x.Revision }).IsUnique().AreNullsDistinct(false);
            b.HasOne<IntegrationConnection>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.ConnectionId }).HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.ConnectionId });
        });
        model.Entity<InboundBatch>(b =>
        {
            b.ToTable("inbound_batches", "integration", t =>
            {
                t.HasCheckConstraint("ck_inbound_batches_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                t.HasCheckConstraint("ck_inbound_batches_rules", "(length(\"PayloadDigest\") = 64) AND \"State\" IN ('Draft','Pending','Approved','Rejected','Cancelled','Running','Completed','Failed','Submitted','Posted')");
            });
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.CreatedBy).HasMaxLength(200).IsRequired();
            b.Property(x => x.Version).HasDefaultValue(1).IsConcurrencyToken();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.CreatedAt });
            b.Property(x => x.CreatedBy).IsRequired();
            b.Property(x => x.ExternalId).IsRequired();
            b.Property(x => x.State).IsRequired();
            b.Property(x => x.PayloadKey).IsRequired();
            b.Property(x => x.PayloadDigest).IsRequired();
            b.Property(x => x.ExternalId).HasMaxLength(200);
            b.Property(x => x.State).HasMaxLength(40);
            b.Property(x => x.PayloadKey).HasMaxLength(1000);
            b.Property(x => x.PayloadDigest).HasMaxLength(64);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.ConnectionId, x.ExternalId }).IsUnique().AreNullsDistinct(false);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.State, x.CreatedAt });
            b.HasOne<IntegrationConnection>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.ConnectionId }).HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.ConnectionId });
            b.HasOne<FieldMapping>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.MappingId }).HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.MappingId });
        });
        model.Entity<InboundRow>(b =>
        {
            b.ToTable("inbound_rows", "integration", t =>
            {
                t.HasCheckConstraint("ck_inbound_rows_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                t.HasCheckConstraint("ck_inbound_rows_rules", "(\"RowNumber\" > 0) AND \"State\" IN ('Draft','Pending','Approved','Rejected','Cancelled','Running','Completed','Failed','Submitted','Posted')");
            });
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.CreatedBy).HasMaxLength(200).IsRequired();
            b.Property(x => x.Version).HasDefaultValue(1).IsConcurrencyToken();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.CreatedAt });
            b.Property(x => x.CreatedBy).IsRequired();
            b.Property(x => x.ExternalId).IsRequired();
            b.Property(x => x.State).IsRequired();
            b.Property(x => x.ExternalId).HasMaxLength(200);
            b.Property(x => x.State).HasMaxLength(40);
            b.Property(x => x.Error).HasMaxLength(2000);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.BatchId, x.RowNumber }).IsUnique().AreNullsDistinct(false);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.State, x.CreatedAt });
            b.HasOne<InboundBatch>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.BatchId }).HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.BatchId });
        });
        model.Entity<ExternalReference>(b =>
        {
            b.ToTable("external_references", "integration", t =>
            {
                t.HasCheckConstraint("ck_external_references_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
            });
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.CreatedBy).HasMaxLength(200).IsRequired();
            b.Property(x => x.Version).HasDefaultValue(1).IsConcurrencyToken();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.CreatedAt });
            b.Property(x => x.CreatedBy).IsRequired();
            b.Property(x => x.SourceType).IsRequired();
            b.Property(x => x.ExternalId).IsRequired();
            b.Property(x => x.SourceType).HasMaxLength(80);
            b.Property(x => x.ExternalId).HasMaxLength(200);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.ConnectionId, x.SourceType, x.ExternalId }).IsUnique().AreNullsDistinct(false);
            b.HasOne<IntegrationConnection>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.ConnectionId }).HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.ConnectionId });
        });
        model.Entity<WebhookSubscription>(b =>
        {
            b.ToTable("webhook_subscriptions", "integration", t =>
            {
                t.HasCheckConstraint("ck_webhook_subscriptions_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
            });
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.CreatedBy).HasMaxLength(200).IsRequired();
            b.Property(x => x.Version).HasDefaultValue(1).IsConcurrencyToken();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.CreatedAt });
            b.Property(x => x.CreatedBy).IsRequired();
            b.Property(x => x.EventType).IsRequired();
            b.Property(x => x.Endpoint).IsRequired();
            b.Property(x => x.SigningSecretReference).IsRequired();
            b.Property(x => x.EventType).HasMaxLength(200);
            b.Property(x => x.Endpoint).HasMaxLength(2000);
            b.Property(x => x.SigningSecretReference).HasMaxLength(1000);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.ConnectionId, x.EventType, x.Endpoint }).IsUnique().AreNullsDistinct(false);
            b.HasOne<IntegrationConnection>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.ConnectionId }).HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.ConnectionId });
        });
        model.Entity<DeliveryAttempt>(b =>
        {
            b.ToTable("delivery_attempts", "integration", t =>
            {
                t.HasCheckConstraint("ck_delivery_attempts_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                t.HasCheckConstraint("ck_delivery_attempts_rules", "(\"AttemptNumber\" > 0 AND (\"HttpStatus\" IS NULL OR \"HttpStatus\" BETWEEN 100 AND 599)) AND \"State\" IN ('Draft','Pending','Approved','Rejected','Cancelled','Running','Completed','Failed','Submitted','Posted')");
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
            b.Property(x => x.Error).HasMaxLength(2000);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.SubscriptionId, x.EventId, x.AttemptNumber }).IsUnique().AreNullsDistinct(false);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.State, x.CreatedAt });
            b.HasOne<WebhookSubscription>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.SubscriptionId }).HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.SubscriptionId });
        });
        model.Entity<IntegrationDeadLetter>(b =>
        {
            b.ToTable("dead_letters", "integration", t =>
            {
                t.HasCheckConstraint("ck_dead_letters_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
            });
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.CreatedBy).HasMaxLength(200).IsRequired();
            b.Property(x => x.Version).HasDefaultValue(1).IsConcurrencyToken();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.CreatedAt });
            b.Property(x => x.CreatedBy).IsRequired();
            b.Property(x => x.PayloadKey).IsRequired();
            b.Property(x => x.Error).IsRequired();
            b.Property(x => x.PayloadKey).HasMaxLength(1000);
            b.Property(x => x.Error).HasMaxLength(2000);
            b.HasOne<IntegrationConnection>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.ConnectionId }).HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.ConnectionId });
            b.HasOne<InboundBatch>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.BatchId }).HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.BatchId });
        });
        model.Entity<InboundBatch>().HasOne<FieldMapping>().WithMany()
         .HasForeignKey(x => new { x.TenantId, x.CompanyId, x.ConnectionId, x.MappingId })
         .HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.ConnectionId, x.Id })
         .OnDelete(DeleteBehavior.Restrict);
    }
}
