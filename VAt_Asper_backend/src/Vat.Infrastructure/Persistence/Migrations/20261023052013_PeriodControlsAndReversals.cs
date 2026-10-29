using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PeriodControlsAndReversals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_tax_ledger_TenantId_CompanyId_PurchaseId_PurchaseLineId",
                schema: "tax",
                table: "tax_ledger");

            migrationBuilder.DropCheckConstraint(
                name: "ck_input_tax_amounts",
                schema: "tax",
                table: "tax_ledger");

            migrationBuilder.DropIndex(
                name: "IX_stock_movements_TenantId_CompanyId_PurchaseId_PurchaseLineId",
                schema: "inventory",
                table: "stock_movements");

            migrationBuilder.DropCheckConstraint(
                name: "ck_receipt_positive",
                schema: "inventory",
                table: "stock_movements");

            migrationBuilder.AddColumn<Guid>(
                name: "ReversalId",
                schema: "tax",
                table: "tax_ledger",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReversesEntryId",
                schema: "tax",
                table: "tax_ledger",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReversalId",
                schema: "inventory",
                table: "stock_movements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReversesEntryId",
                schema: "inventory",
                table: "stock_movements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                schema: "purchasing",
                table: "purchases",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PostedVersion",
                schema: "purchasing",
                table: "purchases",
                type: "bigint",
                nullable: true);

            // Preserve the original posting version before later reversal requests advance Version.
            migrationBuilder.Sql("UPDATE purchasing.purchases SET \"PostedVersion\" = \"Version\" WHERE \"Status\" = 'Posted'");

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                schema: "platform",
                table: "audit_entries",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "fiscal_periods",
                schema: "company",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    From = table.Column<DateOnly>(type: "date", nullable: false),
                    Until = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CloseRequestedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ClosedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Snapshot = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fiscal_periods", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_period_interval", "\"Until\" > \"From\"");
                    table.ForeignKey(
                        name: "FK_fiscal_periods_companies_TenantId_CompanyId",
                        columns: x => new { x.TenantId, x.CompanyId },
                        principalSchema: "company",
                        principalTable: "companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_reversals",
                schema: "purchasing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceVersion = table.Column<long>(type: "bigint", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CorrectionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    RequestedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ApprovedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PostedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_reversals", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.ForeignKey(
                        name: "FK_purchase_reversals_purchase_posting_plans_TenantId_CompanyI~",
                        columns: x => new { x.TenantId, x.CompanyId, x.PlanId },
                        principalSchema: "purchasing",
                        principalTable: "purchase_posting_plans",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_reversals_purchases_TenantId_CompanyId_PurchaseId",
                        columns: x => new { x.TenantId, x.CompanyId, x.PurchaseId },
                        principalSchema: "purchasing",
                        principalTable: "purchases",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tax_ledger_TenantId_CompanyId_PurchaseId_PurchaseLineId",
                schema: "tax",
                table: "tax_ledger",
                columns: new[] { "TenantId", "CompanyId", "PurchaseId", "PurchaseLineId" },
                unique: true,
                filter: "\"ReversesEntryId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_tax_ledger_TenantId_CompanyId_ReversalId",
                schema: "tax",
                table: "tax_ledger",
                columns: new[] { "TenantId", "CompanyId", "ReversalId" });

            migrationBuilder.CreateIndex(
                name: "IX_tax_ledger_TenantId_CompanyId_ReversesEntryId",
                schema: "tax",
                table: "tax_ledger",
                columns: new[] { "TenantId", "CompanyId", "ReversesEntryId" },
                unique: true,
                filter: "\"ReversesEntryId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_input_tax_amounts",
                schema: "tax",
                table: "tax_ledger",
                sql: "(\"ReversesEntryId\" IS NULL AND \"ReversalId\" IS NULL AND \"NetAmount\" >= 0 AND \"TaxAmount\" >= 0 AND \"RecoverableTaxAmount\" BETWEEN 0 AND \"TaxAmount\") OR (\"ReversesEntryId\" IS NOT NULL AND \"ReversalId\" IS NOT NULL AND \"NetAmount\" <= 0 AND \"TaxAmount\" <= 0 AND \"RecoverableTaxAmount\" BETWEEN \"TaxAmount\" AND 0)");

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_TenantId_CompanyId_PurchaseId_PurchaseLineId",
                schema: "inventory",
                table: "stock_movements",
                columns: new[] { "TenantId", "CompanyId", "PurchaseId", "PurchaseLineId" },
                unique: true,
                filter: "\"ReversesEntryId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_TenantId_CompanyId_ReversalId",
                schema: "inventory",
                table: "stock_movements",
                columns: new[] { "TenantId", "CompanyId", "ReversalId" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_TenantId_CompanyId_ReversesEntryId",
                schema: "inventory",
                table: "stock_movements",
                columns: new[] { "TenantId", "CompanyId", "ReversesEntryId" },
                unique: true,
                filter: "\"ReversesEntryId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_receipt_positive",
                schema: "inventory",
                table: "stock_movements",
                sql: "(\"ReversesEntryId\" IS NULL AND \"ReversalId\" IS NULL AND \"Quantity\" > 0) OR (\"ReversesEntryId\" IS NOT NULL AND \"ReversalId\" IS NOT NULL AND \"Quantity\" < 0)");

            migrationBuilder.CreateIndex(
                name: "IX_fiscal_periods_TenantId_CompanyId_From",
                schema: "company",
                table: "fiscal_periods",
                columns: new[] { "TenantId", "CompanyId", "From" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_reversals_TenantId_CompanyId_PlanId",
                schema: "purchasing",
                table: "purchase_reversals",
                columns: new[] { "TenantId", "CompanyId", "PlanId" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_reversals_TenantId_CompanyId_PurchaseId",
                schema: "purchasing",
                table: "purchase_reversals",
                columns: new[] { "TenantId", "CompanyId", "PurchaseId" },
                unique: true,
                filter: "\"Status\" = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_reversals_TenantId_CompanyId_PurchaseId_SourceVers~",
                schema: "purchasing",
                table: "purchase_reversals",
                columns: new[] { "TenantId", "CompanyId", "PurchaseId", "SourceVersion" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_stock_movements_purchase_reversals_TenantId_CompanyId_Rever~",
                schema: "inventory",
                table: "stock_movements",
                columns: new[] { "TenantId", "CompanyId", "ReversalId" },
                principalSchema: "purchasing",
                principalTable: "purchase_reversals",
                principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_stock_movements_stock_movements_TenantId_CompanyId_Reverses~",
                schema: "inventory",
                table: "stock_movements",
                columns: new[] { "TenantId", "CompanyId", "ReversesEntryId" },
                principalSchema: "inventory",
                principalTable: "stock_movements",
                principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_tax_ledger_purchase_reversals_TenantId_CompanyId_ReversalId",
                schema: "tax",
                table: "tax_ledger",
                columns: new[] { "TenantId", "CompanyId", "ReversalId" },
                principalSchema: "purchasing",
                principalTable: "purchase_reversals",
                principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_tax_ledger_tax_ledger_TenantId_CompanyId_ReversesEntryId",
                schema: "tax",
                table: "tax_ledger",
                columns: new[] { "TenantId", "CompanyId", "ReversesEntryId" },
                principalSchema: "tax",
                principalTable: "tax_ledger",
                principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_stock_movements_purchase_reversals_TenantId_CompanyId_Rever~",
                schema: "inventory",
                table: "stock_movements");

            migrationBuilder.DropForeignKey(
                name: "FK_stock_movements_stock_movements_TenantId_CompanyId_Reverses~",
                schema: "inventory",
                table: "stock_movements");

            migrationBuilder.DropForeignKey(
                name: "FK_tax_ledger_purchase_reversals_TenantId_CompanyId_ReversalId",
                schema: "tax",
                table: "tax_ledger");

            migrationBuilder.DropForeignKey(
                name: "FK_tax_ledger_tax_ledger_TenantId_CompanyId_ReversesEntryId",
                schema: "tax",
                table: "tax_ledger");

            migrationBuilder.DropTable(
                name: "fiscal_periods",
                schema: "company");

            migrationBuilder.DropTable(
                name: "purchase_reversals",
                schema: "purchasing");

            migrationBuilder.DropIndex(
                name: "IX_tax_ledger_TenantId_CompanyId_PurchaseId_PurchaseLineId",
                schema: "tax",
                table: "tax_ledger");

            migrationBuilder.DropIndex(
                name: "IX_tax_ledger_TenantId_CompanyId_ReversalId",
                schema: "tax",
                table: "tax_ledger");

            migrationBuilder.DropIndex(
                name: "IX_tax_ledger_TenantId_CompanyId_ReversesEntryId",
                schema: "tax",
                table: "tax_ledger");

            migrationBuilder.DropCheckConstraint(
                name: "ck_input_tax_amounts",
                schema: "tax",
                table: "tax_ledger");

            migrationBuilder.DropIndex(
                name: "IX_stock_movements_TenantId_CompanyId_PurchaseId_PurchaseLineId",
                schema: "inventory",
                table: "stock_movements");

            migrationBuilder.DropIndex(
                name: "IX_stock_movements_TenantId_CompanyId_ReversalId",
                schema: "inventory",
                table: "stock_movements");

            migrationBuilder.DropIndex(
                name: "IX_stock_movements_TenantId_CompanyId_ReversesEntryId",
                schema: "inventory",
                table: "stock_movements");

            migrationBuilder.DropCheckConstraint(
                name: "ck_receipt_positive",
                schema: "inventory",
                table: "stock_movements");

            migrationBuilder.DropColumn(
                name: "ReversalId",
                schema: "tax",
                table: "tax_ledger");

            migrationBuilder.DropColumn(
                name: "ReversesEntryId",
                schema: "tax",
                table: "tax_ledger");

            migrationBuilder.DropColumn(
                name: "ReversalId",
                schema: "inventory",
                table: "stock_movements");

            migrationBuilder.DropColumn(
                name: "ReversesEntryId",
                schema: "inventory",
                table: "stock_movements");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                schema: "purchasing",
                table: "purchases");

            migrationBuilder.DropColumn(
                name: "PostedVersion",
                schema: "purchasing",
                table: "purchases");

            migrationBuilder.DropColumn(
                name: "Reason",
                schema: "platform",
                table: "audit_entries");

            migrationBuilder.CreateIndex(
                name: "IX_tax_ledger_TenantId_CompanyId_PurchaseId_PurchaseLineId",
                schema: "tax",
                table: "tax_ledger",
                columns: new[] { "TenantId", "CompanyId", "PurchaseId", "PurchaseLineId" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_input_tax_amounts",
                schema: "tax",
                table: "tax_ledger",
                sql: "\"NetAmount\" >= 0 AND \"TaxAmount\" >= 0 AND \"RecoverableTaxAmount\" BETWEEN 0 AND \"TaxAmount\"");

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_TenantId_CompanyId_PurchaseId_PurchaseLineId",
                schema: "inventory",
                table: "stock_movements",
                columns: new[] { "TenantId", "CompanyId", "PurchaseId", "PurchaseLineId" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_receipt_positive",
                schema: "inventory",
                table: "stock_movements",
                sql: "\"Quantity\" > 0");
        }
    }
}
