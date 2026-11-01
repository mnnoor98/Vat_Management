using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProductionOperationReversals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_production_movement_sign",
                schema: "inventory",
                table: "production_stock_movements");

            migrationBuilder.DropCheckConstraint(
                name: "ck_production_operation_kind",
                schema: "inventory",
                table: "production_cost_operations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_cost_entry_kind",
                schema: "inventory",
                table: "cost_entries");

            migrationBuilder.DropCheckConstraint(
                name: "ck_cost_entry_sign",
                schema: "inventory",
                table: "cost_entries");

            migrationBuilder.AddColumn<Guid>(
                name: "ReversesOperationId",
                schema: "inventory",
                table: "production_cost_operations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_production_movement_sign",
                schema: "inventory",
                table: "production_stock_movements",
                sql: "(\"Kind\" IN ('Issue','ReceiptReversal') AND \"Quantity\"<0 AND \"Value\"<=0) OR (\"Kind\" IN ('Return','Receipt') AND \"Quantity\">0 AND \"Value\">=0)");

            migrationBuilder.CreateIndex(
                name: "IX_production_cost_operations_TenantId_CompanyId_ReversesOpera~",
                schema: "inventory",
                table: "production_cost_operations",
                columns: new[] { "TenantId", "CompanyId", "ReversesOperationId" },
                unique: true,
                filter: "\"State\"='Posted' AND \"ReversesOperationId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_production_operation_kind",
                schema: "inventory",
                table: "production_cost_operations",
                sql: "\"Kind\" IN ('Issue','Return','Conversion','Receipt','Loss','Close','Reverse')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_production_operation_reversal",
                schema: "inventory",
                table: "production_cost_operations",
                sql: "(\"Kind\"='Reverse')=(\"ReversesOperationId\" IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_cost_entry_kind",
                schema: "inventory",
                table: "cost_entries",
                sql: "\"Kind\" IN ('Opening','Receipt','Issue','Return','Reversal','TransferOut','TransferIn','ProductionIssue','ProductionReturn','ProductionReceipt','ProductionReceiptReversal')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_cost_entry_sign",
                schema: "inventory",
                table: "cost_entries",
                sql: "(\"Kind\" IN ('Opening','Receipt','Return','TransferIn','ProductionReturn','ProductionReceipt') AND \"Quantity\">0 AND \"Value\">=0) OR (\"Kind\" IN ('Issue','Reversal','TransferOut','ProductionIssue','ProductionReceiptReversal') AND \"Quantity\"<0 AND \"Value\"<=0)");

            migrationBuilder.AddForeignKey(
                name: "FK_production_cost_operations_production_cost_operations_Tenan~",
                schema: "inventory",
                table: "production_cost_operations",
                columns: new[] { "TenantId", "CompanyId", "ReversesOperationId" },
                principalSchema: "inventory",
                principalTable: "production_cost_operations",
                principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.Sql(Read("ProductionReversalGuards.sql"));
            migrationBuilder.Sql(Read("ProductionReversalCostGuard.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DO $$ BEGIN IF EXISTS(SELECT 1 FROM inventory.production_cost_operations WHERE \"ReversesOperationId\" IS NOT NULL) THEN RAISE EXCEPTION 'Cannot discard production reversal evidence; use reviewed recovery'; END IF; END $$;");
            var previous=Read("ProductionWipGuards.sql");
            foreach(System.Text.RegularExpressions.Match function in System.Text.RegularExpressions.Regex.Matches(previous,@"CREATE FUNCTION [\s\S]*?END \$\$;"))
                migrationBuilder.Sql(function.Value.Replace("CREATE FUNCTION ","CREATE OR REPLACE FUNCTION ",StringComparison.Ordinal));
            migrationBuilder.Sql(Read("ProductionStockCostGuard.sql"));
            migrationBuilder.DropForeignKey(
                name: "FK_production_cost_operations_production_cost_operations_Tenan~",
                schema: "inventory",
                table: "production_cost_operations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_production_movement_sign",
                schema: "inventory",
                table: "production_stock_movements");

            migrationBuilder.DropIndex(
                name: "IX_production_cost_operations_TenantId_CompanyId_ReversesOpera~",
                schema: "inventory",
                table: "production_cost_operations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_production_operation_kind",
                schema: "inventory",
                table: "production_cost_operations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_production_operation_reversal",
                schema: "inventory",
                table: "production_cost_operations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_cost_entry_kind",
                schema: "inventory",
                table: "cost_entries");

            migrationBuilder.DropCheckConstraint(
                name: "ck_cost_entry_sign",
                schema: "inventory",
                table: "cost_entries");

            migrationBuilder.DropColumn(
                name: "ReversesOperationId",
                schema: "inventory",
                table: "production_cost_operations");

            migrationBuilder.AddCheckConstraint(
                name: "ck_production_movement_sign",
                schema: "inventory",
                table: "production_stock_movements",
                sql: "(\"Kind\"='Issue' AND \"Quantity\"<0 AND \"Value\"<=0) OR (\"Kind\" IN ('Return','Receipt') AND \"Quantity\">0 AND \"Value\">=0)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_production_operation_kind",
                schema: "inventory",
                table: "production_cost_operations",
                sql: "\"Kind\" IN ('Issue','Return','Conversion','Receipt','Loss','Close')");

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
        }
        private static string Read(string name){using var reader=new System.IO.StreamReader(typeof(ProductionOperationReversals).Assembly.GetManifestResourceStream(name)!);return reader.ReadToEnd();}
    }
}
