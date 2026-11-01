using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vat.Manufacturing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class VerifiedProductionOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "production_order_audit",
                schema: "manufacturing",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Actor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_production_order_audit", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.ForeignKey(
                        name: "FK_production_order_audit_production_orders_TenantId_CompanyId~",
                        columns: x => new { x.TenantId, x.CompanyId, x.OrderId },
                        principalSchema: "manufacturing",
                        principalTable: "production_orders",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "production_order_documents",
                schema: "manufacturing",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Json = table.Column<string>(type: "text", nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_production_order_documents", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.ForeignKey(
                        name: "FK_production_order_documents_production_orders_TenantId_Compa~",
                        columns: x => new { x.TenantId, x.CompanyId, x.Id },
                        principalSchema: "manufacturing",
                        principalTable: "production_orders",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_production_order_audit_TenantId_CompanyId_OrderId_Version",
                schema: "manufacturing",
                table: "production_order_audit",
                columns: new[] { "TenantId", "CompanyId", "OrderId", "Version" },
                unique: true);
            migrationBuilder.Sql("""
                CREATE FUNCTION manufacturing.reject_production_evidence_change() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'Production source/audit evidence is immutable'; END $$;
                CREATE TRIGGER freeze_production_order_document BEFORE UPDATE OR DELETE ON manufacturing.production_order_documents FOR EACH ROW EXECUTE FUNCTION manufacturing.reject_production_evidence_change();
                CREATE TRIGGER freeze_production_order_document_truncate BEFORE TRUNCATE ON manufacturing.production_order_documents FOR EACH STATEMENT EXECUTE FUNCTION manufacturing.reject_production_evidence_change();
                CREATE TRIGGER freeze_production_order_audit BEFORE UPDATE OR DELETE ON manufacturing.production_order_audit FOR EACH ROW EXECUTE FUNCTION manufacturing.reject_production_evidence_change();
                CREATE TRIGGER freeze_production_order_audit_truncate BEFORE TRUNCATE ON manufacturing.production_order_audit FOR EACH STATEMENT EXECUTE FUNCTION manufacturing.reject_production_evidence_change();
                CREATE FUNCTION manufacturing.freeze_approved_production_order() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF NOT EXISTS(SELECT 1 FROM manufacturing.production_order_documents d WHERE d."TenantId"=OLD."TenantId" AND d."CompanyId"=OLD."CompanyId" AND d."Id"=OLD."Id") THEN RETURN NEW; END IF;
                  IF (to_jsonb(NEW)-ARRAY['State','Version','ApprovedBy','UpdatedAt']) IS DISTINCT FROM (to_jsonb(OLD)-ARRAY['State','Version','ApprovedBy','UpdatedAt']) OR NEW."Version"<>OLD."Version"+1 OR
                    NOT ((OLD."State"='Draft' AND NEW."State" IN ('Submitted','Cancelled') AND NEW."ApprovedBy" IS NULL) OR
                      (OLD."State"='Submitted' AND NEW."State"='Cancelled' AND NEW."ApprovedBy" IS NULL) OR
                      (OLD."State"='Submitted' AND NEW."State"='Approved' AND NEW."ApprovedBy" IS NOT NULL AND NEW."ApprovedBy"<>OLD."CreatedBy")) THEN
                    RAISE EXCEPTION 'Production order source fields and approved orders are frozen';
                  END IF;
                  RETURN NEW;
                END $$;
                CREATE TRIGGER freeze_approved_production_order BEFORE UPDATE ON manufacturing.production_orders FOR EACH ROW EXECUTE FUNCTION manufacturing.freeze_approved_production_order();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION manufacturing.freeze_approved_production_order() CASCADE; DROP FUNCTION manufacturing.reject_production_evidence_change() CASCADE;");
            migrationBuilder.DropTable(
                name: "production_order_audit",
                schema: "manufacturing");

            migrationBuilder.DropTable(
                name: "production_order_documents",
                schema: "manufacturing");
        }
    }
}
