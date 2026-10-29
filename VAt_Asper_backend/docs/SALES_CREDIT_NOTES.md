# Sales returns and credit notes

Apply migration `SalesCreditNotes` using the existing deployment procedure. It adds credit-note records, linked correcting ledger entries and the original invoice `postedVersion`. Existing posted invoices receive their posting version from their immutable fulfillment source version. Original ledger amounts and closed-period snapshots remain unchanged.

This guide describes the original full-credit flow. [Partial returns](SALES_PARTIAL_RETURNS.md) now extend it with selected quantities and multiple posted notes. Omitted lines credit all remaining quantities. Approval confirms that all invoiced goods were received back at the original warehouse in a condition suitable for stock. A return receipt reference is required as external evidence. The system does not verify that external receipt, process cash refunds or support damaged-goods quarantine. [Price-only credits](SALES_PRICE_CREDITS.md) use a separate request with adjustment evidence.

## API

Base: `/api/v1/companies/{companyId}/sales/{invoiceId}/credit-notes`. POSTs require a UUID `Idempotency-Key`, signed tenant/company access and the permission below.

| Method/path | Permission | Request/result |
|---|---|---|
| POST `/` | `sales.credit.request` | `{"expectedVersion":5,"correctionDate":"2026-11-02","reason":"Full goods return","returnReceiptReference":"RETURN-001"}` |
| GET `/` | `sales.read` | Paginated history; optional `before` UUID and `take=1..100` |
| GET `/{id}` | `sales.read` | Credit-note metadata, original document number, signed amounts and decision |
| POST `/{id}/approve` | `sales.credit.approve` | `{"expectedVersion":6}` using the pending note's `sourceVersion` |
| POST `/{id}/cancel` | `sales.credit.request` | `{"expectedVersion":6,"reason":"Return withdrawn"}` |

A version-5 Posted invoice moves to CreditPending version 6. Independent approval posts the note and moves the invoice to Credited version 7. Requester and original invoice maker cannot approve it. An invoice approver/poster may approve a credit if they are neither of those actors. The existing `Posting:Enabled` gate controls credit posting.

Cancelling a pending request restores Posted with a new version while preserving its original `postedVersion`. For example, cancel version 6 → Posted version 7; a new request uses version 7 → pending version 8 → Credited version 9. Cancelled requests remain in history. Posted notes are terminal. One pending request per invoice is allowed; several partial credits may be posted until every original quantity has been returned. A full credit of an unreturned invoice completes it in one step. Duplicate keys replay the original response; distinct competing commands produce only one successful decision.

## Financial and inventory behavior

The original source/fulfillment/ledger must reconcile at both request and approval. No client amounts or VAT rate are accepted. Optional line quantities follow the partial-return allocation rules. Credit values negate the frozen original line amounts exactly; current tax rules are not consulted. No independent rounding or changed-rate calculation is applied.

Approval appends a positive stock return and negative net/output-VAT entry for each selected original line. Each correcting entry identifies its credit note and `reversesEntryId`. Scoped foreign keys and unique reverse links protect the original relationship. Original entries are not edited. Returned quantities increase on-hand at the original warehouse and preserve reservations belonging to other invoices.

Stock SQL, correcting entries, note approval, invoice state/version, audit, outbox and idempotency response commit atomically. Failures roll them all back, leaving the pending request available for retry. Posted credit notes cannot be cancelled or repeated; remaining quantities can be credited through a new request. Internal credit-note numbers are `CN-{credit-note UUID without hyphens}` and are not statutory numbering.

## Periods, reconciliation and reporting

Correction date must be on/after the invoice date and inside an Open period. The original period may already be Closed. Pending notes block close of their correction period; all admission/approval/cancellation commands use the company period transaction fence. Cancellation is nonfinancial and may release a pending request without requiring that period to be Open.

Original closed snapshots stay frozen. The correction period captures negative sales net/output VAT amounts and the correcting entry IDs in `snapshot.sales`. Period reconciliation considers posted credit-note records as well as source and ledger rows, so missing corrections prevent final close.

The existing sales ledger exposes `creditNoteId` and `reversesEntryId` for corrections. Sales summary sums signed amounts by business date. `invoiceCount` counts original invoices in the requested interval; `creditNoteCount` counts posted credit notes in it. A month containing only returns may have negative totals and invoiceCount zero. Across an original invoice and its complete return, net/output VAT/gross sum to zero.

`SalesCreditPostedV1` is a separate transactional outbox notification with signed negative amounts and the correction date. The reporting inbox durably records it as Ignored, preserving the purchase-only financial projection's meaning. The synchronous sales summary already includes the committed credit regardless of broker delivery. See the [event contract](contracts/sales-credit-posted-v1.schema.json).

Customer master verification, statutory forms/numbering, tax-only adjustments, credit reversal and refund/accounting settlement remain later milestones. Correcting a posted credit requires a future documented append-only adjustment flow; no ledger editing API is available.

[Debit notes](SALES_DEBIT_NOTES.md) support separate positive price adjustments. Credits and returns use remaining values including posted debits, and pending adjustments are serialized across both note types.
