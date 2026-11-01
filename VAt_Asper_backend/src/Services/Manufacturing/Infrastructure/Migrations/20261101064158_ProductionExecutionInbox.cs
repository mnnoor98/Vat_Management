using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vat.Manufacturing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ProductionExecutionInbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "production_execution_checkpoints",
                schema: "manufacturing",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionVersion = table.Column<long>(type: "bigint", nullable: false),
                    ExecutionState = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    WipValue = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ReceivedQuantity = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_production_execution_checkpoints", x => new { x.TenantId, x.CompanyId, x.OrderId });
                    table.CheckConstraint("ck_production_execution_checkpoints", "\"SessionVersion\" >= 0 AND \"ExecutionState\" IN ('Running','Completed')");
                    table.ForeignKey(
                        name: "FK_production_execution_checkpoints_production_orders_TenantId~",
                        columns: x => new { x.TenantId, x.CompanyId, x.OrderId },
                        principalSchema: "manufacturing",
                        principalTable: "production_orders",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "production_execution_dead_letters",
                schema: "manufacturing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ErrorCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Body = table.Column<byte[]>(type: "bytea", maxLength: 1048576, nullable: false),
                    Occurrences = table.Column<int>(type: "integer", nullable: false),
                    FirstSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_production_execution_dead_letters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "production_execution_inbox",
                schema: "manufacturing",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: true),
                    EventType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PayloadSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SessionVersion = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_production_execution_inbox", x => new { x.TenantId, x.CompanyId, x.EventId });
                    table.CheckConstraint("ck_production_execution_inbox", "\"SessionVersion\" >= 0 AND \"Status\" IN ('Processed','Ignored')");
                    table.ForeignKey(
                        name: "FK_production_execution_inbox_production_orders_TenantId_Compa~",
                        columns: x => new { x.TenantId, x.CompanyId, x.OrderId },
                        principalSchema: "manufacturing",
                        principalTable: "production_orders",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_production_execution_checkpoints_TenantId_CompanyId_Session~",
                schema: "manufacturing",
                table: "production_execution_checkpoints",
                columns: new[] { "TenantId", "CompanyId", "SessionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_production_execution_dead_letters_Fingerprint",
                schema: "manufacturing",
                table: "production_execution_dead_letters",
                column: "Fingerprint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_production_execution_dead_letters_TenantId_CompanyId_LastSe~",
                schema: "manufacturing",
                table: "production_execution_dead_letters",
                columns: new[] { "TenantId", "CompanyId", "LastSeenAt" });

            migrationBuilder.CreateIndex(
                name: "IX_production_execution_inbox_TenantId_CompanyId_OrderId_Sessi~",
                schema: "manufacturing",
                table: "production_execution_inbox",
                columns: new[] { "TenantId", "CompanyId", "OrderId", "SessionVersion" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "production_execution_checkpoints",
                schema: "manufacturing");

            migrationBuilder.DropTable(
                name: "production_execution_dead_letters",
                schema: "manufacturing");

            migrationBuilder.DropTable(
                name: "production_execution_inbox",
                schema: "manufacturing");
        }
    }
}
