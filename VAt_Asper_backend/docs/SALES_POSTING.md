# Sales stock and output VAT posting

Apply migration `SalesStockAndOutputTax` through the existing migration deployment procedure. Existing tax rules become `Treatment: "Purchase"`; stock balances receive `ReservedQuantity = 0`. Existing posted purchase snapshots and closed-period checkpoints remain unchanged. Enable financial posting with `Posting:Enabled` after configuring reviewed rules for the pilot scope. Reservation/review remains available while final posting is disabled.

## Tax rules

The existing `POST /api/v1/companies/{companyId}/tax/rules` accepts optional `treatment`, defaulting to `Purchase` for compatibility. Sales requires `"treatment":"Sale"` and `"recoverablePercent":0`. Publish through the existing independent checker endpoint. Published intervals cannot overlap for the same tenant/company/tax code/treatment. Purchase and Sale rules may cover the same interval and have different rates. No statutory rates are seeded.

Sales uses the product's tax code with an explicitly published Sale rule effective on the invoice business date (`from` inclusive, `until` exclusive). Missing or unpublished rules stop reservation without changing stock. Purchase rules cannot supply sales VAT implicitly. Tax-exclusive domestic goods in BDT are the current supported calculation scope; no inclusive pricing, discounts, exemptions classification or foreign-currency conversion is inferred.

## API and versions

Base: `/api/v1/companies/{companyId}/sales`. UUID idempotency keys are required for POSTs, as in the approval workflow.

| Method/path | Permission | Request/result |
|---|---|---|
| POST `/{id}/reserve` | `sales.reserve` | `{"expectedVersion":3,"warehouseId":"..."}`; returns 201 with frozen fulfillment snapshot |
| GET `/{id}/fulfillment` | `sales.read` | Reservation status, warehouse, actors, snapshot and internal document number |
| POST `/{id}/post` | `sales.post` | `{"expectedVersion":4}`; returns posted invoice version 5 |
| POST `/{id}/cancel` | `sales.cancel` | `{"expectedVersion":4,"reason":"Order withdrawn"}`; releases reserved quantities and cancels invoice |
| GET `/{id}/reconciliation` | `sales.read` | Compares posted invoice, frozen fulfillment and issue/output-tax entries |
| GET `/ledger?take=25&before=...` | `tax.read` | Scoped immutable posting entries with next cursor; take 1–100 |
| GET `/summary?from=2026-10-01&until=2026-11-01` | `report.read` | Invoice count, net, output VAT and gross for `[from, until)` |

The state sequence is Draft 1 → Submitted 2 → ApprovedPendingPosting 3 → Reserved 4 → Posted 5. Warehouse must belong to the invoice branch. Reservation records product/UOM, original quantities/prices, tax rule ID/version/rate, legal/evidence references and calculated amounts. Each line's output VAT rounds to four decimal places, midpoint away from zero, before summing. Final posting uses this frozen calculation.

The final poster must differ from both the invoice maker and reservation preparer. The original independent invoice approver may post if they did not prepare the reservation. The shared posting gate must be enabled. A posted invoice cannot be cancelled, re-reserved or posted again. Cancelled reservations remain queryable; create a replacement document for a changed order.

## Inventory and financial guarantees

Balances expose `quantity`, `reservedQuantity` and `availableQuantity` (`quantity - reservedQuantity`). Reservations atomically increment reserved stock only when enough stock remains available, grouping repeated products and updating products in stable order. Database checks enforce `0 <= reserved <= quantity`. A failure on any product rolls back the whole reservation. Purchase receipts preserve existing reservations; purchase reversals can consume only available stock.

Posting atomically decrements both on-hand and reserved quantities and writes one immutable `sales.posting_entries` row per invoice line. Each row contains its negative stock issue and positive net/output VAT amounts; it is separate from the purchase input-tax ledger and has no recoverability field. Invoice status, fulfillment, audit, outbox notification and idempotency result commit in the same PostgreSQL transaction. Concurrent commands are fenced by optimistic source/fulfillment versions and unique line entries; losing commands roll back all balance SQL.

Posting assigns `S-{invoice UUID without hyphens}` as a stable unique internal document number. It is not sequential statutory numbering and must not be presented as a Mushak form/challan number.

## Period close and reports

Reservation and posting require an Open fiscal period under the same company transaction fence as close. Both approved and reserved sales block period close. Cancellation releases stock under that fence and remains available for unposted documents. Final close reconciles sales entries against their frozen sources, includes sales issues in on-hand reconciliation, checks active reservations against reserved balances and captures a separate `snapshot.sales` checkpoint with invoice IDs, entry IDs, sales net and output VAT totals. The ledger fingerprint now includes sales rows. Purchase input-tax checkpoint totals keep their original meaning.

The sales summary reads the committed source ledger in a single SQL statement and is independent of broker delivery. Existing `/reports/financial-summary` remains the purchase/input-tax projection. Its rebuild/backfill operations do not change the sales source ledger. A combined net-VAT return/report is not implemented.

`SalesPostedV1` is delivered through the existing transactional outbox. The reporting inbox records it as a known notification without adding it to purchase totals. It is not the `FinancialEntryRecordedV1` purchase event; downstream consumers must opt into the separate [sales event contract](contracts/sales-posted-v1.schema.json).

## Remaining scope

Full goods returns now use the [Sales credit-note flow](SALES_CREDIT_NOTES.md), preserving original entries and adding linked corrections. Partial goods returns use [cumulative return allocations](SALES_PARTIAL_RETURNS.md). Customer master/registration verification, partial dispatch, debit notes, statutory document numbering/templates, combined VAT returns and external accounting exports remain subsequent work. No financial row editing endpoint is exposed.

Financial-only reductions are implemented through [Sales price credits](SALES_PRICE_CREDITS.md).
