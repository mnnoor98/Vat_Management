using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FullReceiptCostBasis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_cost_adoption_basis",
                schema: "inventory",
                table: "cost_adoptions");

            migrationBuilder.CreateTable(
                name: "cost_landed_reviews",
                schema: "inventory",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(24,4)", precision: 24, scale: 4, nullable: false),
                    Lines = table.Column<string>(type: "jsonb", nullable: false),
                    Evidence = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ApprovedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cost_landed_reviews", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_landed_amount", "\"TotalAmount\">=0");
                    table.CheckConstraint("ck_landed_state", "(\"State\"='Draft' AND \"Version\"=1 AND \"ApprovedBy\" IS NULL AND \"ApprovedAt\" IS NULL) OR (\"State\"='Approved' AND \"Version\"=2 AND \"ApprovedBy\" IS NOT NULL AND \"ApprovedAt\" IS NOT NULL AND \"ApprovedBy\"<>\"CreatedBy\")");
                    table.ForeignKey(
                        name: "FK_cost_landed_reviews_purchase_posting_plans_TenantId_Company~",
                        columns: x => new { x.TenantId, x.CompanyId, x.PlanId },
                        principalSchema: "purchasing",
                        principalTable: "purchase_posting_plans",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_cost_adoption_basis",
                schema: "inventory",
                table: "cost_adoptions",
                sql: "\"Basis\" IN ('PurchaseNetOnly','NetPlusNonrecoverableTaxAndLanded')");

            migrationBuilder.CreateIndex(
                name: "IX_cost_landed_reviews_TenantId_CompanyId_PlanId",
                schema: "inventory",
                table: "cost_landed_reviews",
                columns: new[] { "TenantId", "CompanyId", "PlanId" },
                unique: true,
                filter: "\"State\"='Approved'");
            migrationBuilder.Sql("""
                CREATE FUNCTION inventory.freeze_landed_cost_review() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE snapshot jsonb; source_state text; total numeric;
                BEGIN
                  IF TG_OP='INSERT' THEN
                    IF NEW."State"<>'Draft' THEN RAISE EXCEPTION 'Landed cost review must start as draft'; END IF;
                  ELSIF OLD."State"<>'Draft' OR NEW."State"<>'Approved' OR
                    (to_jsonb(NEW)-ARRAY['State','Version','ApprovedBy','ApprovedAt']) IS DISTINCT FROM (to_jsonb(OLD)-ARRAY['State','Version','ApprovedBy','ApprovedAt']) THEN
                    RAISE EXCEPTION 'Only unchanged independent landed-cost approval is permitted';
                  END IF;
                  SELECT p."Snapshot",p."Status" INTO snapshot,source_state FROM purchasing.purchase_posting_plans p
                    WHERE p."TenantId"=NEW."TenantId" AND p."CompanyId"=NEW."CompanyId" AND p."Id"=NEW."PlanId";
                  IF source_state IS DISTINCT FROM 'Prepared' OR jsonb_typeof(NEW."Lines")<>'array' OR
                    jsonb_array_length(NEW."Lines")<>jsonb_array_length(snapshot->'lines') THEN RAISE EXCEPTION 'Landed cost requires every line of a prepared receipt'; END IF;
                  IF EXISTS(SELECT 1 FROM jsonb_array_elements(NEW."Lines") x WHERE (x->>'Amount')::numeric<0 OR round((x->>'Amount')::numeric,4)<>(x->>'Amount')::numeric OR
                    NOT EXISTS(SELECT 1 FROM jsonb_array_elements(snapshot->'lines') p WHERE (p->>'purchaseLineId')::uuid=(x->>'PurchaseLineId')::uuid)) OR
                    (SELECT count(DISTINCT x->>'PurchaseLineId') FROM jsonb_array_elements(NEW."Lines") x)<>jsonb_array_length(NEW."Lines") THEN
                    RAISE EXCEPTION 'Invalid landed-cost allocation lines';
                  END IF;
                  SELECT sum((x->>'Amount')::numeric) INTO total FROM jsonb_array_elements(NEW."Lines") x;
                  IF total IS DISTINCT FROM NEW."TotalAmount" THEN RAISE EXCEPTION 'Landed-cost allocations do not equal declared total'; END IF;
                  RETURN NEW;
                END $$;
                CREATE TRIGGER freeze_landed_cost_review BEFORE INSERT OR UPDATE ON inventory.cost_landed_reviews FOR EACH ROW EXECUTE FUNCTION inventory.freeze_landed_cost_review();
                CREATE TRIGGER immutable_landed_review_delete BEFORE DELETE ON inventory.cost_landed_reviews FOR EACH ROW EXECUTE FUNCTION inventory.reject_costing_evidence_mutation();
                CREATE TRIGGER immutable_landed_review_truncate BEFORE TRUNCATE ON inventory.cost_landed_reviews FOR EACH STATEMENT EXECUTE FUNCTION inventory.reject_costing_evidence_mutation();
                CREATE FUNCTION inventory.protect_stock_cost_policy_history() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF NEW."State"='Approved' AND EXISTS(SELECT 1 FROM inventory.cost_entries e WHERE e."TenantId"=NEW."TenantId" AND e."CompanyId"=NEW."CompanyId"
                    AND (NEW."ProductId" IS NULL OR e."ProductId"=NEW."ProductId") AND e."BusinessDate">=NEW."EffectiveFrom" AND e."BusinessDate"<NEW."EffectiveUntil") THEN
                    RAISE EXCEPTION 'Approved cost ledger history cannot be reassigned';
                  END IF;
                  RETURN NEW;
                END $$;
                CREATE TRIGGER protect_stock_cost_policy_history BEFORE UPDATE ON inventory.costing_policies FOR EACH ROW EXECUTE FUNCTION inventory.protect_stock_cost_policy_history();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION inventory.protect_stock_cost_policy_history() CASCADE; DROP FUNCTION inventory.freeze_landed_cost_review() CASCADE;");
            migrationBuilder.DropTable(
                name: "cost_landed_reviews",
                schema: "inventory");

            migrationBuilder.DropCheckConstraint(
                name: "ck_cost_adoption_basis",
                schema: "inventory",
                table: "cost_adoptions");

            migrationBuilder.AddCheckConstraint(
                name: "ck_cost_adoption_basis",
                schema: "inventory",
                table: "cost_adoptions",
                sql: "\"Basis\" IN ('PurchaseNetOnly','NetPlusNonrecoverableTax')");
        }
    }
}
