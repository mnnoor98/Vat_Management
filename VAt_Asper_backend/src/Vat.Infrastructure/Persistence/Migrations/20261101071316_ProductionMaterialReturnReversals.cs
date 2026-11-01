using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProductionMaterialReturnReversals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_production_movement_sign",
                schema: "inventory",
                table: "production_stock_movements");

            migrationBuilder.DropCheckConstraint(
                name: "ck_cost_entry_kind",
                schema: "inventory",
                table: "cost_entries");

            migrationBuilder.DropCheckConstraint(
                name: "ck_cost_entry_sign",
                schema: "inventory",
                table: "cost_entries");

            migrationBuilder.AddCheckConstraint(
                name: "ck_production_movement_sign",
                schema: "inventory",
                table: "production_stock_movements",
                sql: "(\"Kind\" IN ('Issue','ReceiptReversal','ReturnReversal') AND \"Quantity\"<0 AND \"Value\"<=0) OR (\"Kind\" IN ('Return','Receipt') AND \"Quantity\">0 AND \"Value\">=0)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_cost_entry_kind",
                schema: "inventory",
                table: "cost_entries",
                sql: "\"Kind\" IN ('Opening','Receipt','Issue','Return','Reversal','TransferOut','TransferIn','ProductionIssue','ProductionReturn','ProductionReturnReversal','ProductionReceipt','ProductionReceiptReversal')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_cost_entry_sign",
                schema: "inventory",
                table: "cost_entries",
                sql: "(\"Kind\" IN ('Opening','Receipt','Return','TransferIn','ProductionReturn','ProductionReceipt') AND \"Quantity\">0 AND \"Value\">=0) OR (\"Kind\" IN ('Issue','Reversal','TransferOut','ProductionIssue','ProductionReturnReversal','ProductionReceiptReversal') AND \"Quantity\"<0 AND \"Value\"<=0)");
            using var reader=new System.IO.StreamReader(typeof(ProductionMaterialReturnReversals).Assembly.GetManifestResourceStream("ProductionOutputGuards.sql")!);
            var guards=reader.ReadToEnd()
                .Replace("original.\"Kind\" NOT IN ('Conversion','Loss','Receipt','ByProduct')","original.\"Kind\" NOT IN ('Return','Conversion','Loss','Receipt','ByProduct')")
                .Replace("(original.\"Kind\" IN ('Receipt','ByProduct') AND (movement_count<>1 OR movement_value<>-original.\"PostedValue\")) OR (original.\"Kind\" NOT IN ('Receipt','ByProduct') AND movement_count<>0)","(original.\"Kind\" IN ('Receipt','ByProduct') AND (movement_count<>1 OR movement_value<>-original.\"PostedValue\")) OR (original.\"Kind\"='Return' AND (movement_count<>jsonb_array_length(original.\"Lines\") OR movement_value<>-original.\"PostedValue\")) OR (original.\"Kind\" NOT IN ('Return','Receipt','ByProduct') AND movement_count<>0)")
                .Replace("(o.\"Kind\"='Reverse' AND original_kind IN ('Receipt','ByProduct') AND NEW.\"Kind\"='ReceiptReversal')","(o.\"Kind\"='Reverse' AND ((original_kind IN ('Receipt','ByProduct') AND NEW.\"Kind\"='ReceiptReversal') OR (original_kind='Return' AND NEW.\"Kind\"='ReturnReversal')))")
                .Replace("  ELSIF NEW.\"Kind\" IN ('Issue','Return') THEN","  ELSIF NEW.\"Kind\"='ReturnReversal' THEN\n    IF NOT EXISTS(SELECT 1 FROM inventory.production_stock_movements r WHERE r.\"TenantId\"=NEW.\"TenantId\" AND r.\"CompanyId\"=NEW.\"CompanyId\" AND r.\"SessionId\"=NEW.\"SessionId\" AND r.\"OperationId\"=o.\"ReversesOperationId\" AND r.\"Id\"=NEW.\"OriginalIssueId\" AND r.\"Kind\"='Return' AND r.\"WarehouseId\"=NEW.\"WarehouseId\" AND r.\"ProductId\"=NEW.\"ProductId\" AND r.\"Quantity\"=-NEW.\"Quantity\" AND r.\"Value\"=-NEW.\"Value\" AND r.\"BusinessDate\"<=NEW.\"BusinessDate\") THEN RAISE EXCEPTION 'Material return reversal must exactly compensate original return'; END IF;\n  ELSIF NEW.\"Kind\" IN ('Issue','Return') THEN");
            migrationBuilder.Sql(guards);
            using var costReader=new System.IO.StreamReader(typeof(ProductionMaterialReturnReversals).Assembly.GetManifestResourceStream("ProductionReversalCostGuard.sql")!);
            migrationBuilder.Sql(costReader.ReadToEnd().Replace("'ProductionIssue','ProductionReturn','ProductionReceipt','ProductionReceiptReversal'","'ProductionIssue','ProductionReturn','ProductionReturnReversal','ProductionReceipt','ProductionReceiptReversal'"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN IF EXISTS(SELECT 1 FROM inventory.production_stock_movements WHERE "Kind"='ReturnReversal') THEN RAISE EXCEPTION 'Cannot discard material return reversal evidence'; END IF; END $$;
                """);
            migrationBuilder.DropCheckConstraint(
                name: "ck_production_movement_sign",
                schema: "inventory",
                table: "production_stock_movements");

            migrationBuilder.DropCheckConstraint(
                name: "ck_cost_entry_kind",
                schema: "inventory",
                table: "cost_entries");

            migrationBuilder.DropCheckConstraint(
                name: "ck_cost_entry_sign",
                schema: "inventory",
                table: "cost_entries");

            migrationBuilder.AddCheckConstraint(
                name: "ck_production_movement_sign",
                schema: "inventory",
                table: "production_stock_movements",
                sql: "(\"Kind\" IN ('Issue','ReceiptReversal') AND \"Quantity\"<0 AND \"Value\"<=0) OR (\"Kind\" IN ('Return','Receipt') AND \"Quantity\">0 AND \"Value\">=0)");

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
            using(var reader=new System.IO.StreamReader(typeof(ProductionMaterialReturnReversals).Assembly.GetManifestResourceStream("ProductionOutputGuards.sql")!))migrationBuilder.Sql(reader.ReadToEnd());
            using(var costReader=new System.IO.StreamReader(typeof(ProductionMaterialReturnReversals).Assembly.GetManifestResourceStream("ProductionReversalCostGuard.sql")!))migrationBuilder.Sql(costReader.ReadToEnd());
        }
    }
}
