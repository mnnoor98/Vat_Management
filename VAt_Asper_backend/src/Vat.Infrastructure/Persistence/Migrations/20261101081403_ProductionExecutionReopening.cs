using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProductionExecutionReopening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_production_operation_kind",
                schema: "inventory",
                table: "production_cost_operations");

            migrationBuilder.AddCheckConstraint(
                name: "ck_production_operation_kind",
                schema: "inventory",
                table: "production_cost_operations",
                sql: "\"Kind\" IN ('Issue','Return','Conversion','Receipt','ByProduct','Loss','Close','Reopen','Reverse')");

            using var reader=new System.IO.StreamReader(typeof(ProductionExecutionReopening).Assembly.GetManifestResourceStream("ProductionOutputGuards.sql")!);
            var guards=ProductionPartialOutputCorrections.PartialGuards(reader.ReadToEnd())
                .Replace("(NEW.\"Kind\"='Close' AND (NEW.\"Amount\"<>0 OR NEW.\"OutputQuantity\"<>0 OR NEW.\"Labor\"+NEW.\"Utility\"+NEW.\"Overhead\"<>0))","(NEW.\"Kind\" IN ('Close','Reopen') AND (NEW.\"Amount\"<>0 OR NEW.\"OutputQuantity\"<>0 OR NEW.\"Labor\"+NEW.\"Utility\"+NEW.\"Overhead\"<>0))")
                .Replace("(o.\"Kind\" IN ('Conversion','Loss','Close') AND movement_count<>0)","(o.\"Kind\" IN ('Conversion','Loss','Close','Reopen') AND movement_count<>0)")
                .Replace("  IF o.\"Kind\"='Close' AND (","  IF o.\"Kind\"='Reopen' AND NOT EXISTS(SELECT 1 FROM inventory.production_cost_sessions h WHERE h.\"TenantId\"=NEW.\"TenantId\" AND h.\"CompanyId\"=NEW.\"CompanyId\" AND h.\"Id\"=NEW.\"SessionId\" AND h.\"State\"='Open') THEN RAISE EXCEPTION 'Approved reopen must transition the execution to open'; END IF;\n  IF o.\"Kind\"='Close' AND (");
            migrationBuilder.Sql(guards);
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION inventory.freeze_production_session() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF TG_OP='INSERT' THEN
                    IF NEW."Version"<>1 OR NEW."State"<>'Open' OR NEW."WipValue"<>0 OR NEW."ReceivedQuantity"<>0 OR
                      (NEW."SourceJson"->>'tenantId')::uuid IS DISTINCT FROM NEW."TenantId" OR (NEW."SourceJson"->>'companyId')::uuid IS DISTINCT FROM NEW."CompanyId" OR (NEW."SourceJson"->>'orderId')::uuid IS DISTINCT FROM NEW."OrderId" OR
                      (NEW."SourceJson"->>'state') IS DISTINCT FROM 'Approved' OR (NEW."SourceJson"->>'version')::integer IS DISTINCT FROM 3 OR
                      NOT EXISTS(SELECT 1 FROM inventory.cost_ledger_heads h WHERE h."TenantId"=NEW."TenantId" AND h."CompanyId"=NEW."CompanyId") THEN RAISE EXCEPTION 'Production requires an approved scoped source and active cost ledger'; END IF;
                  ELSIF OLD."State" NOT IN ('Open','Closed') OR NEW."Version"<>OLD."Version"+1 OR
                    (OLD."State"='Closed' AND NEW."State"<>'Open') OR (OLD."State"='Open' AND NEW."State" NOT IN ('Open','Closed')) OR
                    (to_jsonb(NEW)-ARRAY['State','Version','WipValue','ReceivedQuantity']) IS DISTINCT FROM (to_jsonb(OLD)-ARRAY['State','Version','WipValue','ReceivedQuantity']) THEN
                    RAISE EXCEPTION 'Only sequenced execution changes are permitted; approved source is frozen';
                  END IF;
                  RETURN NEW;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN IF EXISTS(SELECT 1 FROM inventory.production_cost_operations WHERE "Kind"='Reopen') THEN RAISE EXCEPTION 'Cannot discard reviewed execution reopening evidence'; END IF; END $$;
                """);
            migrationBuilder.DropCheckConstraint(
                name: "ck_production_operation_kind",
                schema: "inventory",
                table: "production_cost_operations");

            migrationBuilder.AddCheckConstraint(
                name: "ck_production_operation_kind",
                schema: "inventory",
                table: "production_cost_operations",
                sql: "\"Kind\" IN ('Issue','Return','Conversion','Receipt','ByProduct','Loss','Close','Reverse')");

            using var reader=new System.IO.StreamReader(typeof(ProductionExecutionReopening).Assembly.GetManifestResourceStream("ProductionOutputGuards.sql")!);
            migrationBuilder.Sql(ProductionPartialOutputCorrections.PartialGuards(reader.ReadToEnd()));
            using var sessionReader=new System.IO.StreamReader(typeof(ProductionExecutionReopening).Assembly.GetManifestResourceStream("ProductionReversalGuards.sql")!);
            migrationBuilder.Sql(sessionReader.ReadToEnd().Split("CREATE OR REPLACE FUNCTION inventory.freeze_production_operation",System.StringSplitOptions.None)[0]);
        }
    }
}
