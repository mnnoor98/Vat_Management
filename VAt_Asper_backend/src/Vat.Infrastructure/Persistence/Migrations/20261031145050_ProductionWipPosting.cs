using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProductionWipPosting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_cost_entry_kind",
                schema: "inventory",
                table: "cost_entries");

            migrationBuilder.DropCheckConstraint(
                name: "ck_cost_entry_sign",
                schema: "inventory",
                table: "cost_entries");

            migrationBuilder.CreateTable(
                name: "production_cost_sessions",
                schema: "inventory",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceJson = table.Column<string>(type: "jsonb", nullable: false),
                    SourceSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    WipValue = table.Column<decimal>(type: "numeric(24,4)", precision: 24, scale: 4, nullable: false),
                    ReceivedQuantity = table.Column<decimal>(type: "numeric(24,6)", precision: 24, scale: 6, nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_production_cost_sessions", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_production_cost_session", "\"Version\">=1 AND \"WipValue\">=0 AND \"ReceivedQuantity\">=0 AND \"State\" IN ('Open','Closed')");
                    table.ForeignKey(
                        name: "FK_production_cost_sessions_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "production_cost_operations",
                schema: "inventory",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SessionVersion = table.Column<int>(type: "integer", nullable: false),
                    Evidence = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Lines = table.Column<string>(type: "jsonb", nullable: false),
                    Labor = table.Column<decimal>(type: "numeric(24,4)", precision: 24, scale: 4, nullable: false),
                    Utility = table.Column<decimal>(type: "numeric(24,4)", precision: 24, scale: 4, nullable: false),
                    Overhead = table.Column<decimal>(type: "numeric(24,4)", precision: 24, scale: 4, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(24,4)", precision: 24, scale: 4, nullable: false),
                    OutputQuantity = table.Column<decimal>(type: "numeric(24,6)", precision: 24, scale: 6, nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    PostedValue = table.Column<decimal>(type: "numeric(24,4)", precision: 24, scale: 4, nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ApprovedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PostedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_production_cost_operations", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_production_operation_amounts", "\"Amount\">=0 AND \"OutputQuantity\">=0 AND \"PostedValue\">=0 AND \"Labor\">=0 AND \"Utility\">=0 AND \"Overhead\">=0");
                    table.CheckConstraint("ck_production_operation_kind", "\"Kind\" IN ('Issue','Return','Conversion','Receipt','Loss','Close')");
                    table.CheckConstraint("ck_production_operation_state", "(\"State\"='Draft' AND \"Version\"=1 AND \"ApprovedBy\" IS NULL AND \"PostedAt\" IS NULL AND \"PostedValue\"=0) OR (\"State\"='Cancelled' AND \"Version\"=2 AND \"ApprovedBy\" IS NULL AND \"PostedAt\" IS NULL AND \"PostedValue\"=0) OR (\"State\"='Posted' AND \"Version\"=2 AND \"ApprovedBy\" IS NOT NULL AND \"ApprovedBy\"<>\"CreatedBy\" AND \"PostedAt\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_production_cost_operations_production_cost_sessions_TenantI~",
                        columns: x => new { x.TenantId, x.CompanyId, x.SessionId },
                        principalSchema: "inventory",
                        principalTable: "production_cost_sessions",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "production_stock_movements",
                schema: "inventory",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(24,6)", precision: 24, scale: 6, nullable: false),
                    Value = table.Column<decimal>(type: "numeric(24,4)", precision: 24, scale: 4, nullable: false),
                    OriginalIssueId = table.Column<Guid>(type: "uuid", nullable: true),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_production_stock_movements", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_production_movement_sign", "(\"Kind\"='Issue' AND \"Quantity\"<0 AND \"Value\"<=0) OR (\"Kind\" IN ('Return','Receipt') AND \"Quantity\">0 AND \"Value\">=0)");
                    table.ForeignKey(
                        name: "FK_production_stock_movements_production_cost_operations_Tenan~",
                        columns: x => new { x.TenantId, x.CompanyId, x.OperationId },
                        principalSchema: "inventory",
                        principalTable: "production_cost_operations",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "production_wip_entries",
                schema: "inventory",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionVersion = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<decimal>(type: "numeric(24,4)", precision: 24, scale: 4, nullable: false),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_production_wip_entries", x => new { x.TenantId, x.CompanyId, x.OperationId });
                    table.ForeignKey(
                        name: "FK_production_wip_entries_production_cost_operations_TenantId_~",
                        columns: x => new { x.TenantId, x.CompanyId, x.OperationId },
                        principalSchema: "inventory",
                        principalTable: "production_cost_operations",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_cost_entry_kind",
                schema: "inventory",
                table: "cost_entries",
                sql: "\"Kind\" IN ('Opening','Receipt','Issue','Return','Reversal','TransferOut','TransferIn','ProductionIssue','ProductionReturn','ProductionReceipt')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_cost_entry_sign",
                schema: "inventory",
                table: "cost_entries",
                sql: "(\"Kind\" IN ('Opening','Receipt','Return','TransferIn','ProductionReturn','ProductionReceipt') AND \"Quantity\">0 AND \"Value\">=0) OR (\"Kind\" IN ('Issue','Reversal','TransferOut','ProductionIssue') AND \"Quantity\"<0 AND \"Value\"<=0)");

            migrationBuilder.CreateIndex(
                name: "IX_production_cost_operations_TenantId_CompanyId_SessionId",
                schema: "inventory",
                table: "production_cost_operations",
                columns: new[] { "TenantId", "CompanyId", "SessionId" });

            migrationBuilder.CreateIndex(
                name: "IX_production_cost_sessions_TenantId_CompanyId_OrderId",
                schema: "inventory",
                table: "production_cost_sessions",
                columns: new[] { "TenantId", "CompanyId", "OrderId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_production_stock_movements_TenantId_CompanyId_OperationId",
                schema: "inventory",
                table: "production_stock_movements",
                columns: new[] { "TenantId", "CompanyId", "OperationId" });

            migrationBuilder.CreateIndex(
                name: "IX_production_stock_movements_TenantId_CompanyId_WarehouseId_P~",
                schema: "inventory",
                table: "production_stock_movements",
                columns: new[] { "TenantId", "CompanyId", "WarehouseId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_production_wip_entries_TenantId_CompanyId_SessionId_Session~",
                schema: "inventory",
                table: "production_wip_entries",
                columns: new[] { "TenantId", "CompanyId", "SessionId", "SessionVersion" },
                unique: true);
            migrationBuilder.Sql(Read("ProductionStockCostGuard.sql"));
            migrationBuilder.Sql(Read("ProductionWipGuards.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DO $$ BEGIN IF EXISTS(SELECT 1 FROM inventory.production_cost_sessions) THEN RAISE EXCEPTION 'Cannot remove production evidence after execution import; use reviewed restore/recovery'; END IF; END $$;");
            var previous=Read("ActiveStockCostLedger.sql");var start=previous.IndexOf("CREATE FUNCTION inventory.validate_stock_cost_entry()",StringComparison.Ordinal);
            var end=previous.IndexOf("CREATE CONSTRAINT TRIGGER validate_stock_cost_entry",start,StringComparison.Ordinal);
            migrationBuilder.Sql(previous[start..end].Replace("CREATE FUNCTION inventory.validate_stock_cost_entry()","CREATE OR REPLACE FUNCTION inventory.validate_stock_cost_entry()",StringComparison.Ordinal));
            migrationBuilder.Sql("""
                DROP FUNCTION inventory.require_production_operation_journal() CASCADE;
                DROP FUNCTION inventory.validate_production_stock_movement() CASCADE;
                DROP FUNCTION inventory.validate_production_wip_entry() CASCADE;
                DROP FUNCTION inventory.validate_production_session() CASCADE;
                DROP FUNCTION inventory.check_production_session(uuid,uuid,uuid) CASCADE;
                DROP FUNCTION inventory.freeze_production_operation() CASCADE;
                DROP FUNCTION inventory.freeze_production_session() CASCADE;
                """);
            migrationBuilder.DropTable(
                name: "production_stock_movements",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "production_wip_entries",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "production_cost_operations",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "production_cost_sessions",
                schema: "inventory");

            migrationBuilder.DropCheckConstraint(
                name: "ck_cost_entry_kind",
                schema: "inventory",
                table: "cost_entries");

            migrationBuilder.DropCheckConstraint(
                name: "ck_cost_entry_sign",
                schema: "inventory",
                table: "cost_entries");

            migrationBuilder.AddCheckConstraint(
                name: "ck_cost_entry_kind",
                schema: "inventory",
                table: "cost_entries",
                sql: "\"Kind\" IN ('Opening','Receipt','Issue','Return','Reversal','TransferOut','TransferIn')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_cost_entry_sign",
                schema: "inventory",
                table: "cost_entries",
                sql: "(\"Kind\" IN ('Opening','Receipt','Return','TransferIn') AND \"Quantity\">0 AND \"Value\">=0) OR (\"Kind\" IN ('Issue','Reversal','TransferOut') AND \"Quantity\"<0 AND \"Value\"<=0)");
        }
        private static string Read(string name){using var r=new System.IO.StreamReader(typeof(ProductionWipPosting).Assembly.GetManifestResourceStream(name)!);return r.ReadToEnd();}
    }
}
