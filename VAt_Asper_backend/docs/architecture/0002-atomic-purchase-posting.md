# ADR 0002: Atomic local purchase posting with reviewed snapshots

Status: implemented; supersedes ADR 0001's statement that stock/tax posting is not included.

Later [period controls](0003-period-fencing-and-reversals.md) and [messaging/reporting](0004-outbox-inbox-reporting.md) supersede the initial limits below concerning period close, reversal and outbox delivery.

## Decision

Keep the initial modular deployment and exploit its real transaction boundary. Inventory receipt, tax posting, source status, plan status, audit, outbox and idempotency result commit in one PostgreSQL transaction. Do not simulate distributed partial-success states while all writes share a database. If these modules are extracted into services, replace this boundary with an explicitly specified durable posting process, participant inbox/outbox contracts and reconciliation protocol.

Separate purchase approval from a subsequent reviewed posting plan. Original purchases contain references but no verified tax treatment. Preparation therefore validates immutable product/supplier records, resolves an independently published effective rule for each product, freezes all calculation inputs/results and warehouse selection, and advances the source version. The final posting actor cannot be the purchase maker or plan preparer. Plans cannot be edited; cancel and prepare a new one before posting if necessary.

## Integrity and concurrency

- Every source/plan/reference/ledger lookup uses tenant and company scope.
- Composite foreign keys enforce company ownership of references, warehouses, plans and source lines.
- Rule publishing uses a transaction advisory lock per tenant/company/tax code before checking overlaps. API writes are the supported publication path; do not grant users direct table-write access.
- Purchase and plan versions are concurrency tokens. Source-line uniqueness in stock/tax ledgers prevents duplicate business effects even with distinct request/event keys.
- Balances use PostgreSQL atomic upsert, ordered by product to avoid cross-receipt lock inversion. A later failure rolls back these SQL increments together with the EF ledger writes.
- Reconciliation/status reads use a repeatable-read snapshot rather than mixing source and participant states from different commits.
- Ledger entries have no mutation endpoint. Reconciliation detects external corruption; protected database roles/retention and a correcting-entry workflow remain deployment/future work.

## Scope and controls

The supported policy is domestic BDT goods, tax-exclusive net values, configured percentage tax and configured recoverable percentage. No rates are seeded or read from purchase requests. Rule evidence is retained but not legally certified by the API. Tests use explicitly synthetic rates. Financial posting defaults disabled; environment configuration enables it for a reviewed scenario.

References and published rule versions are immutable in this increment. Rule supersession/amendment, finer eligibility conditions, imports, VDS, stock valuation, UOM conversion, reversals, period-close fencing and statutory returns remain separate work. Without period-close controls, this milestone is an executable pilot building block rather than the entire financial platform.

## Recovery and verification

Database errors produce rollback, leaving the source and plan retryable. The original idempotency key survives only successful commits and replays an unknown successful outcome. Tests inject failure after the balance SQL and before ledger commit, then assert absence of all effects and successful retry with the same key. Additional tests cover two competing posters, simultaneous different purchases into the same balance, rule publication races, effective-date boundaries, cancellation/repreparation, tenant boundaries, missing references, self-posting, permission/gate checks and reconciliation of a deliberately damaged ledger.

No RabbitMQ dispatcher or inbox exists yet. Outbox notifications remain stored. Introducing external delivery must preserve publisher confirmations, unroutable-message handling and durable consumer deduplication, without treating broker delivery as proof of business completion.

## References

- [EF Core transaction behavior](https://learn.microsoft.com/en-us/ef/core/saving/transactions)
- [PostgreSQL INSERT / ON CONFLICT](https://www.postgresql.org/docs/current/sql-insert.html)
