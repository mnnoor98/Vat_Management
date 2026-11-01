using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OrderedCostSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cost_source_heads",
                schema: "inventory",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    EmptyOpeningProven = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cost_source_heads", x => new { x.TenantId, x.CompanyId });
                    table.CheckConstraint("ck_cost_source_sequence", "\"Sequence\" >= 0");
                    table.ForeignKey(
                        name: "FK_cost_source_heads_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cost_source_events",
                schema: "inventory",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    ReceiptId = table.Column<Guid>(type: "uuid", nullable: true),
                    SalesEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(24,6)", precision: 24, scale: 6, nullable: false),
                    PolicyId = table.Column<Guid>(type: "uuid", nullable: true),
                    PolicyVersion = table.Column<int>(type: "integer", nullable: true),
                    Method = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cost_source_events", x => new { x.TenantId, x.CompanyId, x.Sequence });
                    table.CheckConstraint("ck_cost_source_kind", "\"Sequence\" > 0 AND ((\"ReceiptId\" IS NOT NULL AND \"SalesEntryId\" IS NULL AND \"Quantity\" > 0) OR (\"ReceiptId\" IS NULL AND \"SalesEntryId\" IS NOT NULL AND \"Quantity\" < 0))");
                    table.CheckConstraint("ck_cost_source_policy", "(\"PolicyId\" IS NULL AND \"PolicyVersion\" IS NULL AND \"Method\" IS NULL) OR (\"PolicyId\" IS NOT NULL AND \"PolicyVersion\" IS NOT NULL AND \"Method\" IS NOT NULL AND \"PolicyVersion\" = 2 AND \"Method\" IN ('FIFO','WEIGHTED_AVERAGE'))");
                    table.ForeignKey(
                        name: "FK_cost_source_events_cost_source_heads_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "inventory",
                        principalTable: "cost_source_heads",
                        principalColumns: new[] { "TenantId", "CompanyId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cost_source_events_costing_policies_TenantId_CompanyId_Poli~",
                        columns: x => new { x.TenantId, x.CompanyId, x.PolicyId },
                        principalSchema: "inventory",
                        principalTable: "costing_policies",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cost_source_events_posting_entries_TenantId_CompanyId_Sales~",
                        columns: x => new { x.TenantId, x.CompanyId, x.SalesEntryId },
                        principalSchema: "sales",
                        principalTable: "posting_entries",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cost_source_events_purchase_cost_receipts_TenantId_CompanyI~",
                        columns: x => new { x.TenantId, x.CompanyId, x.ReceiptId },
                        principalSchema: "inventory",
                        principalTable: "purchase_cost_receipts",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cost_source_events_TenantId_CompanyId_PolicyId",
                schema: "inventory",
                table: "cost_source_events",
                columns: new[] { "TenantId", "CompanyId", "PolicyId" });

            migrationBuilder.CreateIndex(
                name: "IX_cost_source_events_TenantId_CompanyId_ReceiptId",
                schema: "inventory",
                table: "cost_source_events",
                columns: new[] { "TenantId", "CompanyId", "ReceiptId" },
                unique: true,
                filter: "\"ReceiptId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_cost_source_events_TenantId_CompanyId_SalesEntryId",
                schema: "inventory",
                table: "cost_source_events",
                columns: new[] { "TenantId", "CompanyId", "SalesEntryId" },
                unique: true,
                filter: "\"SalesEntryId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_cost_source_events_TenantId_CompanyId_WarehouseId_ProductId~",
                schema: "inventory",
                table: "cost_source_events",
                columns: new[] { "TenantId", "CompanyId", "WarehouseId", "ProductId", "Sequence" });

            migrationBuilder.Sql("""
                CREATE FUNCTION inventory.freeze_cost_source_head() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF ROW(NEW."TenantId",NEW."CompanyId",NEW."EmptyOpeningProven",NEW."CreatedAt") IS DISTINCT FROM
                     ROW(OLD."TenantId",OLD."CompanyId",OLD."EmptyOpeningProven",OLD."CreatedAt") OR NEW."Sequence" <= OLD."Sequence" THEN
                    RAISE EXCEPTION 'Only forward cost source sequence advancement is permitted';
                  END IF;
                  RETURN NEW;
                END $$;
                CREATE TRIGGER freeze_cost_source_head BEFORE UPDATE ON inventory.cost_source_heads
                  FOR EACH ROW EXECUTE FUNCTION inventory.freeze_cost_source_head();
                CREATE TRIGGER immutable_cost_source_head BEFORE DELETE ON inventory.cost_source_heads
                  FOR EACH ROW EXECUTE FUNCTION inventory.reject_costing_evidence_mutation();
                CREATE TRIGGER immutable_cost_source_head_truncate BEFORE TRUNCATE ON inventory.cost_source_heads
                  FOR EACH STATEMENT EXECUTE FUNCTION inventory.reject_costing_evidence_mutation();
                CREATE TRIGGER immutable_cost_source_events BEFORE UPDATE OR DELETE ON inventory.cost_source_events
                  FOR EACH ROW EXECUTE FUNCTION inventory.reject_costing_evidence_mutation();
                CREATE TRIGGER immutable_cost_source_events_truncate BEFORE TRUNCATE ON inventory.cost_source_events
                  FOR EACH STATEMENT EXECUTE FUNCTION inventory.reject_costing_evidence_mutation();

                CREATE FUNCTION inventory.validate_cost_source_event() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE resolved_policy uuid; sale_date date;
                BEGIN
                  IF NEW."ReceiptId" IS NOT NULL THEN
                    IF NOT EXISTS (SELECT 1 FROM inventory.purchase_cost_receipts r
                      WHERE r."TenantId"=NEW."TenantId" AND r."CompanyId"=NEW."CompanyId" AND r."Id"=NEW."ReceiptId"
                        AND r."WarehouseId"=NEW."WarehouseId" AND r."ProductId"=NEW."ProductId" AND r."Quantity"=NEW."Quantity"
                        AND r."PolicyId" IS NOT DISTINCT FROM NEW."PolicyId"
                        AND r."PolicyVersion" IS NOT DISTINCT FROM NEW."PolicyVersion" AND r."Method" IS NOT DISTINCT FROM NEW."Method") THEN
                      RAISE EXCEPTION 'Cost source does not match its purchase receipt';
                    END IF;
                  ELSE
                    SELECT s."BusinessDate" INTO sale_date FROM sales.posting_entries s
                      WHERE s."TenantId"=NEW."TenantId" AND s."CompanyId"=NEW."CompanyId" AND s."Id"=NEW."SalesEntryId"
                        AND s."WarehouseId"=NEW."WarehouseId" AND s."ProductId"=NEW."ProductId" AND s."Quantity"=NEW."Quantity"
                        AND s."Quantity"<0 AND s."CreditNoteId" IS NULL AND s."DebitNoteId" IS NULL;
                    IF sale_date IS NULL THEN RAISE EXCEPTION 'Cost source does not match an original sales issue'; END IF;
                    SELECT p."Id" INTO resolved_policy FROM inventory.costing_policies p
                      WHERE p."TenantId"=NEW."TenantId" AND p."CompanyId"=NEW."CompanyId" AND p."State"='Approved'
                        AND (p."ProductId" IS NULL OR p."ProductId"=NEW."ProductId")
                        AND p."EffectiveFrom"<=sale_date AND sale_date<p."EffectiveUntil"
                      ORDER BY (p."ProductId" IS NOT NULL) DESC LIMIT 1;
                    IF NEW."PolicyId" IS DISTINCT FROM resolved_policy THEN RAISE EXCEPTION 'Sales cost policy mismatch'; END IF;
                    IF NEW."PolicyId" IS NOT NULL AND NOT EXISTS (SELECT 1 FROM inventory.costing_policies p
                      WHERE p."TenantId"=NEW."TenantId" AND p."CompanyId"=NEW."CompanyId" AND p."Id"=NEW."PolicyId"
                        AND p."Version"=NEW."PolicyVersion" AND p."Method"=NEW."Method") THEN
                      RAISE EXCEPTION 'Sales cost policy snapshot mismatch';
                    END IF;
                  END IF;
                  RETURN NEW;
                END $$;
                CREATE TRIGGER validate_cost_source_event BEFORE INSERT ON inventory.cost_source_events
                  FOR EACH ROW EXECUTE FUNCTION inventory.validate_cost_source_event();

                CREATE FUNCTION inventory.validate_cost_source_clock_tail() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE previous_sequence bigint := 0; source_count bigint;
                BEGIN
                  IF TG_OP='UPDATE' THEN previous_sequence := OLD."Sequence"; END IF;
                  SELECT count(*) INTO source_count FROM inventory.cost_source_events e
                    WHERE e."TenantId"=NEW."TenantId" AND e."CompanyId"=NEW."CompanyId"
                      AND e."Sequence">previous_sequence AND e."Sequence"<=NEW."Sequence";
                  IF source_count <> NEW."Sequence"-previous_sequence THEN RAISE EXCEPTION 'Cost source sequence contains a gap'; END IF;
                  RETURN NEW;
                END $$;
                CREATE CONSTRAINT TRIGGER validate_cost_source_clock_tail AFTER INSERT OR UPDATE ON inventory.cost_source_heads
                  DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION inventory.validate_cost_source_clock_tail();
                CREATE FUNCTION inventory.validate_cost_source_event_clock() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF NOT EXISTS (SELECT 1 FROM inventory.cost_source_heads h
                    WHERE h."TenantId"=NEW."TenantId" AND h."CompanyId"=NEW."CompanyId" AND h."Sequence">=NEW."Sequence") THEN
                    RAISE EXCEPTION 'Cost source event exceeds its company sequence';
                  END IF;
                  RETURN NEW;
                END $$;
                CREATE CONSTRAINT TRIGGER validate_cost_source_event_clock AFTER INSERT ON inventory.cost_source_events
                  DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION inventory.validate_cost_source_event_clock();

                CREATE FUNCTION inventory.protect_sales_cost_policy_history() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF NEW."State"='Approved' AND EXISTS (
                    SELECT 1 FROM inventory.cost_source_events e JOIN sales.posting_entries s
                      ON s."TenantId"=e."TenantId" AND s."CompanyId"=e."CompanyId" AND s."Id"=e."SalesEntryId"
                    WHERE e."TenantId"=NEW."TenantId" AND e."CompanyId"=NEW."CompanyId"
                      AND (NEW."ProductId" IS NULL OR e."ProductId"=NEW."ProductId")
                      AND s."BusinessDate">=NEW."EffectiveFrom" AND s."BusinessDate"<NEW."EffectiveUntil") THEN
                    RAISE EXCEPTION 'Captured sales costing history cannot be reassigned';
                  END IF;
                  RETURN NEW;
                END $$;
                CREATE TRIGGER protect_sales_cost_policy_history BEFORE UPDATE ON inventory.costing_policies
                  FOR EACH ROW EXECUTE FUNCTION inventory.protect_sales_cost_policy_history();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP FUNCTION inventory.protect_sales_cost_policy_history() CASCADE;
                DROP FUNCTION inventory.validate_cost_source_event_clock() CASCADE;
                DROP FUNCTION inventory.validate_cost_source_clock_tail() CASCADE;
                DROP FUNCTION inventory.validate_cost_source_event() CASCADE;
                DROP FUNCTION inventory.freeze_cost_source_head() CASCADE;
                """);
            migrationBuilder.DropTable(
                name: "cost_source_events",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "cost_source_heads",
                schema: "inventory");
        }
    }
}
