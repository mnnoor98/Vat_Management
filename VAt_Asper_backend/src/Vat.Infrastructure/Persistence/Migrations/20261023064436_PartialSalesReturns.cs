using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PartialSalesReturns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_posting_entries_TenantId_CompanyId_ReversesEntryId",
                schema: "sales",
                table: "posting_entries");

            migrationBuilder.DropIndex(
                name: "ux_sales_posted_credit",
                schema: "sales",
                table: "credit_notes");

            migrationBuilder.AddColumn<string>(
                name: "Lines",
                schema: "sales",
                table: "credit_notes",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<string>(
                name: "Mode",
                schema: "sales",
                table: "credit_notes",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "FullInvoice");

            migrationBuilder.CreateIndex(
                name: "IX_posting_entries_TenantId_CompanyId_CreditNoteId_ReversesEnt~",
                schema: "sales",
                table: "posting_entries",
                columns: new[] { "TenantId", "CompanyId", "CreditNoteId", "ReversesEntryId" },
                unique: true,
                filter: "\"ReversesEntryId\" IS NOT NULL");
            migrationBuilder.Sql("""
                UPDATE sales.credit_notes AS note
                SET "Lines" = COALESCE((
                    SELECT jsonb_agg(jsonb_build_object(
                        'originalEntryId', entry."Id", 'lineId', entry."LineId", 'quantity', -entry."Quantity",
                        'netAmount', -entry."NetAmount", 'outputTaxAmount', -entry."OutputTaxAmount") ORDER BY entry."LineId")
                    FROM sales.posting_entries AS entry
                    WHERE entry."TenantId" = note."TenantId" AND entry."CompanyId" = note."CompanyId"
                        AND entry."InvoiceId" = note."InvoiceId" AND entry."CreditNoteId" IS NULL
                ), '[]'::jsonb)
                WHERE note."Mode" = 'FullInvoice' AND note."Lines" = '[]'::jsonb
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_posting_entries_TenantId_CompanyId_CreditNoteId_ReversesEnt~",
                schema: "sales",
                table: "posting_entries");

            migrationBuilder.DropColumn(
                name: "Lines",
                schema: "sales",
                table: "credit_notes");

            migrationBuilder.DropColumn(
                name: "Mode",
                schema: "sales",
                table: "credit_notes");

            migrationBuilder.CreateIndex(
                name: "IX_posting_entries_TenantId_CompanyId_ReversesEntryId",
                schema: "sales",
                table: "posting_entries",
                columns: new[] { "TenantId", "CompanyId", "ReversesEntryId" },
                unique: true,
                filter: "\"ReversesEntryId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_sales_posted_credit",
                schema: "sales",
                table: "credit_notes",
                columns: new[] { "TenantId", "CompanyId", "InvoiceId" },
                unique: true,
                filter: "\"Status\" = 'Posted'");
        }
    }
}
