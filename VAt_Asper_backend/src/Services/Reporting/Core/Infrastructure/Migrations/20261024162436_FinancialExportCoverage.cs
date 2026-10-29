using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vat.Reporting.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FinancialExportCoverage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "financial_coverage_verifications",
                schema: "reporting",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    GenerationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExportId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    SourceManifestJson = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RowCount = table.Column<int>(type: "integer", nullable: false),
                    VerifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_coverage_verifications", x => new { x.TenantId, x.CompanyId, x.GenerationId, x.Id });
                    table.CheckConstraint("ck_financial_coverage_bounds", "\"Version\" > 0 AND \"RowCount\" BETWEEN 0 AND 10000");
                    table.ForeignKey(
                        name: "FK_financial_coverage_verifications_financial_inbox_generation~",
                        columns: x => new { x.TenantId, x.CompanyId, x.GenerationId },
                        principalSchema: "reporting",
                        principalTable: "financial_inbox_generations",
                        principalColumns: new[] { "TenantId", "CompanyId", "GenerationId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_financial_coverage_verifications_TenantId_CompanyId_Genera~1",
                schema: "reporting",
                table: "financial_coverage_verifications",
                columns: new[] { "TenantId", "CompanyId", "GenerationId", "Version" },
                unique: true);
            migrationBuilder.Sql("""
                CREATE TRIGGER financial_coverage_no_mutation BEFORE UPDATE OR DELETE ON reporting.financial_coverage_verifications
                    FOR EACH ROW EXECUTE FUNCTION reporting.reject_snapshot_mutation();
                CREATE TRIGGER financial_coverage_no_truncate BEFORE TRUNCATE ON reporting.financial_coverage_verifications
                    FOR EACH STATEMENT EXECUTE FUNCTION reporting.reject_snapshot_mutation();
                """);

            migrationBuilder.CreateIndex(
                name: "IX_financial_coverage_verifications_TenantId_CompanyId_Generat~",
                schema: "reporting",
                table: "financial_coverage_verifications",
                columns: new[] { "TenantId", "CompanyId", "GenerationId", "ExportId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) => throw new InvalidOperationException("Export coverage evidence is forward-only. Restore a verified backup for an approved rollback.");
    }
}
