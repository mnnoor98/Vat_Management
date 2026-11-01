using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProductionInputUnits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "OutputInputQuantity",
                schema: "inventory",
                table: "production_cost_operations",
                type: "numeric(24,6)",
                precision: 24,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OutputUnitOfMeasure",
                schema: "inventory",
                table: "production_cost_operations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
            using var reader=new System.IO.StreamReader(typeof(ProductionInputUnits).Assembly.GetManifestResourceStream("ProductionUomGuards.sql")!);
            migrationBuilder.Sql(reader.ReadToEnd());
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS(SELECT 1 FROM inventory.production_cost_sessions WHERE "SourceJson"->'evidence'->'uomConversions' IS NOT NULL)
                    OR EXISTS(SELECT 1 FROM inventory.production_cost_operations o WHERE o."OutputUnitOfMeasure" IS NOT NULL OR EXISTS(SELECT 1 FROM jsonb_array_elements(o."Lines") x WHERE x->>'inputUnitOfMeasure' IS NOT NULL)) THEN RAISE EXCEPTION 'Cannot discard frozen production UOM evidence; use reviewed recovery'; END IF;
                END $$;
                DROP TRIGGER validate_production_uom ON inventory.production_cost_operations;
                DROP FUNCTION inventory.validate_production_uom();
                DROP FUNCTION inventory.production_uom_stock_quantity(jsonb,uuid,numeric,text);
                """);
            migrationBuilder.DropColumn(
                name: "OutputInputQuantity",
                schema: "inventory",
                table: "production_cost_operations");

            migrationBuilder.DropColumn(
                name: "OutputUnitOfMeasure",
                schema: "inventory",
                table: "production_cost_operations");
        }
    }
}
