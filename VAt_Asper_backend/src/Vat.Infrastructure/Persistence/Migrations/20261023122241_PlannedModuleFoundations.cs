using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PlannedModuleFoundations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "workflow");

            migrationBuilder.EnsureSchema(
                name: "manufacturing");

            migrationBuilder.EnsureSchema(
                name: "integration");

            migrationBuilder.EnsureSchema(
                name: "compliance");

            migrationBuilder.EnsureSchema(
                name: "identity");

            migrationBuilder.CreateTable(
                name: "adjustments",
                schema: "inventory",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    State = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_adjustments", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_adjustments_rules", "(\"ApprovedBy\" IS NULL OR \"ApprovedBy\" <> \"CreatedBy\") AND \"State\" IN ('Draft','Pending','Approved','Rejected','Cancelled','Running','Completed','Failed','Submitted','Posted')");
                    table.CheckConstraint("ck_adjustments_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_adjustments_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_adjustments_warehouses_TenantId_CompanyId_WarehouseId",
                        columns: x => new { x.TenantId, x.CompanyId, x.WarehouseId },
                        principalSchema: "inventory",
                        principalTable: "warehouses",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "bom_headers",
                schema: "manufacturing",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bom_headers", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_bom_headers_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_bom_headers_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_bom_headers_products_TenantId_CompanyId_ProductId",
                        columns: x => new { x.TenantId, x.CompanyId, x.ProductId },
                        principalSchema: "masterdata",
                        principalTable: "products",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "challan_numbers",
                schema: "sales",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FiscalYear = table.Column<int>(type: "integer", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Number = table.Column<long>(type: "bigint", nullable: false),
                    Series = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    VoidReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    VoidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_challan_numbers", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_challan_numbers_rules", "(\"Number\" > 0 AND \"FiscalYear\" BETWEEN 1900 AND 9999 AND ((\"VoidedAt\" IS NULL AND \"VoidReason\" IS NULL) OR (\"VoidedAt\" IS NOT NULL AND length(trim(\"VoidReason\")) > 0)))");
                    table.CheckConstraint("ck_challan_numbers_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_challan_numbers_branches_TenantId_CompanyId_BranchId",
                        columns: x => new { x.TenantId, x.CompanyId, x.BranchId },
                        principalSchema: "company",
                        principalTable: "branches",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_challan_numbers_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_challan_numbers_sales_invoices_TenantId_CompanyId_InvoiceId",
                        columns: x => new { x.TenantId, x.CompanyId, x.InvoiceId },
                        principalSchema: "sales",
                        principalTable: "sales_invoices",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "company_settings",
                schema: "company",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Value = table.Column<string>(type: "jsonb", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_company_settings", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_company_settings_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_company_settings_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "counterparty_registrations",
                schema: "masterdata",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    Reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RegistrationType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_counterparty_registrations", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_counterparty_registrations_rules", "((\"CustomerId\" IS NULL) <> (\"SupplierId\" IS NULL)) AND (\"EffectiveUntil\" IS NULL OR \"EffectiveUntil\" >= \"EffectiveFrom\")");
                    table.CheckConstraint("ck_counterparty_registrations_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_counterparty_registrations_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_counterparty_registrations_customers_TenantId_CompanyId_Cus~",
                        columns: x => new { x.TenantId, x.CompanyId, x.CustomerId },
                        principalSchema: "sales",
                        principalTable: "customers",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_counterparty_registrations_suppliers_TenantId_CompanyId_Sup~",
                        columns: x => new { x.TenantId, x.CompanyId, x.SupplierId },
                        principalSchema: "masterdata",
                        principalTable: "suppliers",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "delegations",
                schema: "workflow",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Delegate = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Delegator = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_delegations", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_delegations_rules", "(\"Delegator\" <> \"Delegate\" AND \"ExpiresAt\" > \"StartsAt\")");
                    table.CheckConstraint("ck_delegations_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_delegations_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "delivery_references",
                schema: "sales",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DeliveredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EvidenceKey = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_delivery_references", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_delivery_references_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_delivery_references_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_delivery_references_sales_invoices_TenantId_CompanyId_Invoi~",
                        columns: x => new { x.TenantId, x.CompanyId, x.InvoiceId },
                        principalSchema: "sales",
                        principalTable: "sales_invoices",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "form_templates",
                schema: "compliance",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_form_templates", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_form_templates_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_form_templates_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "hs_codes",
                schema: "masterdata",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hs_codes", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_hs_codes_rules", "(\"EffectiveUntil\" IS NULL OR \"EffectiveUntil\" >= \"EffectiveFrom\")");
                    table.CheckConstraint("ck_hs_codes_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_hs_codes_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "integration_connections",
                schema: "integration",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    Endpoint = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Provider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SecretReference = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_integration_connections", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_integration_connections_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_integration_connections_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "menu_items",
                schema: "identity",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Route = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_menu_items", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_menu_items_rules", "(\"ParentId\" IS NULL OR \"ParentId\" <> \"Id\")");
                    table.CheckConstraint("ck_menu_items_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_menu_items_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_menu_items_menu_items_TenantId_CompanyId_ParentId",
                        columns: x => new { x.TenantId, x.CompanyId, x.ParentId },
                        principalSchema: "identity",
                        principalTable: "menu_items",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "permissions",
                schema: "identity",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permissions", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_permissions_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_permissions_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_attachments",
                schema: "purchasing",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ObjectKey = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    PurchaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_attachments", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_purchase_attachments_rules", "(\"SizeBytes\" > 0 AND length(\"Sha256\") = 64)");
                    table.CheckConstraint("ck_purchase_attachments_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_purchase_attachments_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_attachments_purchases_TenantId_CompanyId_PurchaseId",
                        columns: x => new { x.TenantId, x.CompanyId, x.PurchaseId },
                        principalSchema: "purchasing",
                        principalTable: "purchases",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_posting_sagas",
                schema: "purchasing",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PurchaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceVersion = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_posting_sagas", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_purchase_posting_sagas_rules", "(\"SourceVersion\" > 0 AND \"AttemptCount\" >= 0 AND \"State\" IN ('Pending','Running','Completed','Failed'))");
                    table.CheckConstraint("ck_purchase_posting_sagas_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_purchase_posting_sagas_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_posting_sagas_purchases_TenantId_CompanyId_Purchas~",
                        columns: x => new { x.TenantId, x.CompanyId, x.PurchaseId },
                        principalSchema: "purchasing",
                        principalTable: "purchases",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "reconciliation_alerts",
                schema: "reporting",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResolvedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Severity = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reconciliation_alerts", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_reconciliation_alerts_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_reconciliation_alerts_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "registrations",
                schema: "company",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    Reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RegistrationType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_registrations", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_registrations_rules", "(\"EffectiveUntil\" IS NULL OR \"EffectiveUntil\" >= \"EffectiveFrom\") AND (\"ApprovedBy\" IS NULL OR \"ApprovedBy\" <> \"CreatedBy\")");
                    table.CheckConstraint("ck_registrations_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_registrations_branches_TenantId_CompanyId_BranchId",
                        columns: x => new { x.TenantId, x.CompanyId, x.BranchId },
                        principalSchema: "company",
                        principalTable: "branches",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_registrations_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "report_templates",
                schema: "reporting",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Definition = table.Column<string>(type: "jsonb", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_report_templates", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_report_templates_rules", "\"Revision\" > 0 AND (\"ApprovedBy\" IS NULL OR \"ApprovedBy\" <> \"CreatedBy\")");
                    table.CheckConstraint("ck_report_templates_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_report_templates_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "return_periods",
                schema: "compliance",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FiscalPeriodId = table.Column<Guid>(type: "uuid", nullable: false),
                    From = table.Column<DateOnly>(type: "date", nullable: false),
                    Until = table.Column<DateOnly>(type: "date", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_return_periods", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_return_periods_rules", "(\"Until\" >= \"From\")");
                    table.CheckConstraint("ck_return_periods_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_return_periods_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_return_periods_fiscal_periods_TenantId_CompanyId_FiscalPeri~",
                        columns: x => new { x.TenantId, x.CompanyId, x.FiscalPeriodId },
                        principalSchema: "company",
                        principalTable: "fiscal_periods",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                schema: "identity",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_roles_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_roles_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tax_posting_batches",
                schema: "tax",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ExpectedTax = table.Column<decimal>(type: "numeric(24,6)", precision: 24, scale: 6, nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SourceVersion = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tax_posting_batches", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_tax_posting_batches_rules", "\"SourceVersion\" > 0 AND \"State\" IN ('Draft','Pending','Approved','Rejected','Cancelled','Running','Completed','Failed','Submitted','Posted')");
                    table.CheckConstraint("ck_tax_posting_batches_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_tax_posting_batches_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tax_treatments",
                schema: "tax",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tax_treatments", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_tax_treatments_rules", "(\"EffectiveUntil\" IS NULL OR \"EffectiveUntil\" >= \"EffectiveFrom\") AND (\"ApprovedBy\" IS NULL OR \"ApprovedBy\" <> \"CreatedBy\")");
                    table.CheckConstraint("ck_tax_treatments_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_tax_treatments_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "transfers",
                schema: "inventory",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FromWarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    State = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ToWarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transfers", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_transfers_rules", "(\"FromWarehouseId\" <> \"ToWarehouseId\") AND (\"ApprovedBy\" IS NULL OR \"ApprovedBy\" <> \"CreatedBy\") AND \"State\" IN ('Draft','Pending','Approved','Rejected','Cancelled','Running','Completed','Failed','Submitted','Posted')");
                    table.CheckConstraint("ck_transfers_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_transfers_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_transfers_warehouses_TenantId_CompanyId_FromWarehouseId",
                        columns: x => new { x.TenantId, x.CompanyId, x.FromWarehouseId },
                        principalSchema: "inventory",
                        principalTable: "warehouses",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_transfers_warehouses_TenantId_CompanyId_ToWarehouseId",
                        columns: x => new { x.TenantId, x.CompanyId, x.ToWarehouseId },
                        principalSchema: "inventory",
                        principalTable: "warehouses",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "uoms",
                schema: "masterdata",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DecimalPlaces = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_uoms", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_uoms_rules", "(\"DecimalPlaces\" BETWEEN 0 AND 6)");
                    table.CheckConstraint("ck_uoms_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_uoms_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "users",
                schema: "identity",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Disabled = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Issuer = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_users_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_users_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "workflow_definitions",
                schema: "workflow",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflow_definitions", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_workflow_definitions_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_workflow_definitions_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "adjustment_lines",
                schema: "inventory",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdjustmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(24,6)", precision: 24, scale: 6, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_adjustment_lines", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_adjustment_lines_rules", "(\"Quantity\" <> 0)");
                    table.CheckConstraint("ck_adjustment_lines_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_adjustment_lines_adjustments_TenantId_CompanyId_AdjustmentId",
                        columns: x => new { x.TenantId, x.CompanyId, x.AdjustmentId },
                        principalSchema: "inventory",
                        principalTable: "adjustments",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_adjustment_lines_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_adjustment_lines_products_TenantId_CompanyId_ProductId",
                        columns: x => new { x.TenantId, x.CompanyId, x.ProductId },
                        principalSchema: "masterdata",
                        principalTable: "products",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "coefficient_versions",
                schema: "manufacturing",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BomId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    OutputQuantity = table.Column<decimal>(type: "numeric(24,6)", precision: 24, scale: 6, nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
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
                    table.ForeignKey(
                        name: "FK_coefficient_versions_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "form_versions",
                schema: "compliance",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    Mapping = table.Column<string>(type: "jsonb", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_form_versions", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_form_versions_rules", "\"Revision\" > 0 AND (\"EffectiveUntil\" IS NULL OR \"EffectiveUntil\" >= \"EffectiveFrom\") AND (\"ApprovedBy\" IS NULL OR \"ApprovedBy\" <> \"CreatedBy\")");
                    table.CheckConstraint("ck_form_versions_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_form_versions_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_form_versions_form_templates_TenantId_CompanyId_TemplateId",
                        columns: x => new { x.TenantId, x.CompanyId, x.TemplateId },
                        principalSchema: "compliance",
                        principalTable: "form_templates",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "external_references",
                schema: "integration",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_external_references", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_external_references_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_external_references_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_external_references_integration_connections_TenantId_Compan~",
                        columns: x => new { x.TenantId, x.CompanyId, x.ConnectionId },
                        principalSchema: "integration",
                        principalTable: "integration_connections",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "field_mappings",
                schema: "integration",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DocumentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Mapping = table.Column<string>(type: "jsonb", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_field_mappings", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_field_mappings_rules", "\"Revision\" > 0 AND (\"ApprovedBy\" IS NULL OR \"ApprovedBy\" <> \"CreatedBy\")");
                    table.CheckConstraint("ck_field_mappings_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_field_mappings_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_field_mappings_integration_connections_TenantId_CompanyId_C~",
                        columns: x => new { x.TenantId, x.CompanyId, x.ConnectionId },
                        principalSchema: "integration",
                        principalTable: "integration_connections",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "webhook_subscriptions",
                schema: "integration",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    Endpoint = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    EventType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SigningSecretReference = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_webhook_subscriptions", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_webhook_subscriptions_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_webhook_subscriptions_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_webhook_subscriptions_integration_connections_TenantId_Comp~",
                        columns: x => new { x.TenantId, x.CompanyId, x.ConnectionId },
                        principalSchema: "integration",
                        principalTable: "integration_connections",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "report_jobs",
                schema: "reporting",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Parameters = table.Column<string>(type: "jsonb", nullable: false),
                    RequestedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    State = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    Watermark = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_report_jobs", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_report_jobs_rules", "\"State\" IN ('Draft','Pending','Approved','Rejected','Cancelled','Running','Completed','Failed','Submitted','Posted')");
                    table.CheckConstraint("ck_report_jobs_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_report_jobs_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_report_jobs_report_templates_TenantId_CompanyId_TemplateId",
                        columns: x => new { x.TenantId, x.CompanyId, x.TemplateId },
                        principalSchema: "reporting",
                        principalTable: "report_templates",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "role_menus",
                schema: "identity",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MenuId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_menus", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_role_menus_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_role_menus_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_role_menus_menu_items_TenantId_CompanyId_MenuId",
                        columns: x => new { x.TenantId, x.CompanyId, x.MenuId },
                        principalSchema: "identity",
                        principalTable: "menu_items",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_role_menus_roles_TenantId_CompanyId_RoleId",
                        columns: x => new { x.TenantId, x.CompanyId, x.RoleId },
                        principalSchema: "identity",
                        principalTable: "roles",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "role_permissions",
                schema: "identity",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PermissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_permissions", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_role_permissions_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_role_permissions_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_role_permissions_permissions_TenantId_CompanyId_PermissionId",
                        columns: x => new { x.TenantId, x.CompanyId, x.PermissionId },
                        principalSchema: "identity",
                        principalTable: "permissions",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_role_permissions_roles_TenantId_CompanyId_RoleId",
                        columns: x => new { x.TenantId, x.CompanyId, x.RoleId },
                        principalSchema: "identity",
                        principalTable: "roles",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "vds_entries",
                schema: "tax",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(24,6)", precision: 24, scale: 6, nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EvidenceReference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ReversalOfId = table.Column<Guid>(type: "uuid", nullable: true),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vds_entries", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_vds_entries_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_vds_entries_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_vds_entries_suppliers_TenantId_CompanyId_SupplierId",
                        columns: x => new { x.TenantId, x.CompanyId, x.SupplierId },
                        principalSchema: "masterdata",
                        principalTable: "suppliers",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_vds_entries_tax_posting_batches_TenantId_CompanyId_BatchId",
                        columns: x => new { x.TenantId, x.CompanyId, x.BatchId },
                        principalSchema: "tax",
                        principalTable: "tax_posting_batches",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_vds_entries_vds_entries_TenantId_CompanyId_ReversalOfId",
                        columns: x => new { x.TenantId, x.CompanyId, x.ReversalOfId },
                        principalSchema: "tax",
                        principalTable: "vds_entries",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tax_calculation_snapshots",
                schema: "tax",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Calculation = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RuleVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SourceVersion = table.Column<int>(type: "integer", nullable: false),
                    TreatmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tax_calculation_snapshots", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_tax_calculation_snapshots_rules", "\"SourceVersion\" > 0");
                    table.CheckConstraint("ck_tax_calculation_snapshots_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_tax_calculation_snapshots_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tax_calculation_snapshots_tax_rule_versions_TenantId_Compan~",
                        columns: x => new { x.TenantId, x.CompanyId, x.RuleVersionId },
                        principalSchema: "tax",
                        principalTable: "tax_rule_versions",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tax_calculation_snapshots_tax_treatments_TenantId_CompanyId~",
                        columns: x => new { x.TenantId, x.CompanyId, x.TreatmentId },
                        principalSchema: "tax",
                        principalTable: "tax_treatments",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "transfer_lines",
                schema: "inventory",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(24,6)", precision: 24, scale: 6, nullable: false),
                    TransferId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transfer_lines", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_transfer_lines_rules", "\"Quantity\" > 0");
                    table.CheckConstraint("ck_transfer_lines_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_transfer_lines_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_transfer_lines_products_TenantId_CompanyId_ProductId",
                        columns: x => new { x.TenantId, x.CompanyId, x.ProductId },
                        principalSchema: "masterdata",
                        principalTable: "products",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_transfer_lines_transfers_TenantId_CompanyId_TransferId",
                        columns: x => new { x.TenantId, x.CompanyId, x.TransferId },
                        principalSchema: "inventory",
                        principalTable: "transfers",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "product_versions",
                schema: "masterdata",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    UomId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_versions", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_product_versions_rules", "\"Revision\" > 0 AND (\"EffectiveUntil\" IS NULL OR \"EffectiveUntil\" >= \"EffectiveFrom\") AND (\"ApprovedBy\" IS NULL OR \"ApprovedBy\" <> \"CreatedBy\")");
                    table.CheckConstraint("ck_product_versions_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_product_versions_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_product_versions_products_TenantId_CompanyId_ProductId",
                        columns: x => new { x.TenantId, x.CompanyId, x.ProductId },
                        principalSchema: "masterdata",
                        principalTable: "products",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_product_versions_uoms_TenantId_CompanyId_UomId",
                        columns: x => new { x.TenantId, x.CompanyId, x.UomId },
                        principalSchema: "masterdata",
                        principalTable: "uoms",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "branch_access",
                schema: "company",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PermissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_branch_access", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_branch_access_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_branch_access_branches_TenantId_CompanyId_BranchId",
                        columns: x => new { x.TenantId, x.CompanyId, x.BranchId },
                        principalSchema: "company",
                        principalTable: "branches",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_branch_access_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_branch_access_permissions_TenantId_CompanyId_PermissionId",
                        columns: x => new { x.TenantId, x.CompanyId, x.PermissionId },
                        principalSchema: "identity",
                        principalTable: "permissions",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_branch_access_users_TenantId_CompanyId_UserId",
                        columns: x => new { x.TenantId, x.CompanyId, x.UserId },
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "refresh_sessions",
                schema: "identity",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FamilyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReplacedById = table.Column<Guid>(type: "uuid", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TokenDigest = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_sessions", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_refresh_sessions_rules", "(\"ExpiresAt\" > \"CreatedAt\" AND (\"RevokedAt\" IS NULL OR \"RevokedAt\" >= \"CreatedAt\") AND (\"ReplacedById\" IS NULL OR \"ReplacedById\" <> \"Id\"))");
                    table.CheckConstraint("ck_refresh_sessions_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_refresh_sessions_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_refresh_sessions_refresh_sessions_TenantId_CompanyId_Replac~",
                        columns: x => new { x.TenantId, x.CompanyId, x.ReplacedById },
                        principalSchema: "identity",
                        principalTable: "refresh_sessions",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_refresh_sessions_users_TenantId_CompanyId_UserId",
                        columns: x => new { x.TenantId, x.CompanyId, x.UserId },
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "security_audit",
                schema: "identity",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Outcome = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Resource = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_security_audit", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_security_audit_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_security_audit_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_security_audit_users_TenantId_CompanyId_UserId",
                        columns: x => new { x.TenantId, x.CompanyId, x.UserId },
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_roles",
                schema: "identity",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_roles", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_user_roles_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_user_roles_branches_TenantId_CompanyId_BranchId",
                        columns: x => new { x.TenantId, x.CompanyId, x.BranchId },
                        principalSchema: "company",
                        principalTable: "branches",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_roles_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_roles_roles_TenantId_CompanyId_RoleId",
                        columns: x => new { x.TenantId, x.CompanyId, x.RoleId },
                        principalSchema: "identity",
                        principalTable: "roles",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_roles_users_TenantId_CompanyId_UserId",
                        columns: x => new { x.TenantId, x.CompanyId, x.UserId },
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "workflow_versions",
                schema: "workflow",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Definition = table.Column<string>(type: "jsonb", nullable: false),
                    DefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflow_versions", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_workflow_versions_rules", "\"Revision\" > 0 AND (\"EffectiveUntil\" IS NULL OR \"EffectiveUntil\" >= \"EffectiveFrom\") AND (\"ApprovedBy\" IS NULL OR \"ApprovedBy\" <> \"CreatedBy\") AND \"State\" IN ('Draft','Pending','Approved','Rejected','Cancelled','Running','Completed','Failed','Submitted','Posted')");
                    table.CheckConstraint("ck_workflow_versions_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_workflow_versions_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_workflow_versions_workflow_definitions_TenantId_CompanyId_D~",
                        columns: x => new { x.TenantId, x.CompanyId, x.DefinitionId },
                        principalSchema: "workflow",
                        principalTable: "workflow_definitions",
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
                    CoefficientVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(24,6)", precision: 24, scale: 6, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
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
                    table.ForeignKey(
                        name: "FK_bom_lines_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_bom_lines_products_TenantId_CompanyId_ProductId",
                        columns: x => new { x.TenantId, x.CompanyId, x.ProductId },
                        principalSchema: "masterdata",
                        principalTable: "products",
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
                    ApprovedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CoefficientVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PlannedQuantity = table.Column<decimal>(type: "numeric(24,6)", precision: 24, scale: 6, nullable: false),
                    Reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    State = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false)
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
                    table.ForeignKey(
                        name: "FK_production_orders_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_production_orders_warehouses_TenantId_CompanyId_WarehouseId",
                        columns: x => new { x.TenantId, x.CompanyId, x.WarehouseId },
                        principalSchema: "inventory",
                        principalTable: "warehouses",
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
                    ApprovedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CorrectsId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FormVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    LedgerWatermark = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReturnPeriodId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vat_returns", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_vat_returns_rules", "(\"CorrectsId\" IS NULL OR \"CorrectsId\" <> \"Id\") AND \"Revision\" > 0 AND (\"ApprovedBy\" IS NULL OR \"ApprovedBy\" <> \"CreatedBy\") AND \"State\" IN ('Draft','Pending','Approved','Rejected','Cancelled','Running','Completed','Failed','Submitted','Posted')");
                    table.CheckConstraint("ck_vat_returns_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_vat_returns_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
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
                name: "inbound_batches",
                schema: "integration",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MappingId = table.Column<Guid>(type: "uuid", nullable: false),
                    PayloadDigest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PayloadKey = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    State = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inbound_batches", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_inbound_batches_rules", "(length(\"PayloadDigest\") = 64) AND \"State\" IN ('Draft','Pending','Approved','Rejected','Cancelled','Running','Completed','Failed','Submitted','Posted')");
                    table.CheckConstraint("ck_inbound_batches_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_inbound_batches_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inbound_batches_field_mappings_TenantId_CompanyId_MappingId",
                        columns: x => new { x.TenantId, x.CompanyId, x.MappingId },
                        principalSchema: "integration",
                        principalTable: "field_mappings",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inbound_batches_integration_connections_TenantId_CompanyId_~",
                        columns: x => new { x.TenantId, x.CompanyId, x.ConnectionId },
                        principalSchema: "integration",
                        principalTable: "integration_connections",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "delivery_attempts",
                schema: "integration",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    HttpStatus = table.Column<int>(type: "integer", nullable: true),
                    LeaseUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    State = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_delivery_attempts", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_delivery_attempts_rules", "(\"AttemptNumber\" > 0 AND (\"HttpStatus\" IS NULL OR \"HttpStatus\" BETWEEN 100 AND 599)) AND \"State\" IN ('Draft','Pending','Approved','Rejected','Cancelled','Running','Completed','Failed','Submitted','Posted')");
                    table.CheckConstraint("ck_delivery_attempts_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_delivery_attempts_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_delivery_attempts_webhook_subscriptions_TenantId_CompanyId_~",
                        columns: x => new { x.TenantId, x.CompanyId, x.SubscriptionId },
                        principalSchema: "integration",
                        principalTable: "webhook_subscriptions",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "report_exports",
                schema: "reporting",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    ObjectKey = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_report_exports", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_report_exports_rules", "(\"SizeBytes\" > 0 AND length(\"Sha256\") = 64 AND \"ExpiresAt\" > \"CreatedAt\")");
                    table.CheckConstraint("ck_report_exports_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_report_exports_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_report_exports_report_jobs_TenantId_CompanyId_JobId",
                        columns: x => new { x.TenantId, x.CompanyId, x.JobId },
                        principalSchema: "reporting",
                        principalTable: "report_jobs",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "product_tax_classifications",
                schema: "masterdata",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    HsCodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaxCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_tax_classifications", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_product_tax_classifications_rules", "(\"EffectiveUntil\" IS NULL OR \"EffectiveUntil\" >= \"EffectiveFrom\")");
                    table.CheckConstraint("ck_product_tax_classifications_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_product_tax_classifications_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_product_tax_classifications_hs_codes_TenantId_CompanyId_HsC~",
                        columns: x => new { x.TenantId, x.CompanyId, x.HsCodeId },
                        principalSchema: "masterdata",
                        principalTable: "hs_codes",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_product_tax_classifications_product_versions_TenantId_Compa~",
                        columns: x => new { x.TenantId, x.CompanyId, x.ProductVersionId },
                        principalSchema: "masterdata",
                        principalTable: "product_versions",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "approval_instances",
                schema: "workflow",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RequestedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SourceVersion = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    WorkflowVersionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_approval_instances", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_approval_instances_rules", "\"SourceVersion\" > 0 AND \"State\" IN ('Draft','Pending','Approved','Rejected','Cancelled','Running','Completed','Failed','Submitted','Posted')");
                    table.CheckConstraint("ck_approval_instances_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_approval_instances_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_approval_instances_workflow_versions_TenantId_CompanyId_Wor~",
                        columns: x => new { x.TenantId, x.CompanyId, x.WorkflowVersionId },
                        principalSchema: "workflow",
                        principalTable: "workflow_versions",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "escalation_rules",
                schema: "workflow",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AfterMinutes = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EscalateTo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StepNumber = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    WorkflowVersionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_escalation_rules", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_escalation_rules_rules", "(\"StepNumber\" > 0 AND \"AfterMinutes\" > 0)");
                    table.CheckConstraint("ck_escalation_rules_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_escalation_rules_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_escalation_rules_workflow_versions_TenantId_CompanyId_Workf~",
                        columns: x => new { x.TenantId, x.CompanyId, x.WorkflowVersionId },
                        principalSchema: "workflow",
                        principalTable: "workflow_versions",
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
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductionOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(24,6)", precision: 24, scale: 6, nullable: false),
                    SourceReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_material_issues", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_material_issues_rules", "\"Quantity\" > 0");
                    table.CheckConstraint("ck_material_issues_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_material_issues_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_material_issues_production_orders_TenantId_CompanyId_Produc~",
                        columns: x => new { x.TenantId, x.CompanyId, x.ProductionOrderId },
                        principalSchema: "manufacturing",
                        principalTable: "production_orders",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_material_issues_products_TenantId_CompanyId_ProductId",
                        columns: x => new { x.TenantId, x.CompanyId, x.ProductId },
                        principalSchema: "masterdata",
                        principalTable: "products",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_material_issues_warehouses_TenantId_CompanyId_WarehouseId",
                        columns: x => new { x.TenantId, x.CompanyId, x.WarehouseId },
                        principalSchema: "inventory",
                        principalTable: "warehouses",
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
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductionOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(24,6)", precision: 24, scale: 6, nullable: false),
                    SourceReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_production_receipts", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_production_receipts_rules", "\"Quantity\" > 0");
                    table.CheckConstraint("ck_production_receipts_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_production_receipts_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_production_receipts_production_orders_TenantId_CompanyId_Pr~",
                        columns: x => new { x.TenantId, x.CompanyId, x.ProductionOrderId },
                        principalSchema: "manufacturing",
                        principalTable: "production_orders",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_production_receipts_products_TenantId_CompanyId_ProductId",
                        columns: x => new { x.TenantId, x.CompanyId, x.ProductId },
                        principalSchema: "masterdata",
                        principalTable: "products",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_production_receipts_warehouses_TenantId_CompanyId_Warehouse~",
                        columns: x => new { x.TenantId, x.CompanyId, x.WarehouseId },
                        principalSchema: "inventory",
                        principalTable: "warehouses",
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
                    ApprovedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductionOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(24,6)", precision: 24, scale: 6, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wastage_records", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_wastage_records_rules", "(\"Quantity\" > 0) AND (\"ApprovedBy\" IS NULL OR \"ApprovedBy\" <> \"CreatedBy\")");
                    table.CheckConstraint("ck_wastage_records_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_wastage_records_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_wastage_records_production_orders_TenantId_CompanyId_Produc~",
                        columns: x => new { x.TenantId, x.CompanyId, x.ProductionOrderId },
                        principalSchema: "manufacturing",
                        principalTable: "production_orders",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_wastage_records_products_TenantId_CompanyId_ProductId",
                        columns: x => new { x.TenantId, x.CompanyId, x.ProductId },
                        principalSchema: "masterdata",
                        principalTable: "products",
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
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FiledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FilingReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ObjectKey = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ReturnId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_filing_evidence", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_filing_evidence_rules", "(length(\"Sha256\") = 64)");
                    table.CheckConstraint("ck_filing_evidence_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_filing_evidence_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
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
                    ApprovedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DecisionAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FiscalPeriodId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ReturnId = table.Column<Guid>(type: "uuid", nullable: true),
                    State = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_period_close_requests", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_period_close_requests_rules", "(\"ApprovedBy\" IS NULL OR \"ApprovedBy\" <> \"CreatedBy\") AND \"State\" IN ('Draft','Pending','Approved','Rejected','Cancelled','Running','Completed','Failed','Submitted','Posted')");
                    table.CheckConstraint("ck_period_close_requests_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_period_close_requests_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_period_close_requests_fiscal_periods_TenantId_CompanyId_Fis~",
                        columns: x => new { x.TenantId, x.CompanyId, x.FiscalPeriodId },
                        principalSchema: "company",
                        principalTable: "fiscal_periods",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
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
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Differences = table.Column<string>(type: "jsonb", nullable: false),
                    ReturnId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    State = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reconciliation_runs", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_reconciliation_runs_rules", "\"State\" IN ('Draft','Pending','Approved','Rejected','Cancelled','Running','Completed','Failed','Submitted','Posted')");
                    table.CheckConstraint("ck_reconciliation_runs_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_reconciliation_runs_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
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
                    Amount = table.Column<decimal>(type: "numeric(24,6)", precision: 24, scale: 6, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LineCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ReturnId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceSnapshot = table.Column<string>(type: "jsonb", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_return_lines", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_return_lines_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_return_lines_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_return_lines_vat_returns_TenantId_CompanyId_ReturnId",
                        columns: x => new { x.TenantId, x.CompanyId, x.ReturnId },
                        principalSchema: "compliance",
                        principalTable: "vat_returns",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "dead_letters",
                schema: "integration",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: true),
                    FailedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PayloadKey = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ReplayedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dead_letters1", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_dead_letters_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_dead_letters_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_dead_letters_inbound_batches_TenantId_CompanyId_BatchId",
                        columns: x => new { x.TenantId, x.CompanyId, x.BatchId },
                        principalSchema: "integration",
                        principalTable: "inbound_batches",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_dead_letters_integration_connections_TenantId_CompanyId_Con~",
                        columns: x => new { x.TenantId, x.CompanyId, x.ConnectionId },
                        principalSchema: "integration",
                        principalTable: "integration_connections",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inbound_rows",
                schema: "integration",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ExternalId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RowNumber = table.Column<int>(type: "integer", nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    State = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inbound_rows", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_inbound_rows_rules", "(\"RowNumber\" > 0) AND \"State\" IN ('Draft','Pending','Approved','Rejected','Cancelled','Running','Completed','Failed','Submitted','Posted')");
                    table.CheckConstraint("ck_inbound_rows_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_inbound_rows_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inbound_rows_inbound_batches_TenantId_CompanyId_BatchId",
                        columns: x => new { x.TenantId, x.CompanyId, x.BatchId },
                        principalSchema: "integration",
                        principalTable: "inbound_batches",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "approval_tasks",
                schema: "workflow",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedTo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DecisionAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    InstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    StepNumber = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_approval_tasks", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_approval_tasks_rules", "(\"StepNumber\" > 0 AND \"State\" IN ('Pending','Approved','Rejected','Cancelled'))");
                    table.CheckConstraint("ck_approval_tasks_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_approval_tasks_approval_instances_TenantId_CompanyId_Instan~",
                        columns: x => new { x.TenantId, x.CompanyId, x.InstanceId },
                        principalSchema: "workflow",
                        principalTable: "approval_instances",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_approval_tasks_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "approval_actions",
                schema: "workflow",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Actor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Decision = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_approval_actions", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_approval_actions_rules", "(\"Decision\" IN ('Approved','Rejected','Cancelled'))");
                    table.CheckConstraint("ck_approval_actions_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_approval_actions_approval_tasks_TenantId_CompanyId_TaskId",
                        columns: x => new { x.TenantId, x.CompanyId, x.TaskId },
                        principalSchema: "workflow",
                        principalTable: "approval_tasks",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_approval_actions_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_adjustment_lines_TenantId_CompanyId_AdjustmentId",
                schema: "inventory",
                table: "adjustment_lines",
                columns: new[] { "TenantId", "CompanyId", "AdjustmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_adjustment_lines_TenantId_CompanyId_AdjustmentId_ProductId",
                schema: "inventory",
                table: "adjustment_lines",
                columns: new[] { "TenantId", "CompanyId", "AdjustmentId", "ProductId" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_adjustment_lines_TenantId_CompanyId_CreatedAt",
                schema: "inventory",
                table: "adjustment_lines",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_adjustment_lines_TenantId_CompanyId_ProductId",
                schema: "inventory",
                table: "adjustment_lines",
                columns: new[] { "TenantId", "CompanyId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_adjustments_TenantId_CompanyId_CreatedAt",
                schema: "inventory",
                table: "adjustments",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_adjustments_TenantId_CompanyId_Reference",
                schema: "inventory",
                table: "adjustments",
                columns: new[] { "TenantId", "CompanyId", "Reference" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_adjustments_TenantId_CompanyId_WarehouseId",
                schema: "inventory",
                table: "adjustments",
                columns: new[] { "TenantId", "CompanyId", "WarehouseId" });

            migrationBuilder.CreateIndex(
                name: "IX_approval_actions_TenantId_CompanyId_CreatedAt",
                schema: "workflow",
                table: "approval_actions",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_approval_actions_TenantId_CompanyId_TaskId",
                schema: "workflow",
                table: "approval_actions",
                columns: new[] { "TenantId", "CompanyId", "TaskId" });

            migrationBuilder.CreateIndex(
                name: "IX_approval_actions_TenantId_CompanyId_TaskId_IdempotencyKey",
                schema: "workflow",
                table: "approval_actions",
                columns: new[] { "TenantId", "CompanyId", "TaskId", "IdempotencyKey" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_approval_instances_TenantId_CompanyId_CreatedAt",
                schema: "workflow",
                table: "approval_instances",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_approval_instances_TenantId_CompanyId_SourceType_SourceId_S~",
                schema: "workflow",
                table: "approval_instances",
                columns: new[] { "TenantId", "CompanyId", "SourceType", "SourceId", "SourceVersion" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_approval_instances_TenantId_CompanyId_WorkflowVersionId",
                schema: "workflow",
                table: "approval_instances",
                columns: new[] { "TenantId", "CompanyId", "WorkflowVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_approval_tasks_TenantId_CompanyId_CreatedAt",
                schema: "workflow",
                table: "approval_tasks",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_approval_tasks_TenantId_CompanyId_InstanceId",
                schema: "workflow",
                table: "approval_tasks",
                columns: new[] { "TenantId", "CompanyId", "InstanceId" });

            migrationBuilder.CreateIndex(
                name: "IX_approval_tasks_TenantId_CompanyId_InstanceId_StepNumber_Ass~",
                schema: "workflow",
                table: "approval_tasks",
                columns: new[] { "TenantId", "CompanyId", "InstanceId", "StepNumber", "AssignedTo" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

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
                name: "IX_branch_access_TenantId_CompanyId_BranchId",
                schema: "company",
                table: "branch_access",
                columns: new[] { "TenantId", "CompanyId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_branch_access_TenantId_CompanyId_CreatedAt",
                schema: "company",
                table: "branch_access",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_branch_access_TenantId_CompanyId_PermissionId",
                schema: "company",
                table: "branch_access",
                columns: new[] { "TenantId", "CompanyId", "PermissionId" });

            migrationBuilder.CreateIndex(
                name: "IX_branch_access_TenantId_CompanyId_UserId",
                schema: "company",
                table: "branch_access",
                columns: new[] { "TenantId", "CompanyId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_branch_access_TenantId_CompanyId_UserId_BranchId_Permission~",
                schema: "company",
                table: "branch_access",
                columns: new[] { "TenantId", "CompanyId", "UserId", "BranchId", "PermissionId" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_challan_numbers_TenantId_CompanyId_BranchId",
                schema: "sales",
                table: "challan_numbers",
                columns: new[] { "TenantId", "CompanyId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_challan_numbers_TenantId_CompanyId_BranchId_FiscalYear_Seri~",
                schema: "sales",
                table: "challan_numbers",
                columns: new[] { "TenantId", "CompanyId", "BranchId", "FiscalYear", "Series", "Number" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_challan_numbers_TenantId_CompanyId_CreatedAt",
                schema: "sales",
                table: "challan_numbers",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_challan_numbers_TenantId_CompanyId_InvoiceId",
                schema: "sales",
                table: "challan_numbers",
                columns: new[] { "TenantId", "CompanyId", "InvoiceId" });

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
                name: "IX_company_settings_TenantId_CompanyId_CreatedAt",
                schema: "company",
                table: "company_settings",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_company_settings_TenantId_CompanyId_Key",
                schema: "company",
                table: "company_settings",
                columns: new[] { "TenantId", "CompanyId", "Key" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_counterparty_registrations_TenantId_CompanyId_CreatedAt",
                schema: "masterdata",
                table: "counterparty_registrations",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_counterparty_registrations_TenantId_CompanyId_CustomerId",
                schema: "masterdata",
                table: "counterparty_registrations",
                columns: new[] { "TenantId", "CompanyId", "CustomerId" });

            migrationBuilder.CreateIndex(
                name: "IX_counterparty_registrations_TenantId_CompanyId_SupplierId",
                schema: "masterdata",
                table: "counterparty_registrations",
                columns: new[] { "TenantId", "CompanyId", "SupplierId" });

            migrationBuilder.CreateIndex(
                name: "IX_dead_letters_TenantId_CompanyId_BatchId",
                schema: "integration",
                table: "dead_letters",
                columns: new[] { "TenantId", "CompanyId", "BatchId" });

            migrationBuilder.CreateIndex(
                name: "IX_dead_letters_TenantId_CompanyId_ConnectionId",
                schema: "integration",
                table: "dead_letters",
                columns: new[] { "TenantId", "CompanyId", "ConnectionId" });

            migrationBuilder.CreateIndex(
                name: "IX_dead_letters_TenantId_CompanyId_CreatedAt",
                schema: "integration",
                table: "dead_letters",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_delegations_TenantId_CompanyId_CreatedAt",
                schema: "workflow",
                table: "delegations",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_delivery_attempts_TenantId_CompanyId_CreatedAt",
                schema: "integration",
                table: "delivery_attempts",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_delivery_attempts_TenantId_CompanyId_SubscriptionId",
                schema: "integration",
                table: "delivery_attempts",
                columns: new[] { "TenantId", "CompanyId", "SubscriptionId" });

            migrationBuilder.CreateIndex(
                name: "IX_delivery_attempts_TenantId_CompanyId_SubscriptionId_EventId~",
                schema: "integration",
                table: "delivery_attempts",
                columns: new[] { "TenantId", "CompanyId", "SubscriptionId", "EventId", "AttemptNumber" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_delivery_references_TenantId_CompanyId_CreatedAt",
                schema: "sales",
                table: "delivery_references",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_delivery_references_TenantId_CompanyId_InvoiceId",
                schema: "sales",
                table: "delivery_references",
                columns: new[] { "TenantId", "CompanyId", "InvoiceId" });

            migrationBuilder.CreateIndex(
                name: "IX_delivery_references_TenantId_CompanyId_InvoiceId_Reference",
                schema: "sales",
                table: "delivery_references",
                columns: new[] { "TenantId", "CompanyId", "InvoiceId", "Reference" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_escalation_rules_TenantId_CompanyId_CreatedAt",
                schema: "workflow",
                table: "escalation_rules",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_escalation_rules_TenantId_CompanyId_WorkflowVersionId",
                schema: "workflow",
                table: "escalation_rules",
                columns: new[] { "TenantId", "CompanyId", "WorkflowVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_escalation_rules_TenantId_CompanyId_WorkflowVersionId_StepN~",
                schema: "workflow",
                table: "escalation_rules",
                columns: new[] { "TenantId", "CompanyId", "WorkflowVersionId", "StepNumber", "AfterMinutes" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_external_references_TenantId_CompanyId_ConnectionId",
                schema: "integration",
                table: "external_references",
                columns: new[] { "TenantId", "CompanyId", "ConnectionId" });

            migrationBuilder.CreateIndex(
                name: "IX_external_references_TenantId_CompanyId_ConnectionId_SourceT~",
                schema: "integration",
                table: "external_references",
                columns: new[] { "TenantId", "CompanyId", "ConnectionId", "SourceType", "ExternalId" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_external_references_TenantId_CompanyId_CreatedAt",
                schema: "integration",
                table: "external_references",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_field_mappings_TenantId_CompanyId_ConnectionId",
                schema: "integration",
                table: "field_mappings",
                columns: new[] { "TenantId", "CompanyId", "ConnectionId" });

            migrationBuilder.CreateIndex(
                name: "IX_field_mappings_TenantId_CompanyId_ConnectionId_DocumentType~",
                schema: "integration",
                table: "field_mappings",
                columns: new[] { "TenantId", "CompanyId", "ConnectionId", "DocumentType", "Revision" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_field_mappings_TenantId_CompanyId_CreatedAt",
                schema: "integration",
                table: "field_mappings",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

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
                name: "IX_hs_codes_TenantId_CompanyId_Code_EffectiveFrom",
                schema: "masterdata",
                table: "hs_codes",
                columns: new[] { "TenantId", "CompanyId", "Code", "EffectiveFrom" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_hs_codes_TenantId_CompanyId_CreatedAt",
                schema: "masterdata",
                table: "hs_codes",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_inbound_batches_TenantId_CompanyId_ConnectionId",
                schema: "integration",
                table: "inbound_batches",
                columns: new[] { "TenantId", "CompanyId", "ConnectionId" });

            migrationBuilder.CreateIndex(
                name: "IX_inbound_batches_TenantId_CompanyId_ConnectionId_ExternalId",
                schema: "integration",
                table: "inbound_batches",
                columns: new[] { "TenantId", "CompanyId", "ConnectionId", "ExternalId" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_inbound_batches_TenantId_CompanyId_CreatedAt",
                schema: "integration",
                table: "inbound_batches",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_inbound_batches_TenantId_CompanyId_MappingId",
                schema: "integration",
                table: "inbound_batches",
                columns: new[] { "TenantId", "CompanyId", "MappingId" });

            migrationBuilder.CreateIndex(
                name: "IX_inbound_rows_TenantId_CompanyId_BatchId",
                schema: "integration",
                table: "inbound_rows",
                columns: new[] { "TenantId", "CompanyId", "BatchId" });

            migrationBuilder.CreateIndex(
                name: "IX_inbound_rows_TenantId_CompanyId_BatchId_RowNumber",
                schema: "integration",
                table: "inbound_rows",
                columns: new[] { "TenantId", "CompanyId", "BatchId", "RowNumber" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_inbound_rows_TenantId_CompanyId_CreatedAt",
                schema: "integration",
                table: "inbound_rows",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_integration_connections_TenantId_CompanyId_Code",
                schema: "integration",
                table: "integration_connections",
                columns: new[] { "TenantId", "CompanyId", "Code" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_integration_connections_TenantId_CompanyId_CreatedAt",
                schema: "integration",
                table: "integration_connections",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

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
                name: "IX_menu_items_TenantId_CompanyId_Code",
                schema: "identity",
                table: "menu_items",
                columns: new[] { "TenantId", "CompanyId", "Code" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_menu_items_TenantId_CompanyId_CreatedAt",
                schema: "identity",
                table: "menu_items",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_menu_items_TenantId_CompanyId_ParentId",
                schema: "identity",
                table: "menu_items",
                columns: new[] { "TenantId", "CompanyId", "ParentId" });

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
                name: "IX_permissions_TenantId_CompanyId_Code",
                schema: "identity",
                table: "permissions",
                columns: new[] { "TenantId", "CompanyId", "Code" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_permissions_TenantId_CompanyId_CreatedAt",
                schema: "identity",
                table: "permissions",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_product_tax_classifications_TenantId_CompanyId_CreatedAt",
                schema: "masterdata",
                table: "product_tax_classifications",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_product_tax_classifications_TenantId_CompanyId_HsCodeId",
                schema: "masterdata",
                table: "product_tax_classifications",
                columns: new[] { "TenantId", "CompanyId", "HsCodeId" });

            migrationBuilder.CreateIndex(
                name: "IX_product_tax_classifications_TenantId_CompanyId_ProductVers~1",
                schema: "masterdata",
                table: "product_tax_classifications",
                columns: new[] { "TenantId", "CompanyId", "ProductVersionId", "EffectiveFrom" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_product_tax_classifications_TenantId_CompanyId_ProductVersi~",
                schema: "masterdata",
                table: "product_tax_classifications",
                columns: new[] { "TenantId", "CompanyId", "ProductVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_product_versions_TenantId_CompanyId_CreatedAt",
                schema: "masterdata",
                table: "product_versions",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_product_versions_TenantId_CompanyId_ProductId",
                schema: "masterdata",
                table: "product_versions",
                columns: new[] { "TenantId", "CompanyId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_product_versions_TenantId_CompanyId_ProductId_Revision",
                schema: "masterdata",
                table: "product_versions",
                columns: new[] { "TenantId", "CompanyId", "ProductId", "Revision" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_product_versions_TenantId_CompanyId_UomId",
                schema: "masterdata",
                table: "product_versions",
                columns: new[] { "TenantId", "CompanyId", "UomId" });

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
                name: "IX_production_orders_TenantId_CompanyId_WarehouseId",
                schema: "manufacturing",
                table: "production_orders",
                columns: new[] { "TenantId", "CompanyId", "WarehouseId" });

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
                name: "IX_purchase_attachments_TenantId_CompanyId_CreatedAt",
                schema: "purchasing",
                table: "purchase_attachments",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_attachments_TenantId_CompanyId_PurchaseId",
                schema: "purchasing",
                table: "purchase_attachments",
                columns: new[] { "TenantId", "CompanyId", "PurchaseId" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_attachments_TenantId_CompanyId_PurchaseId_Sha256",
                schema: "purchasing",
                table: "purchase_attachments",
                columns: new[] { "TenantId", "CompanyId", "PurchaseId", "Sha256" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_posting_sagas_TenantId_CompanyId_CreatedAt",
                schema: "purchasing",
                table: "purchase_posting_sagas",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_posting_sagas_TenantId_CompanyId_PurchaseId",
                schema: "purchasing",
                table: "purchase_posting_sagas",
                columns: new[] { "TenantId", "CompanyId", "PurchaseId" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_posting_sagas_TenantId_CompanyId_PurchaseId_Source~",
                schema: "purchasing",
                table: "purchase_posting_sagas",
                columns: new[] { "TenantId", "CompanyId", "PurchaseId", "SourceVersion" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_reconciliation_alerts_TenantId_CompanyId_CreatedAt",
                schema: "reporting",
                table: "reconciliation_alerts",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

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
                name: "IX_refresh_sessions_TenantId_CompanyId_CreatedAt",
                schema: "identity",
                table: "refresh_sessions",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_refresh_sessions_TenantId_CompanyId_ReplacedById",
                schema: "identity",
                table: "refresh_sessions",
                columns: new[] { "TenantId", "CompanyId", "ReplacedById" });

            migrationBuilder.CreateIndex(
                name: "IX_refresh_sessions_TenantId_CompanyId_TokenDigest",
                schema: "identity",
                table: "refresh_sessions",
                columns: new[] { "TenantId", "CompanyId", "TokenDigest" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_refresh_sessions_TenantId_CompanyId_UserId",
                schema: "identity",
                table: "refresh_sessions",
                columns: new[] { "TenantId", "CompanyId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_registrations_TenantId_CompanyId_BranchId",
                schema: "company",
                table: "registrations",
                columns: new[] { "TenantId", "CompanyId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_registrations_TenantId_CompanyId_CreatedAt",
                schema: "company",
                table: "registrations",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_registrations_TenantId_CompanyId_RegistrationType_Reference~",
                schema: "company",
                table: "registrations",
                columns: new[] { "TenantId", "CompanyId", "RegistrationType", "Reference", "EffectiveFrom" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_report_exports_TenantId_CompanyId_CreatedAt",
                schema: "reporting",
                table: "report_exports",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_report_exports_TenantId_CompanyId_JobId",
                schema: "reporting",
                table: "report_exports",
                columns: new[] { "TenantId", "CompanyId", "JobId" });

            migrationBuilder.CreateIndex(
                name: "IX_report_exports_TenantId_CompanyId_JobId_ObjectKey",
                schema: "reporting",
                table: "report_exports",
                columns: new[] { "TenantId", "CompanyId", "JobId", "ObjectKey" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_report_jobs_TenantId_CompanyId_CreatedAt",
                schema: "reporting",
                table: "report_jobs",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_report_jobs_TenantId_CompanyId_TemplateId",
                schema: "reporting",
                table: "report_jobs",
                columns: new[] { "TenantId", "CompanyId", "TemplateId" });

            migrationBuilder.CreateIndex(
                name: "IX_report_templates_TenantId_CompanyId_Code_Revision",
                schema: "reporting",
                table: "report_templates",
                columns: new[] { "TenantId", "CompanyId", "Code", "Revision" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_report_templates_TenantId_CompanyId_CreatedAt",
                schema: "reporting",
                table: "report_templates",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

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
                name: "IX_role_menus_TenantId_CompanyId_CreatedAt",
                schema: "identity",
                table: "role_menus",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_role_menus_TenantId_CompanyId_MenuId",
                schema: "identity",
                table: "role_menus",
                columns: new[] { "TenantId", "CompanyId", "MenuId" });

            migrationBuilder.CreateIndex(
                name: "IX_role_menus_TenantId_CompanyId_RoleId",
                schema: "identity",
                table: "role_menus",
                columns: new[] { "TenantId", "CompanyId", "RoleId" });

            migrationBuilder.CreateIndex(
                name: "IX_role_menus_TenantId_CompanyId_RoleId_MenuId",
                schema: "identity",
                table: "role_menus",
                columns: new[] { "TenantId", "CompanyId", "RoleId", "MenuId" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_role_permissions_TenantId_CompanyId_CreatedAt",
                schema: "identity",
                table: "role_permissions",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_role_permissions_TenantId_CompanyId_PermissionId",
                schema: "identity",
                table: "role_permissions",
                columns: new[] { "TenantId", "CompanyId", "PermissionId" });

            migrationBuilder.CreateIndex(
                name: "IX_role_permissions_TenantId_CompanyId_RoleId",
                schema: "identity",
                table: "role_permissions",
                columns: new[] { "TenantId", "CompanyId", "RoleId" });

            migrationBuilder.CreateIndex(
                name: "IX_role_permissions_TenantId_CompanyId_RoleId_PermissionId",
                schema: "identity",
                table: "role_permissions",
                columns: new[] { "TenantId", "CompanyId", "RoleId", "PermissionId" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_roles_TenantId_CompanyId_Code",
                schema: "identity",
                table: "roles",
                columns: new[] { "TenantId", "CompanyId", "Code" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_roles_TenantId_CompanyId_CreatedAt",
                schema: "identity",
                table: "roles",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_security_audit_TenantId_CompanyId_CreatedAt",
                schema: "identity",
                table: "security_audit",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_security_audit_TenantId_CompanyId_UserId",
                schema: "identity",
                table: "security_audit",
                columns: new[] { "TenantId", "CompanyId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_tax_calculation_snapshots_TenantId_CompanyId_CreatedAt",
                schema: "tax",
                table: "tax_calculation_snapshots",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_tax_calculation_snapshots_TenantId_CompanyId_RuleVersionId",
                schema: "tax",
                table: "tax_calculation_snapshots",
                columns: new[] { "TenantId", "CompanyId", "RuleVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_tax_calculation_snapshots_TenantId_CompanyId_SourceType_Sou~",
                schema: "tax",
                table: "tax_calculation_snapshots",
                columns: new[] { "TenantId", "CompanyId", "SourceType", "SourceId", "SourceVersion" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_tax_calculation_snapshots_TenantId_CompanyId_TreatmentId",
                schema: "tax",
                table: "tax_calculation_snapshots",
                columns: new[] { "TenantId", "CompanyId", "TreatmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_tax_posting_batches_TenantId_CompanyId_CreatedAt",
                schema: "tax",
                table: "tax_posting_batches",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_tax_posting_batches_TenantId_CompanyId_SourceType_SourceId_~",
                schema: "tax",
                table: "tax_posting_batches",
                columns: new[] { "TenantId", "CompanyId", "SourceType", "SourceId", "SourceVersion" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_tax_treatments_TenantId_CompanyId_Code_EffectiveFrom",
                schema: "tax",
                table: "tax_treatments",
                columns: new[] { "TenantId", "CompanyId", "Code", "EffectiveFrom" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_tax_treatments_TenantId_CompanyId_CreatedAt",
                schema: "tax",
                table: "tax_treatments",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_transfer_lines_TenantId_CompanyId_CreatedAt",
                schema: "inventory",
                table: "transfer_lines",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_transfer_lines_TenantId_CompanyId_ProductId",
                schema: "inventory",
                table: "transfer_lines",
                columns: new[] { "TenantId", "CompanyId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_transfer_lines_TenantId_CompanyId_TransferId",
                schema: "inventory",
                table: "transfer_lines",
                columns: new[] { "TenantId", "CompanyId", "TransferId" });

            migrationBuilder.CreateIndex(
                name: "IX_transfer_lines_TenantId_CompanyId_TransferId_ProductId",
                schema: "inventory",
                table: "transfer_lines",
                columns: new[] { "TenantId", "CompanyId", "TransferId", "ProductId" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_transfers_TenantId_CompanyId_CreatedAt",
                schema: "inventory",
                table: "transfers",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_transfers_TenantId_CompanyId_FromWarehouseId",
                schema: "inventory",
                table: "transfers",
                columns: new[] { "TenantId", "CompanyId", "FromWarehouseId" });

            migrationBuilder.CreateIndex(
                name: "IX_transfers_TenantId_CompanyId_Reference",
                schema: "inventory",
                table: "transfers",
                columns: new[] { "TenantId", "CompanyId", "Reference" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_transfers_TenantId_CompanyId_ToWarehouseId",
                schema: "inventory",
                table: "transfers",
                columns: new[] { "TenantId", "CompanyId", "ToWarehouseId" });

            migrationBuilder.CreateIndex(
                name: "IX_uoms_TenantId_CompanyId_Code",
                schema: "masterdata",
                table: "uoms",
                columns: new[] { "TenantId", "CompanyId", "Code" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_uoms_TenantId_CompanyId_CreatedAt",
                schema: "masterdata",
                table: "uoms",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_user_roles_TenantId_CompanyId_BranchId",
                schema: "identity",
                table: "user_roles",
                columns: new[] { "TenantId", "CompanyId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_user_roles_TenantId_CompanyId_CreatedAt",
                schema: "identity",
                table: "user_roles",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_user_roles_TenantId_CompanyId_RoleId",
                schema: "identity",
                table: "user_roles",
                columns: new[] { "TenantId", "CompanyId", "RoleId" });

            migrationBuilder.CreateIndex(
                name: "IX_user_roles_TenantId_CompanyId_UserId",
                schema: "identity",
                table: "user_roles",
                columns: new[] { "TenantId", "CompanyId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_user_roles_TenantId_CompanyId_UserId_RoleId_BranchId",
                schema: "identity",
                table: "user_roles",
                columns: new[] { "TenantId", "CompanyId", "UserId", "RoleId", "BranchId" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_users_TenantId_CompanyId_CreatedAt",
                schema: "identity",
                table: "users",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_users_TenantId_CompanyId_Issuer_Subject",
                schema: "identity",
                table: "users",
                columns: new[] { "TenantId", "CompanyId", "Issuer", "Subject" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

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
                name: "IX_vds_entries_TenantId_CompanyId_BatchId",
                schema: "tax",
                table: "vds_entries",
                columns: new[] { "TenantId", "CompanyId", "BatchId" });

            migrationBuilder.CreateIndex(
                name: "IX_vds_entries_TenantId_CompanyId_CreatedAt",
                schema: "tax",
                table: "vds_entries",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_vds_entries_TenantId_CompanyId_ReversalOfId",
                schema: "tax",
                table: "vds_entries",
                columns: new[] { "TenantId", "CompanyId", "ReversalOfId" });

            migrationBuilder.CreateIndex(
                name: "IX_vds_entries_TenantId_CompanyId_SupplierId",
                schema: "tax",
                table: "vds_entries",
                columns: new[] { "TenantId", "CompanyId", "SupplierId" });

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

            migrationBuilder.CreateIndex(
                name: "IX_webhook_subscriptions_TenantId_CompanyId_ConnectionId",
                schema: "integration",
                table: "webhook_subscriptions",
                columns: new[] { "TenantId", "CompanyId", "ConnectionId" });

            migrationBuilder.CreateIndex(
                name: "IX_webhook_subscriptions_TenantId_CompanyId_ConnectionId_Event~",
                schema: "integration",
                table: "webhook_subscriptions",
                columns: new[] { "TenantId", "CompanyId", "ConnectionId", "EventType", "Endpoint" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_webhook_subscriptions_TenantId_CompanyId_CreatedAt",
                schema: "integration",
                table: "webhook_subscriptions",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_workflow_definitions_TenantId_CompanyId_Code",
                schema: "workflow",
                table: "workflow_definitions",
                columns: new[] { "TenantId", "CompanyId", "Code" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_workflow_definitions_TenantId_CompanyId_CreatedAt",
                schema: "workflow",
                table: "workflow_definitions",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_workflow_versions_TenantId_CompanyId_CreatedAt",
                schema: "workflow",
                table: "workflow_versions",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_workflow_versions_TenantId_CompanyId_DefinitionId",
                schema: "workflow",
                table: "workflow_versions",
                columns: new[] { "TenantId", "CompanyId", "DefinitionId" });

            migrationBuilder.CreateIndex(
                name: "IX_workflow_versions_TenantId_CompanyId_DefinitionId_Revision",
                schema: "workflow",
                table: "workflow_versions",
                columns: new[] { "TenantId", "CompanyId", "DefinitionId", "Revision" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "adjustment_lines",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "approval_actions",
                schema: "workflow");

            migrationBuilder.DropTable(
                name: "bom_lines",
                schema: "manufacturing");

            migrationBuilder.DropTable(
                name: "branch_access",
                schema: "company");

            migrationBuilder.DropTable(
                name: "challan_numbers",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "company_settings",
                schema: "company");

            migrationBuilder.DropTable(
                name: "counterparty_registrations",
                schema: "masterdata");

            migrationBuilder.DropTable(
                name: "dead_letters",
                schema: "integration");

            migrationBuilder.DropTable(
                name: "delegations",
                schema: "workflow");

            migrationBuilder.DropTable(
                name: "delivery_attempts",
                schema: "integration");

            migrationBuilder.DropTable(
                name: "delivery_references",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "escalation_rules",
                schema: "workflow");

            migrationBuilder.DropTable(
                name: "external_references",
                schema: "integration");

            migrationBuilder.DropTable(
                name: "filing_evidence",
                schema: "compliance");

            migrationBuilder.DropTable(
                name: "inbound_rows",
                schema: "integration");

            migrationBuilder.DropTable(
                name: "material_issues",
                schema: "manufacturing");

            migrationBuilder.DropTable(
                name: "period_close_requests",
                schema: "compliance");

            migrationBuilder.DropTable(
                name: "product_tax_classifications",
                schema: "masterdata");

            migrationBuilder.DropTable(
                name: "production_receipts",
                schema: "manufacturing");

            migrationBuilder.DropTable(
                name: "purchase_attachments",
                schema: "purchasing");

            migrationBuilder.DropTable(
                name: "purchase_posting_sagas",
                schema: "purchasing");

            migrationBuilder.DropTable(
                name: "reconciliation_alerts",
                schema: "reporting");

            migrationBuilder.DropTable(
                name: "reconciliation_runs",
                schema: "compliance");

            migrationBuilder.DropTable(
                name: "refresh_sessions",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "registrations",
                schema: "company");

            migrationBuilder.DropTable(
                name: "report_exports",
                schema: "reporting");

            migrationBuilder.DropTable(
                name: "return_lines",
                schema: "compliance");

            migrationBuilder.DropTable(
                name: "role_menus",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "role_permissions",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "security_audit",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "tax_calculation_snapshots",
                schema: "tax");

            migrationBuilder.DropTable(
                name: "transfer_lines",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "user_roles",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "vds_entries",
                schema: "tax");

            migrationBuilder.DropTable(
                name: "wastage_records",
                schema: "manufacturing");

            migrationBuilder.DropTable(
                name: "adjustments",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "approval_tasks",
                schema: "workflow");

            migrationBuilder.DropTable(
                name: "webhook_subscriptions",
                schema: "integration");

            migrationBuilder.DropTable(
                name: "inbound_batches",
                schema: "integration");

            migrationBuilder.DropTable(
                name: "hs_codes",
                schema: "masterdata");

            migrationBuilder.DropTable(
                name: "product_versions",
                schema: "masterdata");

            migrationBuilder.DropTable(
                name: "report_jobs",
                schema: "reporting");

            migrationBuilder.DropTable(
                name: "vat_returns",
                schema: "compliance");

            migrationBuilder.DropTable(
                name: "menu_items",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "permissions",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "tax_treatments",
                schema: "tax");

            migrationBuilder.DropTable(
                name: "transfers",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "roles",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "users",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "tax_posting_batches",
                schema: "tax");

            migrationBuilder.DropTable(
                name: "production_orders",
                schema: "manufacturing");

            migrationBuilder.DropTable(
                name: "approval_instances",
                schema: "workflow");

            migrationBuilder.DropTable(
                name: "field_mappings",
                schema: "integration");

            migrationBuilder.DropTable(
                name: "uoms",
                schema: "masterdata");

            migrationBuilder.DropTable(
                name: "report_templates",
                schema: "reporting");

            migrationBuilder.DropTable(
                name: "form_versions",
                schema: "compliance");

            migrationBuilder.DropTable(
                name: "return_periods",
                schema: "compliance");

            migrationBuilder.DropTable(
                name: "coefficient_versions",
                schema: "manufacturing");

            migrationBuilder.DropTable(
                name: "workflow_versions",
                schema: "workflow");

            migrationBuilder.DropTable(
                name: "integration_connections",
                schema: "integration");

            migrationBuilder.DropTable(
                name: "form_templates",
                schema: "compliance");

            migrationBuilder.DropTable(
                name: "bom_headers",
                schema: "manufacturing");

            migrationBuilder.DropTable(
                name: "workflow_definitions",
                schema: "workflow");
        }
    }
}
