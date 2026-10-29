using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SalesDebitNotes : Migration
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

            migrationBuilder.AddColumn<Guid>(
                name: "AdjustsEntryId",
                schema: "sales",
                table: "posting_entries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DebitNoteId",
                schema: "sales",
                table: "posting_entries",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "debit_notes",
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
                    EvidenceReference = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    RequestedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ApprovedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OriginalDocumentNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DocumentNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Lines = table.Column<string>(type: "jsonb", nullable: false),
                    NetAmount = table.Column<decimal>(type: "numeric(24,4)", precision: 24, scale: 4, nullable: false),
                    OutputTaxAmount = table.Column<decimal>(type: "numeric(24,4)", precision: 24, scale: 4, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PostedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_debit_notes", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.UniqueConstraint("AK_debit_notes_TenantId_CompanyId_InvoiceId_Id", x => new { x.TenantId, x.CompanyId, x.InvoiceId, x.Id });
                    table.CheckConstraint("ck_debit_amounts", "\"NetAmount\" > 0 AND \"OutputTaxAmount\" >= 0");
                    table.CheckConstraint("ck_debit_maker_checker", "\"ApprovedBy\" IS NULL OR \"ApprovedBy\" <> \"RequestedBy\"");
                    table.ForeignKey(
                        name: "FK_debit_notes_sales_invoices_TenantId_CompanyId_InvoiceId",
                        columns: x => new { x.TenantId, x.CompanyId, x.InvoiceId },
                        principalSchema: "sales",
                        principalTable: "sales_invoices",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_posting_entries_TenantId_CompanyId_DebitNoteId_AdjustsEntry~",
                schema: "sales",
                table: "posting_entries",
                columns: new[] { "TenantId", "CompanyId", "DebitNoteId", "AdjustsEntryId" },
                unique: true,
                filter: "\"AdjustsEntryId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_posting_entries_TenantId_CompanyId_InvoiceId_AdjustsEntryId",
                schema: "sales",
                table: "posting_entries",
                columns: new[] { "TenantId", "CompanyId", "InvoiceId", "AdjustsEntryId" });

            migrationBuilder.CreateIndex(
                name: "IX_posting_entries_TenantId_CompanyId_InvoiceId_DebitNoteId",
                schema: "sales",
                table: "posting_entries",
                columns: new[] { "TenantId", "CompanyId", "InvoiceId", "DebitNoteId" });

            migrationBuilder.CreateIndex(
                name: "IX_posting_entries_TenantId_CompanyId_InvoiceId_LineId",
                schema: "sales",
                table: "posting_entries",
                columns: new[] { "TenantId", "CompanyId", "InvoiceId", "LineId" },
                unique: true,
                filter: "\"CreditNoteId\" IS NULL AND \"DebitNoteId\" IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_sales_issue_amounts",
                schema: "sales",
                table: "posting_entries",
                sql: "(\"CreditNoteId\" IS NULL AND \"ReversesEntryId\" IS NULL AND \"DebitNoteId\" IS NULL AND \"AdjustsEntryId\" IS NULL AND \"EntryType\" = 'Goods' AND \"Quantity\" < 0 AND \"NetAmount\" >= 0 AND \"OutputTaxAmount\" >= 0) OR (\"DebitNoteId\" IS NULL AND \"AdjustsEntryId\" IS NULL AND \"CreditNoteId\" IS NOT NULL AND \"ReversesEntryId\" IS NOT NULL AND ((\"EntryType\" = 'Goods' AND \"Quantity\" > 0) OR (\"EntryType\" = 'PriceCredit' AND \"Quantity\" = 0 AND \"NetAmount\" < 0)) AND \"NetAmount\" <= 0 AND \"OutputTaxAmount\" <= 0) OR (\"CreditNoteId\" IS NULL AND \"ReversesEntryId\" IS NULL AND \"DebitNoteId\" IS NOT NULL AND \"AdjustsEntryId\" IS NOT NULL AND \"EntryType\" = 'PriceDebit' AND \"Quantity\" = 0 AND \"NetAmount\" > 0 AND \"OutputTaxAmount\" >= 0)");

            migrationBuilder.CreateIndex(
                name: "IX_debit_notes_TenantId_CompanyId_DocumentNumber",
                schema: "sales",
                table: "debit_notes",
                columns: new[] { "TenantId", "CompanyId", "DocumentNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_debit_notes_TenantId_CompanyId_InvoiceId",
                schema: "sales",
                table: "debit_notes",
                columns: new[] { "TenantId", "CompanyId", "InvoiceId" },
                unique: true,
                filter: "\"Status\" = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_debit_notes_TenantId_CompanyId_InvoiceId_SourceVersion",
                schema: "sales",
                table: "debit_notes",
                columns: new[] { "TenantId", "CompanyId", "InvoiceId", "SourceVersion" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_posting_entries_debit_notes_TenantId_CompanyId_InvoiceId_De~",
                schema: "sales",
                table: "posting_entries",
                columns: new[] { "TenantId", "CompanyId", "InvoiceId", "DebitNoteId" },
                principalSchema: "sales",
                principalTable: "debit_notes",
                principalColumns: new[] { "TenantId", "CompanyId", "InvoiceId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_sales_entries_debit_original",
                schema: "sales",
                table: "posting_entries",
                columns: new[] { "TenantId", "CompanyId", "InvoiceId", "AdjustsEntryId" },
                principalSchema: "sales",
                principalTable: "posting_entries",
                principalColumns: new[] { "TenantId", "CompanyId", "InvoiceId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_posting_entries_debit_notes_TenantId_CompanyId_InvoiceId_De~",
                schema: "sales",
                table: "posting_entries");

            migrationBuilder.DropForeignKey(
                name: "fk_sales_entries_debit_original",
                schema: "sales",
                table: "posting_entries");

            migrationBuilder.DropTable(
                name: "debit_notes",
                schema: "sales");

            migrationBuilder.DropIndex(
                name: "IX_posting_entries_TenantId_CompanyId_DebitNoteId_AdjustsEntry~",
                schema: "sales",
                table: "posting_entries");

            migrationBuilder.DropIndex(
                name: "IX_posting_entries_TenantId_CompanyId_InvoiceId_AdjustsEntryId",
                schema: "sales",
                table: "posting_entries");

            migrationBuilder.DropIndex(
                name: "IX_posting_entries_TenantId_CompanyId_InvoiceId_DebitNoteId",
                schema: "sales",
                table: "posting_entries");

            migrationBuilder.DropIndex(
                name: "IX_posting_entries_TenantId_CompanyId_InvoiceId_LineId",
                schema: "sales",
                table: "posting_entries");

            migrationBuilder.DropCheckConstraint(
                name: "ck_sales_issue_amounts",
                schema: "sales",
                table: "posting_entries");

            migrationBuilder.DropColumn(
                name: "AdjustsEntryId",
                schema: "sales",
                table: "posting_entries");

            migrationBuilder.DropColumn(
                name: "DebitNoteId",
                schema: "sales",
                table: "posting_entries");

            migrationBuilder.CreateIndex(
                name: "IX_posting_entries_TenantId_CompanyId_InvoiceId_LineId",
                schema: "sales",
                table: "posting_entries",
                columns: new[] { "TenantId", "CompanyId", "InvoiceId", "LineId" },
                unique: true,
                filter: "\"CreditNoteId\" IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_sales_issue_amounts",
                schema: "sales",
                table: "posting_entries",
                sql: "(\"CreditNoteId\" IS NULL AND \"ReversesEntryId\" IS NULL AND \"EntryType\" = 'Goods' AND \"Quantity\" < 0 AND \"NetAmount\" >= 0 AND \"OutputTaxAmount\" >= 0) OR (\"CreditNoteId\" IS NOT NULL AND \"ReversesEntryId\" IS NOT NULL AND ((\"EntryType\" = 'Goods' AND \"Quantity\" > 0) OR (\"EntryType\" = 'PriceCredit' AND \"Quantity\" = 0 AND \"NetAmount\" < 0)) AND \"NetAmount\" <= 0 AND \"OutputTaxAmount\" <= 0)");
        }
    }
}
