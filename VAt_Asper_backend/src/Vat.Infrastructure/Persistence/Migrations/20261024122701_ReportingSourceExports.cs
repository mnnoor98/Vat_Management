using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReportingSourceExports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "reporting_source_exports",
                schema: "platform",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ManifestJson = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    RowsJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reporting_source_exports", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("CK_reporting_source_exports_size", "octet_length(\"RowsJson\") <= 8388608");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
            => throw new InvalidOperationException("Reporting source export evidence is forward-only. Restore a verified backup for an approved rollback.");
    }
}
