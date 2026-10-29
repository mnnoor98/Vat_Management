# Bounded financial source journal reads

The FinancialOutbox source now exposes a read contract over the immutable company journal introduced in migration 20. This completes the source-side bounded journal API. Reporting's durable consumer cursor, catch-up proof and production cutover remain separate work.

## Contract

With `ReportingSourceServer:Enabled`, a delegated caller with `report.read` can request:

```http
GET /internal/v1/reporting-source/companies/{companyId}/exports/journal?epoch={epoch}&after=0&through=42&take=100
```

Obtain the epoch and committed boundary from the checkpoint endpoint or an immutable export boundary. Both `epoch` and `through` are explicit; the server never substitutes a moving latest boundary during pagination. `after` is exclusive and `through` inclusive. Empty ranges are valid. Requests require a nonempty epoch, `0 <= after <= through`, and `take` from 1 to 100. A future boundary or changed epoch fails with 409; the consumer must not infer completion from an error.

`ReportingSourceJournalPageV1` returns version, trusted tenant/company, financial scope, epoch, legacy baseline, requested boundaries, `nextAfterSequence`, `complete`, and events containing sequence, original event ID and `envelopeJson`. The JSON string preserves the stored source envelope, including metadata. The reader neither reconstructs financial envelopes nor changes dispatcher leases, receipts or business data. Positions through `legacyThroughSequence` are baseline order rather than historical commit order.

Continue using `after=nextAfterSequence` with the **same epoch and through** until `complete` is true. `nextAfterSequence` advances only to the last returned position. New source commits beyond `through` cannot change these pages. A new catch-up pass must explicitly choose a later committed boundary.

## Bounds and failure behavior

- At most 100 metadata records are read first. Payload sizes use PostgreSQL `octet_length(Payload::text)` before materialization.
- Events are limited to 128 KiB of stored envelope text; serialized pages are limited to 1 MiB. A conservative sixfold JSON escape allowance and per-event/page overhead determine a smaller contiguous prefix when necessary. The full serialized response is checked again.
- An oversized next event returns `source_event_limit` (409). A smaller preceding prefix may be returned first; the large event is never skipped. A new contract or operator recovery is required to proceed past it.
- Missing positions, a short unexpected prefix or contradictory metadata fail with `source_sequence_gap` (409). The reader cannot return `complete=true` while omitting a committed position in the requested range.
- Database reads share a 20-second cancellation deadline and the caller's cancellation token. Immutable journal prefixes and immutable epoch/baseline identity allow multiple reads without holding a company writer lock.
- Verified actor tenant/company scope and HTTP permission checks apply before journal data is returned. Unauthenticated callers receive 401; missing permission or a disallowed company receives 403. Other tenants cannot retrieve the scoped journal.

The service is registered through dependency injection in financial hosting, with contract DTOs in neutral `Vat.Messaging`. No new migration or persistent database is required; the financial chain remains 20 and staged Reporting remains four migrations. This change does not mark `LiveCoverageVerified` true.

## Next gate

Add the bounded HTTP source adapter and persist generation bootstrap epoch/boundary plus a contiguous source cursor. Deliver each original envelope into the generation inbox and atomically bind position progress to accepted evidence, handling duplicates, reorder, quarantine and epoch changes before enabling financial catch-up proofs. Customer live coverage and coordinated promotion still follow that work.

## Verification on 2026-10-24

All 56 solution projects built in Release with zero warnings/errors; the latest test source also built cleanly. Forty affected PostgreSQL/API cases passed, including three new journal cases plus the existing Reporting export/customer, messaging and legacy backfill/reversal regressions. Tests verify original envelope equality, fixed pages after a real Purchase reversal, cursor/epoch/scope/permission failures, empty ranges, byte-limited continuation, oversized-event refusal without skipping, and fail-closed behavior for a missing committed position. Simulated journal damage is rolled back inside the disposable test database.

Evidence: `artifacts/source-journal-build.log`, `artifacts/source-journal-tests-build.log`, `artifacts/source-journal-tests.log`, `artifacts/source-journal-proof.json`, and `TestResults/source-journal`. The temporary PostgreSQL cluster stopped and its directory was removed. This is an affected-suite run, not a full-suite, broker/container, persistent-data migration or production API cutover claim.
