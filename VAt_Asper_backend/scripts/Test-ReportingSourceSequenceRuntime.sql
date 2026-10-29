\set ON_ERROR_STOP on
-- Runtime permission/trigger probe. All inserted metadata is rolled back.
BEGIN;
DO $$
DECLARE tenant uuid := gen_random_uuid(); company uuid := gen_random_uuid(); event_id uuid := gen_random_uuid(); position bigint;
BEGIN
  INSERT INTO platform.outbox_messages ("Id", "TenantId", "CompanyId", "AggregateId", "AggregateVersion", "EventType", "Payload", "CreatedAt", "AttemptCount", "TotalAttempts")
  VALUES (event_id, tenant, company, event_id, 1, 'FinancialEntryRecordedV1', '{}'::jsonb, now(), 0, 0);
  SELECT "Sequence" INTO position FROM platform.reporting_source_heads WHERE "TenantId"=tenant AND "CompanyId"=company;
  IF position IS DISTINCT FROM 1 THEN RAISE EXCEPTION 'Runtime sequence insert failed'; END IF;
  UPDATE platform.outbox_messages SET "AttemptCount"=1 WHERE "Id"=event_id;
  SELECT "Sequence" INTO position FROM platform.reporting_source_heads WHERE "TenantId"=tenant AND "CompanyId"=company;
  IF position IS DISTINCT FROM 1 THEN RAISE EXCEPTION 'Dispatcher update advanced source position'; END IF;
END $$;
ROLLBACK;
