using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProductionPartialOutputCorrections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_production_cost_operations_TenantId_CompanyId_ReversesOpera~",
                schema: "inventory",
                table: "production_cost_operations");

            migrationBuilder.CreateIndex(
                name: "IX_production_cost_operations_TenantId_CompanyId_ReversesOpera~",
                schema: "inventory",
                table: "production_cost_operations",
                columns: new[] { "TenantId", "CompanyId", "ReversesOperationId" },
                filter: "\"State\"='Posted' AND \"ReversesOperationId\" IS NOT NULL");

            using var reader=new System.IO.StreamReader(typeof(ProductionPartialOutputCorrections).Assembly.GetManifestResourceStream("ProductionOutputGuards.sql")!);
            var guards=MaterialReturnGuards(reader.ReadToEnd())
                .Replace("(NEW.\"Kind\" IN ('Close','Reverse') AND (NEW.\"Amount\"<>0 OR NEW.\"OutputQuantity\"<>0 OR NEW.\"Labor\"+NEW.\"Utility\"+NEW.\"Overhead\"<>0))","(NEW.\"Kind\"='Close' AND (NEW.\"Amount\"<>0 OR NEW.\"OutputQuantity\"<>0 OR NEW.\"Labor\"+NEW.\"Utility\"+NEW.\"Overhead\"<>0)) OR (NEW.\"Kind\"='Reverse' AND (NEW.\"Amount\"<>0 OR NEW.\"Labor\"+NEW.\"Utility\"+NEW.\"Overhead\"<>0))")
                .Replace("movement_value numeric; movement_count integer; original inventory.production_cost_operations%ROWTYPE; original_value numeric;","movement_value numeric; movement_quantity numeric; movement_count integer; original inventory.production_cost_operations%ROWTYPE; original_value numeric;")
                .Replace("SELECT coalesce(sum(m.\"Value\"),0),count(*) INTO movement_value,movement_count FROM inventory.production_stock_movements m WHERE","SELECT coalesce(sum(m.\"Value\"),0),coalesce(sum(m.\"Quantity\"),0),count(*) INTO movement_value,movement_quantity,movement_count FROM inventory.production_stock_movements m WHERE")
                .Replace("expected:=-original_value;","expected:=CASE WHEN original.\"Kind\" IN ('Receipt','ByProduct') THEN -movement_value ELSE -original_value END;")
                .Replace("(original.\"Kind\" IN ('Receipt','ByProduct') AND (movement_count<>1 OR movement_value<>-original.\"PostedValue\"))","(original.\"Kind\" IN ('Receipt','ByProduct') AND (movement_count<>1 OR movement_value<>-o.\"PostedValue\" OR movement_quantity<>-o.\"OutputQuantity\"))")
                .Replace("r.\"Kind\"='Receipt' AND r.\"WarehouseId\"=NEW.\"WarehouseId\" AND r.\"ProductId\"=NEW.\"ProductId\" AND r.\"Quantity\"=-NEW.\"Quantity\" AND r.\"Value\"=-NEW.\"Value\" AND r.\"BusinessDate\"<=NEW.\"BusinessDate\"","r.\"Kind\"='Receipt' AND r.\"WarehouseId\"=NEW.\"WarehouseId\" AND r.\"ProductId\"=NEW.\"ProductId\" AND NEW.\"Quantity\"=-o.\"OutputQuantity\" AND NEW.\"Value\"=-o.\"PostedValue\" AND r.\"BusinessDate\"<=NEW.\"BusinessDate\" AND r.\"Quantity\">=-(SELECT coalesce(sum(x.\"Quantity\"),0) FROM inventory.production_stock_movements x WHERE x.\"TenantId\"=r.\"TenantId\" AND x.\"CompanyId\"=r.\"CompanyId\" AND x.\"OriginalIssueId\"=r.\"Id\" AND x.\"Kind\"='ReceiptReversal') AND r.\"Value\">=-(SELECT coalesce(sum(x.\"Value\"),0) FROM inventory.production_stock_movements x WHERE x.\"TenantId\"=r.\"TenantId\" AND x.\"CompanyId\"=r.\"CompanyId\" AND x.\"OriginalIssueId\"=r.\"Id\" AND x.\"Kind\"='ReceiptReversal') AND -NEW.\"Value\"=CASE WHEN r.\"Quantity\"=-(SELECT coalesce(sum(x.\"Quantity\"),0) FROM inventory.production_stock_movements x WHERE x.\"TenantId\"=r.\"TenantId\" AND x.\"CompanyId\"=r.\"CompanyId\" AND x.\"OriginalIssueId\"=r.\"Id\" AND x.\"Kind\"='ReceiptReversal') THEN r.\"Value\"+(SELECT coalesce(sum(x.\"Value\"),0) FROM inventory.production_stock_movements x WHERE x.\"TenantId\"=r.\"TenantId\" AND x.\"CompanyId\"=r.\"CompanyId\" AND x.\"OriginalIssueId\"=r.\"Id\" AND x.\"Kind\"='ReceiptReversal' AND x.\"Id\"<>NEW.\"Id\") ELSE round(r.\"Value\"*(-NEW.\"Quantity\")/r.\"Quantity\",4) END");
            migrationBuilder.Sql(guards);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN IF EXISTS(SELECT 1 FROM inventory.production_cost_operations WHERE "Kind"='Reverse' AND "OutputQuantity">0) THEN RAISE EXCEPTION 'Cannot discard partial output correction evidence'; END IF; END $$;
                """);
            migrationBuilder.DropIndex(
                name: "IX_production_cost_operations_TenantId_CompanyId_ReversesOpera~",
                schema: "inventory",
                table: "production_cost_operations");

            migrationBuilder.CreateIndex(
                name: "IX_production_cost_operations_TenantId_CompanyId_ReversesOpera~",
                schema: "inventory",
                table: "production_cost_operations",
                columns: new[] { "TenantId", "CompanyId", "ReversesOperationId" },
                unique: true,
                filter: "\"State\"='Posted' AND \"ReversesOperationId\" IS NOT NULL");

            using var reader=new System.IO.StreamReader(typeof(ProductionPartialOutputCorrections).Assembly.GetManifestResourceStream("ProductionOutputGuards.sql")!);
            migrationBuilder.Sql(MaterialReturnGuards(reader.ReadToEnd()));
        }

        internal static string PartialGuards(string guards)=>MaterialReturnGuards(guards)
            .Replace("(NEW.\"Kind\" IN ('Close','Reverse') AND (NEW.\"Amount\"<>0 OR NEW.\"OutputQuantity\"<>0 OR NEW.\"Labor\"+NEW.\"Utility\"+NEW.\"Overhead\"<>0))","(NEW.\"Kind\"='Close' AND (NEW.\"Amount\"<>0 OR NEW.\"OutputQuantity\"<>0 OR NEW.\"Labor\"+NEW.\"Utility\"+NEW.\"Overhead\"<>0)) OR (NEW.\"Kind\"='Reverse' AND (NEW.\"Amount\"<>0 OR NEW.\"Labor\"+NEW.\"Utility\"+NEW.\"Overhead\"<>0))")
            .Replace("movement_value numeric; movement_count integer; original inventory.production_cost_operations%ROWTYPE; original_value numeric;","movement_value numeric; movement_quantity numeric; movement_count integer; original inventory.production_cost_operations%ROWTYPE; original_value numeric;")
            .Replace("SELECT coalesce(sum(m.\"Value\"),0),count(*) INTO movement_value,movement_count FROM inventory.production_stock_movements m WHERE","SELECT coalesce(sum(m.\"Value\"),0),coalesce(sum(m.\"Quantity\"),0),count(*) INTO movement_value,movement_quantity,movement_count FROM inventory.production_stock_movements m WHERE")
            .Replace("expected:=-original_value;","expected:=CASE WHEN original.\"Kind\" IN ('Receipt','ByProduct') THEN -movement_value ELSE -original_value END;")
            .Replace("(original.\"Kind\" IN ('Receipt','ByProduct') AND (movement_count<>1 OR movement_value<>-original.\"PostedValue\"))","(original.\"Kind\" IN ('Receipt','ByProduct') AND (movement_count<>1 OR movement_value<>-o.\"PostedValue\" OR movement_quantity<>-o.\"OutputQuantity\"))")
            .Replace("r.\"Kind\"='Receipt' AND r.\"WarehouseId\"=NEW.\"WarehouseId\" AND r.\"ProductId\"=NEW.\"ProductId\" AND r.\"Quantity\"=-NEW.\"Quantity\" AND r.\"Value\"=-NEW.\"Value\" AND r.\"BusinessDate\"<=NEW.\"BusinessDate\"","r.\"Kind\"='Receipt' AND r.\"WarehouseId\"=NEW.\"WarehouseId\" AND r.\"ProductId\"=NEW.\"ProductId\" AND NEW.\"Quantity\"=-o.\"OutputQuantity\" AND NEW.\"Value\"=-o.\"PostedValue\" AND r.\"BusinessDate\"<=NEW.\"BusinessDate\" AND r.\"Quantity\">=-(SELECT coalesce(sum(x.\"Quantity\"),0) FROM inventory.production_stock_movements x WHERE x.\"TenantId\"=r.\"TenantId\" AND x.\"CompanyId\"=r.\"CompanyId\" AND x.\"OriginalIssueId\"=r.\"Id\" AND x.\"Kind\"='ReceiptReversal') AND r.\"Value\">=-(SELECT coalesce(sum(x.\"Value\"),0) FROM inventory.production_stock_movements x WHERE x.\"TenantId\"=r.\"TenantId\" AND x.\"CompanyId\"=r.\"CompanyId\" AND x.\"OriginalIssueId\"=r.\"Id\" AND x.\"Kind\"='ReceiptReversal') AND -NEW.\"Value\"=CASE WHEN r.\"Quantity\"=-(SELECT coalesce(sum(x.\"Quantity\"),0) FROM inventory.production_stock_movements x WHERE x.\"TenantId\"=r.\"TenantId\" AND x.\"CompanyId\"=r.\"CompanyId\" AND x.\"OriginalIssueId\"=r.\"Id\" AND x.\"Kind\"='ReceiptReversal') THEN r.\"Value\"+(SELECT coalesce(sum(x.\"Value\"),0) FROM inventory.production_stock_movements x WHERE x.\"TenantId\"=r.\"TenantId\" AND x.\"CompanyId\"=r.\"CompanyId\" AND x.\"OriginalIssueId\"=r.\"Id\" AND x.\"Kind\"='ReceiptReversal' AND x.\"Id\"<>NEW.\"Id\") ELSE round(r.\"Value\"*(-NEW.\"Quantity\")/r.\"Quantity\",4) END");

        private static string MaterialReturnGuards(string guards)=>guards
            .Replace("original.\"Kind\" NOT IN ('Conversion','Loss','Receipt','ByProduct')","original.\"Kind\" NOT IN ('Return','Conversion','Loss','Receipt','ByProduct')")
            .Replace("(original.\"Kind\" IN ('Receipt','ByProduct') AND (movement_count<>1 OR movement_value<>-original.\"PostedValue\")) OR (original.\"Kind\" NOT IN ('Receipt','ByProduct') AND movement_count<>0)","(original.\"Kind\" IN ('Receipt','ByProduct') AND (movement_count<>1 OR movement_value<>-original.\"PostedValue\")) OR (original.\"Kind\"='Return' AND (movement_count<>jsonb_array_length(original.\"Lines\") OR movement_value<>-original.\"PostedValue\")) OR (original.\"Kind\" NOT IN ('Return','Receipt','ByProduct') AND movement_count<>0)")
            .Replace("(o.\"Kind\"='Reverse' AND original_kind IN ('Receipt','ByProduct') AND NEW.\"Kind\"='ReceiptReversal')","(o.\"Kind\"='Reverse' AND ((original_kind IN ('Receipt','ByProduct') AND NEW.\"Kind\"='ReceiptReversal') OR (original_kind='Return' AND NEW.\"Kind\"='ReturnReversal')))")
            .Replace("  ELSIF NEW.\"Kind\" IN ('Issue','Return') THEN","  ELSIF NEW.\"Kind\"='ReturnReversal' THEN\n    IF NOT EXISTS(SELECT 1 FROM inventory.production_stock_movements r WHERE r.\"TenantId\"=NEW.\"TenantId\" AND r.\"CompanyId\"=NEW.\"CompanyId\" AND r.\"SessionId\"=NEW.\"SessionId\" AND r.\"OperationId\"=o.\"ReversesOperationId\" AND r.\"Id\"=NEW.\"OriginalIssueId\" AND r.\"Kind\"='Return' AND r.\"WarehouseId\"=NEW.\"WarehouseId\" AND r.\"ProductId\"=NEW.\"ProductId\" AND r.\"Quantity\"=-NEW.\"Quantity\" AND r.\"Value\"=-NEW.\"Value\" AND r.\"BusinessDate\"<=NEW.\"BusinessDate\") THEN RAISE EXCEPTION 'Material return reversal must exactly compensate original return'; END IF;\n  ELSIF NEW.\"Kind\" IN ('Issue','Return') THEN");
    }
}
