# Reporting financial source commit checkpoints

The financial owner now journals Purchase/reversal outbox inserts in the same PostgreSQL transaction as the business commit. This is the source-side checkpoint foundation; the Reporting consumer has not yet persisted or verified a contiguous source cursor and `LiveCoverageVerified` remains false.

## Contract and ordering

Financial migration `20261024170000_ReportingSourceSequence` adds three source-owned tables under `platform`: `reporting_source_heads`, `reporting_source_journal`, and `reporting_export_boundaries`. These are PostgreSQL trigger-managed infrastructure tables, deliberately outside the application entity model.

Each tenant/company has an immutable epoch, a sequence, and `LegacyThroughSequence`. The outbox INSERT trigger locks and increments the head row and appends the original event ID/payload to the journal. The lock lasts until transaction settlement, so concurrent writers in that scope cannot commit a later sequence ahead of an earlier transaction. Rollback removes both sequence advancement and journal evidence. Dispatcher updates do not allocate positions. Transactions that insert multiple events expose them atomically; positions within that transaction do not represent separate commits.

Migration installation locks outbox writes while backfilling existing events. Their UUID order is only a historical baseline. Positions through `LegacyThroughSequence` explicitly do **not** claim historical commit order. Thereafter sequence is commit ordered within the epoch. Journal and export boundaries reject UPDATE, DELETE and TRUNCATE. Head guards prohibit resets, skips, identity changes, DELETE and TRUNCATE.

Existing reconciled export preparation retains its exclusive company financial fence. Its INSERT trigger atomically captures the head into an immutable export boundary, checks the event count, and compares journal payload evidence with source outbox rows. A changed or missing source event aborts the export transaction, including audit and receipt. Prior exports are never assigned an inferred boundary.

Audited backfill first restores missing outbox rows from existing immutable journal evidence, preserving the original event ID and entire envelope. Exact restoration reuses its existing position; it does not allocate a new sequence. Legacy ledger entries without journal evidence still use normal reconciled backfill and receive a new committed position. Recovery counts include restored rows. A mismatching attempt to reuse a journaled event ID is rejected.

With `ReportingSourceServer:Enabled`, FinancialOutbox additionally exposes:

- `GET /internal/v1/reporting-source/companies/{companyId}/exports/checkpoint`
- `GET /internal/v1/reporting-source/companies/{companyId}/exports/{exportId}/boundary`

Both require `report.read` and verified tenant/company claims. The response contains `epoch`, `sequence`, and `legacyThroughSequence`. Missing checkpoints and exports created before this migration return `checkpoint_unavailable` (409); an inaccessible export returns 404. Existing manifest V1 bytes and export idempotency responses are unchanged. These reads must be compared as epoch/sequence pairs, never as timestamps, UUIDs or local coverage-proof versions.

The current checkpoint is a point-in-time committed source observation. It is not a live subscription, freshness lease or proof that Reporting has received every position. Customer history uses a separate scope and receives no financial sequence boundary.

## Remaining gates

The subsequent [bounded source journal API](0033-reporting-bounded-source-journal.md) implements the source read contract described below; the consumer adapter and durable contiguous cursor remain pending.

Add bounded, fixed-through-sequence journal delivery/read contracts, bind generation bootstrap to an export boundary, durably track contiguous positions despite duplicate/reordered messages, and verify epoch/gap handling before claiming financial catch-up. Then complete customer live contracts, coordinated promotion, independent Reporting HTTP composition and persistent database cutover. Runtime ownership remains five independent cores and seven coordinated financial APIs.

## Verification on 2026-10-24

All 56 solution projects built in Release with zero warnings/errors. The unchanged staged Reporting core passed 70 tests; after correcting exact-envelope backfill recovery, the final financial/API run passed 37 source export, customer reporting, messaging and legacy reversal/backfill cases. The new cases verify company-scoped checkpoints, permissions, transaction rollback, a waiting concurrent writer, immutable evidence, and frozen boundaries across a real Purchase reversal. The recovery case verifies original event ID/payload preservation without sequence advancement. This is 107 affected passing cases, not a full-suite or live broker/container run.

A pre-upgrade local financial backup was restored successfully in a disposable database. Migration 20 was then applied to `vat_management`. Its existing three financial events became three explicitly marked legacy positions in one company stream; no old exports received invented boundaries. `scripts/Verify-ReportingSourceSequence.sql` checks contiguous positions, source payload equality and boundary/epoch consistency. Existing database/index/constraint checks, EF pending-model checks and all five owned-service/runtime role boundary checks passed. The financial runtime role also executed `scripts/Test-ReportingSourceSequenceRuntime.sql`: source INSERT created a position, dispatcher UPDATE left it unchanged, and the entire probe rolled back.

Logs are under `artifacts/source-sequence-*.log`; final API TRX results are under `TestResults/source-sequence-recovery`, and the unchanged core's 70 passing results are under `TestResults/source-sequence`. Both test PostgreSQL clusters stopped and were removed. Reporting still has four staged migrations and no persistent database/runtime cutover.
