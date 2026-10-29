using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CustomerPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "payments",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Method = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    EvidenceReference = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    PaymentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(24,4)", precision: 24, scale: 4, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    RequestedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ApprovedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PostedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RequestedAllocations = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payments", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.UniqueConstraint("AK_payments_TenantId_CompanyId_CustomerId_Id", x => new { x.TenantId, x.CompanyId, x.CustomerId, x.Id });
                    table.CheckConstraint("ck_payment_amount", "\"Amount\" > 0 AND \"Amount\" <= 1000000000000000");
                    table.ForeignKey(
                        name: "FK_payments_customers_TenantId_CompanyId_CustomerId",
                        columns: x => new { x.TenantId, x.CompanyId, x.CustomerId },
                        principalSchema: "sales",
                        principalTable: "customers",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payment_allocations",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentVersion = table.Column<long>(type: "bigint", nullable: false),
                    AllocationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(24,4)", precision: 24, scale: 4, nullable: false),
                    AllocatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_allocations", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_payment_allocation_amount", "\"Amount\" > 0 AND \"PaymentVersion\" >= 2");
                    table.ForeignKey(
                        name: "FK_payment_allocations_payments_TenantId_CompanyId_CustomerId_~",
                        columns: x => new { x.TenantId, x.CompanyId, x.CustomerId, x.PaymentId },
                        principalSchema: "sales",
                        principalTable: "payments",
                        principalColumns: new[] { "TenantId", "CompanyId", "CustomerId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payment_allocations_sales_invoices_TenantId_CompanyId_Invoi~",
                        columns: x => new { x.TenantId, x.CompanyId, x.InvoiceId },
                        principalSchema: "sales",
                        principalTable: "sales_invoices",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_payment_allocations_TenantId_CompanyId_CustomerId_PaymentId",
                schema: "sales",
                table: "payment_allocations",
                columns: new[] { "TenantId", "CompanyId", "CustomerId", "PaymentId" });

            migrationBuilder.CreateIndex(
                name: "IX_payment_allocations_TenantId_CompanyId_InvoiceId",
                schema: "sales",
                table: "payment_allocations",
                columns: new[] { "TenantId", "CompanyId", "InvoiceId" });

            migrationBuilder.CreateIndex(
                name: "IX_payment_allocations_TenantId_CompanyId_PaymentId_PaymentVer~",
                schema: "sales",
                table: "payment_allocations",
                columns: new[] { "TenantId", "CompanyId", "PaymentId", "PaymentVersion", "InvoiceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payments_TenantId_CompanyId_Reference",
                schema: "sales",
                table: "payments",
                columns: new[] { "TenantId", "CompanyId", "Reference" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payment_allocations",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "payments",
                schema: "sales");
        }
    }
}
