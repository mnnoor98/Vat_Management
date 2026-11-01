using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ActiveStockCostLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cost_adoptions",
                schema: "inventory",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Basis = table.Column<string>(type: "text", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Evidence = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Snapshot = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Lines = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ApprovedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cost_adoptions", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_cost_adoption_basis", "\"Basis\" IN ('PurchaseNetOnly','NetPlusNonrecoverableTax')");
                    table.CheckConstraint("ck_cost_adoption_state", "(\"State\"='Draft' AND \"Version\"=1 AND \"ApprovedBy\" IS NULL AND \"ApprovedAt\" IS NULL) OR (\"State\"='Approved' AND \"Version\"=2 AND \"ApprovedBy\" IS NOT NULL AND \"ApprovedAt\" IS NOT NULL AND \"ApprovedBy\"<>\"CreatedBy\")");
                    table.ForeignKey(
                        name: "FK_cost_adoptions_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cost_ledger_heads",
                schema: "inventory",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    AdoptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OpeningDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Basis = table.Column<string>(type: "text", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cost_ledger_heads", x => new { x.TenantId, x.CompanyId });
                    table.CheckConstraint("ck_cost_ledger_sequence", "\"Sequence\">=0");
                    table.ForeignKey(
                        name: "FK_cost_ledger_heads_cost_adoptions_TenantId_CompanyId_Adoptio~",
                        columns: x => new { x.TenantId, x.CompanyId, x.AdoptionId },
                        principalSchema: "inventory",
                        principalTable: "cost_adoptions",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cost_entries",
                schema: "inventory",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalSourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Method = table.Column<string>(type: "text", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(24,6)", precision: 24, scale: 6, nullable: false),
                    Value = table.Column<decimal>(type: "numeric(24,4)", precision: 24, scale: 4, nullable: false),
                    Allocations = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cost_entries", x => new { x.TenantId, x.CompanyId, x.Sequence });
                    table.CheckConstraint("ck_cost_entry_kind", "\"Kind\" IN ('Opening','Receipt','Issue','Return','Reversal','TransferOut','TransferIn')");
                    table.CheckConstraint("ck_cost_entry_sign", "(\"Kind\" IN ('Opening','Receipt','Return','TransferIn') AND \"Quantity\">0 AND \"Value\">=0) OR (\"Kind\" IN ('Issue','Reversal','TransferOut') AND \"Quantity\"<0 AND \"Value\"<=0)");
                    table.ForeignKey(
                        name: "FK_cost_entries_cost_ledger_heads_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "inventory",
                        principalTable: "cost_ledger_heads",
                        principalColumns: new[] { "TenantId", "CompanyId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cost_positions",
                schema: "inventory",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Method = table.Column<string>(type: "text", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(24,6)", precision: 24, scale: 6, nullable: false),
                    Value = table.Column<decimal>(type: "numeric(24,4)", precision: 24, scale: 4, nullable: false),
                    Layers = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cost_positions", x => new { x.TenantId, x.CompanyId, x.WarehouseId, x.ProductId });
                    table.CheckConstraint("ck_cost_position_method", "\"Method\" IN ('FIFO','WEIGHTED_AVERAGE')");
                    table.CheckConstraint("ck_cost_position_value", "\"Quantity\">=0 AND \"Value\">=0 AND (\"Quantity\">0 OR \"Value\"=0)");
                    table.ForeignKey(
                        name: "FK_cost_positions_cost_ledger_heads_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "inventory",
                        principalTable: "cost_ledger_heads",
                        principalColumns: new[] { "TenantId", "CompanyId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cost_entries_TenantId_CompanyId_Kind_SourceId_WarehouseId_P~",
                schema: "inventory",
                table: "cost_entries",
                columns: new[] { "TenantId", "CompanyId", "Kind", "SourceId", "WarehouseId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cost_entries_TenantId_CompanyId_WarehouseId_ProductId_Seque~",
                schema: "inventory",
                table: "cost_entries",
                columns: new[] { "TenantId", "CompanyId", "WarehouseId", "ProductId", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_cost_ledger_heads_TenantId_CompanyId_AdoptionId",
                schema: "inventory",
                table: "cost_ledger_heads",
                columns: new[] { "TenantId", "CompanyId", "AdoptionId" });
            using var sql = new System.IO.StreamReader(typeof(ActiveStockCostLedger).Assembly.GetManifestResourceStream("ActiveStockCostLedger.sql")!);
            migrationBuilder.Sql(sql.ReadToEnd());
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP FUNCTION inventory.require_source_cost_coverage() CASCADE;
                DROP FUNCTION inventory.validate_stock_cost_entry() CASCADE;
                DROP FUNCTION inventory.validate_cost_ledger_head() CASCADE;
                DROP FUNCTION inventory.validate_cost_position() CASCADE;
                DROP FUNCTION inventory.check_cost_position(uuid,uuid,uuid,uuid) CASCADE;
                DROP FUNCTION inventory.freeze_cost_ledger_head() CASCADE;
                DROP FUNCTION inventory.freeze_cost_adoption() CASCADE;
                """);
            migrationBuilder.DropTable(
                name: "cost_entries",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "cost_positions",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "cost_ledger_heads",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "cost_adoptions",
                schema: "inventory");
        }
    }
}
