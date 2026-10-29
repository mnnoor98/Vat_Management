using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SalesPriceCredits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_sales_issue_amounts",
                schema: "sales",
                table: "posting_entries");

            migrationBuilder.AddColumn<string>(
                name: "EntryType",
                schema: "sales",
                table: "posting_entries",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Goods");

            migrationBuilder.AddColumn<string>(
                name: "AdjustmentEvidenceReference",
                schema: "sales",
                table: "credit_notes",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_sales_issue_amounts",
                schema: "sales",
                table: "posting_entries",
                sql: "(\"CreditNoteId\" IS NULL AND \"ReversesEntryId\" IS NULL AND \"EntryType\" = 'Goods' AND \"Quantity\" < 0 AND \"NetAmount\" >= 0 AND \"OutputTaxAmount\" >= 0) OR (\"CreditNoteId\" IS NOT NULL AND \"ReversesEntryId\" IS NOT NULL AND ((\"EntryType\" = 'Goods' AND \"Quantity\" > 0) OR (\"EntryType\" = 'PriceCredit' AND \"Quantity\" = 0 AND \"NetAmount\" < 0)) AND \"NetAmount\" <= 0 AND \"OutputTaxAmount\" <= 0)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_sales_issue_amounts",
                schema: "sales",
                table: "posting_entries");

            migrationBuilder.DropColumn(
                name: "EntryType",
                schema: "sales",
                table: "posting_entries");

            migrationBuilder.DropColumn(
                name: "AdjustmentEvidenceReference",
                schema: "sales",
                table: "credit_notes");

            migrationBuilder.AddCheckConstraint(
                name: "ck_sales_issue_amounts",
                schema: "sales",
                table: "posting_entries",
                sql: "(\"CreditNoteId\" IS NULL AND \"ReversesEntryId\" IS NULL AND \"Quantity\" < 0 AND \"NetAmount\" >= 0 AND \"OutputTaxAmount\" >= 0) OR (\"CreditNoteId\" IS NOT NULL AND \"ReversesEntryId\" IS NOT NULL AND \"Quantity\" > 0 AND \"NetAmount\" <= 0 AND \"OutputTaxAmount\" <= 0)");
        }
    }
}
