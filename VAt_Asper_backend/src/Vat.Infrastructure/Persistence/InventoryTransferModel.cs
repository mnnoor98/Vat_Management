using Microsoft.EntityFrameworkCore;
using Vat.Domain.Catalog;
using Vat.Domain.Inventory;

namespace Vat.Infrastructure.Persistence;

internal static class InventoryTransferModel
{
    public static void Configure(ModelBuilder model)
    {
        model.SharedTypeEntity<Dictionary<string, object>>("inventory.transfers", b =>
        {
            foreach (var field in new[] { "VehicleNumber", "DriverName", "Remarks", "DispatchTime" })
                b.IndexerProperty<string?>(field).HasColumnName(field.ToLowerInvariant()).HasColumnType("text");
        });
        model.SharedTypeEntity<Dictionary<string, object>>("inventory.transfer_lines", b =>
        {
            b.IndexerProperty<decimal?>("UnitPrice").HasColumnName("unitprice").HasPrecision(24, 6);
            b.HasAlternateKey("TenantId", "CompanyId", "TransferId", "Id");
        });
        model.Entity<TransferJournalEntry>(b =>
        {
            b.ToTable("transfer_journal", "inventory", t => t.HasCheckConstraint("ck_transfer_journal_pair",
                "\"Quantity\" > 0 AND \"FromWarehouseId\" <> \"ToWarehouseId\" AND length(\"EvidenceSha256\") = 64"));
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever(); b.Property(x => x.Quantity).HasPrecision(24, 6);
            b.Property(x => x.EvidenceSha256).HasMaxLength(64); b.Property(x => x.RecordedBy).HasMaxLength(200);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.TransferLineId }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.BusinessDate });
            b.HasOne("inventory.transfers", null).WithMany().HasForeignKey("TenantId", "CompanyId", "TransferId")
                .HasPrincipalKey("TenantId", "CompanyId", "Id").OnDelete(DeleteBehavior.Restrict);
            b.HasOne("inventory.transfer_lines", null).WithMany().HasForeignKey("TenantId", "CompanyId", "TransferId", "TransferLineId")
                .HasPrincipalKey("TenantId", "CompanyId", "TransferId", "Id").OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Warehouse>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.FromWarehouseId }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Warehouse>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.ToWarehouseId }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Product>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.ProductId }).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
