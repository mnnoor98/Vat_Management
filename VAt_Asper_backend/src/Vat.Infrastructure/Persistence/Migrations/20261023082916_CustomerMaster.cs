using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CustomerMaster : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "sales",
                table: "sales_invoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerSnapshot",
                schema: "sales",
                table: "sales_invoices",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "customers",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Contact = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Address = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    TaxRegistrationReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customers", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.ForeignKey(
                        name: "FK_customers_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sales_invoices_TenantId_CompanyId_CustomerId",
                schema: "sales",
                table: "sales_invoices",
                columns: new[] { "TenantId", "CompanyId", "CustomerId" });

            migrationBuilder.CreateIndex(
                name: "IX_customers_TenantId_CompanyId_Code",
                schema: "sales",
                table: "customers",
                columns: new[] { "TenantId", "CompanyId", "Code" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_sales_invoices_customers_TenantId_CompanyId_CustomerId",
                schema: "sales",
                table: "sales_invoices",
                columns: new[] { "TenantId", "CompanyId", "CustomerId" },
                principalSchema: "sales",
                principalTable: "customers",
                principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_sales_invoices_customers_TenantId_CompanyId_CustomerId",
                schema: "sales",
                table: "sales_invoices");

            migrationBuilder.DropTable(
                name: "customers",
                schema: "sales");

            migrationBuilder.DropIndex(
                name: "IX_sales_invoices_TenantId_CompanyId_CustomerId",
                schema: "sales",
                table: "sales_invoices");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "sales",
                table: "sales_invoices");

            migrationBuilder.DropColumn(
                name: "CustomerSnapshot",
                schema: "sales",
                table: "sales_invoices");
        }
    }
}
