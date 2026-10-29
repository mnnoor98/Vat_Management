using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vat.Domain.Posting;
using Vat.Domain.Purchasing;

namespace Vat.Services.Purchase.Infrastructure;

public sealed class PurchaseAudit
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

public sealed class PurchaseCommand
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

public sealed class PurchaseDbContext(DbContextOptions<PurchaseDbContext> options) : DbContext(options)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public DbSet<Vat.Domain.Purchasing.Purchase> Purchases => Set<Vat.Domain.Purchasing.Purchase>();
    public DbSet<PurchaseLine> PurchaseLines => Set<PurchaseLine>();
    public DbSet<PurchasePostingPlan> PostingPlans => Set<PurchasePostingPlan>();
    public DbSet<PurchaseReversal> Reversals => Set<PurchaseReversal>();
    public DbSet<PurchaseAudit> Audits => Set<PurchaseAudit>();
    public DbSet<PurchaseCommand> Commands => Set<PurchaseCommand>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Vat.Domain.Purchasing.Purchase>(b =>
        {
            b.ToTable("purchases", "purchasing");
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.ExternalReference).HasMaxLength(100);
            b.Property(x => x.SupplierReference).HasMaxLength(100);
            b.Property(x => x.Currency).HasMaxLength(3);
            b.Property(x => x.CreatedBy).HasMaxLength(200);
            b.Property(x => x.ApprovedBy).HasMaxLength(200);
            b.Property(x => x.RejectionReason).HasMaxLength(1000);
            b.Property(x => x.CancellationReason).HasMaxLength(1000);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.ExternalReference }).IsUnique();
            b.HasMany(x => x.Lines).WithOne().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.PurchaseId })
                .HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<PurchaseLine>(b =>
        {
            b.ToTable("purchase_lines", "purchasing");
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.PurchaseId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.ProductReference).HasMaxLength(100);
            b.Property(x => x.Description).HasMaxLength(500);
        });

        model.Entity<PurchasePostingPlan>(b =>
        {
            b.ToTable("purchase_posting_plans", "purchasing");
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.Property(x => x.PreparedBy).HasMaxLength(200);
            b.Property(x => x.PostedBy).HasMaxLength(200);
            b.Property(x => x.CancellationReason).HasMaxLength(1000);
            b.Property(x => x.Snapshot).HasConversion(
                snapshot => JsonSerializer.Serialize(snapshot, Json),
                json => JsonSerializer.Deserialize<CalculationSnapshot>(json, Json)!).HasColumnType("jsonb");
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.PurchaseId, x.SourceVersion }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.PurchaseId }).IsUnique().HasFilter("\"Status\" = 'Prepared'");
        });

        model.Entity<PurchaseReversal>(b =>
        {
            b.ToTable("purchase_reversals", "purchasing");
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.Property(x => x.Reason).HasMaxLength(1000);
            b.Property(x => x.RequestedBy).HasMaxLength(200);
            b.Property(x => x.ApprovedBy).HasMaxLength(200);
            b.Property(x => x.CancellationReason).HasMaxLength(1000);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.PurchaseId, x.SourceVersion }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.PurchaseId }).IsUnique().HasFilter("\"Status\" = 'Pending'");
        });

        model.Entity<PurchaseAudit>(b =>
        {
            b.ToTable("purchase_audit", "purchasing");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Actor).HasMaxLength(200);
            b.Property(x => x.Action).HasMaxLength(100);
            b.Property(x => x.CorrelationId).HasMaxLength(100);
            b.Property(x => x.Reason).HasMaxLength(1000);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.ResourceId, x.OccurredAt });
        });

        model.Entity<PurchaseCommand>(b =>
        {
            b.ToTable("purchase_commands", "purchasing");
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Operation, x.Key });
            b.Property(x => x.Operation).HasMaxLength(200);
            b.Property(x => x.Key).HasMaxLength(100);
            b.Property(x => x.Actor).HasMaxLength(200);
            b.Property(x => x.RequestHash).HasMaxLength(64);
            b.Property(x => x.ResponseJson).HasColumnType("text");
            b.Property(x => x.Location).HasMaxLength(500);
        });
    }
}
