using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PurchasePosting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "masterdata");

            migrationBuilder.EnsureSchema(
                name: "inventory");

            migrationBuilder.EnsureSchema(
                name: "tax");

            migrationBuilder.CreateTable(
                name: "products",
                schema: "masterdata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    UnitOfMeasure = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TaxCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_products", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.ForeignKey(
                        name: "FK_products_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "suppliers",
                schema: "masterdata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_suppliers", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.ForeignKey(
                        name: "FK_suppliers_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tax_rule_versions",
                schema: "tax",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaxCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveUntil = table.Column<DateOnly>(type: "date", nullable: false),
                    RatePercent = table.Column<decimal>(type: "numeric(7,4)", precision: 7, scale: 4, nullable: false),
                    RecoverablePercent = table.Column<decimal>(type: "numeric(7,4)", precision: 7, scale: 4, nullable: false),
                    LegalBasis = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    EvidenceReference = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PublishedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tax_rule_versions", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_rule_interval", "\"EffectiveUntil\" > \"EffectiveFrom\"");
                    table.CheckConstraint("ck_rule_maker_checker", "\"PublishedBy\" IS NULL OR \"PublishedBy\" <> \"CreatedBy\"");
                    table.CheckConstraint("ck_rule_percentages", "\"RatePercent\" BETWEEN 0 AND 100 AND \"RecoverablePercent\" BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "FK_tax_rule_versions_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "warehouses",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_warehouses", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.ForeignKey(
                        name: "FK_warehouses_branches_TenantId_CompanyId_BranchId",
                        columns: x => new { x.TenantId, x.CompanyId, x.BranchId },
                        principalSchema: "company",
                        principalTable: "branches",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_posting_plans",
                schema: "purchasing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceVersion = table.Column<long>(type: "bigint", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    PreparedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PostedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PostedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Snapshot = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_posting_plans", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.ForeignKey(
                        name: "FK_purchase_posting_plans_purchases_TenantId_CompanyId_Purchas~",
                        columns: x => new { x.TenantId, x.CompanyId, x.PurchaseId },
                        principalSchema: "purchasing",
                        principalTable: "purchases",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_posting_plans_warehouses_TenantId_CompanyId_Wareho~",
                        columns: x => new { x.TenantId, x.CompanyId, x.WarehouseId },
                        principalSchema: "inventory",
                        principalTable: "warehouses",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_balances",
                schema: "inventory",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(24,6)", precision: 24, scale: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_balances", x => new { x.TenantId, x.CompanyId, x.WarehouseId, x.ProductId });
                    table.CheckConstraint("ck_balance_nonnegative", "\"Quantity\" >= 0");
                    table.ForeignKey(
                        name: "FK_stock_balances_products_TenantId_CompanyId_ProductId",
                        columns: x => new { x.TenantId, x.CompanyId, x.ProductId },
                        principalSchema: "masterdata",
                        principalTable: "products",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_balances_warehouses_TenantId_CompanyId_WarehouseId",
                        columns: x => new { x.TenantId, x.CompanyId, x.WarehouseId },
                        principalSchema: "inventory",
                        principalTable: "warehouses",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_movements",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseLineId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(24,6)", precision: 24, scale: 6, nullable: false),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_movements", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_receipt_positive", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_stock_movements_products_TenantId_CompanyId_ProductId",
                        columns: x => new { x.TenantId, x.CompanyId, x.ProductId },
                        principalSchema: "masterdata",
                        principalTable: "products",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_movements_purchase_lines_TenantId_CompanyId_PurchaseI~",
                        columns: x => new { x.TenantId, x.CompanyId, x.PurchaseId, x.PurchaseLineId },
                        principalSchema: "purchasing",
                        principalTable: "purchase_lines",
                        principalColumns: new[] { "TenantId", "CompanyId", "PurchaseId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_movements_purchase_posting_plans_TenantId_CompanyId_P~",
                        columns: x => new { x.TenantId, x.CompanyId, x.PlanId },
                        principalSchema: "purchasing",
                        principalTable: "purchase_posting_plans",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_movements_warehouses_TenantId_CompanyId_WarehouseId",
                        columns: x => new { x.TenantId, x.CompanyId, x.WarehouseId },
                        principalSchema: "inventory",
                        principalTable: "warehouses",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tax_ledger",
                schema: "tax",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseLineId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaxRuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    NetAmount = table.Column<decimal>(type: "numeric(24,4)", precision: 24, scale: 4, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(24,4)", precision: 24, scale: 4, nullable: false),
                    RecoverableTaxAmount = table.Column<decimal>(type: "numeric(24,4)", precision: 24, scale: 4, nullable: false),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tax_ledger", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_input_tax_amounts", "\"NetAmount\" >= 0 AND \"TaxAmount\" >= 0 AND \"RecoverableTaxAmount\" BETWEEN 0 AND \"TaxAmount\"");
                    table.ForeignKey(
                        name: "FK_tax_ledger_purchase_lines_TenantId_CompanyId_PurchaseId_Pur~",
                        columns: x => new { x.TenantId, x.CompanyId, x.PurchaseId, x.PurchaseLineId },
                        principalSchema: "purchasing",
                        principalTable: "purchase_lines",
                        principalColumns: new[] { "TenantId", "CompanyId", "PurchaseId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tax_ledger_purchase_posting_plans_TenantId_CompanyId_PlanId",
                        columns: x => new { x.TenantId, x.CompanyId, x.PlanId },
                        principalSchema: "purchasing",
                        principalTable: "purchase_posting_plans",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tax_ledger_tax_rule_versions_TenantId_CompanyId_TaxRuleId",
                        columns: x => new { x.TenantId, x.CompanyId, x.TaxRuleId },
                        principalSchema: "tax",
                        principalTable: "tax_rule_versions",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_products_TenantId_CompanyId_Reference",
                schema: "masterdata",
                table: "products",
                columns: new[] { "TenantId", "CompanyId", "Reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_posting_plans_TenantId_CompanyId_PurchaseId",
                schema: "purchasing",
                table: "purchase_posting_plans",
                columns: new[] { "TenantId", "CompanyId", "PurchaseId" },
                unique: true,
                filter: "\"Status\" = 'Prepared'");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_posting_plans_TenantId_CompanyId_PurchaseId_Source~",
                schema: "purchasing",
                table: "purchase_posting_plans",
                columns: new[] { "TenantId", "CompanyId", "PurchaseId", "SourceVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_posting_plans_TenantId_CompanyId_WarehouseId",
                schema: "purchasing",
                table: "purchase_posting_plans",
                columns: new[] { "TenantId", "CompanyId", "WarehouseId" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_balances_TenantId_CompanyId_ProductId",
                schema: "inventory",
                table: "stock_balances",
                columns: new[] { "TenantId", "CompanyId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_TenantId_CompanyId_PlanId",
                schema: "inventory",
                table: "stock_movements",
                columns: new[] { "TenantId", "CompanyId", "PlanId" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_TenantId_CompanyId_ProductId",
                schema: "inventory",
                table: "stock_movements",
                columns: new[] { "TenantId", "CompanyId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_TenantId_CompanyId_PurchaseId_PurchaseLineId",
                schema: "inventory",
                table: "stock_movements",
                columns: new[] { "TenantId", "CompanyId", "PurchaseId", "PurchaseLineId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_TenantId_CompanyId_WarehouseId",
                schema: "inventory",
                table: "stock_movements",
                columns: new[] { "TenantId", "CompanyId", "WarehouseId" });

            migrationBuilder.CreateIndex(
                name: "IX_suppliers_TenantId_CompanyId_Reference",
                schema: "masterdata",
                table: "suppliers",
                columns: new[] { "TenantId", "CompanyId", "Reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tax_ledger_TenantId_CompanyId_PlanId",
                schema: "tax",
                table: "tax_ledger",
                columns: new[] { "TenantId", "CompanyId", "PlanId" });

            migrationBuilder.CreateIndex(
                name: "IX_tax_ledger_TenantId_CompanyId_PurchaseId_PurchaseLineId",
                schema: "tax",
                table: "tax_ledger",
                columns: new[] { "TenantId", "CompanyId", "PurchaseId", "PurchaseLineId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tax_ledger_TenantId_CompanyId_TaxRuleId",
                schema: "tax",
                table: "tax_ledger",
                columns: new[] { "TenantId", "CompanyId", "TaxRuleId" });

            migrationBuilder.CreateIndex(
                name: "IX_tax_rule_versions_TenantId_CompanyId_TaxCode_EffectiveFrom",
                schema: "tax",
                table: "tax_rule_versions",
                columns: new[] { "TenantId", "CompanyId", "TaxCode", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_warehouses_TenantId_CompanyId_BranchId",
                schema: "inventory",
                table: "warehouses",
                columns: new[] { "TenantId", "CompanyId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_warehouses_TenantId_CompanyId_Code",
                schema: "inventory",
                table: "warehouses",
                columns: new[] { "TenantId", "CompanyId", "Code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "stock_balances",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "stock_movements",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "suppliers",
                schema: "masterdata");

            migrationBuilder.DropTable(
                name: "tax_ledger",
                schema: "tax");

            migrationBuilder.DropTable(
                name: "products",
                schema: "masterdata");

            migrationBuilder.DropTable(
                name: "purchase_posting_plans",
                schema: "purchasing");

            migrationBuilder.DropTable(
                name: "tax_rule_versions",
                schema: "tax");

            migrationBuilder.DropTable(
                name: "warehouses",
                schema: "inventory");
        }
    }
}
