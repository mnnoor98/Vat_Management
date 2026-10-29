# Reporting ownership cutover plan (source snapshot boundary delivered)

Integration isolation is delivered in decision 0025. Reporting remains coordinated until the contracts and acceptance gates below exist. Creating an empty Reporting database or replacing the DSN would break current reads/recovery.

Decision [0027](0027-reporting-source-export.md) delivers the bounded Purchase-financial V1 snapshot contract and owner-local export preparation. Subsequent stages are tracked below.

Decision [0028](0028-reporting-snapshot-generation-core.md) adds independently built Reporting core projects, an owned snapshot context/migration and audited, idempotent financial snapshot bootstrap/activation. Decision [0029](0029-reporting-customer-history-snapshots.md) adds complete bounded customer history exports and independent static statement/ageing projections with a second owned migration. Live inbox/catch-up, coordinated customer/financial promotion and production API/database cutover remain pending. Static heads explicitly do not claim live coverage.

Decision [0030](0030-reporting-financial-generation-inbox.md) adds the financial-only generation inbox, immutable live entries, durable pending reversals, quarantine, retirement fencing and atomic operator replay, using a third owned migration. This partially delivers step 3 below; source completeness/checkpoints, customer live projections, coordinated promotion and production transport/cutover remain pending. Receipt of all currently observed messages does not imply caught-up source coverage.

Decision [0031](0031-reporting-export-coverage-proof.md) adds exact baseline-plus-live coverage proofs against a specific frozen source export, with a fourth owned migration. This supplies source-capture evidence for step 4. It does not establish a continuing source commit watermark or certify current source/customer live coverage; those gates remain required before promotion/cutover.

## Source-backed dependencies

| Capability | Current dependency | Required boundary |
|---|---|---|
| Financial freshness | `ReportingService.Summary`: financial outbox, inbox/dead letters, posted Purchase plans and reversals in one repeatable-read transaction | Source completeness manifest plus owned projection/checkpoint state; downstream unavailability cannot imply caught-up |
| Backfill | `ReportingService.Backfill`: company financial fence, Purchase reconciliation, source event append and audit | Owner-local idempotent backfill command with immutable export/reconciliation evidence |
| Rebuild | `ReportingService.Rebuild`: deletes projection/inbox/checkpoint and resets source outbox dispatch/lease state in one transaction | Owned shadow generation and replay/export receipts; do not split the existing transaction into separate HTTP deletes/resets |
| Customer statement | `CustomerReportingStore.Read/Statement`: Sales reconciliation, posting entries/credit/debit document numbers, posted payments and cash corrections | Reconciled signed document-movement snapshots with customer/document identity and complete opening history |
| Customer ageing | `CustomerReportingStore.Ageing`: invoice due dates, dated allocations, release journals, cash/refund history | Invoice/customer snapshots and dated allocation/release movements with explicit as-of semantics |

Current `SalesPostedV1` includes invoice/warehouse/document/date/totals but not customer ID or invoice due date. Current credit/debit notifications also cannot independently reconstruct all customer history. Payment/allocation/correction events carry useful customer references, but Reporting currently treats these as notifications rather than customer projections. Existing notifications must not be called a complete isolated customer report without authoritative bootstrap and gap checks.

## Delivery sequence

1. Add versioned financial-source manifest/export contracts. A manifest identifies tenant, company, scope, export ID, source reconciliation evidence, exact event/document counts and a deterministic fingerprint. Persist the snapshot boundary or equivalent durable evidence so pagination/retries cannot drift. A largest UUID or event creation timestamp alone is not a commit watermark.
2. Expose owner-local audited/idempotent source backfill and export preparation. Preserve the company admission fence and reconciler checks. Export authoritative signed journal/document movements; do not recalculate historical tax allocations or credit rounding from new DTO arithmetic.
3. Implement an owned Reporting core/context with generation-scoped financial/customer projections, inbox deduplication, quarantine, checkpoints, operator audit and idempotency. Projection effects and operator receipts must commit together. Existing operator replay rollback proof from 0025 is an acceptance requirement for the new context.
4. Build a new generation from the stable export while the active generation remains readable. Replay with generation/event identity and verify counts, source fingerprints and signed totals before an atomic generation promotion. Rebuild must not require deleting the active generation and independently resetting source leases. Retain original payload evidence through the supported replay horizon.
5. Move summary/entry and customer APIs only when each declared coverage scope is complete. Keep missing/unreachable source coverage explicitly incomplete; expose lag/quarantine and a verified checkpoint. Preserve current `[from, until)` statements and dated as-of allocation/release behavior.
6. Create the persistent owned database only with the usable core, migration chain, least-privilege runtime role and verified bootstrap/cutover utility. Rehearse source/target backups and restore, freeze old writers, compare nonempty source/target fingerprints, switch API/worker owners, then revoke cross-owner runtime access. No dual writers or silent fallback to financial SQL.

## Required acceptance evidence

- Nonempty Purchase/reversal financial history and Sales/credit/debit/payment/refund/release customer history produce identical signed totals and statement/ageing results before and after cutover.
- Duplicate and reordered delivery, missing events, contradictory payloads, source unavailable, partial export/rebuild, interrupted promotion and stale generation writes fail safely and recover deterministically.
- Replay audit failure rolls back projection/inbox/dead-letter changes; ambiguous operator responses resolve through the original actor-bound command key.
- Company/tenant composite scope, permission denial, bounded pages/body sizes, declared source retention and migration/runtime access are tested.
- Standalone Reporting starts with only its owned connection; published packages contain no financial core libraries. Source/Reporting restarts and a restored backup preserve receipt and generation guarantees.

External issuer, broker/container and operational load acceptance remain separate gates. After the source boundary in 0027, generation-scoped projection/bootstrap is the next implementation task; Integration's host cutover does not imply Reporting isolation.

## Source checkpoint increment (0032)

[Source commit checkpoints](0032-reporting-source-commit-checkpoints.md) add financial migration 20, an immutable source journal and export/epoch boundaries. The local source database is migrated and its existing events are explicitly marked as a legacy baseline. This advances the source-side fence; bounded journal delivery, Reporting's durable contiguous cursor and financial caught-up verification still precede customer live coverage and production cutover. Runtime ownership remains five independent and seven coordinated APIs.

[Bounded source journal reads](0033-reporting-bounded-source-journal.md) complete the source-side paging contract without another migration. The consumer HTTP adapter, durable position tracking and catch-up verification are the next gates.
