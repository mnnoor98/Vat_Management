# Partial sales goods returns

Apply migration `PartialSalesReturns` through the normal deployment procedure. It freezes per-line allocations on credit notes, backfills legacy full-credit allocations from original posting entries and permits several posted credits against one original line. One pending credit per invoice remains enforced. Rollback to the previous schema cannot represent repeated partial credits; retain this schema after they are posted.

## Select return quantities

`GET /api/v1/companies/{companyId}/sales/{invoiceId}/returnable-lines` requires `sales.read`. It returns the current invoice version/state, `canRequestCredit` and each source line's ID/product, sold quantity, posted returned quantity, pending quantity, remaining quantity and remaining net/output VAT. Remaining values exclude pending requests; pending quantity is shown separately. A pending request blocks another request on that invoice. The response uses a consistent ledger read and refuses unreconciled sources.

Create a credit through the existing endpoint and permission `sales.credit.request`:

```json
{
  "expectedVersion": 5,
  "correctionDate": "2026-10-24",
  "reason": "Some goods returned",
  "returnReceiptReference": "RETURN-001",
  "lines": [
    {"lineId":"00000000-0000-0000-0000-000000000001","quantity":0.5}
  ]
}
```

Use actual line IDs from the returnable-lines or fulfillment response. Select 1–200 distinct lines of this invoice. Each quantity must be positive, have at most six decimal places and fit its remaining sold quantity. Duplicate/foreign/empty lines are rejected. No client amounts or tax rates are accepted. Omitted/null `lines` means **all remaining quantities**; an explicit empty array is invalid. A first omitted-lines request preserves the original full-return behavior.

The note response exposes frozen `lines` with original entry ID, line ID, positive quantity and negative net/output-tax allocation. `mode` is `FullInvoice` only when the note covers every original quantity and its original net/output-VAT values in one credit; otherwise it is `PartialReturn`. Every request needs a UUID idempotency key. Approval and cancellation use the pending note's sourceVersion and the existing maker-checker rules.

## Allocation and state

Allocation uses the original posted net/output-tax values, not a current tax rule or a newly computed tax on the partial price. For each line, calculate the target cumulative credit at its cumulative returned quantity, rounded to four decimals midpoint away from zero. This note receives that target minus already posted credit. Integer rational arithmetic avoids intermediate division changing rounding at a half-unit boundary. On the final quantity, the target is the exact original amount, so all rounding remainder is consumed once.

For example, original output VAT 0.0500 on three units returned one at a time allocates 0.0167, 0.0166 and 0.0167. Their sum is exactly 0.0500. A tiny-value final return can have zero additional VAT while still returning physical stock. Net and VAT are allocated independently from their frozen totals. Each note's signed amounts sum its allocations. When price credits exist, the remaining-pool rules in [Sales price credits](SALES_PRICE_CREDITS.md) preserve earlier allocations and prevent crediting reduced values again.

Approval returns only the selected quantities to the original warehouse, preserving other reservations. Invoice state becomes `PartiallyCredited` while any original quantity remains, and `Credited` after the last quantity. Original postedVersion stays fixed. Example: Posted 5 → CreditPending 6 → PartiallyCredited 7 → CreditPending 8 → Credited 9. Cancellation increments the source version and restores Posted or PartiallyCredited according to earlier posted credits, without consuming quantity or rounding capacity.

Correction date must be in an Open period, on/after the original invoice date and on/after the last posted return date for that invoice. Cancelled requests do not impose a date or quantity allocation on later requests. Closed original periods remain immutable; later corrections appear in their own business-date periods.

## Concurrency, reporting and compatibility

The pending-note constraint and source/credit-note optimistic versions serialize admission and decision per invoice. Repeated approvals, competing requests and approval/cancel races cannot commit duplicate stock returns or consume the same remaining quantity twice. A per-note/original-entry unique link allows subsequent notes to reference the same original entry while preventing duplicate lines within a note. Failures roll back stock, allocation posting, source state, audit, event and idempotency response together.

Reconciliation checks the entire credit history in source-version order, recomputes each frozen cumulative allocation, enforces quantity/value limits and matches every correcting row to its note. Final period close includes this reconciliation. Sales summaries continue summing signed amounts with separate invoice and credit-note counts.

Existing full-invoice credits keep `SalesCreditPostedV1`. Partial notes, including a later all-remaining completion, publish `SalesPartialCreditPostedV1` with their explicit allocations. The [new event contract](contracts/sales-partial-credit-posted-v1.schema.json) allows zero incremental amounts for rounded remainders. The reporting inbox records this separate notification without adding it to purchase totals. Consumers of the full-return event must explicitly adopt the new event to process partial returns.

This remains a goods-return flow backed by an external receipt reference. [Price-only credits](SALES_PRICE_CREDITS.md) have a separate request/event and share the original remaining value budget. Damaged-stock quarantine, cash refunds, customer verification and statutory forms/numbering remain outside the current implementation.

[Debit notes](SALES_DEBIT_NOTES.md) increase retained goods value without quantity movement. Subsequent returns allocate from the adjusted pool; all adjustment kinds share one pending source transition.
