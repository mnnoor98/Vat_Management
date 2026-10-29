using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vat.Manufacturing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ManufacturingOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "manufacturing");

            migrationBuilder.CreateTable(
                name: "bom_headers",
                schema: "manufacturing",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bom_headers", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_bom_headers_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                });

            migrationBuilder.CreateTable(
                name: "coefficient_versions",
                schema: "manufacturing",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    BomId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    OutputQuantity = table.Column<decimal>(type: "numeric(24,6)", precision: 24, scale: 6, nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    State = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ApprovedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_coefficient_versions", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_coefficient_versions_rules", "(\"OutputQuantity\" > 0) AND \"Revision\" > 0 AND (\"EffectiveUntil\" IS NULL OR \"EffectiveUntil\" >= \"EffectiveFrom\") AND (\"ApprovedBy\" IS NULL OR \"ApprovedBy\" <> \"CreatedBy\") AND \"State\" IN ('Draft','Pending','Approved','Rejected','Cancelled','Running','Completed','Failed','Submitted','Posted')");
                    table.CheckConstraint("ck_coefficient_versions_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_coefficient_versions_bom_headers_TenantId_CompanyId_BomId",
                        columns: x => new { x.TenantId, x.CompanyId, x.BomId },
                        principalSchema: "manufacturing",
                        principalTable: "bom_headers",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "bom_lines",
                schema: "manufacturing",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    CoefficientVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(24,6)", precision: 24, scale: 6, nullable: false),
                    WastagePercent = table.Column<decimal>(type: "numeric(24,6)", precision: 24, scale: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bom_lines", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_bom_lines_rules", "(\"Quantity\" > 0 AND \"WastagePercent\" BETWEEN 0 AND 100)");
                    table.CheckConstraint("ck_bom_lines_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_bom_lines_coefficient_versions_TenantId_CompanyId_Coefficie~",
                        columns: x => new { x.TenantId, x.CompanyId, x.CoefficientVersionId },
                        principalSchema: "manufacturing",
                        principalTable: "coefficient_versions",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "production_orders",
                schema: "manufacturing",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    CoefficientVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PlannedQuantity = table.Column<decimal>(type: "numeric(24,6)", precision: 24, scale: 6, nullable: false),
                    State = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ApprovedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_production_orders", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_production_orders_rules", "(\"PlannedQuantity\" > 0) AND (\"ApprovedBy\" IS NULL OR \"ApprovedBy\" <> \"CreatedBy\") AND \"State\" IN ('Draft','Pending','Approved','Rejected','Cancelled','Running','Completed','Failed','Submitted','Posted')");
                    table.CheckConstraint("ck_production_orders_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_production_orders_coefficient_versions_TenantId_CompanyId_C~",
                        columns: x => new { x.TenantId, x.CompanyId, x.CoefficientVersionId },
                        principalSchema: "manufacturing",
                        principalTable: "coefficient_versions",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "material_issues",
                schema: "manufacturing",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    ProductionOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(24,6)", precision: 24, scale: 6, nullable: false),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SourceReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_material_issues", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_material_issues_rules", "\"Quantity\" > 0");
                    table.CheckConstraint("ck_material_issues_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_material_issues_production_orders_TenantId_CompanyId_Produc~",
                        columns: x => new { x.TenantId, x.CompanyId, x.ProductionOrderId },
                        principalSchema: "manufacturing",
                        principalTable: "production_orders",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "production_receipts",
                schema: "manufacturing",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    ProductionOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(24,6)", precision: 24, scale: 6, nullable: false),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SourceReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_production_receipts", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_production_receipts_rules", "\"Quantity\" > 0");
                    table.CheckConstraint("ck_production_receipts_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_production_receipts_production_orders_TenantId_CompanyId_Pr~",
                        columns: x => new { x.TenantId, x.CompanyId, x.ProductionOrderId },
                        principalSchema: "manufacturing",
                        principalTable: "production_orders",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "wastage_records",
                schema: "manufacturing",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    ProductionOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(24,6)", precision: 24, scale: 6, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ApprovedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wastage_records", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_wastage_records_rules", "(\"Quantity\" > 0) AND (\"ApprovedBy\" IS NULL OR \"ApprovedBy\" <> \"CreatedBy\")");
                    table.CheckConstraint("ck_wastage_records_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_wastage_records_production_orders_TenantId_CompanyId_Produc~",
                        columns: x => new { x.TenantId, x.CompanyId, x.ProductionOrderId },
                        principalSchema: "manufacturing",
                        principalTable: "production_orders",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bom_headers_TenantId_CompanyId_Code",
                schema: "manufacturing",
                table: "bom_headers",
                columns: new[] { "TenantId", "CompanyId", "Code" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_bom_headers_TenantId_CompanyId_CreatedAt",
                schema: "manufacturing",
                table: "bom_headers",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_bom_headers_TenantId_CompanyId_ProductId",
                schema: "manufacturing",
                table: "bom_headers",
                columns: new[] { "TenantId", "CompanyId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_bom_lines_TenantId_CompanyId_CoefficientVersionId",
                schema: "manufacturing",
                table: "bom_lines",
                columns: new[] { "TenantId", "CompanyId", "CoefficientVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_bom_lines_TenantId_CompanyId_CoefficientVersionId_ProductId",
                schema: "manufacturing",
                table: "bom_lines",
                columns: new[] { "TenantId", "CompanyId", "CoefficientVersionId", "ProductId" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_bom_lines_TenantId_CompanyId_CreatedAt",
                schema: "manufacturing",
                table: "bom_lines",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_bom_lines_TenantId_CompanyId_ProductId",
                schema: "manufacturing",
                table: "bom_lines",
                columns: new[] { "TenantId", "CompanyId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_coefficient_versions_TenantId_CompanyId_BomId",
                schema: "manufacturing",
                table: "coefficient_versions",
                columns: new[] { "TenantId", "CompanyId", "BomId" });

            migrationBuilder.CreateIndex(
                name: "IX_coefficient_versions_TenantId_CompanyId_BomId_Revision",
                schema: "manufacturing",
                table: "coefficient_versions",
                columns: new[] { "TenantId", "CompanyId", "BomId", "Revision" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_coefficient_versions_TenantId_CompanyId_CreatedAt",
                schema: "manufacturing",
                table: "coefficient_versions",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_coefficient_versions_TenantId_CompanyId_State_CreatedAt",
                schema: "manufacturing",
                table: "coefficient_versions",
                columns: new[] { "TenantId", "CompanyId", "State", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_material_issues_TenantId_CompanyId_BusinessDate",
                schema: "manufacturing",
                table: "material_issues",
                columns: new[] { "TenantId", "CompanyId", "BusinessDate" });

            migrationBuilder.CreateIndex(
                name: "IX_material_issues_TenantId_CompanyId_CreatedAt",
                schema: "manufacturing",
                table: "material_issues",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_material_issues_TenantId_CompanyId_ProductId",
                schema: "manufacturing",
                table: "material_issues",
                columns: new[] { "TenantId", "CompanyId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_material_issues_TenantId_CompanyId_ProductionOrderId",
                schema: "manufacturing",
                table: "material_issues",
                columns: new[] { "TenantId", "CompanyId", "ProductionOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_material_issues_TenantId_CompanyId_SourceReference",
                schema: "manufacturing",
                table: "material_issues",
                columns: new[] { "TenantId", "CompanyId", "SourceReference" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_material_issues_TenantId_CompanyId_WarehouseId",
                schema: "manufacturing",
                table: "material_issues",
                columns: new[] { "TenantId", "CompanyId", "WarehouseId" });

            migrationBuilder.CreateIndex(
                name: "IX_production_orders_TenantId_CompanyId_BusinessDate",
                schema: "manufacturing",
                table: "production_orders",
                columns: new[] { "TenantId", "CompanyId", "BusinessDate" });

            migrationBuilder.CreateIndex(
                name: "IX_production_orders_TenantId_CompanyId_CoefficientVersionId",
                schema: "manufacturing",
                table: "production_orders",
                columns: new[] { "TenantId", "CompanyId", "CoefficientVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_production_orders_TenantId_CompanyId_CreatedAt",
                schema: "manufacturing",
                table: "production_orders",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_production_orders_TenantId_CompanyId_Reference",
                schema: "manufacturing",
                table: "production_orders",
                columns: new[] { "TenantId", "CompanyId", "Reference" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_production_orders_TenantId_CompanyId_State_CreatedAt",
                schema: "manufacturing",
                table: "production_orders",
                columns: new[] { "TenantId", "CompanyId", "State", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_production_orders_TenantId_CompanyId_WarehouseId",
                schema: "manufacturing",
                table: "production_orders",
                columns: new[] { "TenantId", "CompanyId", "WarehouseId" });

            migrationBuilder.CreateIndex(
                name: "IX_production_receipts_TenantId_CompanyId_BusinessDate",
                schema: "manufacturing",
                table: "production_receipts",
                columns: new[] { "TenantId", "CompanyId", "BusinessDate" });

            migrationBuilder.CreateIndex(
                name: "IX_production_receipts_TenantId_CompanyId_CreatedAt",
                schema: "manufacturing",
                table: "production_receipts",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_production_receipts_TenantId_CompanyId_ProductId",
                schema: "manufacturing",
                table: "production_receipts",
                columns: new[] { "TenantId", "CompanyId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_production_receipts_TenantId_CompanyId_ProductionOrderId",
                schema: "manufacturing",
                table: "production_receipts",
                columns: new[] { "TenantId", "CompanyId", "ProductionOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_production_receipts_TenantId_CompanyId_SourceReference",
                schema: "manufacturing",
                table: "production_receipts",
                columns: new[] { "TenantId", "CompanyId", "SourceReference" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_production_receipts_TenantId_CompanyId_WarehouseId",
                schema: "manufacturing",
                table: "production_receipts",
                columns: new[] { "TenantId", "CompanyId", "WarehouseId" });

            migrationBuilder.CreateIndex(
                name: "IX_wastage_records_TenantId_CompanyId_CreatedAt",
                schema: "manufacturing",
                table: "wastage_records",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_wastage_records_TenantId_CompanyId_ProductId",
                schema: "manufacturing",
                table: "wastage_records",
                columns: new[] { "TenantId", "CompanyId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_wastage_records_TenantId_CompanyId_ProductionOrderId",
                schema: "manufacturing",
                table: "wastage_records",
                columns: new[] { "TenantId", "CompanyId", "ProductionOrderId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bom_lines",
                schema: "manufacturing");

            migrationBuilder.DropTable(
                name: "material_issues",
                schema: "manufacturing");

            migrationBuilder.DropTable(
                name: "production_receipts",
                schema: "manufacturing");

            migrationBuilder.DropTable(
                name: "wastage_records",
                schema: "manufacturing");

            migrationBuilder.DropTable(
                name: "production_orders",
                schema: "manufacturing");

            migrationBuilder.DropTable(
                name: "coefficient_versions",
                schema: "manufacturing");

            migrationBuilder.DropTable(
                name: "bom_headers",
                schema: "manufacturing");
        }
    }
}
