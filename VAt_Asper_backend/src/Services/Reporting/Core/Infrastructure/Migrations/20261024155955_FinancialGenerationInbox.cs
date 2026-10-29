using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vat.Reporting.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FinancialGenerationInbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "financial_inbox_generations",
                schema: "reporting",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    GenerationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_inbox_generations", x => new { x.TenantId, x.CompanyId, x.GenerationId });
                    table.CheckConstraint("ck_financial_inbox_generation_state", "\"Status\" IN ('Open','Retired') AND \"Version\" > 0");
                    table.ForeignKey(
                        name: "FK_financial_inbox_generations_snapshot_generations_TenantId_C~",
                        columns: x => new { x.TenantId, x.CompanyId, x.GenerationId },
                        principalSchema: "reporting",
                        principalTable: "snapshot_generations",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "financial_inbox_dead_letters",
                schema: "reporting",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    GenerationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DeliveryJson = table.Column<string>(type: "character varying(1500000)", maxLength: 1500000, nullable: false),
                    ErrorCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_inbox_dead_letters", x => new { x.TenantId, x.CompanyId, x.GenerationId, x.Id });
                    table.ForeignKey(
                        name: "FK_financial_inbox_dead_letters_financial_inbox_generations_Te~",
                        columns: x => new { x.TenantId, x.CompanyId, x.GenerationId },
                        principalSchema: "reporting",
                        principalTable: "financial_inbox_generations",
                        principalColumns: new[] { "TenantId", "CompanyId", "GenerationId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "financial_inbox_messages",
                schema: "reporting",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    GenerationId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    BodyHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DeliveryJson = table.Column<string>(type: "character varying(1500000)", maxLength: 1500000, nullable: false),
                    RowJson = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: false),
                    TransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReversalOfTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_inbox_messages", x => new { x.TenantId, x.CompanyId, x.GenerationId, x.EventId });
                    table.CheckConstraint("ck_financial_inbox_message_state", "\"Status\" IN ('Processed','PendingOriginal','Quarantined','BaselineDuplicate')");
                    table.ForeignKey(
                        name: "FK_financial_inbox_messages_financial_inbox_generations_Tenant~",
                        columns: x => new { x.TenantId, x.CompanyId, x.GenerationId },
                        principalSchema: "reporting",
                        principalTable: "financial_inbox_generations",
                        principalColumns: new[] { "TenantId", "CompanyId", "GenerationId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "financial_live_entries",
                schema: "reporting",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    GenerationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReversalOfTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    RowJson = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: false),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    NetAmount = table.Column<decimal>(type: "numeric(24,4)", precision: 24, scale: 4, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(24,4)", precision: 24, scale: 4, nullable: false),
                    RecoverableTaxAmount = table.Column<decimal>(type: "numeric(24,4)", precision: 24, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_live_entries", x => new { x.TenantId, x.CompanyId, x.GenerationId, x.TransactionId });
                    table.ForeignKey(
                        name: "FK_financial_live_entries_financial_inbox_generations_TenantId~",
                        columns: x => new { x.TenantId, x.CompanyId, x.GenerationId },
                        principalSchema: "reporting",
                        principalTable: "financial_inbox_generations",
                        principalColumns: new[] { "TenantId", "CompanyId", "GenerationId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_financial_inbox_dead_letters_TenantId_CompanyId_GenerationI~",
                schema: "reporting",
                table: "financial_inbox_dead_letters",
                columns: new[] { "TenantId", "CompanyId", "GenerationId", "Fingerprint" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_financial_inbox_messages_TenantId_CompanyId_GenerationId_Re~",
                schema: "reporting",
                table: "financial_inbox_messages",
                columns: new[] { "TenantId", "CompanyId", "GenerationId", "ReversalOfTransactionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_financial_live_entries_TenantId_CompanyId_GenerationId_Even~",
                schema: "reporting",
                table: "financial_live_entries",
                columns: new[] { "TenantId", "CompanyId", "GenerationId", "EventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_financial_live_entries_TenantId_CompanyId_GenerationId_Purc~",
                schema: "reporting",
                table: "financial_live_entries",
                columns: new[] { "TenantId", "CompanyId", "GenerationId", "PurchaseId" },
                unique: true,
                filter: "\"Kind\" = 'Purchase'");

            migrationBuilder.CreateIndex(
                name: "IX_financial_live_entries_TenantId_CompanyId_GenerationId_Reve~",
                schema: "reporting",
                table: "financial_live_entries",
                columns: new[] { "TenantId", "CompanyId", "GenerationId", "ReversalOfTransactionId" },
                unique: true,
                filter: "\"ReversalOfTransactionId\" IS NOT NULL");
            migrationBuilder.Sql("""
                CREATE FUNCTION reporting.guard_financial_inbox_evidence() RETURNS trigger
                LANGUAGE plpgsql AS $$ BEGIN
                    IF TG_TABLE_NAME = 'financial_inbox_messages' THEN
                        IF (to_jsonb(NEW) - ARRAY['Status','UpdatedAt']) IS DISTINCT FROM (to_jsonb(OLD) - ARRAY['Status','UpdatedAt']) THEN
                            RAISE EXCEPTION 'Inbox evidence is immutable';
                        END IF;
                    ELSIF TG_TABLE_NAME = 'financial_inbox_dead_letters' THEN
                        IF (to_jsonb(NEW) - 'ResolvedAt') IS DISTINCT FROM (to_jsonb(OLD) - 'ResolvedAt') THEN
                            RAISE EXCEPTION 'Quarantine evidence is immutable';
                        END IF;
                    ELSE
                        IF (to_jsonb(NEW) - ARRAY['Status','Version']) IS DISTINCT FROM (to_jsonb(OLD) - ARRAY['Status','Version'])
                           OR OLD."Status" <> 'Open' OR NEW."Status" <> 'Retired' OR NEW."Version" <> OLD."Version" + 1 THEN
                            RAISE EXCEPTION 'Inbox retirement is terminal and versioned';
                        END IF;
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER financial_live_no_update BEFORE UPDATE ON reporting.financial_live_entries
                    FOR EACH ROW EXECUTE FUNCTION reporting.reject_snapshot_mutation();
                DO $$ DECLARE table_name text; BEGIN
                    FOREACH table_name IN ARRAY ARRAY['financial_inbox_generations','financial_inbox_messages','financial_inbox_dead_letters','financial_live_entries'] LOOP
                        EXECUTE format('CREATE TRIGGER financial_inbox_no_delete BEFORE DELETE ON reporting.%I FOR EACH ROW EXECUTE FUNCTION reporting.reject_snapshot_mutation()', table_name);
                        EXECUTE format('CREATE TRIGGER financial_inbox_no_truncate BEFORE TRUNCATE ON reporting.%I FOR EACH STATEMENT EXECUTE FUNCTION reporting.reject_snapshot_mutation()', table_name);
                        IF table_name <> 'financial_live_entries' THEN
                            EXECUTE format('CREATE TRIGGER financial_inbox_guard_update BEFORE UPDATE ON reporting.%I FOR EACH ROW EXECUTE FUNCTION reporting.guard_financial_inbox_evidence()', table_name);
                        END IF;
                    END LOOP;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) => throw new InvalidOperationException("Generation inbox evidence is forward-only. Restore a verified backup for an approved rollback.");
    }
}
