# Receipt reversals and customer refunds

Deploy migration `PaymentCorrections` after `SalesInvoiceDueDates`. It adds scoped correction notes, negative cash-adjustment entries and positive allocation-release entries. Original receipts/allocations remain unchanged. Retain this schema after posting corrections; rolling back would discard financial evidence.

## Two distinct operations

A receipt reversal corrects a bounced, duplicate or otherwise invalid collection. It reverses all remaining receipt cash and releases all remaining allocations belonging to that receipt. It can restore customer debt; it does not require customer credit and does not reverse sales revenue, VAT or stock.

A refund records externally paid cash/bank money returned from verified customer credit. It may be partial. It must fit the receipt's remaining cash and current customer credit. Refunding an allocated invoice overpayment requires explicit releases of the original allocations. This API records externally evidenced actions; it neither initiates nor verifies a bank transfer.

All commands use tenant/company access and a UUID Idempotency-Key. Permission policies are separate: payment.reverse.request/approve and payment.refund.request/approve. Payment history reads require payment.read; statement/ageing still require report.read.

## Reversal request

POST `/api/v1/companies/{companyId}/payments/{paymentId}/reversals`:

```json
{"expectedVersion":2,"correctionDate":"2026-10-26","reason":"Bounced collection","evidenceReference":"BANK-RETURN-001"}
```

The server freezes the remaining receipt amount and remaining releases; the client cannot choose reversal amount, VAT or release allocations. The original payment method is retained. A receipt with more than 2000 remaining allocation rows must be handled through a separately designed larger-document workflow; it is not silently partially reversed.

## Refund request

POST `/payments/{paymentId}/refunds` under the same company base:

```json
{"expectedVersion":2,"correctionDate":"2026-10-25","amount":90,"method":"Bank","reason":"Return customer advance","evidenceReference":"REFUND-TRANSFER-001","releases":[]}
```

Method is Cash or Bank. Positive amounts have at most four decimal places, capped by the supported receipt limit. Reason and evidence are mandatory (1000 characters each). Up to 200 distinct original allocations may be selected. Retrieve original allocation IDs from GET `/payments/{paymentId}`.

To refund invoice credit instead of unapplied cash, supply releases such as:

```json
{"allocationId":"00000000-0000-0000-0000-000000000001","amount":21}
```

Each ID must belong to this receipt. Release amount cannot exceed the original allocation less earlier posted releases, or the invoice's current overpayment credit. Releases across one invoice are checked together. The cash portion not covered by releases must fit this receipt's unallocated cash. The entire refund and each invoice release must fit eligible credit both currently and as of correctionDate. A later sales credit cannot fund a backdated refund. Other outstanding debt can therefore reduce the refundable amount. Pending invoice credit/debit decisions must be resolved before releasing that invoice's credit.

These budgets are checked at request and again at approval, under the per-customer transaction lock. Two pending refunds on different receipts reserve no shared customer credit; only eligible approvals commit. An intervening sale/debit/refund may make a request unapprovable; cancel and recreate it.

## Review and state

A request returns 201 with canonical Location `/payments/{paymentId}/corrections/{id}`, Mode Reversal or Refund, frozen Amount/Releases, SourceVersion and note Version. Read/list through `/corrections/{id}` or `/corrections`.

Request changes the receipt from Posted to CorrectionPending and increments its version. Exactly one pending correction is permitted per receipt. Approval uses POST `/payments/{paymentId}/reversals/{id}/approve` or `/refunds/{id}/approve` with `{"expectedVersion":3}` using the note's actual sourceVersion. The requester and original receipt maker cannot approve. Posting must be enabled and the correction period Open. Mode-specific routes cannot be used to bypass the other permission policy.

Approval commits note status, source status/version, negative cash entry, frozen allocation releases, audit, outbox and idempotency response atomically. Same-key retries replay the committed response; failures roll back everything. Internal numbers are RV-{UUID without hyphens} and RF-{UUID without hyphens}; they are not statutory numbering.

Partial refunds restore Posted with the next source version. A full refund leaves Refunded; a remaining-amount reversal leaves Reversed. Both terminal states prevent further allocation/correction. Prior refunds are deducted before a reversal amount is calculated, preventing the original amount from being returned twice.

Cancel a pending note through its mode-specific `/cancel` endpoint with expectedVersion and reason, using request permission. Cancellation restores Posted and consumes no cash/releases. It remains nonfinancial under the company fence, including outside an Open period. Posted correction notes cannot be cancelled or repeated.

Correction date cannot precede receipt date, any admitted positive allocation date or an earlier posted correction date. Cancelled corrections do not constrain later dates. Later allocations cannot be dated before a posted receipt correction. A receipt from a Closed period may be corrected in a later Open period.

## Example and reporting

For a sale gross 210 and receipt 300 allocated entirely to that invoice, unapplied cash is 90. Refund 90 leaves net collected/allocated 210. A later sales credit of 21 makes invoice credit 21; refund 21 with a release of 21 leaves net collected/allocated 189 and balance zero. Reversing the remaining receipt then returns 189 and releases 189, restoring customer debt 189. Total cash offsets are exactly 300, not 411.

Payment reads keep original positive allocations and expose allocationReleases separately. allocatedAmount is net of releases; unallocatedAmount uses net cash. returnedAmount includes both reversal and refund offsets; netCollectedAmount is original collection less offsets. lastCorrectionDate is the latest posted correction date.

Customer receivables retain collectedAmount as gross original posted receipts and add returnedAmount/netCollectedAmount. Balance equals signed sales gross minus net collected. Invoice outstanding uses net allocations. Unallocated cash is net collected minus net allocations.

Statements retain the original Payment movement and add positive PaymentRefund/ReceiptReversal movements on correctionDate. Cash corrections do not create net revenue or output VAT. Ageing applies inclusive business-date cut-offs separately to original receipts, cash offsets, allocations and releases. Reversed/refunded receipt originals remain visible at earlier cut-offs. Current and historical report math use the same signed evidence.

Pending notes block close of their correction period. Close reconciles frozen corrections with cash/release journals and source-version history, including originals in older periods. Missing cash or release rows block reports and final close. Payment checkpoints keep gross collections/positive allocations and separately record cashAdjustmentIds, allocationReleaseIds, returnedAmount and releasedAmount. Closed older snapshots remain frozen.

[CustomerPaymentCorrectionPostedV1](contracts/customer-payment-correction-posted-v1.schema.json) is a known reporting-inbox notification. Its Amount is positive correction cash out; CashEntryId references the immutable negative journal entry. It does not affect purchase projections. Existing collection/allocation event contracts are unchanged.

## Verification and limits

`./scripts/Test-Local.ps1 -Filter FullyQualifiedName~PaymentCorrectionApiTests` runs focused PostgreSQL tests; omit Filter for the full suite.

Partial receipt reversals, allocation-only reassignments, reversing an already posted refund/correction, bank-account reconciliation, payment-provider integration, GL bank journals and statutory receipt/refund forms remain separate workflows. Sales credit/debit eligibility remains governed by its own admission and approval rules.
