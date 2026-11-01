using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CostingPolicyReceiptEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "costing_policies",
                schema: "inventory",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: true),
                    Method = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveUntil = table.Column<DateOnly>(type: "date", nullable: false),
                    State = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ApprovedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_costing_policies", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_cost_policy_dates", "\"EffectiveFrom\" < \"EffectiveUntil\"");
                    table.CheckConstraint("ck_cost_policy_method", "\"Method\" IN ('FIFO','WEIGHTED_AVERAGE')");
                    table.CheckConstraint("ck_cost_policy_state", "(\"State\" = 'Draft' AND \"Version\" = 1 AND \"ApprovedBy\" IS NULL AND \"ApprovedAt\" IS NULL) OR (\"State\" = 'Approved' AND \"Version\" = 2 AND \"ApprovedBy\" IS NOT NULL AND \"ApprovedBy\" <> \"CreatedBy\" AND \"ApprovedAt\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_costing_policies_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_costing_policies_products_TenantId_CompanyId_ProductId",
                        columns: x => new { x.TenantId, x.CompanyId, x.ProductId },
                        principalSchema: "masterdata",
                        principalTable: "products",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_cost_receipts",
                schema: "inventory",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceMovementId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseLineId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(24,6)", precision: 24, scale: 6, nullable: false),
                    NetAmount = table.Column<decimal>(type: "numeric(24,4)", precision: 24, scale: 4, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(24,4)", precision: 24, scale: 4, nullable: false),
                    RecoverableTaxAmount = table.Column<decimal>(type: "numeric(24,4)", precision: 24, scale: 4, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    PolicyId = table.Column<Guid>(type: "uuid", nullable: true),
                    PolicyVersion = table.Column<int>(type: "integer", nullable: true),
                    Method = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_cost_receipts", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_cost_receipt_amounts", "\"Quantity\" > 0 AND \"NetAmount\" >= 0 AND \"TaxAmount\" >= 0 AND \"RecoverableTaxAmount\" >= 0 AND \"RecoverableTaxAmount\" <= \"TaxAmount\"");
                    table.CheckConstraint("ck_cost_receipt_policy", "(\"PolicyId\" IS NULL AND \"PolicyVersion\" IS NULL AND \"Method\" IS NULL) OR (\"PolicyId\" IS NOT NULL AND \"PolicyVersion\" IS NOT NULL AND \"Method\" IS NOT NULL AND \"PolicyVersion\" = 2 AND \"Method\" IN ('FIFO','WEIGHTED_AVERAGE'))");
                    table.ForeignKey(
                        name: "FK_purchase_cost_receipts_costing_policies_TenantId_CompanyId_~",
                        columns: x => new { x.TenantId, x.CompanyId, x.PolicyId },
                        principalSchema: "inventory",
                        principalTable: "costing_policies",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_cost_receipts_stock_movements_TenantId_CompanyId_S~",
                        columns: x => new { x.TenantId, x.CompanyId, x.SourceMovementId },
                        principalSchema: "inventory",
                        principalTable: "stock_movements",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_costing_policies_TenantId_CompanyId_ProductId_EffectiveFrom",
                schema: "inventory",
                table: "costing_policies",
                columns: new[] { "TenantId", "CompanyId", "ProductId", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_cost_receipts_TenantId_CompanyId_PolicyId",
                schema: "inventory",
                table: "purchase_cost_receipts",
                columns: new[] { "TenantId", "CompanyId", "PolicyId" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_cost_receipts_TenantId_CompanyId_ProductId_Busines~",
                schema: "inventory",
                table: "purchase_cost_receipts",
                columns: new[] { "TenantId", "CompanyId", "ProductId", "BusinessDate" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_cost_receipts_TenantId_CompanyId_SourceMovementId",
                schema: "inventory",
                table: "purchase_cost_receipts",
                columns: new[] { "TenantId", "CompanyId", "SourceMovementId" },
                unique: true);
            migrationBuilder.Sql("""
            CREATE FUNCTION inventory.reject_costing_evidence_mutation() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'Costing evidence is immutable'; END $$;

            CREATE FUNCTION inventory.freeze_costing_policy() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
              IF TG_OP = 'DELETE' THEN RAISE EXCEPTION 'Costing policy history cannot be deleted'; END IF;
              IF TG_OP = 'INSERT' THEN
                IF NEW."State" <> 'Draft' THEN RAISE EXCEPTION 'A costing policy must start as Draft'; END IF;
                RETURN NEW;
              END IF;
              IF OLD."State" <> 'Draft' OR NEW."State" <> 'Approved' THEN
                RAISE EXCEPTION 'Only draft-to-approved policy transitions are permitted';
              END IF;
              IF ROW(NEW."TenantId",NEW."CompanyId",NEW."Id",NEW."ProductId",NEW."Method",NEW."EffectiveFrom",NEW."EffectiveUntil",NEW."CreatedBy",NEW."CreatedAt")
                IS DISTINCT FROM ROW(OLD."TenantId",OLD."CompanyId",OLD."Id",OLD."ProductId",OLD."Method",OLD."EffectiveFrom",OLD."EffectiveUntil",OLD."CreatedBy",OLD."CreatedAt") THEN
                RAISE EXCEPTION 'Policy source fields cannot change during approval';
              END IF;
              PERFORM pg_advisory_xact_lock(hashtextextended('costing-db-policy:' || NEW."TenantId"::text || ':' || NEW."CompanyId"::text,0));
              IF EXISTS (SELECT 1 FROM inventory.costing_policies p WHERE p."TenantId"=NEW."TenantId" AND p."CompanyId"=NEW."CompanyId"
                AND p."Id"<>NEW."Id" AND p."ProductId" IS NOT DISTINCT FROM NEW."ProductId" AND p."State"='Approved'
                AND p."EffectiveFrom"<NEW."EffectiveUntil" AND NEW."EffectiveFrom"<p."EffectiveUntil") THEN
                RAISE EXCEPTION 'Approved costing policy ranges overlap';
              END IF;
              IF EXISTS (SELECT 1 FROM inventory.purchase_cost_receipts r WHERE r."TenantId"=NEW."TenantId" AND r."CompanyId"=NEW."CompanyId"
                AND (NEW."ProductId" IS NULL OR r."ProductId"=NEW."ProductId")
                AND r."BusinessDate">=NEW."EffectiveFrom" AND r."BusinessDate"<NEW."EffectiveUntil") THEN
                RAISE EXCEPTION 'Captured costing history cannot be reassigned';
              END IF;
              RETURN NEW;
            END $$;

            CREATE TRIGGER freeze_costing_policy BEFORE INSERT OR UPDATE OR DELETE ON inventory.costing_policies
            FOR EACH ROW EXECUTE FUNCTION inventory.freeze_costing_policy();
            CREATE TRIGGER freeze_costing_policy_truncate BEFORE TRUNCATE ON inventory.costing_policies
            FOR EACH STATEMENT EXECUTE FUNCTION inventory.reject_costing_evidence_mutation();
            CREATE TRIGGER immutable_purchase_cost_receipts BEFORE UPDATE OR DELETE ON inventory.purchase_cost_receipts
            FOR EACH ROW EXECUTE FUNCTION inventory.reject_costing_evidence_mutation();
            CREATE TRIGGER immutable_purchase_cost_receipts_truncate BEFORE TRUNCATE ON inventory.purchase_cost_receipts
            FOR EACH STATEMENT EXECUTE FUNCTION inventory.reject_costing_evidence_mutation();

            CREATE FUNCTION inventory.validate_purchase_cost_receipt() RETURNS trigger LANGUAGE plpgsql AS $$
            DECLARE resolved_policy uuid;
            BEGIN
              IF NOT EXISTS (SELECT 1 FROM inventory.stock_movements m WHERE m."TenantId"=NEW."TenantId" AND m."CompanyId"=NEW."CompanyId"
                AND m."Id"=NEW."SourceMovementId" AND m."PurchaseId"=NEW."PurchaseId" AND m."PurchaseLineId"=NEW."PurchaseLineId"
                AND m."PlanId"=NEW."PlanId" AND m."WarehouseId"=NEW."WarehouseId" AND m."ProductId"=NEW."ProductId"
                AND m."BusinessDate"=NEW."BusinessDate" AND m."Quantity"=NEW."Quantity" AND m."ReversesEntryId" IS NULL AND m."ReversalId" IS NULL) THEN
                RAISE EXCEPTION 'Cost receipt must match an original stock movement';
              END IF;
              SELECT p."Id" INTO resolved_policy FROM inventory.costing_policies p
                WHERE p."TenantId"=NEW."TenantId" AND p."CompanyId"=NEW."CompanyId" AND p."State"='Approved'
                AND (p."ProductId" IS NULL OR p."ProductId"=NEW."ProductId")
                AND p."EffectiveFrom"<=NEW."BusinessDate" AND NEW."BusinessDate"<p."EffectiveUntil"
                ORDER BY (p."ProductId" IS NOT NULL) DESC LIMIT 1;
              IF NEW."PolicyId" IS DISTINCT FROM resolved_policy THEN RAISE EXCEPTION 'Cost receipt policy does not match the effective policy'; END IF;
              IF NEW."PolicyId" IS NOT NULL AND NOT EXISTS (SELECT 1 FROM inventory.costing_policies p WHERE p."TenantId"=NEW."TenantId"
                AND p."CompanyId"=NEW."CompanyId" AND p."Id"=NEW."PolicyId" AND p."Version"=NEW."PolicyVersion" AND p."Method"=NEW."Method") THEN
                RAISE EXCEPTION 'Cost receipt must freeze the approved policy version and method';
              END IF;
              RETURN NEW;
            END $$;
            CREATE TRIGGER validate_purchase_cost_receipt BEFORE INSERT ON inventory.purchase_cost_receipts
            FOR EACH ROW EXECUTE FUNCTION inventory.validate_purchase_cost_receipt();
            """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS inventory.validate_purchase_cost_receipt() CASCADE; DROP FUNCTION IF EXISTS inventory.freeze_costing_policy() CASCADE; DROP FUNCTION IF EXISTS inventory.reject_costing_evidence_mutation() CASCADE;");
            migrationBuilder.DropTable(
                name: "purchase_cost_receipts",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "costing_policies",
                schema: "inventory");
        }
    }
}
