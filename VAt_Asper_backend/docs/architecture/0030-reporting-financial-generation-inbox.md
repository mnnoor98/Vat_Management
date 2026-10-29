# Reporting financial generation inbox

Reporting's staged independent core now accepts generation-scoped financial events over its verified immutable Purchase/reversal baseline. This is a library/storage milestone. The deployed Reporting API and worker still use their existing financial implementation; this change does not provision a persistent database or switch runtime ownership.

## Contract and commit boundary

`ReportingFinancialEnvelopeV1` and its payload are neutral wire-compatible readers for the financial owner's `FinancialEntryRecordedV1` events. The own core has no reference to the financial application/domain/infrastructure assemblies. `IFinancialGenerationInbox.Deliver` requires an explicitly selected tenant, company and generation as well as the bounded existing `InboxDeliveryV1` evidence contract. A future authenticated delivery host must supply that expected scope from its trusted routing/authorization context; envelope claims are checked against it rather than used to choose a tenant.

The new generation-specific receipt binds the generation and complete delivery fingerprint. It can acknowledge `Processed`, `Duplicate`, durable `PendingOriginal`, or `Quarantined` evidence only after the owned transaction commits. It is deliberately separate from the existing runtime inbox receipt: the old dispatcher must not be pointed at this library without a matching generation-routing transport. Customer and other notification events are outside this financial-only scope and fail closed rather than being described as complete customer projections.

`Open` requires a verified baseline generation plus an authenticated `ReportManage` actor, command UUID and reason. Open/retire/replay operations persist actor/payload-bound receipts and operator audit atomically. Retire requires an expected version and is terminal. All deliveries and operator transitions serialize on the same company advisory lock. A reused context explicitly refreshes retirement state, so another process's retirement cannot be bypassed through tracked state. An already committed operator retry returns its original receipt without reopening or rewriting a generation.

## Owned storage and projection

Reporting migration `20261024155955_FinancialGenerationInbox` adds four scoped tables:

| Table | Purpose |
|---|---|
| `financial_inbox_generations` | Open/retired receive fence and version, linked to the verified baseline |
| `financial_inbox_messages` | Generation/event deduplication, original bounded delivery evidence, row identity and processing disposition |
| `financial_live_entries` | Immutable signed transactions beyond the immutable baseline |
| `financial_inbox_dead_letters` | Original delivery evidence, error/fingerprint and optional resolution time |

Every table retains tenant/company/generation keys and a scoped foreign key. PostgreSQL guards forbid deletion/truncation, freeze live financial entries, freeze inbox payload/scope/identity, permit only disposition/time updates, and make retirement terminal/versioned. Quarantine evidence remains immutable while resolution time may change. The migration is forward-only. Runtime grants should allow SELECT/INSERT and only the necessary mutable columns for inbox disposition, dead-letter resolution and generation retirement, with a separate migration identity. No request performs DDL.

Events overlapping the baseline compare full row identity and signed amounts and contribute no extra entry. The first accepted event binds its original bytes/metadata; conflicting copies are quarantined. Live transaction/event IDs, Purchase identity and full reversal identity are checked across baseline and live entries. A second full reversal cannot cancel an original twice. Money is validated losslessly and totals remain separated by currency.

A reversal arriving before its original is durably retained as `PendingOriginal` and does not change amounts. When the original arrives, its pending dependants are checked against the original's branch, warehouse, date, aggregate version, currency, reference and exact signed amounts in the same commit. Contradictory dependants move to quarantine. At most 32 pending candidates per missing original are retained in that state; additional candidates are quarantined, bounding the drain work. Original evidence is limited to the delivery protocol's 1 MiB prefix; oversized evidence is quarantined and cannot be replayed as a truncated message.

`Replay` uses retained evidence and commits projection/inbox/dead-letter resolution, operator audit and command receipt together. Audit failure rolls everything back. A changed actor/body under the same operator command key conflicts. Repeated deliveries and concurrent retries cannot double count. No source outbox dispatch/lease is reset and no active baseline is deleted.

## Reads and remaining coverage

`Summary` selects an explicit generation in a repeatable-read transaction, combines immutable baseline and live entries, preserves `[from, until)` boundaries, groups by currency and reports pending originals, unresolved quarantine and the most recent distinct evidence arrival time. Existing static snapshot/head reads retain their original static behavior. `LastDeliveryAt` is an arrival metric, not a commit watermark or duplicate-delivery heartbeat.

Every result still declares **`LiveCoverageVerified=false`**. Empty inbox/quarantine does not prove that all source commits have arrived. A frozen export capture timestamp, largest UUID or event time is not a source commit checkpoint. There is no automatic promotion, live head, customer event projection or production HTTP endpoint in this stage.

The next gates are a durable source completeness/catch-up contract, customer live coverage with complete dated movements, coordinated customer/financial generation verification and atomic promotion, runtime routes/worker transport, persistent migration/provisioning and backup/restore/cutover rehearsals. The own Reporting chain now has three migrations; the financial chain remains at 19. Runtime ownership remains five independent business APIs and seven coordinated APIs.

Subsequently, [0031](0031-reporting-export-coverage-proof.md) adds complete event-set verification against a frozen financial export, using a fourth owned migration. This verifies source-capture coverage only; continuing commit/catch-up and customer/promotion/runtime gates remain pending.

## Verification

Final verification on 24 October 2026: all 56 solution projects built in Release with zero warnings/errors. All **75 selected affected cases passed**: 56 independent Reporting core/HTTP cases (including 16 new inbox cases) and 19 financial-source/customer API cases. The real Purchase/reversal parity test additionally delivers the financial owner's actual outbox bytes into the own generation inbox and proves that baseline overlap is deduplicated without changing signed totals. Customer statement/ageing parity remains valid against the three-migration owned database.

The new tests cover concurrent duplicate delivery, reversal-before-original across restart, baseline overlap, contradictory payload/scope/schema/metadata, multiple reversals, generation isolation, terminal retirement including a reused context, external pending/quarantine state refresh, per-currency/date totals, immutable evidence, bounded/truncated quarantine, and audit-failure rollback followed by idempotent replay. The retained-quarantine replay fixture represents an earlier parser rejecting otherwise valid evidence. Both financial and Reporting EF models have no pending migration changes. Dependency inspection proves the own infrastructure package references only its own three projects, neutral messaging and SharedKernel.

An earlier combined run passed all projection comparisons but timed out dropping a temporary customer parity database. Only temporary Reporting database cleanup commands now allow 120 seconds for DROP/checkpoint completion; application and source HTTP deadlines are unchanged. The final run completed cleanup and PostgreSQL shutdown successfully. Logs use `artifacts/financial-inbox-*`, final TRX results use `TestResults/financial-inbox-verified`, and source/test/dependency evidence is retained under `artifacts/reporting-financial-inbox`.

The filter selected the two affected test projects; seven other projects had no matching tests. This was not a full-suite rerun, live broker/container verification, permanent database migration or production runtime cutover.
