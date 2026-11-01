using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PhysicalProductionOutputs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_production_operation_kind",
                schema: "inventory",
                table: "production_cost_operations");

            migrationBuilder.AddColumn<Guid>(
                name: "OutputProductId",
                schema: "inventory",
                table: "production_cost_operations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OutputWarehouseId",
                schema: "inventory",
                table: "production_cost_operations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_production_operation_kind",
                schema: "inventory",
                table: "production_cost_operations",
                sql: "\"Kind\" IN ('Issue','Return','Conversion','Receipt','ByProduct','Loss','Close','Reverse')");
            using var reader=new System.IO.StreamReader(typeof(PhysicalProductionOutputs).Assembly.GetManifestResourceStream("ProductionOutputGuards.sql")!);
            migrationBuilder.Sql(reader.ReadToEnd());
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS(SELECT 1 FROM inventory.production_cost_sessions WHERE "SourceJson"->'evidence'->'byProducts' IS NOT NULL)
                    OR EXISTS(SELECT 1 FROM inventory.production_cost_operations WHERE "Kind"='ByProduct') THEN RAISE EXCEPTION 'Cannot discard frozen physical production output evidence; use reviewed recovery'; END IF;
                END $$;
                """);
            migrationBuilder.DropCheckConstraint(
                name: "ck_production_operation_kind",
                schema: "inventory",
                table: "production_cost_operations");

            migrationBuilder.DropColumn(
                name: "OutputProductId",
                schema: "inventory",
                table: "production_cost_operations");

            migrationBuilder.DropColumn(
                name: "OutputWarehouseId",
                schema: "inventory",
                table: "production_cost_operations");

            migrationBuilder.AddCheckConstraint(
                name: "ck_production_operation_kind",
                schema: "inventory",
                table: "production_cost_operations",
                sql: "\"Kind\" IN ('Issue','Return','Conversion','Receipt','Loss','Close','Reverse')");
            using(var reversal=new System.IO.StreamReader(typeof(PhysicalProductionOutputs).Assembly.GetManifestResourceStream("ProductionReversalGuards.sql")!))migrationBuilder.Sql(reversal.ReadToEnd());
            using(var units=new System.IO.StreamReader(typeof(PhysicalProductionOutputs).Assembly.GetManifestResourceStream("ProductionUomGuards.sql")!))migrationBuilder.Sql(units.ReadToEnd());
        }
    }
}
