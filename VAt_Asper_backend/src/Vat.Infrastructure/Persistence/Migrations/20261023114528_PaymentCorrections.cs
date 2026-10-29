using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PaymentCorrections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_payment_allocations_TenantId_CompanyId_CustomerId_PaymentId",
                schema: "sales",
                table: "payment_allocations");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_payment_allocations_TenantId_CompanyId_CustomerId_PaymentId~",
                schema: "sales",
                table: "payment_allocations",
                columns: new[] { "TenantId", "CompanyId", "CustomerId", "PaymentId", "InvoiceId", "Id" });

            migrationBuilder.CreateTable(
                name: "payment_corrections",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceVersion = table.Column<long>(type: "bigint", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    Mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CorrectionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(24,4)", precision: 24, scale: 4, nullable: false),
                    Method = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    EvidenceReference = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    RequestedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ApprovedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DocumentNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PostedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Releases = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_corrections", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.UniqueConstraint("AK_payment_corrections_TenantId_CompanyId_CustomerId_PaymentId~", x => new { x.TenantId, x.CompanyId, x.CustomerId, x.PaymentId, x.Id });
                    table.CheckConstraint("ck_payment_correction_amount", "\"Amount\" > 0 AND \"Mode\" IN ('Reversal','Refund') AND \"Method\" IN ('Cash','Bank')");
                    table.CheckConstraint("ck_payment_correction_checker", "\"ApprovedBy\" IS NULL OR \"ApprovedBy\" <> \"RequestedBy\"");
                    table.ForeignKey(
                        name: "fk_correction_receipt",
                        columns: x => new { x.TenantId, x.CompanyId, x.CustomerId, x.PaymentId },
                        principalSchema: "sales",
                        principalTable: "payments",
                        principalColumns: new[] { "TenantId", "CompanyId", "CustomerId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payment_allocation_releases",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CorrectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AllocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(24,4)", precision: 24, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_allocation_releases", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_payment_release_positive", "\"Amount\" > 0");
                    table.ForeignKey(
                        name: "fk_release_correction",
                        columns: x => new { x.TenantId, x.CompanyId, x.CustomerId, x.PaymentId, x.CorrectionId },
                        principalSchema: "sales",
                        principalTable: "payment_corrections",
                        principalColumns: new[] { "TenantId", "CompanyId", "CustomerId", "PaymentId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_release_original_allocation",
                        columns: x => new { x.TenantId, x.CompanyId, x.CustomerId, x.PaymentId, x.InvoiceId, x.AllocationId },
                        principalSchema: "sales",
                        principalTable: "payment_allocations",
                        principalColumns: new[] { "TenantId", "CompanyId", "CustomerId", "PaymentId", "InvoiceId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payment_cash_adjustments",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CorrectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(24,4)", precision: 24, scale: 4, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_cash_adjustments", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_payment_cash_negative", "\"Amount\" < 0");
                    table.ForeignKey(
                        name: "fk_cash_correction",
                        columns: x => new { x.TenantId, x.CompanyId, x.CustomerId, x.PaymentId, x.CorrectionId },
                        principalSchema: "sales",
                        principalTable: "payment_corrections",
                        principalColumns: new[] { "TenantId", "CompanyId", "CustomerId", "PaymentId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_payment_allocation_releases_TenantId_CompanyId_CorrectionId~",
                schema: "sales",
                table: "payment_allocation_releases",
                columns: new[] { "TenantId", "CompanyId", "CorrectionId", "AllocationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_allocation_releases_TenantId_CompanyId_CustomerId_~1",
                schema: "sales",
                table: "payment_allocation_releases",
                columns: new[] { "TenantId", "CompanyId", "CustomerId", "PaymentId", "InvoiceId", "AllocationId" });

            migrationBuilder.CreateIndex(
                name: "IX_payment_allocation_releases_TenantId_CompanyId_CustomerId_P~",
                schema: "sales",
                table: "payment_allocation_releases",
                columns: new[] { "TenantId", "CompanyId", "CustomerId", "PaymentId", "CorrectionId" });

            migrationBuilder.CreateIndex(
                name: "IX_payment_cash_adjustments_TenantId_CompanyId_CorrectionId",
                schema: "sales",
                table: "payment_cash_adjustments",
                columns: new[] { "TenantId", "CompanyId", "CorrectionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_cash_adjustments_TenantId_CompanyId_CustomerId_Paym~",
                schema: "sales",
                table: "payment_cash_adjustments",
                columns: new[] { "TenantId", "CompanyId", "CustomerId", "PaymentId", "CorrectionId" });

            migrationBuilder.CreateIndex(
                name: "IX_payment_corrections_TenantId_CompanyId_DocumentNumber",
                schema: "sales",
                table: "payment_corrections",
                columns: new[] { "TenantId", "CompanyId", "DocumentNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_corrections_TenantId_CompanyId_PaymentId",
                schema: "sales",
                table: "payment_corrections",
                columns: new[] { "TenantId", "CompanyId", "PaymentId" },
                unique: true,
                filter: "\"Status\" = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_payment_corrections_TenantId_CompanyId_PaymentId_SourceVers~",
                schema: "sales",
                table: "payment_corrections",
                columns: new[] { "TenantId", "CompanyId", "PaymentId", "SourceVersion" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payment_allocation_releases",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "payment_cash_adjustments",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "payment_corrections",
                schema: "sales");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_payment_allocations_TenantId_CompanyId_CustomerId_PaymentId~",
                schema: "sales",
                table: "payment_allocations");

            migrationBuilder.CreateIndex(
                name: "IX_payment_allocations_TenantId_CompanyId_CustomerId_PaymentId",
                schema: "sales",
                table: "payment_allocations",
                columns: new[] { "TenantId", "CompanyId", "CustomerId", "PaymentId" });
        }
    }
}
