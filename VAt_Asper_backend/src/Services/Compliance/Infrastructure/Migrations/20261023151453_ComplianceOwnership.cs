using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vat.Compliance.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ComplianceOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "compliance");

            migrationBuilder.CreateTable(
                name: "form_templates",
                schema: "compliance",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_form_templates", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_form_templates_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                });

            migrationBuilder.CreateTable(
                name: "return_periods",
                schema: "compliance",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    FiscalPeriodId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    From = table.Column<DateOnly>(type: "date", nullable: false),
                    Until = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_return_periods", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_return_periods_rules", "(\"Until\" >= \"From\")");
                    table.CheckConstraint("ck_return_periods_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                });

            migrationBuilder.CreateTable(
                name: "form_versions",
                schema: "compliance",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    Mapping = table.Column<string>(type: "jsonb", nullable: false),
                    ApprovedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_form_versions", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_form_versions_rules", "\"Revision\" > 0 AND (\"EffectiveUntil\" IS NULL OR \"EffectiveUntil\" >= \"EffectiveFrom\") AND (\"ApprovedBy\" IS NULL OR \"ApprovedBy\" <> \"CreatedBy\")");
                    table.CheckConstraint("ck_form_versions_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_form_versions_form_templates_TenantId_CompanyId_TemplateId",
                        columns: x => new { x.TenantId, x.CompanyId, x.TemplateId },
                        principalSchema: "compliance",
                        principalTable: "form_templates",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "vat_returns",
                schema: "compliance",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    ReturnPeriodId = table.Column<Guid>(type: "uuid", nullable: false),
                    FormVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    LedgerWatermark = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ApprovedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CorrectsId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vat_returns", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_vat_returns_rules", "(\"CorrectsId\" IS NULL OR \"CorrectsId\" <> \"Id\") AND \"Revision\" > 0 AND (\"ApprovedBy\" IS NULL OR \"ApprovedBy\" <> \"CreatedBy\") AND \"State\" IN ('Draft','Pending','Approved','Rejected','Cancelled','Running','Completed','Failed','Submitted','Posted')");
                    table.CheckConstraint("ck_vat_returns_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_vat_returns_form_versions_TenantId_CompanyId_FormVersionId",
                        columns: x => new { x.TenantId, x.CompanyId, x.FormVersionId },
                        principalSchema: "compliance",
                        principalTable: "form_versions",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_vat_returns_return_periods_TenantId_CompanyId_ReturnPeriodId",
                        columns: x => new { x.TenantId, x.CompanyId, x.ReturnPeriodId },
                        principalSchema: "compliance",
                        principalTable: "return_periods",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_vat_returns_vat_returns_TenantId_CompanyId_CorrectsId",
                        columns: x => new { x.TenantId, x.CompanyId, x.CorrectsId },
                        principalSchema: "compliance",
                        principalTable: "vat_returns",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "filing_evidence",
                schema: "compliance",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    ReturnId = table.Column<Guid>(type: "uuid", nullable: false),
                    ObjectKey = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FilingReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FiledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_filing_evidence", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_filing_evidence_rules", "(length(\"Sha256\") = 64)");
                    table.CheckConstraint("ck_filing_evidence_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_filing_evidence_vat_returns_TenantId_CompanyId_ReturnId",
                        columns: x => new { x.TenantId, x.CompanyId, x.ReturnId },
                        principalSchema: "compliance",
                        principalTable: "vat_returns",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "period_close_requests",
                schema: "compliance",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    FiscalPeriodId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReturnId = table.Column<Guid>(type: "uuid", nullable: true),
                    State = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    RequestedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ApprovedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DecisionAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_period_close_requests", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_period_close_requests_rules", "(\"ApprovedBy\" IS NULL OR \"ApprovedBy\" <> \"CreatedBy\") AND \"State\" IN ('Draft','Pending','Approved','Rejected','Cancelled','Running','Completed','Failed','Submitted','Posted')");
                    table.CheckConstraint("ck_period_close_requests_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_period_close_requests_vat_returns_TenantId_CompanyId_Return~",
                        columns: x => new { x.TenantId, x.CompanyId, x.ReturnId },
                        principalSchema: "compliance",
                        principalTable: "vat_returns",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "reconciliation_runs",
                schema: "compliance",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    ReturnId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    State = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Differences = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reconciliation_runs", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_reconciliation_runs_rules", "\"State\" IN ('Draft','Pending','Approved','Rejected','Cancelled','Running','Completed','Failed','Submitted','Posted')");
                    table.CheckConstraint("ck_reconciliation_runs_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_reconciliation_runs_vat_returns_TenantId_CompanyId_ReturnId",
                        columns: x => new { x.TenantId, x.CompanyId, x.ReturnId },
                        principalSchema: "compliance",
                        principalTable: "vat_returns",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "return_lines",
                schema: "compliance",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    ReturnId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(24,6)", precision: 24, scale: 6, nullable: false),
                    SourceSnapshot = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_return_lines", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_return_lines_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_return_lines_vat_returns_TenantId_CompanyId_ReturnId",
                        columns: x => new { x.TenantId, x.CompanyId, x.ReturnId },
                        principalSchema: "compliance",
                        principalTable: "vat_returns",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_filing_evidence_TenantId_CompanyId_CreatedAt",
                schema: "compliance",
                table: "filing_evidence",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_filing_evidence_TenantId_CompanyId_ReturnId",
                schema: "compliance",
                table: "filing_evidence",
                columns: new[] { "TenantId", "CompanyId", "ReturnId" });

            migrationBuilder.CreateIndex(
                name: "IX_filing_evidence_TenantId_CompanyId_ReturnId_Sha256",
                schema: "compliance",
                table: "filing_evidence",
                columns: new[] { "TenantId", "CompanyId", "ReturnId", "Sha256" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_form_templates_TenantId_CompanyId_Code",
                schema: "compliance",
                table: "form_templates",
                columns: new[] { "TenantId", "CompanyId", "Code" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_form_templates_TenantId_CompanyId_CreatedAt",
                schema: "compliance",
                table: "form_templates",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_form_versions_TenantId_CompanyId_CreatedAt",
                schema: "compliance",
                table: "form_versions",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_form_versions_TenantId_CompanyId_TemplateId",
                schema: "compliance",
                table: "form_versions",
                columns: new[] { "TenantId", "CompanyId", "TemplateId" });

            migrationBuilder.CreateIndex(
                name: "IX_form_versions_TenantId_CompanyId_TemplateId_Revision",
                schema: "compliance",
                table: "form_versions",
                columns: new[] { "TenantId", "CompanyId", "TemplateId", "Revision" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_period_close_requests_TenantId_CompanyId_CreatedAt",
                schema: "compliance",
                table: "period_close_requests",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_period_close_requests_TenantId_CompanyId_FiscalPeriodId",
                schema: "compliance",
                table: "period_close_requests",
                columns: new[] { "TenantId", "CompanyId", "FiscalPeriodId" });

            migrationBuilder.CreateIndex(
                name: "IX_period_close_requests_TenantId_CompanyId_ReturnId",
                schema: "compliance",
                table: "period_close_requests",
                columns: new[] { "TenantId", "CompanyId", "ReturnId" });

            migrationBuilder.CreateIndex(
                name: "IX_period_close_requests_TenantId_CompanyId_State_CreatedAt",
                schema: "compliance",
                table: "period_close_requests",
                columns: new[] { "TenantId", "CompanyId", "State", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_reconciliation_runs_TenantId_CompanyId_CreatedAt",
                schema: "compliance",
                table: "reconciliation_runs",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_reconciliation_runs_TenantId_CompanyId_ReturnId",
                schema: "compliance",
                table: "reconciliation_runs",
                columns: new[] { "TenantId", "CompanyId", "ReturnId" });

            migrationBuilder.CreateIndex(
                name: "IX_reconciliation_runs_TenantId_CompanyId_State_CreatedAt",
                schema: "compliance",
                table: "reconciliation_runs",
                columns: new[] { "TenantId", "CompanyId", "State", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_return_lines_TenantId_CompanyId_CreatedAt",
                schema: "compliance",
                table: "return_lines",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_return_lines_TenantId_CompanyId_ReturnId",
                schema: "compliance",
                table: "return_lines",
                columns: new[] { "TenantId", "CompanyId", "ReturnId" });

            migrationBuilder.CreateIndex(
                name: "IX_return_lines_TenantId_CompanyId_ReturnId_LineCode",
                schema: "compliance",
                table: "return_lines",
                columns: new[] { "TenantId", "CompanyId", "ReturnId", "LineCode" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_return_periods_TenantId_CompanyId_Code",
                schema: "compliance",
                table: "return_periods",
                columns: new[] { "TenantId", "CompanyId", "Code" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_return_periods_TenantId_CompanyId_CreatedAt",
                schema: "compliance",
                table: "return_periods",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_return_periods_TenantId_CompanyId_FiscalPeriodId",
                schema: "compliance",
                table: "return_periods",
                columns: new[] { "TenantId", "CompanyId", "FiscalPeriodId" });

            migrationBuilder.CreateIndex(
                name: "IX_vat_returns_TenantId_CompanyId_CorrectsId",
                schema: "compliance",
                table: "vat_returns",
                columns: new[] { "TenantId", "CompanyId", "CorrectsId" });

            migrationBuilder.CreateIndex(
                name: "IX_vat_returns_TenantId_CompanyId_CreatedAt",
                schema: "compliance",
                table: "vat_returns",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_vat_returns_TenantId_CompanyId_FormVersionId",
                schema: "compliance",
                table: "vat_returns",
                columns: new[] { "TenantId", "CompanyId", "FormVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_vat_returns_TenantId_CompanyId_ReturnPeriodId",
                schema: "compliance",
                table: "vat_returns",
                columns: new[] { "TenantId", "CompanyId", "ReturnPeriodId" });

            migrationBuilder.CreateIndex(
                name: "IX_vat_returns_TenantId_CompanyId_ReturnPeriodId_FormVersionId~",
                schema: "compliance",
                table: "vat_returns",
                columns: new[] { "TenantId", "CompanyId", "ReturnPeriodId", "FormVersionId", "Revision" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_vat_returns_TenantId_CompanyId_State_CreatedAt",
                schema: "compliance",
                table: "vat_returns",
                columns: new[] { "TenantId", "CompanyId", "State", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "filing_evidence",
                schema: "compliance");

            migrationBuilder.DropTable(
                name: "period_close_requests",
                schema: "compliance");

            migrationBuilder.DropTable(
                name: "reconciliation_runs",
                schema: "compliance");

            migrationBuilder.DropTable(
                name: "return_lines",
                schema: "compliance");

            migrationBuilder.DropTable(
                name: "vat_returns",
                schema: "compliance");

            migrationBuilder.DropTable(
                name: "form_versions",
                schema: "compliance");

            migrationBuilder.DropTable(
                name: "return_periods",
                schema: "compliance");

            migrationBuilder.DropTable(
                name: "form_templates",
                schema: "compliance");
        }
    }
}
