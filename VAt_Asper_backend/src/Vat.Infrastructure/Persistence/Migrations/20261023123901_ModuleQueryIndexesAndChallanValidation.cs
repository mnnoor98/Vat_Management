using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ModuleQueryIndexesAndChallanValidation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_challan_numbers_rules",
                schema: "sales",
                table: "challan_numbers");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_versions_TenantId_CompanyId_State_CreatedAt",
                schema: "workflow",
                table: "workflow_versions",
                columns: new[] { "TenantId", "CompanyId", "State", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_vds_entries_TenantId_CompanyId_BusinessDate",
                schema: "tax",
                table: "vds_entries",
                columns: new[] { "TenantId", "CompanyId", "BusinessDate" });

            migrationBuilder.CreateIndex(
                name: "IX_vat_returns_TenantId_CompanyId_State_CreatedAt",
                schema: "compliance",
                table: "vat_returns",
                columns: new[] { "TenantId", "CompanyId", "State", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_transfers_TenantId_CompanyId_BusinessDate",
                schema: "inventory",
                table: "transfers",
                columns: new[] { "TenantId", "CompanyId", "BusinessDate" });

            migrationBuilder.CreateIndex(
                name: "IX_transfers_TenantId_CompanyId_State_CreatedAt",
                schema: "inventory",
                table: "transfers",
                columns: new[] { "TenantId", "CompanyId", "State", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_tax_posting_batches_TenantId_CompanyId_BusinessDate",
                schema: "tax",
                table: "tax_posting_batches",
                columns: new[] { "TenantId", "CompanyId", "BusinessDate" });

            migrationBuilder.CreateIndex(
                name: "IX_tax_posting_batches_TenantId_CompanyId_State_CreatedAt",
                schema: "tax",
                table: "tax_posting_batches",
                columns: new[] { "TenantId", "CompanyId", "State", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_report_jobs_TenantId_CompanyId_State_CreatedAt",
                schema: "reporting",
                table: "report_jobs",
                columns: new[] { "TenantId", "CompanyId", "State", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_reconciliation_runs_TenantId_CompanyId_State_CreatedAt",
                schema: "compliance",
                table: "reconciliation_runs",
                columns: new[] { "TenantId", "CompanyId", "State", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_posting_sagas_TenantId_CompanyId_State_CreatedAt",
                schema: "purchasing",
                table: "purchase_posting_sagas",
                columns: new[] { "TenantId", "CompanyId", "State", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_production_receipts_TenantId_CompanyId_BusinessDate",
                schema: "manufacturing",
                table: "production_receipts",
                columns: new[] { "TenantId", "CompanyId", "BusinessDate" });

            migrationBuilder.CreateIndex(
                name: "IX_production_orders_TenantId_CompanyId_BusinessDate",
                schema: "manufacturing",
                table: "production_orders",
                columns: new[] { "TenantId", "CompanyId", "BusinessDate" });

            migrationBuilder.CreateIndex(
                name: "IX_production_orders_TenantId_CompanyId_State_CreatedAt",
                schema: "manufacturing",
                table: "production_orders",
                columns: new[] { "TenantId", "CompanyId", "State", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_period_close_requests_TenantId_CompanyId_State_CreatedAt",
                schema: "compliance",
                table: "period_close_requests",
                columns: new[] { "TenantId", "CompanyId", "State", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_material_issues_TenantId_CompanyId_BusinessDate",
                schema: "manufacturing",
                table: "material_issues",
                columns: new[] { "TenantId", "CompanyId", "BusinessDate" });

            migrationBuilder.CreateIndex(
                name: "IX_inbound_rows_TenantId_CompanyId_State_CreatedAt",
                schema: "integration",
                table: "inbound_rows",
                columns: new[] { "TenantId", "CompanyId", "State", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_inbound_batches_TenantId_CompanyId_State_CreatedAt",
                schema: "integration",
                table: "inbound_batches",
                columns: new[] { "TenantId", "CompanyId", "State", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_delivery_attempts_TenantId_CompanyId_State_CreatedAt",
                schema: "integration",
                table: "delivery_attempts",
                columns: new[] { "TenantId", "CompanyId", "State", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_coefficient_versions_TenantId_CompanyId_State_CreatedAt",
                schema: "manufacturing",
                table: "coefficient_versions",
                columns: new[] { "TenantId", "CompanyId", "State", "CreatedAt" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_challan_numbers_rules",
                schema: "sales",
                table: "challan_numbers",
                sql: "(\"Number\" > 0 AND \"FiscalYear\" BETWEEN 1900 AND 9999 AND ((\"VoidedAt\" IS NULL AND \"VoidReason\" IS NULL) OR (\"VoidedAt\" IS NOT NULL AND \"VoidReason\" IS NOT NULL AND length(trim(\"VoidReason\")) > 0)))");

            migrationBuilder.CreateIndex(
                name: "IX_approval_tasks_TenantId_CompanyId_State_CreatedAt",
                schema: "workflow",
                table: "approval_tasks",
                columns: new[] { "TenantId", "CompanyId", "State", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_approval_instances_TenantId_CompanyId_State_CreatedAt",
                schema: "workflow",
                table: "approval_instances",
                columns: new[] { "TenantId", "CompanyId", "State", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_adjustments_TenantId_CompanyId_BusinessDate",
                schema: "inventory",
                table: "adjustments",
                columns: new[] { "TenantId", "CompanyId", "BusinessDate" });

            migrationBuilder.CreateIndex(
                name: "IX_adjustments_TenantId_CompanyId_State_CreatedAt",
                schema: "inventory",
                table: "adjustments",
                columns: new[] { "TenantId", "CompanyId", "State", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_workflow_versions_TenantId_CompanyId_State_CreatedAt",
                schema: "workflow",
                table: "workflow_versions");

            migrationBuilder.DropIndex(
                name: "IX_vds_entries_TenantId_CompanyId_BusinessDate",
                schema: "tax",
                table: "vds_entries");

            migrationBuilder.DropIndex(
                name: "IX_vat_returns_TenantId_CompanyId_State_CreatedAt",
                schema: "compliance",
                table: "vat_returns");

            migrationBuilder.DropIndex(
                name: "IX_transfers_TenantId_CompanyId_BusinessDate",
                schema: "inventory",
                table: "transfers");

            migrationBuilder.DropIndex(
                name: "IX_transfers_TenantId_CompanyId_State_CreatedAt",
                schema: "inventory",
                table: "transfers");

            migrationBuilder.DropIndex(
                name: "IX_tax_posting_batches_TenantId_CompanyId_BusinessDate",
                schema: "tax",
                table: "tax_posting_batches");

            migrationBuilder.DropIndex(
                name: "IX_tax_posting_batches_TenantId_CompanyId_State_CreatedAt",
                schema: "tax",
                table: "tax_posting_batches");

            migrationBuilder.DropIndex(
                name: "IX_report_jobs_TenantId_CompanyId_State_CreatedAt",
                schema: "reporting",
                table: "report_jobs");

            migrationBuilder.DropIndex(
                name: "IX_reconciliation_runs_TenantId_CompanyId_State_CreatedAt",
                schema: "compliance",
                table: "reconciliation_runs");

            migrationBuilder.DropIndex(
                name: "IX_purchase_posting_sagas_TenantId_CompanyId_State_CreatedAt",
                schema: "purchasing",
                table: "purchase_posting_sagas");

            migrationBuilder.DropIndex(
                name: "IX_production_receipts_TenantId_CompanyId_BusinessDate",
                schema: "manufacturing",
                table: "production_receipts");

            migrationBuilder.DropIndex(
                name: "IX_production_orders_TenantId_CompanyId_BusinessDate",
                schema: "manufacturing",
                table: "production_orders");

            migrationBuilder.DropIndex(
                name: "IX_production_orders_TenantId_CompanyId_State_CreatedAt",
                schema: "manufacturing",
                table: "production_orders");

            migrationBuilder.DropIndex(
                name: "IX_period_close_requests_TenantId_CompanyId_State_CreatedAt",
                schema: "compliance",
                table: "period_close_requests");

            migrationBuilder.DropIndex(
                name: "IX_material_issues_TenantId_CompanyId_BusinessDate",
                schema: "manufacturing",
                table: "material_issues");

            migrationBuilder.DropIndex(
                name: "IX_inbound_rows_TenantId_CompanyId_State_CreatedAt",
                schema: "integration",
                table: "inbound_rows");

            migrationBuilder.DropIndex(
                name: "IX_inbound_batches_TenantId_CompanyId_State_CreatedAt",
                schema: "integration",
                table: "inbound_batches");

            migrationBuilder.DropIndex(
                name: "IX_delivery_attempts_TenantId_CompanyId_State_CreatedAt",
                schema: "integration",
                table: "delivery_attempts");

            migrationBuilder.DropIndex(
                name: "IX_coefficient_versions_TenantId_CompanyId_State_CreatedAt",
                schema: "manufacturing",
                table: "coefficient_versions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_challan_numbers_rules",
                schema: "sales",
                table: "challan_numbers");

            migrationBuilder.DropIndex(
                name: "IX_approval_tasks_TenantId_CompanyId_State_CreatedAt",
                schema: "workflow",
                table: "approval_tasks");

            migrationBuilder.DropIndex(
                name: "IX_approval_instances_TenantId_CompanyId_State_CreatedAt",
                schema: "workflow",
                table: "approval_instances");

            migrationBuilder.DropIndex(
                name: "IX_adjustments_TenantId_CompanyId_BusinessDate",
                schema: "inventory",
                table: "adjustments");

            migrationBuilder.DropIndex(
                name: "IX_adjustments_TenantId_CompanyId_State_CreatedAt",
                schema: "inventory",
                table: "adjustments");

            migrationBuilder.AddCheckConstraint(
                name: "ck_challan_numbers_rules",
                schema: "sales",
                table: "challan_numbers",
                sql: "(\"Number\" > 0 AND \"FiscalYear\" BETWEEN 1900 AND 9999 AND ((\"VoidedAt\" IS NULL AND \"VoidReason\" IS NULL) OR (\"VoidedAt\" IS NOT NULL AND length(trim(\"VoidReason\")) > 0)))");
        }
    }
}
