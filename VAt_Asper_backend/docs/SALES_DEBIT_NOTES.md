# Sales debit notes / price increases

Apply migration `SalesDebitNotes` using the existing deployment procedure. It adds scoped debit notes and positive, zero-quantity `PriceDebit` journal entries. Keep this schema after posting debits: the previous schema cannot represent them.

## Request and decisions

`POST /api/v1/companies/{companyId}/sales/{invoiceId}/debit-notes` requires `sales.debit.request`, tenant/company access and a UUID `Idempotency-Key`.

```json
{
  "expectedVersion": 5,
  "correctionDate": "2026-10-24",
  "reason": "Agreed increase on retained goods",
  "evidenceReference": "PRICE-AGREEMENT-002",
  "lines": [{"lineId":"00000000-0000-0000-0000-000000000001","netAmount":25}]
}
```

Use original invoice line IDs. Supply 1?200 distinct lines with positive tax-exclusive net amounts at up to four decimals. Each selected line must retain physical goods. Original net plus all signed posted adjustments plus this increase cannot exceed 1,000,000,000,000,000 per line. Reason and evidence reference are required; the API stores evidence without independently verifying it. Clients cannot choose VAT or stock quantities.

GET the response Location or list the same collection. Approve at `/{id}/approve` with `sales.debit.approve` and `{"expectedVersion":6}` using the pending note's actual `sourceVersion`. Cancel at `/{id}/cancel` with request permission, that version and a reason. The requester and original invoice maker cannot approve. Approval requires the financial posting gate and an Open correction period. Correction dates cannot precede the invoice or latest posted credit/debit. Cancellation is nonfinancial and consumes no value.

## Financial behavior

Request changes the invoice to DebitPending. A single pending adjustment is shared across debit notes, price credits and goods returns through the source version. Approval/cancellation restores Posted, or PartiallyCredited when earlier credits were posted; the original postedVersion remains fixed. Fully returned invoices reject increases.

VAT uses the original frozen line rate. Consecutive debits use cumulative four-decimal targets, midpoint away from zero, with exact rational arithmetic. A posted credit or return starts a new debit rounding pool. A debit starts new price-credit and goods-return pools from the resulting remaining values, preserving previous allocations.

For example, an original net 100/VAT 5 followed by debit 25/VAT 1.25 leaves net 125/VAT 6.25 available for subsequent credits or return. Returning half credits net 62.5/VAT 3.125 and returns half a unit. Debits themselves leave stock and reservations unchanged.

Approval atomically commits the note, immutable positive journal entries, source state/version, audit, outbox event and idempotency result. Failed commands roll back together. Retries cannot duplicate committed amounts. Entries expose debitNoteId and adjustsEntryId; original rows remain unchanged.

The returnable-lines response includes pending net/VAT debit amounts and canRequestDebit. Remaining values include posted adjustments only. Sales summaries include signed debit values and debitNoteCount separately from creditNoteCount. Correction-period checkpoints include positive debit values and block close while a debit is pending. Closed original snapshots remain frozen. Reconciliation replays mixed adjustments in source-version order and blocks admission/close on mismatched allocations or missing journal rows.

[SalesDebitPostedV1](contracts/sales-debit-posted-v1.schema.json) carries positive allocations and evidence, with no quantity field. The reporting inbox recognizes it as a notification and does not add it to purchase projections.

DN numbers are internal identifiers. Statutory templates/numbering, tax-only changes, new tax treatments, customer settlement and cash collection remain subsequent work. Synthetic examples do not establish legal rates or eligibility.
