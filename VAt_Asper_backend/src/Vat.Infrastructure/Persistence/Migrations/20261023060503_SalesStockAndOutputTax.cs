using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SalesStockAndOutputTax : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Treatment",
                schema: "tax",
                table: "tax_rule_versions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Purchase");

            migrationBuilder.AddColumn<decimal>(
                name: "ReservedQuantity",
                schema: "inventory",
                table: "stock_balances",
                type: "numeric(24,6)",
                precision: 24,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "fulfillments",
                schema: "sales",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SourceVersion = table.Column<long>(type: "bigint", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReservedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PostedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DocumentNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReservedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PostedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Snapshot = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fulfillments", x => new { x.TenantId, x.CompanyId, x.InvoiceId });
                    table.ForeignKey(
                        name: "FK_fulfillments_sales_invoices_TenantId_CompanyId_InvoiceId",
                        columns: x => new { x.TenantId, x.CompanyId, x.InvoiceId },
                        principalSchema: "sales",
                        principalTable: "sales_invoices",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_fulfillments_warehouses_TenantId_CompanyId_WarehouseId",
                        columns: x => new { x.TenantId, x.CompanyId, x.WarehouseId },
                        principalSchema: "inventory",
                        principalTable: "warehouses",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "posting_entries",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaxRuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(24,6)", precision: 24, scale: 6, nullable: false),
                    NetAmount = table.Column<decimal>(type: "numeric(24,4)", precision: 24, scale: 4, nullable: false),
                    OutputTaxAmount = table.Column<decimal>(type: "numeric(24,4)", precision: 24, scale: 4, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_posting_entries", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_sales_issue_amounts", "\"Quantity\" < 0 AND \"NetAmount\" >= 0 AND \"OutputTaxAmount\" >= 0");
                    table.ForeignKey(
                        name: "FK_posting_entries_fulfillments_TenantId_CompanyId_InvoiceId",
                        columns: x => new { x.TenantId, x.CompanyId, x.InvoiceId },
                        principalSchema: "sales",
                        principalTable: "fulfillments",
                        principalColumns: new[] { "TenantId", "CompanyId", "InvoiceId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_posting_entries_products_TenantId_CompanyId_ProductId",
                        columns: x => new { x.TenantId, x.CompanyId, x.ProductId },
                        principalSchema: "masterdata",
                        principalTable: "products",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_posting_entries_sales_invoice_lines_TenantId_CompanyId_Invo~",
                        columns: x => new { x.TenantId, x.CompanyId, x.InvoiceId, x.LineId },
                        principalSchema: "sales",
                        principalTable: "sales_invoice_lines",
                        principalColumns: new[] { "TenantId", "CompanyId", "SalesInvoiceId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_posting_entries_tax_rule_versions_TenantId_CompanyId_TaxRul~",
                        columns: x => new { x.TenantId, x.CompanyId, x.TaxRuleId },
                        principalSchema: "tax",
                        principalTable: "tax_rule_versions",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_posting_entries_warehouses_TenantId_CompanyId_WarehouseId",
                        columns: x => new { x.TenantId, x.CompanyId, x.WarehouseId },
                        principalSchema: "inventory",
                        principalTable: "warehouses",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_rule_treatment",
                schema: "tax",
                table: "tax_rule_versions",
                sql: "\"Treatment\" IN ('Purchase', 'Sale') AND (\"Treatment\" <> 'Sale' OR \"RecoverablePercent\" = 0)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_balance_reserved",
                schema: "inventory",
                table: "stock_balances",
                sql: "\"ReservedQuantity\" >= 0 AND \"ReservedQuantity\" <= \"Quantity\"");

            migrationBuilder.CreateIndex(
                name: "IX_fulfillments_TenantId_CompanyId_DocumentNumber",
                schema: "sales",
                table: "fulfillments",
                columns: new[] { "TenantId", "CompanyId", "DocumentNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_fulfillments_TenantId_CompanyId_WarehouseId",
                schema: "sales",
                table: "fulfillments",
                columns: new[] { "TenantId", "CompanyId", "WarehouseId" });

            migrationBuilder.CreateIndex(
                name: "IX_posting_entries_TenantId_CompanyId_BusinessDate",
                schema: "sales",
                table: "posting_entries",
                columns: new[] { "TenantId", "CompanyId", "BusinessDate" });

            migrationBuilder.CreateIndex(
                name: "IX_posting_entries_TenantId_CompanyId_InvoiceId_LineId",
                schema: "sales",
                table: "posting_entries",
                columns: new[] { "TenantId", "CompanyId", "InvoiceId", "LineId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_posting_entries_TenantId_CompanyId_ProductId",
                schema: "sales",
                table: "posting_entries",
                columns: new[] { "TenantId", "CompanyId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_posting_entries_TenantId_CompanyId_TaxRuleId",
                schema: "sales",
                table: "posting_entries",
                columns: new[] { "TenantId", "CompanyId", "TaxRuleId" });

            migrationBuilder.CreateIndex(
                name: "IX_posting_entries_TenantId_CompanyId_WarehouseId",
                schema: "sales",
                table: "posting_entries",
                columns: new[] { "TenantId", "CompanyId", "WarehouseId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "posting_entries",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "fulfillments",
                schema: "sales");

            migrationBuilder.DropCheckConstraint(
                name: "ck_rule_treatment",
                schema: "tax",
                table: "tax_rule_versions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_balance_reserved",
                schema: "inventory",
                table: "stock_balances");

            migrationBuilder.DropColumn(
                name: "Treatment",
                schema: "tax",
                table: "tax_rule_versions");

            migrationBuilder.DropColumn(
                name: "ReservedQuantity",
                schema: "inventory",
                table: "stock_balances");
        }
    }
}
