using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vat.Reporting.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CustomerHistorySnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "customer_snapshot_generations",
                schema: "reporting",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExportId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExportJson = table.Column<string>(type: "character varying(8500000)", maxLength: 8500000, nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CapturedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ImportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_snapshot_generations", x => new { x.TenantId, x.CompanyId, x.CustomerId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "customer_snapshot_audit",
                schema: "reporting",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    GenerationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CommandKey = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_snapshot_audit", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.ForeignKey(
                        name: "FK_customer_snapshot_audit_customer_snapshot_generations_Tenan~",
                        columns: x => new { x.TenantId, x.CompanyId, x.CustomerId, x.GenerationId },
                        principalSchema: "reporting",
                        principalTable: "customer_snapshot_generations",
                        principalColumns: new[] { "TenantId", "CompanyId", "CustomerId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_customer_snapshot_audit_TenantId_CompanyId_CustomerId_Gener~",
                schema: "reporting",
                table: "customer_snapshot_audit",
                columns: new[] { "TenantId", "CompanyId", "CustomerId", "GenerationId" });

            migrationBuilder.CreateIndex(
                name: "IX_customer_snapshot_generations_TenantId_CompanyId_ExportId",
                schema: "reporting",
                table: "customer_snapshot_generations",
                columns: new[] { "TenantId", "CompanyId", "ExportId" },
                unique: true);
            migrationBuilder.Sql("""
                DO $$ DECLARE table_name text; BEGIN
                    FOREACH table_name IN ARRAY ARRAY['customer_snapshot_generations','customer_snapshot_audit'] LOOP
                        EXECUTE format('CREATE TRIGGER snapshot_no_row_mutation BEFORE UPDATE OR DELETE ON reporting.%I FOR EACH ROW EXECUTE FUNCTION reporting.reject_snapshot_mutation()', table_name);
                        EXECUTE format('CREATE TRIGGER snapshot_no_truncate BEFORE TRUNCATE ON reporting.%I FOR EACH STATEMENT EXECUTE FUNCTION reporting.reject_snapshot_mutation()', table_name);
                    END LOOP;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) => throw new InvalidOperationException("Customer snapshot evidence is forward-only. Restore a verified backup for an approved rollback.");
    }
}
