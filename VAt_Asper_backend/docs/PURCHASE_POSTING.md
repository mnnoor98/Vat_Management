# Purchase posting API

This increment posts registered domestic goods purchases in BDT using tax-exclusive prices, one effective percentage rule per product tax code, and configured recoverability. It does not cover imports, services, supplementary duties, VDS, inclusive-price treatment, currency conversion, stock valuation, UOM conversion or filing. The later [period/reversal increment](PERIODS_AND_REVERSALS.md) adds period admission gates and full correcting entries. No legal rates or eligibility assumptions are seeded. Review the rule evidence and calculation behavior for the intended pilot before enabling `Posting__Enabled=true`.

All routes below start with `/api/v1/companies/{companyId}`. Existing bearer, tenant/company, permission, idempotency and correlation requirements apply to every mutation. Codes/references are case-sensitive. Reference data and published rules have no edit/delete endpoint in this iteration.

## Register immutable references

| Method and route | Request | Permission |
|---|---|---|
| POST `/products` | `reference`, `description`, `unitOfMeasure`, `taxCode` | masterdata.manage |
| GET `/products/{id}` | - | masterdata.read |
| POST `/suppliers` | `reference`, `name` | masterdata.manage |
| GET `/suppliers/{id}` | - | masterdata.read |
| POST `/warehouses` | `branchId`, `code`, `name` | inventory.manage |
| GET `/warehouses/{id}` | - | inventory.read |

Product/supplier references must match the approved purchase. A warehouse must belong to its company and purchase branch. Product descriptions, UOM, tax classification and supplier identity are frozen into the plan. This registry does not independently verify a supplier's registration or determine statutory input-credit eligibility.

## Draft and publish a rule

`POST /tax/rules` requires `tax.rule.manage` and this body shape:

```json
{
  "taxCode": "<product tax code>",
  "effectiveFrom": "2026-01-01",
  "effectiveUntil": "2027-01-01",
  "ratePercent": "<reviewed numeric percentage>",
  "recoverablePercent": "<reviewed numeric percentage>",
  "legalBasis": "<applicable provision and treatment>",
  "evidenceReference": "<reviewed evidence reference>"
}
```

This illustrates fields, not a directly runnable request: replace percentage placeholders with JSON numbers. Percentages must be in [0,100], with at most four decimals. The date interval is **[effectiveFrom, effectiveUntil)**: the end date is excluded. A draft has version 1. A different subject with `tax.rule.publish` calls `POST /tax/rules/{id}/publish` with `{ "expectedVersion": 1 }`. The published version becomes 2.

Publication serializes by tenant/company/tax code. Overlapping published intervals return `409 tax_rule_overlap`; adjacent intervals are allowed. Missing, unpublished or out-of-date rules block preparation. Evidence text is recorded for review; the API does not fetch or certify it. Published rules are immutable. This increment supports registering known, disjoint intervals; later regulatory amendments need a separately designed supersession workflow rather than manual table edits.

## Prepare and review

After purchase approval (normally source version 3):

An Open fiscal period must cover the source business date for approval, preparation and final posting. Create periods with the [period API](PERIODS_AND_REVERSALS.md); a missing period is an error.

`POST /purchases/{purchaseId}/posting-plans`, permission `purchase.prepare`:

```json
{ "expectedVersion": 3, "warehouseId": "<warehouse UUID>" }
```

The purchase becomes `PostingPrepared`, normally version 4. The response is `201`, includes a Location, and contains an immutable snapshot with:

- Source line IDs, supplier/product identities, product UOM and classification.
- Rule IDs, rule versions, legal/evidence references and configured percentages.
- Net, tax, recoverable tax and gross amounts, plus calculation algorithm version.
- Selected warehouse, prepared source version and preparer identity.

The server resolves all rates from published rules. Each line's net value already uses the source document's four-decimal, midpoint-away-from-zero rounding. Line tax is `round4(net * ratePercent / 100)`, and recoverable tax is `round4(lineTax * recoverablePercent / 100)`. Totals sum the rounded line values; gross is net plus total tax, not net plus recoverable tax. This is an explicit calculation policy for this implementation, requiring domain approval for its intended use. It does not claim to implement every VAT rounding rule.

`GET /purchases/{purchaseId}/posting-plans/{planId}` returns the frozen plan. `GET /purchases/{purchaseId}/posting-status` returns a consistent source status/version and latest plan. Both require `purchase.read`.

## Authorize posting

`POST /purchases/{purchaseId}/posting-plans/{planId}/post`, permission `purchase.post`:

```json
{ "expectedVersion": 4 }
```

The actor must differ from both the original purchase maker and the plan preparer. A source approver may post if they did not prepare the plan. The request version refers to the prepared purchase, not the rule or plan version.

When enabled, one database transaction writes stock movements, increments balances, appends input tax entries, marks the plan Posted, marks the purchase Posted (normally version 5), and records audit plus three outbox events. A posted purchase cannot be posted again with a new key. The original successful key replays the original result. There is no background process to wait for and no distributed saga in this deployment.

Stored notifications include `PurchasePostedV1`, `StockReceiptPostedV1` and `TaxPostingCompletedV1`. The later [messaging increment](MESSAGING_AND_REPORTING.md) adds confirmed broker delivery, inbox processing and a self-contained `FinancialEntryRecordedV1` for report totals. Enable messaging separately; broker delivery is not another financial posting.

## Cancellation and recovery

Before posting, cancel a plan with permission `purchase.prepare`:

`POST /purchases/{purchaseId}/posting-plans/{planId}/cancel`:

```json
{ "expectedVersion": 4, "reason": "Warehouse selection needs correction" }
```

The cancelled plan and reason remain in history; the purchase returns to ApprovedPendingPosting with an incremented version. Create a replacement plan using that current version. A cancelled or posted plan cannot be reused. This is not a reversal of a posted financial record.

For a database/process failure, all local effects roll back. Retry the same request with the same Idempotency-Key after recovery. If the client lost the response after commit, the stored result is replayed. If source data changed or the operation completed under another key, read posting status instead of repeatedly issuing new commands. No partial stock/tax repair is normally required because they share the transaction. External database damage must be investigated; the reconciliation endpoint reports it and does not rewrite ledger history.

## Inspect and reconcile

| Route | Permission | Behavior |
|---|---|---|
| GET `/inventory/balances?warehouseId={id}` | inventory.read | Company/warehouse-scoped balance page; `take` defaults 25, max 100; pass last product ID as `beforeProduct`. |
| GET `/tax/ledger?purchaseId={id}` | tax.read | Scoped input-tax entries; optional purchase filter, `take`, and last entry ID as `before`. |
| GET `/purchases/{id}/reconciliation` | purchase.read | Repeatable-read comparison of source, frozen plan and individual stock/tax entries. |

Reconciliation requires a posted source and plan, matching versions/source lines, matching ledger line counts, and matching quantities, warehouse, products, rules, dates and monetary values. It reports `isReconciled`, totals and differences. An unposted purchase is not reported as reconciled. This checks the purchase's participant entries; it is not a global inventory valuation or return reconciliation.

## Migration and configuration

Apply committed migrations before enabling the new endpoints against an existing database:

```powershell
dotnet ef database update --project src/Vat.Infrastructure --startup-project src/Vat.Api
```

Existing approved purchases remain eligible for preparation once matching references/rules are registered. The previous approval API remains unchanged. No data is auto-posted on migration or startup. `Posting__Enabled` defaults false and only controls the final posting command; reference preparation, rule publication, plan review and status checks remain available.
