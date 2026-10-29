using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SalesInvoiceDueDates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "DueDate",
                schema: "sales",
                table: "sales_invoices",
                type: "date",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_sales_invoice_due_date",
                schema: "sales",
                table: "sales_invoices",
                sql: "\"DueDate\" IS NULL OR \"DueDate\" >= \"BusinessDate\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_sales_invoice_due_date",
                schema: "sales",
                table: "sales_invoices");

            migrationBuilder.DropColumn(
                name: "DueDate",
                schema: "sales",
                table: "sales_invoices");
        }
    }
}
