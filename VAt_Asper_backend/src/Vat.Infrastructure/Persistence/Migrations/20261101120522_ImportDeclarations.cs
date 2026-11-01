using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ImportDeclarations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ImportDeclarationId",
                schema: "inventory",
                table: "cost_landed_reviews",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "import_declarations",
                schema: "inventory",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeclarationNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DeclarationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ForeignInvoiceAmount = table.Column<decimal>(type: "numeric(24,4)", precision: 24, scale: 4, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "numeric(24,8)", precision: 24, scale: 8, nullable: false),
                    FunctionalInvoiceAmount = table.Column<decimal>(type: "numeric(24,4)", precision: 24, scale: 4, nullable: false),
                    RecoverableTaxAmount = table.Column<decimal>(type: "numeric(24,4)", precision: 24, scale: 4, nullable: false),
                    NonrecoverableTaxAmount = table.Column<decimal>(type: "numeric(24,4)", precision: 24, scale: 4, nullable: false),
                    CapitalizableChargeAmount = table.Column<decimal>(type: "numeric(24,4)", precision: 24, scale: 4, nullable: false),
                    Charges = table.Column<string>(type: "jsonb", nullable: false),
                    Evidence = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ApprovedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_declarations", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_import_declaration_amounts", "\"ForeignInvoiceAmount\">=0 AND \"ExchangeRate\">0 AND \"FunctionalInvoiceAmount\">=0 AND \"RecoverableTaxAmount\">=0 AND \"NonrecoverableTaxAmount\">=0 AND \"CapitalizableChargeAmount\">=0");
                    table.CheckConstraint("ck_import_declaration_state", "(\"State\"='Draft' AND \"Version\"=1 AND \"ApprovedBy\" IS NULL AND \"ApprovedAt\" IS NULL) OR (\"State\"='Approved' AND \"Version\"=2 AND \"ApprovedBy\" IS NOT NULL AND \"ApprovedAt\" IS NOT NULL AND \"ApprovedBy\"<>\"CreatedBy\")");
                    table.ForeignKey(
                        name: "FK_import_declarations_purchase_posting_plans_TenantId_Company~",
                        columns: x => new { x.TenantId, x.CompanyId, x.PlanId },
                        principalSchema: "purchasing",
                        principalTable: "purchase_posting_plans",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cost_landed_reviews_TenantId_CompanyId_ImportDeclarationId",
                schema: "inventory",
                table: "cost_landed_reviews",
                columns: new[] { "TenantId", "CompanyId", "ImportDeclarationId" });

            migrationBuilder.CreateIndex(
                name: "IX_import_declarations_TenantId_CompanyId_DeclarationNumber",
                schema: "inventory",
                table: "import_declarations",
                columns: new[] { "TenantId", "CompanyId", "DeclarationNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_import_declarations_TenantId_CompanyId_PlanId",
                schema: "inventory",
                table: "import_declarations",
                columns: new[] { "TenantId", "CompanyId", "PlanId" },
                unique: true,
                filter: "\"State\"='Approved'");

            migrationBuilder.AddForeignKey(
                name: "FK_cost_landed_reviews_import_declarations_TenantId_CompanyId_~",
                schema: "inventory",
                table: "cost_landed_reviews",
                columns: new[] { "TenantId", "CompanyId", "ImportDeclarationId" },
                principalSchema: "inventory",
                principalTable: "import_declarations",
                principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("""
                CREATE FUNCTION inventory.freeze_import_declaration() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE source_state text; charge_total numeric;
                BEGIN
                  IF TG_OP='INSERT' THEN
                    IF NEW."State"<>'Draft' THEN RAISE EXCEPTION 'Import declaration must start as draft'; END IF;
                  ELSIF OLD."State"<>'Draft' OR NEW."State"<>'Approved' OR
                    (to_jsonb(NEW)-ARRAY['State','Version','ApprovedBy','ApprovedAt']) IS DISTINCT FROM (to_jsonb(OLD)-ARRAY['State','Version','ApprovedBy','ApprovedAt']) THEN
                    RAISE EXCEPTION 'Only unchanged independent import approval is permitted';
                  END IF;
                  SELECT p."Status" INTO source_state FROM purchasing.purchase_posting_plans p
                    WHERE p."TenantId"=NEW."TenantId" AND p."CompanyId"=NEW."CompanyId" AND p."Id"=NEW."PlanId";
                  IF source_state IS DISTINCT FROM 'Prepared' THEN RAISE EXCEPTION 'Import declaration requires a prepared receipt'; END IF;
                  IF NEW."FunctionalInvoiceAmount"<>round(NEW."ForeignInvoiceAmount"*NEW."ExchangeRate",4) OR jsonb_typeof(NEW."Charges")<>'array' THEN
                    RAISE EXCEPTION 'Invalid import currency basis or charges';
                  END IF;
                  IF EXISTS(SELECT 1 FROM jsonb_array_elements(NEW."Charges") x WHERE x->>'Kind' NOT IN ('Duty','Freight','Insurance','Port','Clearing','Other')
                    OR (x->>'Amount')::numeric<0 OR round((x->>'Amount')::numeric,4)<>(x->>'Amount')::numeric OR length(trim(x->>'Evidence'))=0) THEN
                    RAISE EXCEPTION 'Invalid import charge evidence';
                  END IF;
                  SELECT coalesce(sum((x->>'Amount')::numeric) FILTER (WHERE (x->>'Capitalizable')::boolean),0) INTO charge_total FROM jsonb_array_elements(NEW."Charges") x;
                  IF charge_total<>NEW."CapitalizableChargeAmount" THEN RAISE EXCEPTION 'Import capitalizable charge total does not reconcile'; END IF;
                  RETURN NEW;
                END $$;
                CREATE TRIGGER freeze_import_declaration BEFORE INSERT OR UPDATE ON inventory.import_declarations FOR EACH ROW EXECUTE FUNCTION inventory.freeze_import_declaration();
                CREATE TRIGGER immutable_import_declaration_delete BEFORE DELETE ON inventory.import_declarations FOR EACH ROW EXECUTE FUNCTION inventory.reject_costing_evidence_mutation();
                CREATE TRIGGER immutable_import_declaration_truncate BEFORE TRUNCATE ON inventory.import_declarations FOR EACH STATEMENT EXECUTE FUNCTION inventory.reject_costing_evidence_mutation();
                CREATE FUNCTION inventory.validate_import_landed_link() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE declaration_state text; declaration_plan uuid; declared_total numeric;
                BEGIN
                  IF NEW."ImportDeclarationId" IS NULL THEN RETURN NEW; END IF;
                  SELECT d."State",d."PlanId",d."CapitalizableChargeAmount" INTO declaration_state,declaration_plan,declared_total
                    FROM inventory.import_declarations d WHERE d."TenantId"=NEW."TenantId" AND d."CompanyId"=NEW."CompanyId" AND d."Id"=NEW."ImportDeclarationId";
                  IF declaration_state IS DISTINCT FROM 'Approved' OR declaration_plan IS DISTINCT FROM NEW."PlanId" OR declared_total IS DISTINCT FROM NEW."TotalAmount" THEN
                    RAISE EXCEPTION 'Landed allocation does not reconcile to an approved import declaration';
                  END IF;
                  RETURN NEW;
                END $$;
                CREATE TRIGGER validate_import_landed_link BEFORE INSERT OR UPDATE ON inventory.cost_landed_reviews FOR EACH ROW EXECUTE FUNCTION inventory.validate_import_landed_link();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION inventory.validate_import_landed_link() CASCADE; DROP FUNCTION inventory.freeze_import_declaration() CASCADE;");
            migrationBuilder.DropForeignKey(
                name: "FK_cost_landed_reviews_import_declarations_TenantId_CompanyId_~",
                schema: "inventory",
                table: "cost_landed_reviews");

            migrationBuilder.DropTable(
                name: "import_declarations",
                schema: "inventory");

            migrationBuilder.DropIndex(
                name: "IX_cost_landed_reviews_TenantId_CompanyId_ImportDeclarationId",
                schema: "inventory",
                table: "cost_landed_reviews");

            migrationBuilder.DropColumn(
                name: "ImportDeclarationId",
                schema: "inventory",
                table: "cost_landed_reviews");
        }
    }
}
