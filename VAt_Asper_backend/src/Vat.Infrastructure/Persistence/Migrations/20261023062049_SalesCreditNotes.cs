using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SalesCreditNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_posting_entries_TenantId_CompanyId_InvoiceId_LineId",
                schema: "sales",
                table: "posting_entries");

            migrationBuilder.DropCheckConstraint(
                name: "ck_sales_issue_amounts",
                schema: "sales",
                table: "posting_entries");

            migrationBuilder.AddColumn<long>(
                name: "PostedVersion",
                schema: "sales",
                table: "sales_invoices",
                type: "bigint",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE sales.sales_invoices AS invoice SET "PostedVersion" = plan."SourceVersion" + 1
                FROM sales.fulfillments AS plan
                WHERE invoice."TenantId" = plan."TenantId" AND invoice."CompanyId" = plan."CompanyId"
                    AND invoice."Id" = plan."InvoiceId" AND plan."Status" = 'Posted' AND invoice."PostedVersion" IS NULL
                """);

            migrationBuilder.AddColumn<Guid>(
                name: "CreditNoteId",
                schema: "sales",
                table: "posting_entries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReversesEntryId",
                schema: "sales",
                table: "posting_entries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_posting_entries_TenantId_CompanyId_InvoiceId_Id",
                schema: "sales",
                table: "posting_entries",
                columns: new[] { "TenantId", "CompanyId", "InvoiceId", "Id" });

            migrationBuilder.CreateTable(
                name: "credit_notes",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceVersion = table.Column<long>(type: "bigint", nullable: false),
                    OriginalPostedVersion = table.Column<long>(type: "bigint", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CorrectionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ReturnReceiptReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RequestedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ApprovedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OriginalDocumentNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DocumentNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    NetAmount = table.Column<decimal>(type: "numeric(24,4)", precision: 24, scale: 4, nullable: false),
                    OutputTaxAmount = table.Column<decimal>(type: "numeric(24,4)", precision: 24, scale: 4, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PostedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_credit_notes", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.UniqueConstraint("AK_credit_notes_TenantId_CompanyId_InvoiceId_Id", x => new { x.TenantId, x.CompanyId, x.InvoiceId, x.Id });
                    table.CheckConstraint("ck_credit_amounts", "\"NetAmount\" <= 0 AND \"OutputTaxAmount\" <= 0");
                    table.CheckConstraint("ck_credit_maker_checker", "\"ApprovedBy\" IS NULL OR \"ApprovedBy\" <> \"RequestedBy\"");
                    table.ForeignKey(
                        name: "FK_credit_notes_sales_invoices_TenantId_CompanyId_InvoiceId",
                        columns: x => new { x.TenantId, x.CompanyId, x.InvoiceId },
                        principalSchema: "sales",
                        principalTable: "sales_invoices",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_posting_entries_TenantId_CompanyId_InvoiceId_CreditNoteId",
                schema: "sales",
                table: "posting_entries",
                columns: new[] { "TenantId", "CompanyId", "InvoiceId", "CreditNoteId" });

            migrationBuilder.CreateIndex(
                name: "IX_posting_entries_TenantId_CompanyId_InvoiceId_LineId",
                schema: "sales",
                table: "posting_entries",
                columns: new[] { "TenantId", "CompanyId", "InvoiceId", "LineId" },
                unique: true,
                filter: "\"CreditNoteId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_posting_entries_TenantId_CompanyId_InvoiceId_ReversesEntryId",
                schema: "sales",
                table: "posting_entries",
                columns: new[] { "TenantId", "CompanyId", "InvoiceId", "ReversesEntryId" });

            migrationBuilder.CreateIndex(
                name: "IX_posting_entries_TenantId_CompanyId_ReversesEntryId",
                schema: "sales",
                table: "posting_entries",
                columns: new[] { "TenantId", "CompanyId", "ReversesEntryId" },
                unique: true,
                filter: "\"ReversesEntryId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_sales_issue_amounts",
                schema: "sales",
                table: "posting_entries",
                sql: "(\"CreditNoteId\" IS NULL AND \"ReversesEntryId\" IS NULL AND \"Quantity\" < 0 AND \"NetAmount\" >= 0 AND \"OutputTaxAmount\" >= 0) OR (\"CreditNoteId\" IS NOT NULL AND \"ReversesEntryId\" IS NOT NULL AND \"Quantity\" > 0 AND \"NetAmount\" <= 0 AND \"OutputTaxAmount\" <= 0)");

            migrationBuilder.CreateIndex(
                name: "IX_credit_notes_TenantId_CompanyId_DocumentNumber",
                schema: "sales",
                table: "credit_notes",
                columns: new[] { "TenantId", "CompanyId", "DocumentNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_credit_notes_TenantId_CompanyId_InvoiceId_SourceVersion",
                schema: "sales",
                table: "credit_notes",
                columns: new[] { "TenantId", "CompanyId", "InvoiceId", "SourceVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_sales_pending_credit",
                schema: "sales",
                table: "credit_notes",
                columns: new[] { "TenantId", "CompanyId", "InvoiceId" },
                unique: true,
                filter: "\"Status\" = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "ux_sales_posted_credit",
                schema: "sales",
                table: "credit_notes",
                columns: new[] { "TenantId", "CompanyId", "InvoiceId" },
                unique: true,
                filter: "\"Status\" = 'Posted'");

            migrationBuilder.AddForeignKey(
                name: "FK_posting_entries_credit_notes_TenantId_CompanyId_InvoiceId_C~",
                schema: "sales",
                table: "posting_entries",
                columns: new[] { "TenantId", "CompanyId", "InvoiceId", "CreditNoteId" },
                principalSchema: "sales",
                principalTable: "credit_notes",
                principalColumns: new[] { "TenantId", "CompanyId", "InvoiceId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_posting_entries_posting_entries_TenantId_CompanyId_InvoiceI~",
                schema: "sales",
                table: "posting_entries",
                columns: new[] { "TenantId", "CompanyId", "InvoiceId", "ReversesEntryId" },
                principalSchema: "sales",
                principalTable: "posting_entries",
                principalColumns: new[] { "TenantId", "CompanyId", "InvoiceId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_posting_entries_credit_notes_TenantId_CompanyId_InvoiceId_C~",
                schema: "sales",
                table: "posting_entries");

            migrationBuilder.DropForeignKey(
                name: "FK_posting_entries_posting_entries_TenantId_CompanyId_InvoiceI~",
                schema: "sales",
                table: "posting_entries");

            migrationBuilder.DropTable(
                name: "credit_notes",
                schema: "sales");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_posting_entries_TenantId_CompanyId_InvoiceId_Id",
                schema: "sales",
                table: "posting_entries");

            migrationBuilder.DropIndex(
                name: "IX_posting_entries_TenantId_CompanyId_InvoiceId_CreditNoteId",
                schema: "sales",
                table: "posting_entries");

            migrationBuilder.DropIndex(
                name: "IX_posting_entries_TenantId_CompanyId_InvoiceId_LineId",
                schema: "sales",
                table: "posting_entries");

            migrationBuilder.DropIndex(
                name: "IX_posting_entries_TenantId_CompanyId_InvoiceId_ReversesEntryId",
                schema: "sales",
                table: "posting_entries");

            migrationBuilder.DropIndex(
                name: "IX_posting_entries_TenantId_CompanyId_ReversesEntryId",
                schema: "sales",
                table: "posting_entries");

            migrationBuilder.DropCheckConstraint(
                name: "ck_sales_issue_amounts",
                schema: "sales",
                table: "posting_entries");

            migrationBuilder.DropColumn(
                name: "PostedVersion",
                schema: "sales",
                table: "sales_invoices");

            migrationBuilder.DropColumn(
                name: "CreditNoteId",
                schema: "sales",
                table: "posting_entries");

            migrationBuilder.DropColumn(
                name: "ReversesEntryId",
                schema: "sales",
                table: "posting_entries");

            migrationBuilder.CreateIndex(
                name: "IX_posting_entries_TenantId_CompanyId_InvoiceId_LineId",
                schema: "sales",
                table: "posting_entries",
                columns: new[] { "TenantId", "CompanyId", "InvoiceId", "LineId" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_sales_issue_amounts",
                schema: "sales",
                table: "posting_entries",
                sql: "\"Quantity\" < 0 AND \"NetAmount\" >= 0 AND \"OutputTaxAmount\" >= 0");
        }
    }
}
