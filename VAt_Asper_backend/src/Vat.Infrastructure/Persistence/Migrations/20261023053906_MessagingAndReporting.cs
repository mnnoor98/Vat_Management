using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MessagingAndReporting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "reporting");

            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                schema: "platform",
                table: "outbox_messages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "LastError",
                schema: "platform",
                table: "outbox_messages",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LeaseExpiresAt",
                schema: "platform",
                table: "outbox_messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LeaseToken",
                schema: "platform",
                table: "outbox_messages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextAttemptAt",
                schema: "platform",
                table: "outbox_messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "QuarantinedAt",
                schema: "platform",
                table: "outbox_messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TotalAttempts",
                schema: "platform",
                table: "outbox_messages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "dead_letters",
                schema: "reporting",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Body = table.Column<byte[]>(type: "bytea", nullable: false),
                    MessageId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    EventType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ErrorCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Occurrences = table.Column<int>(type: "integer", nullable: false),
                    IsTruncated = table.Column<bool>(type: "boolean", nullable: false),
                    FirstSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dead_letters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "financial_entries",
                schema: "reporting",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExternalReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    NetAmount = table.Column<decimal>(type: "numeric(24,4)", precision: 24, scale: 4, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(24,4)", precision: 24, scale: 4, nullable: false),
                    RecoverableTaxAmount = table.Column<decimal>(type: "numeric(24,4)", precision: 24, scale: 4, nullable: false),
                    ReversalOfTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_entries", x => new { x.TenantId, x.CompanyId, x.TransactionId });
                });

            migrationBuilder.CreateTable(
                name: "inbox_messages",
                schema: "reporting",
                columns: table => new
                {
                    Consumer = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    PayloadHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inbox_messages", x => new { x.Consumer, x.EventId });
                });

            migrationBuilder.CreateTable(
                name: "projection_checkpoints",
                schema: "reporting",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LatestObservedEventAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_projection_checkpoints", x => new { x.TenantId, x.CompanyId });
                });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_NextAttemptAt_CreatedAt",
                schema: "platform",
                table: "outbox_messages",
                columns: new[] { "NextAttemptAt", "CreatedAt" },
                filter: "\"DispatchedAt\" IS NULL AND \"QuarantinedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_dead_letters_Fingerprint",
                schema: "reporting",
                table: "dead_letters",
                column: "Fingerprint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_dead_letters_TenantId_CompanyId_ResolvedAt",
                schema: "reporting",
                table: "dead_letters",
                columns: new[] { "TenantId", "CompanyId", "ResolvedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_financial_entries_TenantId_CompanyId_BusinessDate",
                schema: "reporting",
                table: "financial_entries",
                columns: new[] { "TenantId", "CompanyId", "BusinessDate" });

            migrationBuilder.CreateIndex(
                name: "IX_inbox_messages_TenantId_CompanyId",
                schema: "reporting",
                table: "inbox_messages",
                columns: new[] { "TenantId", "CompanyId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dead_letters",
                schema: "reporting");

            migrationBuilder.DropTable(
                name: "financial_entries",
                schema: "reporting");

            migrationBuilder.DropTable(
                name: "inbox_messages",
                schema: "reporting");

            migrationBuilder.DropTable(
                name: "projection_checkpoints",
                schema: "reporting");

            migrationBuilder.DropIndex(
                name: "IX_outbox_messages_NextAttemptAt_CreatedAt",
                schema: "platform",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                schema: "platform",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "LastError",
                schema: "platform",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "LeaseExpiresAt",
                schema: "platform",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "LeaseToken",
                schema: "platform",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "NextAttemptAt",
                schema: "platform",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "QuarantinedAt",
                schema: "platform",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "TotalAttempts",
                schema: "platform",
                table: "outbox_messages");
        }
    }
}
