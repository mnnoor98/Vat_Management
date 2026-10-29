using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vat.Domain.Company;

namespace Vat.Services.Company.Infrastructure;

public sealed class CompanyAudit
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

public sealed class CompanyCommand
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

public sealed class CompanyOutbox
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid AggregateId { get; set; }
    public long AggregateVersion { get; set; }
    public string EventType { get; set; } = "";
    public string CorrelationId { get; set; } = "";
    public string PayloadJson { get; set; } = "";
    public DateTimeOffset OccurredAt { get; set; }
}

public sealed class CompanyDbContext(DbContextOptions<CompanyDbContext> options) : DbContext(options)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public DbSet<Domain.Company.Company> Companies => Set<Domain.Company.Company>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<FiscalPeriod> FiscalPeriods => Set<FiscalPeriod>();
    public DbSet<CompanyAudit> Audits => Set<CompanyAudit>();
    public DbSet<CompanyCommand> Commands => Set<CompanyCommand>();
    public DbSet<CompanyOutbox> Outbox => Set<CompanyOutbox>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Domain.Company.Company>(b =>
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
            b.HasOne<Domain.Company.Company>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<FiscalPeriod>(b =>
        {
            b.ToTable("fiscal_periods", "company", t => t.HasCheckConstraint("ck_period_interval", "\"Until\" > \"From\""));
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Name).HasMaxLength(100);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.Property(x => x.CloseRequestedBy).HasMaxLength(200);
            b.Property(x => x.ClosedBy).HasMaxLength(200);
            b.Property(x => x.Snapshot).HasConversion(
                snapshot => JsonSerializer.Serialize(snapshot, Json),
                json => JsonSerializer.Deserialize<PeriodCloseSnapshot>(json, Json)).HasColumnType("jsonb");
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.From });
            b.HasOne<Domain.Company.Company>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<CompanyAudit>(b =>
        {
            b.ToTable("company_audit", "company");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Actor).HasMaxLength(200);
            b.Property(x => x.Action).HasMaxLength(100);
            b.Property(x => x.CorrelationId).HasMaxLength(100);
            b.Property(x => x.Reason).HasMaxLength(1000);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.ResourceId, x.OccurredAt });
        });

        model.Entity<CompanyCommand>(b =>
        {
            b.ToTable("company_commands", "company");
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Operation, x.Key });
            b.Property(x => x.Operation).HasMaxLength(200);
            b.Property(x => x.Key).HasMaxLength(100);
            b.Property(x => x.Actor).HasMaxLength(200);
            b.Property(x => x.RequestHash).HasMaxLength(64);
            b.Property(x => x.ResponseJson).HasColumnType("text");
            b.Property(x => x.Location).HasMaxLength(500);
        });

        model.Entity<CompanyOutbox>(b =>
        {
            b.ToTable("company_outbox", "company");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.EventType).HasMaxLength(100);
            b.Property(x => x.CorrelationId).HasMaxLength(100);
            b.Property(x => x.PayloadJson).HasColumnType("text");
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.OccurredAt });
        });
    }
}
