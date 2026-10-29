using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SalesInvoiceWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "sales");

            migrationBuilder.CreateTable(
                name: "sales_invoices",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CustomerReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ApprovedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CancellationReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    NetAmount = table.Column<decimal>(type: "numeric(24,4)", precision: 24, scale: 4, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_invoices", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_sales_invoice_net", "\"NetAmount\" >= 0");
                    table.ForeignKey(
                        name: "FK_sales_invoices_branches_TenantId_CompanyId_BranchId",
                        columns: x => new { x.TenantId, x.CompanyId, x.BranchId },
                        principalSchema: "company",
                        principalTable: "branches",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sales_invoice_lines",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SalesInvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(20,4)", precision: 20, scale: 4, nullable: false),
                    NetAmount = table.Column<decimal>(type: "numeric(24,4)", precision: 24, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_invoice_lines", x => new { x.TenantId, x.CompanyId, x.SalesInvoiceId, x.Id });
                    table.CheckConstraint("ck_line_price", "\"UnitPrice\" >= 0 AND \"UnitPrice\" <= 1000000000");
                    table.CheckConstraint("ck_line_quantity", "\"Quantity\" > 0 AND \"Quantity\" <= 1000000");
                    table.ForeignKey(
                        name: "FK_sales_invoice_lines_sales_invoices_TenantId_CompanyId_Sales~",
                        columns: x => new { x.TenantId, x.CompanyId, x.SalesInvoiceId },
                        principalSchema: "sales",
                        principalTable: "sales_invoices",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sales_invoices_TenantId_CompanyId_BranchId",
                schema: "sales",
                table: "sales_invoices",
                columns: new[] { "TenantId", "CompanyId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_invoices_TenantId_CompanyId_ExternalReference",
                schema: "sales",
                table: "sales_invoices",
                columns: new[] { "TenantId", "CompanyId", "ExternalReference" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sales_invoice_lines",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "sales_invoices",
                schema: "sales");
        }
    }
}
