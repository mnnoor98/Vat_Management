using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vat.Reporting.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReportingSnapshotGenerations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "reporting");

            migrationBuilder.CreateTable(
                name: "snapshot_command_receipts",
                schema: "reporting",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Operation = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Key = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RequestHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResponseJson = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_snapshot_command_receipts", x => new { x.TenantId, x.CompanyId, x.Operation, x.Key });
                });

            migrationBuilder.CreateTable(
                name: "snapshot_generations",
                schema: "reporting",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExportId = table.Column<Guid>(type: "uuid", nullable: false),
                    ManifestJson = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CapturedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ImportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EventCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_snapshot_generations", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_snapshot_generation_count", "\"EventCount\" BETWEEN 0 AND 10000");
                });

            migrationBuilder.CreateTable(
                name: "snapshot_entries",
                schema: "reporting",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    GenerationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    NetAmount = table.Column<decimal>(type: "numeric(24,4)", precision: 24, scale: 4, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(24,4)", precision: 24, scale: 4, nullable: false),
                    RecoverableTaxAmount = table.Column<decimal>(type: "numeric(24,4)", precision: 24, scale: 4, nullable: false),
                    RowJson = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_snapshot_entries", x => new { x.TenantId, x.CompanyId, x.GenerationId, x.TransactionId });
                    table.ForeignKey(
                        name: "FK_snapshot_entries_snapshot_generations_TenantId_CompanyId_Ge~",
                        columns: x => new { x.TenantId, x.CompanyId, x.GenerationId },
                        principalSchema: "reporting",
                        principalTable: "snapshot_generations",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "snapshot_heads",
                schema: "reporting",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    GenerationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_snapshot_heads", x => new { x.TenantId, x.CompanyId });
                    table.CheckConstraint("ck_snapshot_head_version", "\"Version\" > 0");
                    table.ForeignKey(
                        name: "FK_snapshot_heads_snapshot_generations_TenantId_CompanyId_Gene~",
                        columns: x => new { x.TenantId, x.CompanyId, x.GenerationId },
                        principalSchema: "reporting",
                        principalTable: "snapshot_generations",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "snapshot_operator_audit",
                schema: "reporting",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GenerationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CommandKey = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_snapshot_operator_audit", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.ForeignKey(
                        name: "FK_snapshot_operator_audit_snapshot_generations_TenantId_Compa~",
                        columns: x => new { x.TenantId, x.CompanyId, x.GenerationId },
                        principalSchema: "reporting",
                        principalTable: "snapshot_generations",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_snapshot_entries_TenantId_CompanyId_GenerationId_BusinessDa~",
                schema: "reporting",
                table: "snapshot_entries",
                columns: new[] { "TenantId", "CompanyId", "GenerationId", "BusinessDate", "Currency" });

            migrationBuilder.CreateIndex(
                name: "IX_snapshot_entries_TenantId_CompanyId_GenerationId_EventId",
                schema: "reporting",
                table: "snapshot_entries",
                columns: new[] { "TenantId", "CompanyId", "GenerationId", "EventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_snapshot_generations_TenantId_CompanyId_ExportId",
                schema: "reporting",
                table: "snapshot_generations",
                columns: new[] { "TenantId", "CompanyId", "ExportId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_snapshot_heads_TenantId_CompanyId_GenerationId",
                schema: "reporting",
                table: "snapshot_heads",
                columns: new[] { "TenantId", "CompanyId", "GenerationId" });
            migrationBuilder.Sql("""
                CREATE FUNCTION reporting.reject_snapshot_mutation() RETURNS trigger
                LANGUAGE plpgsql AS $$ BEGIN
                    RAISE EXCEPTION 'Reporting snapshot evidence is append-only';
                END $$;
                DO $$ DECLARE table_name text; BEGIN
                    FOREACH table_name IN ARRAY ARRAY['snapshot_generations','snapshot_entries','snapshot_command_receipts','snapshot_operator_audit'] LOOP
                        EXECUTE format('CREATE TRIGGER snapshot_no_row_mutation BEFORE UPDATE OR DELETE ON reporting.%I FOR EACH ROW EXECUTE FUNCTION reporting.reject_snapshot_mutation()', table_name);
                        EXECUTE format('CREATE TRIGGER snapshot_no_truncate BEFORE TRUNCATE ON reporting.%I FOR EACH STATEMENT EXECUTE FUNCTION reporting.reject_snapshot_mutation()', table_name);
                    END LOOP;
                END $$;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_snapshot_operator_audit_TenantId_CompanyId_GenerationId",
                schema: "reporting",
                table: "snapshot_operator_audit",
                columns: new[] { "TenantId", "CompanyId", "GenerationId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) => throw new InvalidOperationException("Reporting snapshot evidence is forward-only. Restore a verified backup for an approved rollback.");
    }
}
