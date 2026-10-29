using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Vat.Infrastructure.Persistence.Migrations;

[DbContext(typeof(VatDbContext))]
[Migration("20261024170000_ReportingSourceSequence")]
public sealed class ReportingSourceSequence : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE platform.reporting_source_heads (
          "TenantId" uuid NOT NULL, "CompanyId" uuid NOT NULL, "Epoch" uuid NOT NULL,
          "Sequence" bigint NOT NULL CHECK ("Sequence" >= 0),
          "LegacyThroughSequence" bigint NOT NULL CHECK ("LegacyThroughSequence" >= 0 AND "LegacyThroughSequence" <= "Sequence"),
          PRIMARY KEY ("TenantId", "CompanyId"));
        CREATE TABLE platform.reporting_source_journal (
          "TenantId" uuid NOT NULL, "CompanyId" uuid NOT NULL, "Sequence" bigint NOT NULL CHECK ("Sequence" > 0),
          "EventId" uuid NOT NULL UNIQUE, "Payload" jsonb NOT NULL,
          PRIMARY KEY ("TenantId", "CompanyId", "Sequence"),
          FOREIGN KEY ("TenantId", "CompanyId") REFERENCES platform.reporting_source_heads);
        CREATE TABLE platform.reporting_export_boundaries (
          "TenantId" uuid NOT NULL, "CompanyId" uuid NOT NULL, "ExportId" uuid NOT NULL,
          "Epoch" uuid NOT NULL, "Sequence" bigint NOT NULL, "LegacyThroughSequence" bigint NOT NULL,
          PRIMARY KEY ("TenantId", "CompanyId", "ExportId"),
          FOREIGN KEY ("TenantId", "CompanyId", "ExportId") REFERENCES platform.reporting_source_exports ("TenantId", "CompanyId", "Id"));
        -- The migration transaction blocks concurrent outbox inserts through installation.
        LOCK TABLE platform.outbox_messages IN SHARE ROW EXCLUSIVE MODE;
        INSERT INTO platform.reporting_source_heads
        SELECT "TenantId", "CompanyId", gen_random_uuid(), count(*), count(*)
        FROM platform.outbox_messages WHERE "EventType" = 'FinancialEntryRecordedV1' GROUP BY "TenantId", "CompanyId";
        -- Historical order is explicitly a baseline, never historical commit order.
        INSERT INTO platform.reporting_source_journal
        SELECT "TenantId", "CompanyId", row_number() OVER (PARTITION BY "TenantId", "CompanyId" ORDER BY "Id"), "Id", "Payload"
        FROM platform.outbox_messages WHERE "EventType" = 'FinancialEntryRecordedV1';
        CREATE FUNCTION platform.record_reporting_source_sequence() RETURNS trigger LANGUAGE plpgsql AS $$
        DECLARE next_sequence bigint;
        BEGIN
          IF NEW."EventType" <> 'FinancialEntryRecordedV1' THEN RETURN NEW; END IF;
          -- Audited backfill may restore a deleted outbox row from immutable evidence.
          IF EXISTS (SELECT 1 FROM platform.reporting_source_journal WHERE "EventId" = NEW."Id") THEN
            IF NOT EXISTS (SELECT 1 FROM platform.reporting_source_journal
              WHERE "EventId" = NEW."Id" AND "TenantId" = NEW."TenantId" AND "CompanyId" = NEW."CompanyId" AND "Payload" = NEW."Payload") THEN
              RAISE EXCEPTION 'Restored source event differs from immutable journal';
            END IF;
            RETURN NEW;
          END IF;
          INSERT INTO platform.reporting_source_heads VALUES (NEW."TenantId", NEW."CompanyId", gen_random_uuid(), 0, 0)
          ON CONFLICT DO NOTHING;
          -- Row lock survives until commit: another writer cannot overtake this transaction.
          UPDATE platform.reporting_source_heads SET "Sequence" = "Sequence" + 1
          WHERE "TenantId" = NEW."TenantId" AND "CompanyId" = NEW."CompanyId" RETURNING "Sequence" INTO next_sequence;
          INSERT INTO platform.reporting_source_journal VALUES (NEW."TenantId", NEW."CompanyId", next_sequence, NEW."Id", NEW."Payload");
          RETURN NEW;
        END $$;
        CREATE TRIGGER reporting_source_sequence AFTER INSERT ON platform.outbox_messages
        FOR EACH ROW EXECUTE FUNCTION platform.record_reporting_source_sequence();
        CREATE FUNCTION platform.freeze_reporting_export_boundary() RETURNS trigger LANGUAGE plpgsql AS $$
        DECLARE head platform.reporting_source_heads%ROWTYPE;
        BEGIN
          IF NEW."ManifestJson"::jsonb->>'scope' IS DISTINCT FROM 'purchase-financial-v1' THEN RETURN NEW; END IF;
          INSERT INTO platform.reporting_source_heads VALUES (NEW."TenantId", NEW."CompanyId", gen_random_uuid(), 0, 0)
          ON CONFLICT DO NOTHING;
          SELECT * INTO head FROM platform.reporting_source_heads
          WHERE "TenantId" = NEW."TenantId" AND "CompanyId" = NEW."CompanyId" FOR UPDATE;
          IF head."Sequence" <> (NEW."ManifestJson"::jsonb->>'eventCount')::bigint OR EXISTS (
            SELECT 1 FROM platform.reporting_source_journal j
            LEFT JOIN platform.outbox_messages o ON o."Id" = j."EventId"
            WHERE j."TenantId" = NEW."TenantId" AND j."CompanyId" = NEW."CompanyId"
              AND (o."Id" IS NULL OR o."TenantId" <> j."TenantId" OR o."CompanyId" <> j."CompanyId"
                   OR o."EventType" <> 'FinancialEntryRecordedV1' OR o."Payload" <> j."Payload")
          ) THEN RAISE EXCEPTION 'Reporting source journal differs from reconciled export'; END IF;
          INSERT INTO platform.reporting_export_boundaries VALUES
            (NEW."TenantId", NEW."CompanyId", NEW."Id", head."Epoch", head."Sequence", head."LegacyThroughSequence");
          RETURN NEW;
        END $$;
        CREATE TRIGGER reporting_export_boundary AFTER INSERT ON platform.reporting_source_exports
        FOR EACH ROW EXECUTE FUNCTION platform.freeze_reporting_export_boundary();
        CREATE FUNCTION platform.reject_reporting_sequence_evidence_change() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN RAISE EXCEPTION 'Reporting source sequence evidence is immutable'; END $$;
        CREATE TRIGGER immutable_reporting_source_journal BEFORE UPDATE OR DELETE ON platform.reporting_source_journal
        FOR EACH ROW EXECUTE FUNCTION platform.reject_reporting_sequence_evidence_change();
        CREATE TRIGGER immutable_reporting_source_journal_truncate BEFORE TRUNCATE ON platform.reporting_source_journal
        FOR EACH STATEMENT EXECUTE FUNCTION platform.reject_reporting_sequence_evidence_change();
        CREATE TRIGGER immutable_reporting_export_boundary BEFORE UPDATE OR DELETE ON platform.reporting_export_boundaries
        FOR EACH ROW EXECUTE FUNCTION platform.reject_reporting_sequence_evidence_change();
        CREATE TRIGGER immutable_reporting_export_boundary_truncate BEFORE TRUNCATE ON platform.reporting_export_boundaries
        FOR EACH STATEMENT EXECUTE FUNCTION platform.reject_reporting_sequence_evidence_change();
        CREATE TRIGGER immutable_reporting_source_head_delete BEFORE DELETE ON platform.reporting_source_heads
        FOR EACH ROW EXECUTE FUNCTION platform.reject_reporting_sequence_evidence_change();
        CREATE TRIGGER immutable_reporting_source_head_truncate BEFORE TRUNCATE ON platform.reporting_source_heads
        FOR EACH STATEMENT EXECUTE FUNCTION platform.reject_reporting_sequence_evidence_change();
        CREATE FUNCTION platform.guard_reporting_source_head() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
          IF (NEW."TenantId", NEW."CompanyId", NEW."Epoch", NEW."LegacyThroughSequence") IS DISTINCT FROM
             (OLD."TenantId", OLD."CompanyId", OLD."Epoch", OLD."LegacyThroughSequence")
             OR NEW."Sequence" <> OLD."Sequence" + 1 THEN
            RAISE EXCEPTION 'Reporting source checkpoint cannot be reset or skipped';
          END IF;
          RETURN NEW;
        END $$;
        CREATE TRIGGER guard_reporting_source_head BEFORE UPDATE ON platform.reporting_source_heads
        FOR EACH ROW EXECUTE FUNCTION platform.guard_reporting_source_head();
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => throw new InvalidOperationException("Source sequence evidence is forward-only.");
}
