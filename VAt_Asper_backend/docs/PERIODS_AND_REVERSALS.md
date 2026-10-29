# Fiscal periods and purchase reversals

This milestone adds company-wide financial admission controls and full correcting entries for the supported domestic-goods purchase flow. It does not generate a statutory return or decide the legally appropriate correction period. An authorized domain reviewer chooses the correction date and reason.

All routes below start with `/api/v1/companies/{companyId}` and use existing signed company grants, permissions, Idempotency-Key, expected-version and correlation requirements.

## Create an open period

`POST /periods`, permission `period.manage`:

```json
{ "name": "October 2026", "from": "2026-10-01", "until": "2026-11-01" }
```

Intervals are **[from, until)**, company-wide across branches, and cannot overlap. A new period is Open, version 1. Read `/periods/{id}` with `period.read`, or list `/periods?take=25&before={lastId}` (max 100).

Purchase approval, posting-plan preparation and final posting now require an existing Open period for the original business date. Reversal request/approval require an Open period for the correction date. Draft creation and submission remain possible without a period; they create no ledger entries. No missing-period fallback silently opens a period.

## Request and approve close

1. Resolve all ApprovedPendingPosting / PostingPrepared purchases dated in the period and pending reversals with correction dates in the period.
2. Call `POST /periods/{id}/request-close` with `period.close.request`:

```json
{ "expectedVersion": 1, "reason": "All financial work is ready for review" }
```

3. The period becomes Closing, version 2. New approval, preparation, posting and reversal admission into that period are blocked.
4. A different subject with `period.close.approve` calls `POST /periods/{id}/close`:

```json
{ "expectedVersion": 2, "reason": "Reconciliation and evidence reviewed" }
```

The server rechecks pending work, reconciles each affected purchase's source/plan/original/correcting entries, and compares company warehouse balances with movement totals. Mismatches block close. It then stores a frozen checkpoint containing ledger entry IDs, affected purchase IDs, signed period totals and a SHA-256 ledger fingerprint. Status becomes Closed and the version advances. Audit and PeriodClosedV1 outbox storage commit in the same transaction.

The financial barrier is enforced in PostgreSQL, not through a cached event flag. Financial commands hold a shared transaction advisory lock for their company through commit. Period creation and close transitions take its exclusive counterpart, wait for existing commands to finish, and prevent new ones from checking stale state. Ordinary financial commands still run concurrently. This barrier is valid for the current shared database; separate future services need a distributed close protocol.

If review needs more work, `POST /periods/{id}/cancel-close` with `period.close.request`, the current version and a reason returns Closing to Open. All reasons are retained in audit. **Closed cannot be reopened by this API.** A close cancellation does not remove a frozen closed checkpoint.

The checkpoint is an internal ledger checkpoint in BDT, not a filed VAT return. It includes only entries whose own business/correction date falls inside the period. Therefore a later-period reversal does not rewrite an earlier closed period's totals.

## Cancel an unposted purchase

To dispose of an approved document without financial effects:

`POST /purchases/{id}/cancel-unposted`, permission `purchase.cancel`:

```json
{ "expectedVersion": 3, "reason": "Duplicate source invoice" }
```

This requires ApprovedPendingPosting. If a plan is Prepared, cancel the plan first and use the returned source version. The source becomes Cancelled and retains its reason. A posted purchase cannot use this endpoint. Cancelling an approved, unposted source resolves its close blocker without fabricating a stock/tax posting.

## Request and authorize full reversal

For a reconciled Posted purchase (normally source version 5):

`POST /purchases/{id}/reversals`, permission `purchase.reverse.request`:

```json
{
  "expectedVersion": 5,
  "correctionDate": "2026-11-02",
  "reason": "Full correction approved for this source transaction"
}
```

The correction date cannot precede the original business date and must belong to an Open period. The original period may already be Closed. The source becomes ReversalPending (normally version 6); the request retains original plan, source version, reason and requester. `GET /purchases/{id}/reversals/{reversalId}` requires `purchase.read`.

A different actor with `purchase.reverse.approve`, who is also not the original purchase maker, calls:

`POST /purchases/{id}/reversals/{reversalId}/approve`:

```json
{ "expectedVersion": 6 }
```

The server rechecks the original reconciliation and correction period. In one transaction it:

- Decrements stock only if each warehouse/product has enough available balance.
- Appends negative stock movements linked to the exact original entry IDs.
- Appends negative net/tax/recoverable-tax entries using the original frozen amounts and rule IDs.
- Marks source Reversed and request Posted; retains original positive entries and posted plan.
- Writes reasoned audit, PurchaseReversedV1 outbox storage and the successful idempotency result.

No current rate is recalculated. Constraints allow only one correcting entry for each original entry. A reversal cannot make stock negative. `Posting__Enabled` controls final reversal posting as well as ordinary posting.

The reversal is full-document only. Partial returns, credit notes, re-reversal and statutory adjustment classifications require subsequent business workflows. Correcting rows are not external filing evidence.

## Cancel or retry a reversal

Before authorization, `POST /purchases/{id}/reversals/{reversalId}/cancel` requires `purchase.reverse.request`:

```json
{ "expectedVersion": 6, "reason": "Correction date needs review" }
```

The request remains as Cancelled history, source returns to Posted with a new current version, and its original `postedVersion` remains unchanged. A later request uses the current source version. A cancelled/posted request cannot be reused.

Failures roll back balance changes, correcting entries, source/request state, audit, outbox and idempotency success together. Retry the same request/key after recovery. A successful retry replays the stored result even if the period was subsequently closed; it performs no new write. An insufficient-stock failure remains pending and must be resolved through an authorized stock/business workflow or cancellation, not direct ledger editing.

`GET /purchases/{id}/reconciliation` now verifies both original and correcting rows. A fully reversed purchase reports two entries per source line in each ledger, signed net/tax/recoverable totals of zero, and `isReconciled=true` only when every reversal matches its original. This is distinct from each period's signed totals.

## Permissions and error codes

| Permission | Operation |
|---|---|
| period.manage | Create non-overlapping periods. |
| period.read | Read period state and frozen checkpoint. |
| period.close.request | Request or cancel a close review. |
| period.close.approve | Independently approve final close. |
| purchase.cancel | Cancel an approved unposted purchase. |
| purchase.reverse.request | Request or cancel a full reversal. |
| purchase.reverse.approve | Independently authorize correcting entries. |

Important 409 codes: `period_unavailable`, `period_not_open`, `period_overlap`, `period_has_pending_operations`, `period_reconciliation_failed`, `period_balance_reconciliation_failed`, `reconciliation_failed`, `insufficient_stock`, `version_conflict`, `invalid_state`. Maker/checker violations return 403 `self_approval_forbidden`.

## Upgrade

Apply the committed `PeriodControlsAndReversals` migration using the normal EF update command. It adds period/reversal tables, ledger linkage, partial uniqueness and sign constraints, audit reasons, cancellation reasons and original posting-version metadata. Existing Posted sources retain their current version as their original posted version. Original positive ledger records are retained.

Existing installations must register non-overlapping periods covering dates they intend to admit before further purchase approvals/postings. No period is automatically created or closed, and no existing source is automatically posted or reversed. Perform a migration rehearsal for real data. The later [messaging/reporting increment](MESSAGING_AND_REPORTING.md) adds optional broker delivery and reporting. Filing, a reopening workflow and end-user database access remain outside scope.
