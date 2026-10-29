# ADR 0003: Company transaction fence and linked correcting entries

Status: implemented for the current modular PostgreSQL deployment.

## Financial admission fence

Use one deterministic PostgreSQL transaction advisory-lock key per tenant/company. Ordinary financial admission takes a shared lock; period creation and close transitions take an exclusive lock. All locks are held through the command executor's commit/rollback. This prevents an in-flight posting from committing after a close transition based on an earlier Open read. It also serializes overlap checks for period definitions across API replicas.

Guard purchase approval, preparation, posting, reversal requests and reversal authorization. Cancellation commands share the fence as well. Draft/submission/rejection have no financial admission effect. The fence is intentionally company-wide: close checks remain stable across all branches, and balances can be compared against all movements without mixing concurrent commits. A close review has persisted Closing status, a separate approver and a cancel-review path; Closed is immutable through the API.

Pending approved purchases, prepared plans and correction-date pending reversals block request-close. Final close checks them again, reconciles affected purchases and all company warehouse balance projections, then freezes entry identities and signed period totals with a ledger fingerprint. Affected sources include those with dated original or correcting entries, even when the source's original date belongs to another period.

## Correcting entries

Maintain immutable original stock/tax entries and append one negating entry per original, linked by ReversesEntryId and the approved reversal request. Partial unique indexes preserve one original per source line and one correction per original. Scoped foreign keys prevent cross-company references. Sign checks distinguish positive receipts/input tax from negative corrections; zero-valued tax remains valid.

A reversal request changes the source version but preserves its original PostedVersion. Independent authorization rechecks open correction period, expected source version, original reconciliation and stock availability. Balance decrements and all correcting/source/audit/outbox/idempotency writes remain atomic. Cancellation returns the source to Posted without losing original-version evidence. Reversal of a closed original period uses a later open period; frozen earlier totals remain unchanged.

## Scope and limits

Only full reversal of the current domestic-goods purchase flow is supported. Business/legal selection of correction dates, partial returns, filing mappings, period reopening and service extraction remain separate work. The ledger fingerprint is an internal checkpoint, not a digital signature or government filing receipt. End users must not receive direct database write access that bypasses these application protocols.

The invariant is local to this shared database. Future independent stores require a distributed admission fence, participant acknowledgements and durable reconciliation before closing; a broadcast PeriodClosed event alone is insufficient.

## Verification

Real-PostgreSQL tests cover overlapping-period races, close versus purchase/reversal admission, missing/closing/closed periods, maker-checker enforcement, pending work, closed-period snapshot preservation after later corrections, concurrent reversal authorization, insufficient stock, failed reversal after balance decrement, cancellation/retry and damaged ledger/balance detection. Existing purchase, tax-rule, posting and authorization tests remain in the suite.

Reference: [PostgreSQL advisory-lock semantics](https://www.postgresql.org/docs/current/explicit-locking.html#ADVISORY-LOCKS).
