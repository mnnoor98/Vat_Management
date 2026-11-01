# Posted stock valuation ledger

Implemented: 31 October 2026. This extends the earlier purchase-net preview with persisted cost positions, FIFO layers / Moving Weighted Average pools and an immutable journal for the stock movements currently implemented by the coordinated financial owner.

## Selected cost basis

The user selected **net purchase + non-recoverable VAT + allocated landed cost**. The UI creates opening adoption using `NetPlusNonrecoverableTaxAndLanded`.

For an original receipt:

`receipt cost = frozen net amount + frozen tax amount - frozen recoverable tax amount + approved BDT landed allocation for that purchase line`.

Recoverable VAT is excluded. Landed allocation is a separately reviewed BDT amount; components already included in the purchase calculation must not be entered again. No exchange rate, duty, freight or other charge is guessed. An explicit independently approved zero-charge allocation is required when there are no landed charges. The original purchase tax/net evidence is retained separately.

The API retains an explicit `PurchaseNetOnly` adoption option for a separately approved restricted basis. It must not be confused with the selected full basis. The older `/cost-preview` remains a separate purchase-net-only diagnostic and is not the valuation ledger.

## Activation and opening evidence

1. Approve effective-dated company/default and product policies for the opening date.
2. Open `/app/inventory/valuation`, load **all** nonzero warehouse/product positions and enter each reviewed opening value and supporting reconciliation reference. Quantities are exact decimal strings and include reserved units.
3. Save the opening draft. An empty company can use an explicitly reviewed empty opening.
4. A different user reviews the frozen date, basis, quantities, methods and values and approves the draft. Approval activates the ledger atomically.

The complete opening must match every nonzero physical position. Preparation records a fingerprint of physical quantities and stock/purchase, quantity-sales and transfer activity counts. Subsequent stock activity invalidates approval even if net stock returns to the same quantity. Approval checks the open period, current references/policy and snapshot again under company fencing. No company can reset or readopt an active ledger.

Opening is an approved cutover. One opening layer is created per reviewed position; historical FIFO layers or historical issue costs are not reconstructed. Importing detailed legacy layers and reviewing returns of sales before adoption are separate workflows. Backdating a quantity posting before the opening date is rejected.

Opening preparation supports at most 10,000 positions. The UI loads every 100-row page and rejects an incomplete or over-limit collection. It never silently approves the visible page only. Opening review/edit tables show 25 positions at a time. The API also checks completeness independently of the UI.

## Receipt and landed-cost workflow

Prepare the normal approved purchase posting plan first. The valuation screen lists prepared receipts with purchase reference, warehouse, date and frozen source lines. Enter the declared landed-cost total, allocate it across **every** source line and provide the evidence/zero-charge declaration. Amounts are exact decimal strings with four places. Allocations must equal the declared total exactly; every line, including zero allocations, must appear once.

A different user approves the allocation against the still-prepared plan. Only one approved allocation may exist per plan. An approved review cannot be edited/deleted/truncated or moved to another plan. Posting without the review fails and rolls back physical stock, cost state, tax, outbox, audit and command evidence. After review, retry the normal purchase posting command.

Charges must be reviewed before receipt posting. This does not provide retroactive landed-cost true-ups after some stock has been sold; those require an approved split between remaining inventory and already-issued cost, and closed-period handling. No posted source is silently rewritten to accommodate a late invoice.

## Supported movements and conservation

| Source movement | Cost behavior |
|---|---|
| Original purchase receipt | Add full frozen receipt cost to FIFO layers or the moving-average pool |
| Original sales issue | Consume FIFO layers or allocate the current weighted-average pool; persist original issue allocations |
| Partial/full goods return | Restore the remaining original issue allocation cost, never the sales price or current purchase price; partial returns consume original allocations in order and final depletion takes the residual |
| Sales price-only credit/debit | No inventory quantity or inventory-cost entry |
| Warehouse transfer | Persist equal/opposite cost legs; preserve total company value and purchase-origin evidence; ignore the transfer document's commercial unit price for costing |
| Whole purchase reversal | Remove the original full receipt value. FIFO requires the complete original-origin quantity/value back in the original warehouse. Moving Average rejects removal that would leave a negative or unfunded residual pool |

Receipt/issue/return/transfer/reversal entries commit with their physical source command. Company costing fencing is acquired before stock-row writes. The same command/idempotency key cannot duplicate cost entries. Rollback restores cost positions and both source/valuation sequence heads.

Integer quantity units at scale 6 and money units at scale 4 avoid floating-point drift and multiplication overflow. Partial allocation rounds half away from zero; complete depletion takes the exact residual. Current physical quantity = cost-position quantity = sum of journal quantities, and position value = sum of journal values. FIFO layer totals must also match the position. Reserved stock stays in on-hand value.

Existing stock cannot change costing method automatically. An approved policy with a different method blocks quantity posting while stock remains. A zero-stock position can start under the new approved method. Reviewed conversion of nonzero stock is separate work.

