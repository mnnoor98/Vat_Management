using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IntegrationMappingScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_field_mappings_TenantId_CompanyId_ConnectionId_Id",
                schema: "integration",
                table: "field_mappings",
                columns: new[] { "TenantId", "CompanyId", "ConnectionId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_inbound_batches_TenantId_CompanyId_ConnectionId_MappingId",
                schema: "integration",
                table: "inbound_batches",
                columns: new[] { "TenantId", "CompanyId", "ConnectionId", "MappingId" });

            migrationBuilder.AddForeignKey(
                name: "FK_inbound_batches_field_mappings_TenantId_CompanyId_Connectio~",
                schema: "integration",
                table: "inbound_batches",
                columns: new[] { "TenantId", "CompanyId", "ConnectionId", "MappingId" },
                principalSchema: "integration",
                principalTable: "field_mappings",
                principalColumns: new[] { "TenantId", "CompanyId", "ConnectionId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_inbound_batches_field_mappings_TenantId_CompanyId_Connectio~",
                schema: "integration",
                table: "inbound_batches");

            migrationBuilder.DropIndex(
                name: "IX_inbound_batches_TenantId_CompanyId_ConnectionId_MappingId",
                schema: "integration",
                table: "inbound_batches");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_field_mappings_TenantId_CompanyId_ConnectionId_Id",
                schema: "integration",
                table: "field_mappings");
        }
    }
}
