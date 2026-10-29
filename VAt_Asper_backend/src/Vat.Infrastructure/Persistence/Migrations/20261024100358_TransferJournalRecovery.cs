using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TransferJournalRecovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Adopt prototype columns without losing existing data, on fresh and upgraded databases.
            migrationBuilder.Sql("""
                ALTER TABLE inventory.transfers ADD COLUMN IF NOT EXISTS dispatchtime text;
                ALTER TABLE inventory.transfers ADD COLUMN IF NOT EXISTS drivername text;
                ALTER TABLE inventory.transfers ADD COLUMN IF NOT EXISTS remarks text;
                ALTER TABLE inventory.transfers ADD COLUMN IF NOT EXISTS vehiclenumber text;
                ALTER TABLE inventory.transfer_lines ADD COLUMN IF NOT EXISTS unitprice numeric(24,6);
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='inventory'
                    AND table_name='transfers' AND column_name IN ('dispatchtime','drivername','remarks','vehiclenumber')
                    AND data_type NOT IN ('text','character varying')) THEN
                    RAISE EXCEPTION 'Transfer metadata columns have incompatible types; review before migration';
                  END IF;
                  IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='inventory'
                    AND table_name='transfer_lines' AND column_name='unitprice' AND data_type <> 'numeric') THEN
                    RAISE EXCEPTION 'Transfer unitprice has incompatible type; review before migration';
                  END IF;
                  IF EXISTS (SELECT 1 FROM inventory.transfer_lines WHERE unitprice::text IN ('NaN','Infinity','-Infinity')
                    OR abs(unitprice)>=1000000000000000000 OR unitprice<>round(unitprice,6)) THEN
                    RAISE EXCEPTION 'Transfer prices cannot be represented losslessly as numeric(24,6)';
                  END IF;
                END $$;
                ALTER TABLE inventory.transfers ALTER COLUMN dispatchtime TYPE text;
                ALTER TABLE inventory.transfers ALTER COLUMN drivername TYPE text;
                ALTER TABLE inventory.transfers ALTER COLUMN remarks TYPE text;
                ALTER TABLE inventory.transfers ALTER COLUMN vehiclenumber TYPE text;
                ALTER TABLE inventory.transfer_lines ALTER COLUMN unitprice TYPE numeric(24,6);
                """);
            migrationBuilder.AddUniqueConstraint(
                name: "AK_transfer_lines_TenantId_CompanyId_TransferId_Id",
                schema: "inventory",
                table: "transfer_lines",
                columns: new[] { "TenantId", "CompanyId", "TransferId", "Id" });

            migrationBuilder.CreateTable(
                name: "transfer_journal",
                schema: "inventory",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TransferId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransferLineId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromWarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToWarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(24,6)", precision: 24, scale: 6, nullable: false),
                    EvidenceSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RecordedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transfer_journal", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_transfer_journal_pair", "\"Quantity\" > 0 AND \"FromWarehouseId\" <> \"ToWarehouseId\" AND length(\"EvidenceSha256\") = 64");
                    table.ForeignKey(
                        name: "FK_transfer_journal_products_TenantId_CompanyId_ProductId",
                        columns: x => new { x.TenantId, x.CompanyId, x.ProductId },
                        principalSchema: "masterdata",
                        principalTable: "products",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_transfer_journal_transfer_lines_TenantId_CompanyId_Transfer~",
                        columns: x => new { x.TenantId, x.CompanyId, x.TransferId, x.TransferLineId },
                        principalSchema: "inventory",
                        principalTable: "transfer_lines",
                        principalColumns: new[] { "TenantId", "CompanyId", "TransferId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_transfer_journal_transfers_TenantId_CompanyId_TransferId",
                        columns: x => new { x.TenantId, x.CompanyId, x.TransferId },
                        principalSchema: "inventory",
                        principalTable: "transfers",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_transfer_journal_warehouses_TenantId_CompanyId_FromWarehous~",
                        columns: x => new { x.TenantId, x.CompanyId, x.FromWarehouseId },
                        principalSchema: "inventory",
                        principalTable: "warehouses",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_transfer_journal_warehouses_TenantId_CompanyId_ToWarehouseId",
                        columns: x => new { x.TenantId, x.CompanyId, x.ToWarehouseId },
                        principalSchema: "inventory",
                        principalTable: "warehouses",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_transfer_journal_TenantId_CompanyId_BusinessDate",
                schema: "inventory",
                table: "transfer_journal",
                columns: new[] { "TenantId", "CompanyId", "BusinessDate" });

            migrationBuilder.CreateIndex(
                name: "IX_transfer_journal_TenantId_CompanyId_FromWarehouseId",
                schema: "inventory",
                table: "transfer_journal",
                columns: new[] { "TenantId", "CompanyId", "FromWarehouseId" });

            migrationBuilder.CreateIndex(
                name: "IX_transfer_journal_TenantId_CompanyId_ProductId",
                schema: "inventory",
                table: "transfer_journal",
                columns: new[] { "TenantId", "CompanyId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_transfer_journal_TenantId_CompanyId_ToWarehouseId",
                schema: "inventory",
                table: "transfer_journal",
                columns: new[] { "TenantId", "CompanyId", "ToWarehouseId" });

            migrationBuilder.CreateIndex(
                name: "IX_transfer_journal_TenantId_CompanyId_TransferId_TransferLine~",
                schema: "inventory",
                table: "transfer_journal",
                columns: new[] { "TenantId", "CompanyId", "TransferId", "TransferLineId" });

            migrationBuilder.CreateIndex(
                name: "IX_transfer_journal_TenantId_CompanyId_TransferLineId",
                schema: "inventory",
                table: "transfer_journal",
                columns: new[] { "TenantId", "CompanyId", "TransferLineId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            throw new InvalidOperationException("Transfer journal recovery is forward-only. Restore the verified pre-migration backup for an approved rollback; do not drop journal or adopted historical columns.");
    }
}