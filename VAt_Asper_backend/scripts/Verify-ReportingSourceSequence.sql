\set ON_ERROR_STOP on
BEGIN TRANSACTION ISOLATION LEVEL REPEATABLE READ READ ONLY;
DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM public."__EFMigrationsHistory" WHERE "MigrationId" = '20261024170000_ReportingSourceSequence') THEN
    RAISE EXCEPTION 'Reporting source sequence migration is missing';
  END IF;
  IF EXISTS (
    SELECT 1 FROM platform.reporting_source_heads h
    LEFT JOIN LATERAL (
      SELECT count(*) AS count, coalesce(max("Sequence"), 0) AS last
      FROM platform.reporting_source_journal j WHERE j."TenantId" = h."TenantId" AND j."CompanyId" = h."CompanyId"
    ) j ON true WHERE h."Sequence" <> j.count OR h."Sequence" <> j.last
  ) THEN RAISE EXCEPTION 'Source journal has missing positions'; END IF;
  IF EXISTS (
    SELECT 1 FROM platform.outbox_messages o
    LEFT JOIN platform.reporting_source_journal j ON j."EventId" = o."Id"
    WHERE o."EventType" = 'FinancialEntryRecordedV1' AND
      (j."EventId" IS NULL OR j."TenantId" <> o."TenantId" OR j."CompanyId" <> o."CompanyId" OR j."Payload" <> o."Payload")
  ) OR EXISTS (
    SELECT 1 FROM platform.reporting_source_journal j
    LEFT JOIN platform.outbox_messages o ON o."Id" = j."EventId"
    WHERE o."Id" IS NULL OR o."EventType" <> 'FinancialEntryRecordedV1'
  ) THEN RAISE EXCEPTION 'Source outbox differs from immutable journal evidence'; END IF;
  IF EXISTS (
    SELECT 1 FROM platform.reporting_export_boundaries b
    JOIN platform.reporting_source_heads h USING ("TenantId", "CompanyId")
    JOIN platform.reporting_source_exports e ON e."TenantId" = b."TenantId" AND e."CompanyId" = b."CompanyId" AND e."Id" = b."ExportId"
    WHERE b."Epoch" <> h."Epoch" OR b."LegacyThroughSequence" <> h."LegacyThroughSequence" OR b."Sequence" > h."Sequence"
      OR b."Sequence" <> (e."ManifestJson"::jsonb->>'eventCount')::bigint
      OR e."ManifestJson"::jsonb->>'scope' IS DISTINCT FROM 'purchase-financial-v1'
  ) THEN RAISE EXCEPTION 'Frozen export boundary differs from its source epoch'; END IF;
END $$;
SELECT (SELECT count(*) FROM public."__EFMigrationsHistory") AS financial_migrations,
       count(*) AS company_streams, coalesce(sum("Sequence"), 0) AS committed_positions,
       coalesce(sum("LegacyThroughSequence"), 0) AS legacy_positions,
       (SELECT count(*) FROM platform.reporting_source_journal) AS journal_rows,
       (SELECT count(*) FROM platform.reporting_export_boundaries) AS bound_exports
FROM platform.reporting_source_heads;
SELECT count(*) AS application_tables_including_history
FROM information_schema.tables
WHERE table_type = 'BASE TABLE' AND table_schema NOT IN ('pg_catalog', 'information_schema');
COMMIT;
