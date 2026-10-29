# Price-only sales credits

Apply migration `SalesPriceCredits` through the existing deployment procedure. Existing journal rows are classified as `Goods`; new `PriceCredit` rows require zero quantity and negative net value. Original postings, legacy credit allocations and closed snapshots are unchanged. Retain this schema after price credits are posted; the prior schema cannot represent zero-quantity adjustments.

## Request and review

`POST /api/v1/companies/{companyId}/sales/{invoiceId}/price-credits` requires `sales.credit.request`, company/tenant access and a UUID `Idempotency-Key`:

```json
{
  "expectedVersion": 5,
  "correctionDate": "2026-10-24",
  "reason": "Agreed reduction on retained goods",
  "evidenceReference": "PRICE-AGREEMENT-001",
  "lines": [
    {"lineId":"00000000-0000-0000-0000-000000000001","netAmount":25}
  ]
}
```

Use actual original line IDs from `/returnable-lines` or the fulfillment snapshot. `netAmount` is the positive tax-exclusive credit requested, at most four decimal places. Select 1–200 distinct invoice lines. Empty/null lists, duplicate/foreign IDs, nonpositive amounts and values above the remaining net budget are rejected. Clients cannot supply VAT rates, VAT amounts, gross amounts or stock quantities. Evidence and reason are mandatory; an evidence reference is stored for review and is not independently verified by this API.

The response uses the existing credit-note shape with `mode: "PriceOnly"`, zero line quantities, signed negative net/VAT amounts and `adjustmentEvidenceReference`. `returnReceiptReference` is empty because no goods receipt is asserted. Its Location is the canonical `/credit-notes/{id}` resource.

Approve through `POST /sales/{invoiceId}/credit-notes/{id}/approve` using `sales.credit.approve` and the pending sourceVersion. Cancel through the existing note `/cancel` endpoint using `sales.credit.request` and a reason. The requester and original invoice maker cannot approve the adjustment. The existing financial posting gate, Open correction period, version checks, audit, idempotency and company transaction fence apply.

## Value allocation with goods returns

Price credits and goods returns consume the same original line's net/output-VAT budget. Price VAT is allocated from the remaining frozen values, not a current tax rule or a client-entered VAT rate. Correction dates cannot precede the invoice or its latest posted credit or debit. Cancelled requests consume no quantity/value and do not constrain later correction dates.

Consecutive price credits use rounded cumulative net targets against their monetary pool. VAT rounds to four decimals, midpoint away from zero, using exact integer rational arithmetic. The last monetary credit consumes the exact remaining VAT. A goods return establishes a new monetary pool from the retained quantity's remaining values. A price adjustment establishes a new goods-return pool from the then-remaining quantity/net/VAT. This preserves earlier allocations while preventing later returns from crediting amounts already reduced.

For example, sell one unit for net 100 and VAT 5. A price credit of net 20 allocates VAT 1 with no stock effect. Returning half the unit afterwards credits net 40/VAT 2. The last half credits the remaining net 40/VAT 2. Across all adjustments, net 100/VAT 5 are credited exactly and one physical unit is returned.

Without price adjustments, existing cumulative quantity-return calculations retain their original behavior. Each frozen note is reconciled against the credit history in source-version order; changing an old allocation or a journal value prevents further admission and final period close.

## Stock, state and reporting

Price approval writes immutable journal rows with `entryType: "PriceCredit"`, quantity 0 and linked original entries. It performs no stock balance update and preserves reservations. Goods rows remain `entryType: "Goods"`; inventory reconciliation includes only goods quantities. Journal, note, invoice version/state, audit, event and idempotency result commit atomically.

One pending note per invoice is shared by price credits, goods returns and debit notes. Concurrent requests cannot freeze overlapping budgets simultaneously. Repeated approvals cannot duplicate committed credits. Cancellation restores Posted or PartiallyCredited according to prior posted credits. `postedVersion` stays fixed.

Invoice completion remains based on physical returned quantities. An invoice whose entire value has been credited may remain PartiallyCredited with outstanding goods. The returnable-lines API exposes `canRequestPriceCredit` plus pending net/VAT credit amounts separately from pending physical quantity. Remaining net/VAT values include posted adjustments and exclude pending ones. Goods may later return with zero additional monetary credit. Fully returned invoices are Credited and cannot accept more credits.

Sales summary includes price adjustments in signed totals and in `creditNoteCount`; `priceCreditCount` is its price-only subset. Period checkpoints include price entries and negative value totals in the correction period while original closed snapshots remain frozen. Existing purchase reports retain their meaning.

`SalesPriceCreditPostedV1` is a [separate event contract](contracts/sales-price-credit-posted-v1.schema.json) with evidence and zero-quantity allocations. The reporting inbox records it as a known notification without adding it to purchase totals. Full- and partial-goods event contracts are preserved. A full physical return after a prior price credit uses the partial-goods event when it credits only the residual original value.

This implements controlled value reductions against an existing posted sale. Tax-only adjustments, cash refunds, customer validation, statutory templates/numbering and external accounting settlement remain subsequent work. No legal eligibility decision or statutory filing is inferred from an evidence reference.

[Sales debit notes](SALES_DEBIT_NOTES.md) increase the remaining value pool without stock movement. A debit starts a new monetary and goods-return pool; pending debit amounts are exposed separately.
