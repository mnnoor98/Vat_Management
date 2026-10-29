using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Vat.Services.Inventory.Infrastructure;

public sealed class InventoryAudit
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

public sealed class InventoryCommand
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

public sealed class InventoryDbContext(DbContextOptions<InventoryDbContext> options) : DbContext(options)
{
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<StockBalance> StockBalances => Set<StockBalance>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<TransferHeader> Transfers => Set<TransferHeader>();
    public DbSet<TransferLine> TransferLines => Set<TransferLine>();
    public DbSet<TransferJournalEntry> TransferJournal => Set<TransferJournalEntry>();
    public DbSet<StockAdjustmentHeader> Adjustments => Set<StockAdjustmentHeader>();
    public DbSet<StockAdjustmentLine> AdjustmentLines => Set<StockAdjustmentLine>();
    public DbSet<InventoryAudit> Audits => Set<InventoryAudit>();
    public DbSet<InventoryCommand> Commands => Set<InventoryCommand>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Warehouse>(b =>
        {
            b.ToTable("warehouses", "inventory");
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Code).HasMaxLength(100);
            b.Property(x => x.Name).HasMaxLength(200);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.Code }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.BranchId });
        });

        model.Entity<StockBalance>(b =>
        {
            b.ToTable("stock_balances", "inventory", t =>
            {
                t.HasCheckConstraint("ck_balance_nonnegative", "\"Quantity\" >= 0");
                t.HasCheckConstraint("ck_balance_reserved", "\"ReservedQuantity\" >= 0 AND \"ReservedQuantity\" <= \"Quantity\"");
            });
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.WarehouseId, x.ProductId });
            b.Property(x => x.ReservedQuantity).HasPrecision(24, 6).HasDefaultValue(0m);
            b.Ignore(x => x.AvailableQuantity);
            b.Property(x => x.Quantity).HasPrecision(24, 6);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.ProductId });
        });

        model.Entity<StockMovement>(b =>
        {
            b.ToTable("stock_movements", "inventory", t => t.HasCheckConstraint("ck_receipt_positive",
                "(\"ReversesEntryId\" IS NULL AND \"ReversalId\" IS NULL AND \"Quantity\" > 0) OR (\"ReversesEntryId\" IS NOT NULL AND \"ReversalId\" IS NOT NULL AND \"Quantity\" < 0)"));
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Quantity).HasPrecision(24, 6);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.PurchaseId, x.PurchaseLineId }).IsUnique().HasFilter("\"ReversesEntryId\" IS NULL");
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.WarehouseId });
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.ProductId });
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.PlanId });
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.ReversesEntryId });
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.ReversalId });
        });

        model.Entity<TransferHeader>(b =>
        {
            b.ToTable("transfers", "inventory", t =>
            {
                t.HasCheckConstraint("ck_inventory_transfers_from_to", "\"FromWarehouseId\" <> \"ToWarehouseId\"");
                t.HasCheckConstraint("ck_inventory_transfers_state", "\"State\" IN ('Draft','Pending','Approved','Rejected','Cancelled','Running','Completed','Failed','Submitted','Posted')");
            });
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Reference).HasMaxLength(100);
            b.Property(x => x.State).HasMaxLength(40);
            b.Property(x => x.CreatedBy).HasMaxLength(200);
            b.Property(x => x.ApprovedBy).HasMaxLength(200);
            b.Property(x => x.DriverName).HasColumnType("text");
            b.Property(x => x.VehicleNumber).HasColumnType("text");
            b.Property(x => x.Remarks).HasColumnType("text");
            b.Property(x => x.DispatchTime).HasColumnType("text");
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.Reference }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.BusinessDate });
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.State, x.CreatedAt });
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.FromWarehouseId });
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.ToWarehouseId });
        });

        model.Entity<TransferLine>(b =>
        {
            b.ToTable("transfer_lines", "inventory", t => t.HasCheckConstraint("ck_transfer_lines_quantity", "\"Quantity\" > 0"));
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.HasAlternateKey(x => new { x.TenantId, x.CompanyId, x.TransferId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Quantity).HasPrecision(24, 6);
            b.Property(x => x.UnitPrice).HasPrecision(24, 6);
            b.Property(x => x.Remarks).HasColumnType("text");
            b.Property(x => x.CreatedBy).HasMaxLength(200);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.TransferId });
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.ProductId });
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.TransferId, x.ProductId });
        });

        model.Entity<TransferJournalEntry>(b =>
        {
            b.ToTable("transfer_journal", "inventory", t => t.HasCheckConstraint("ck_transfer_journal_pair",
                "\"Quantity\" > 0 AND \"FromWarehouseId\" <> \"ToWarehouseId\" AND length(\"EvidenceSha256\") = 64"));
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Quantity).HasPrecision(24, 6);
            b.Property(x => x.EvidenceSha256).HasMaxLength(64);
            b.Property(x => x.RecordedBy).HasMaxLength(200);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.TransferLineId }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.BusinessDate });
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.FromWarehouseId });
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.ToWarehouseId });
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.ProductId });
        });

        model.Entity<StockAdjustmentHeader>(b =>
        {
            b.ToTable("adjustments", "inventory", t => t.HasCheckConstraint("ck_adjustments_state",
                "\"State\" IN ('Draft','Pending','Approved','Rejected','Cancelled','Running','Completed','Failed','Submitted','Posted')"));
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Reference).HasMaxLength(100);
            b.Property(x => x.Reason).HasMaxLength(1000);
            b.Property(x => x.State).HasMaxLength(40);
            b.Property(x => x.ApprovedBy).HasMaxLength(200);
            b.Property(x => x.CreatedBy).HasMaxLength(200);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.Reference }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.BusinessDate });
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.State, x.CreatedAt });
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.WarehouseId });
        });

        model.Entity<StockAdjustmentLine>(b =>
        {
            b.ToTable("adjustment_lines", "inventory", t => t.HasCheckConstraint("ck_adjustment_lines_quantity", "\"Quantity\" <> 0"));
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Quantity).HasPrecision(24, 6);
            b.Property(x => x.CreatedBy).HasMaxLength(200);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.AdjustmentId });
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.ProductId });
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.AdjustmentId, x.ProductId });
        });

        model.Entity<InventoryAudit>(b =>
        {
            b.ToTable("audit_trail", "inventory");
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Action).HasMaxLength(100);
            b.Property(x => x.Actor).HasMaxLength(200);
            b.Property(x => x.CorrelationId).HasMaxLength(100);
            b.Property(x => x.Reason).HasMaxLength(1000);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.ResourceId });
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.OccurredAt });
        });

        model.Entity<InventoryCommand>(b =>
        {
            b.ToTable("idempotent_commands", "inventory");
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

