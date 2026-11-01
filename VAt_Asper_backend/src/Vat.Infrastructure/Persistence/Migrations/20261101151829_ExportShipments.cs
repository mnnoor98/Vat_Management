using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExportShipments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "export_shipments",
                schema: "inventory",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeclarationNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DeclarationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DestinationCountry = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    TransportDocument = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ForeignProceedsAmount = table.Column<decimal>(type: "numeric(24,4)", precision: 24, scale: 4, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "numeric(24,8)", precision: 24, scale: 8, nullable: false),
                    FunctionalProceedsAmount = table.Column<decimal>(type: "numeric(24,4)", precision: 24, scale: 4, nullable: false),
                    DispatchCostAmount = table.Column<decimal>(type: "numeric(24,4)", precision: 24, scale: 4, nullable: false),
                    TaxTreatment = table.Column<string>(type: "text", nullable: false),
                    FrozenOutputTaxAmount = table.Column<decimal>(type: "numeric(24,4)", precision: 24, scale: 4, nullable: false),
                    Evidence = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    SourceFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ApprovedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_export_shipments", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_export_shipment_amounts", "\"ForeignProceedsAmount\">=0 AND \"ExchangeRate\">0 AND \"FunctionalProceedsAmount\">=0 AND \"DispatchCostAmount\">=0 AND \"FrozenOutputTaxAmount\">=0");
                    table.CheckConstraint("ck_export_shipment_state", "(\"State\"='Draft' AND \"Version\"=1 AND \"ApprovedBy\" IS NULL AND \"ApprovedAt\" IS NULL) OR (\"State\"='Approved' AND \"Version\"=2 AND \"ApprovedBy\" IS NOT NULL AND \"ApprovedAt\" IS NOT NULL AND \"ApprovedBy\"<>\"CreatedBy\")");
                    table.CheckConstraint("ck_export_shipment_tax", "\"TaxTreatment\" IN ('ZeroRated','Exempt','Standard')");
                    table.ForeignKey(
                        name: "FK_export_shipments_sales_invoices_TenantId_CompanyId_InvoiceId",
                        columns: x => new { x.TenantId, x.CompanyId, x.InvoiceId },
                        principalSchema: "sales",
                        principalTable: "sales_invoices",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_export_shipments_TenantId_CompanyId_DeclarationNumber",
                schema: "inventory",
                table: "export_shipments",
                columns: new[] { "TenantId", "CompanyId", "DeclarationNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_export_shipments_TenantId_CompanyId_InvoiceId",
                schema: "inventory",
                table: "export_shipments",
                columns: new[] { "TenantId", "CompanyId", "InvoiceId" },
                unique: true,
                filter: "\"State\"='Approved'");
            migrationBuilder.Sql("""
                CREATE FUNCTION inventory.freeze_export_shipment() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE fulfillment_state text; invoice_date date; sale_count integer; cost_count integer; issue_cost numeric; output_tax numeric;
                BEGIN
                  IF TG_OP='INSERT' THEN
                    IF NEW."State"<>'Draft' THEN RAISE EXCEPTION 'Export shipment must start as draft'; END IF;
                  ELSIF OLD."State"<>'Draft' OR NEW."State"<>'Approved' OR
                    (to_jsonb(NEW)-ARRAY['State','Version','ApprovedBy','ApprovedAt']) IS DISTINCT FROM (to_jsonb(OLD)-ARRAY['State','Version','ApprovedBy','ApprovedAt']) THEN
                    RAISE EXCEPTION 'Only unchanged independent export approval is permitted';
                  END IF;
                  SELECT f."Status",i."BusinessDate" INTO fulfillment_state,invoice_date FROM sales.fulfillments f JOIN sales.sales_invoices i
                    ON i."TenantId"=f."TenantId" AND i."CompanyId"=f."CompanyId" AND i."Id"=f."InvoiceId"
                    WHERE f."TenantId"=NEW."TenantId" AND f."CompanyId"=NEW."CompanyId" AND f."InvoiceId"=NEW."InvoiceId";
                  IF fulfillment_state IS DISTINCT FROM 'Posted' OR NEW."DeclarationDate"<invoice_date THEN RAISE EXCEPTION 'Export evidence requires a posted dispatch and valid declaration date'; END IF;
                  SELECT count(*),coalesce(sum(s."OutputTaxAmount"),0) INTO sale_count,output_tax FROM sales.posting_entries s
                    WHERE s."TenantId"=NEW."TenantId" AND s."CompanyId"=NEW."CompanyId" AND s."InvoiceId"=NEW."InvoiceId" AND s."CreditNoteId" IS NULL AND s."DebitNoteId" IS NULL;
                  SELECT count(*),coalesce(-sum(c."Value"),0) INTO cost_count,issue_cost FROM inventory.cost_entries c JOIN sales.posting_entries s
                    ON s."TenantId"=c."TenantId" AND s."CompanyId"=c."CompanyId" AND s."Id"=c."SourceId"
                    WHERE c."TenantId"=NEW."TenantId" AND c."CompanyId"=NEW."CompanyId" AND c."Kind"='Issue' AND s."InvoiceId"=NEW."InvoiceId" AND s."CreditNoteId" IS NULL AND s."DebitNoteId" IS NULL;
                  IF sale_count=0 OR cost_count<>sale_count OR issue_cost<>NEW."DispatchCostAmount" OR output_tax<>NEW."FrozenOutputTaxAmount" THEN RAISE EXCEPTION 'Export dispatch cost/tax evidence does not reconcile'; END IF;
                  IF NEW."FunctionalProceedsAmount"<>round(NEW."ForeignProceedsAmount"*NEW."ExchangeRate",4) OR (NEW."TaxTreatment"<>'Standard' AND NEW."FrozenOutputTaxAmount"<>0) THEN RAISE EXCEPTION 'Export currency or tax treatment does not reconcile'; END IF;
                  RETURN NEW;
                END $$;
                CREATE TRIGGER freeze_export_shipment BEFORE INSERT OR UPDATE ON inventory.export_shipments FOR EACH ROW EXECUTE FUNCTION inventory.freeze_export_shipment();
                CREATE TRIGGER immutable_export_shipment_delete BEFORE DELETE ON inventory.export_shipments FOR EACH ROW EXECUTE FUNCTION inventory.reject_costing_evidence_mutation();
                CREATE TRIGGER immutable_export_shipment_truncate BEFORE TRUNCATE ON inventory.export_shipments FOR EACH STATEMENT EXECUTE FUNCTION inventory.reject_costing_evidence_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION inventory.freeze_export_shipment() CASCADE;");
            migrationBuilder.DropTable(
                name: "export_shipments",
                schema: "inventory");
        }
    }
}