## Persistence and database safeguards

- `inventory.cost_adoptions`: frozen opening document and independent approval.
- `inventory.cost_ledger_heads`: approved basis/opening and dense valuation sequence.
- `inventory.cost_positions`: current quantity/value and FIFO layers or weighted-average pool.
- `inventory.cost_entries`: immutable movement/allocation evidence.
- `inventory.cost_landed_reviews`: complete declared landed charges, frozen line allocation and independent approval.

Deferred database guards verify source identity/date/quantity, effective approved policy method, receipt basis, allocation totals, balanced transfer legs, dense sequence tails, opening authorization and physical/journal/position/layer reconciliation at commit. Active purchase/sales/transfer quantity sources require cost evidence in the same transaction. Unintegrated writers cannot legitimately update active physical quantities without cost reconciliation.

The original supported-source preview journal remains intact. Its narrower receipt/sales sequence is not the full valuation sequence. Runtime writers require the new table privileges; migration-owner credentials remain separate.

## Read and export contracts

`/inventory/all-balances` and `/inventory/valuation-summary` promote values only when an approved ledger exists and the company reconciles in a Repeatable Read snapshot. Unadopted nonzero stock remains `Unavailable`. Zero stock remains zero with no invented unit cost. Reconciliation failure clears authoritative values instead of serving a partial total. Summary aggregates cover all balances; top-valued items use actual reconciled values. A mixed company returns `costingMethod: MIXED`; each position retains its actual approved method. The legacy method query parameter cannot override persisted costing.

The numeric compatibility fields remain. New `totalValuationExact`, `unitCostExact`, `quantityExact`, `reservedQuantityExact`, `availableQuantityExact` and summary/warehouse exact-value fields preserve decimal text. The stock screen displays the exact strings and sums current-page values/quantities in integer units. CSV prefers the exact strings. Spreadsheet software may still infer numeric cell types when importing CSV; consumers should preserve these columns as decimal text. Unit cost is a rounded display value; the four-place total is authoritative.

The valuation screen shows a reconciled company value and cursor-paged immutable journal. Ledger quantities, amounts and sequences are strings. A failed or mismatched read clears previous values; approval requires fresh, independently authored review. Failed draft retries retain their command key until the payload changes.

All routes are under `/api/v1/companies/{companyId}/inventory`:

| Route | Permission / behavior |
|---|---|
| `GET cost-opening-positions?skip=` | `costing.read`, exact nonzero physical positions, 100 per page |
| `GET/POST cost-adoptions` | `costing.read/manage`, cursor list or reviewed opening creation |
| `GET cost-adoptions/{id}` | Scoped opening details |
| `POST cost-adoptions/{id}/approve` | `costing.approve`, expected version and independent actor |
| `GET landed-cost-sources?before=` | `costing.read`, prepared receipt/source lines, 25 per page |
| `GET landed-cost-sources/{planId}` | Scoped frozen source details |
| `GET/POST landed-cost-reviews` | `costing.read/manage`, cursor list or complete allocation creation |
| `GET landed-cost-reviews/{id}` | Scoped review details |
| `POST landed-cost-reviews/{id}/approve` | `costing.approve`, expected version and independent actor |
| `GET cost-ledger/status` | `costing.read`, basis/opening/sequence/reconciliation/value |
| `GET cost-ledger/entries?take=&before=` | `costing.read`, descending immutable sequence cursor, take 1-100 |

POST commands use the existing UUID `Idempotency-Key`, audit and coordinated transaction. Tenant/company scope is always checked. UI route: `/app/inventory/valuation`, linked from stock balances.

## Rollout and remaining scope

Apply reviewed financial-owner migrations `20261031135250_ActiveStockCostLedger` and `20261031135906_FullReceiptCostBasis`, after the policy/source migrations. SQL safeguards are packaged as an embedded migration resource. No live database has been migrated by this implementation. Drain old stock writers before activation, deploy all financial writers with cost hooks, then review and approve the company's opening. Pending migrations must be applied before deploying code that queries their tables.

This milestone covers the stock movements implemented by the coordinated financial owner. A subsequent [production/WIP milestone](PRODUCTION_WIP_EXECUTION.md) adds approved order imports, actual material issue/return, conversion costs and finished receipts to the same ledger. Physical scrap/by-products, import currency/tax workflows, export evidence and subcontract production remain separate work. These implementations do not certify current statutory treatment, historical-as-of valuation, detailed legacy FIFO reconstruction, late-charge true-ups, nonzero method conversion or full general-ledger accounting.

Company-level serialization and whole-company reconciliation prioritize correctness. High-volume load testing, restore/migration rehearsal, live browser UAT and business opening sign-off remain release gates. See the [next implementation plan](../../NEXT_IMPLEMENTATION_PLAN_2026-10-31.md).
