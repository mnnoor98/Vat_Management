using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vat.Integration.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class IntegrationMetadataOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "integration");

            migrationBuilder.CreateTable(
                name: "integration_connections",
                schema: "integration",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Provider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Endpoint = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    SecretReference = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_integration_connections", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_integration_connections_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                });

            migrationBuilder.CreateTable(
                name: "external_references",
                schema: "integration",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    ConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_external_references", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_external_references_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
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
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    ConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    Mapping = table.Column<string>(type: "jsonb", nullable: false),
                    ApprovedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_field_mappings", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.UniqueConstraint("AK_field_mappings_TenantId_CompanyId_ConnectionId_Id", x => new { x.TenantId, x.CompanyId, x.ConnectionId, x.Id });
                    table.CheckConstraint("ck_field_mappings_rules", "\"Revision\" > 0 AND (\"ApprovedBy\" IS NULL OR \"ApprovedBy\" <> \"CreatedBy\")");
                    table.CheckConstraint("ck_field_mappings_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
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
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    ConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Endpoint = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    SigningSecretReference = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_webhook_subscriptions", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_webhook_subscriptions_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_webhook_subscriptions_integration_connections_TenantId_Comp~",
                        columns: x => new { x.TenantId, x.CompanyId, x.ConnectionId },
                        principalSchema: "integration",
                        principalTable: "integration_connections",
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
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    ConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    MappingId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    State = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    PayloadKey = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    PayloadDigest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inbound_batches", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_inbound_batches_rules", "(length(\"PayloadDigest\") = 64) AND \"State\" IN ('Draft','Pending','Approved','Rejected','Cancelled','Running','Completed','Failed','Submitted','Posted')");
                    table.CheckConstraint("ck_inbound_batches_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_inbound_batches_field_mappings_TenantId_CompanyId_Connectio~",
                        columns: x => new { x.TenantId, x.CompanyId, x.ConnectionId, x.MappingId },
                        principalSchema: "integration",
                        principalTable: "field_mappings",
                        principalColumns: new[] { "TenantId", "CompanyId", "ConnectionId", "Id" },
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
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    SubscriptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LeaseUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    HttpStatus = table.Column<int>(type: "integer", nullable: true),
                    Error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_delivery_attempts", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_delivery_attempts_rules", "(\"AttemptNumber\" > 0 AND (\"HttpStatus\" IS NULL OR \"HttpStatus\" BETWEEN 100 AND 599)) AND \"State\" IN ('Draft','Pending','Approved','Rejected','Cancelled','Running','Completed','Failed','Submitted','Posted')");
                    table.CheckConstraint("ck_delivery_attempts_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_delivery_attempts_webhook_subscriptions_TenantId_CompanyId_~",
                        columns: x => new { x.TenantId, x.CompanyId, x.SubscriptionId },
                        principalSchema: "integration",
                        principalTable: "webhook_subscriptions",
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
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    ConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    EventId = table.Column<Guid>(type: "uuid", nullable: true),
                    PayloadKey = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    FailedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReplayedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dead_letters", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_dead_letters_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
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
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    RowNumber = table.Column<int>(type: "integer", nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    State = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inbound_rows", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_inbound_rows_rules", "(\"RowNumber\" > 0) AND \"State\" IN ('Draft','Pending','Approved','Rejected','Cancelled','Running','Completed','Failed','Submitted','Posted')");
                    table.CheckConstraint("ck_inbound_rows_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_inbound_rows_inbound_batches_TenantId_CompanyId_BatchId",
                        columns: x => new { x.TenantId, x.CompanyId, x.BatchId },
                        principalSchema: "integration",
                        principalTable: "inbound_batches",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

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
                name: "IX_delivery_attempts_TenantId_CompanyId_CreatedAt",
                schema: "integration",
                table: "delivery_attempts",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_delivery_attempts_TenantId_CompanyId_State_CreatedAt",
                schema: "integration",
                table: "delivery_attempts",
                columns: new[] { "TenantId", "CompanyId", "State", "CreatedAt" });

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
                name: "IX_inbound_batches_TenantId_CompanyId_ConnectionId_MappingId",
                schema: "integration",
                table: "inbound_batches",
                columns: new[] { "TenantId", "CompanyId", "ConnectionId", "MappingId" });

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
                name: "IX_inbound_batches_TenantId_CompanyId_State_CreatedAt",
                schema: "integration",
                table: "inbound_batches",
                columns: new[] { "TenantId", "CompanyId", "State", "CreatedAt" });

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
                name: "IX_inbound_rows_TenantId_CompanyId_State_CreatedAt",
                schema: "integration",
                table: "inbound_rows",
                columns: new[] { "TenantId", "CompanyId", "State", "CreatedAt" });

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dead_letters",
                schema: "integration");

            migrationBuilder.DropTable(
                name: "delivery_attempts",
                schema: "integration");

            migrationBuilder.DropTable(
                name: "external_references",
                schema: "integration");

            migrationBuilder.DropTable(
                name: "inbound_rows",
                schema: "integration");

            migrationBuilder.DropTable(
                name: "webhook_subscriptions",
                schema: "integration");

            migrationBuilder.DropTable(
                name: "inbound_batches",
                schema: "integration");

            migrationBuilder.DropTable(
                name: "field_mappings",
                schema: "integration");

            migrationBuilder.DropTable(
                name: "integration_connections",
                schema: "integration");
        }
    }
}
