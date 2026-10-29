using Microsoft.EntityFrameworkCore;
using Vat.Domain.Catalog;
using Vat.Domain.Sales;

namespace Vat.Services.MasterData.Infrastructure;

public sealed class MasterDataAudit
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

public sealed class MasterDataCommand
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

public sealed class MasterDataDbContext(DbContextOptions<MasterDataDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<MasterDataAudit> Audits => Set<MasterDataAudit>();
    public DbSet<MasterDataCommand> Commands => Set<MasterDataCommand>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Product>(b =>
        {
            b.ToTable("products", "masterdata");
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Reference).HasMaxLength(100);
            b.Property(x => x.Description).HasMaxLength(500);
            b.Property(x => x.UnitOfMeasure).HasMaxLength(30);
            b.Property(x => x.TaxCode).HasMaxLength(100);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.Reference }).IsUnique();
        });

        model.Entity<Supplier>(b =>
        {
            b.ToTable("suppliers", "masterdata");
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Reference).HasMaxLength(100);
            b.Property(x => x.Name).HasMaxLength(200);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.Reference }).IsUnique();
        });

        model.Entity<Customer>(b =>
        {
            b.ToTable("customers", "masterdata");
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

        model.Entity<MasterDataAudit>(b =>
        {
            b.ToTable("masterdata_audit", "masterdata");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Actor).HasMaxLength(200);
            b.Property(x => x.Action).HasMaxLength(100);
            b.Property(x => x.CorrelationId).HasMaxLength(100);
            b.Property(x => x.Reason).HasMaxLength(1000);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.ResourceId, x.OccurredAt });
        });

        model.Entity<MasterDataCommand>(b =>
        {
            b.ToTable("masterdata_commands", "masterdata");
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

