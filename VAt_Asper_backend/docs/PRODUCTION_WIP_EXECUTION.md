# Production orders and actual WIP execution

Implemented 2026-10-31. Verification results are recorded in the root implementation report.

## Ownership and approvals

Manufacturing owns a production order and its frozen approved coefficient revision, BOM, stock UOMs, planned output, business date and output warehouse. Draft → Submitted → Approved requires an independent approver. Approved evidence and its audit history are immutable. Estimated coefficient prices and conversion costs are excluded from the execution contract.

The coordinated financial owner imports an approved order through a configured, authenticated Manufacturing HTTP endpoint. It verifies company/tenant scope, source state, maker/checker, digest and current product/UOM references before creating one execution session per order. The owner source remains approved; financial execution status is available from the financial API. Accepted/posted outbox events are durable, but a reverse status consumer in Manufacturing is not implemented. This is not a cross-database atomic transaction.

Each financial operation is a separate evidence-backed draft. Posting requires an independent approver, current operation and session versions, an open period, enabled financial posting and an active reconciled cost ledger. Failed commands leave no physical stock, cost, WIP, audit or outbox changes; retry with the same idempotency key.

## Actual cost movements

| Operation | Stock effect | WIP effect |
|---|---|---|
| Issue | Consume unreserved BOM material at the approved FIFO/Moving Weighted Average cost | Add actual issued value |
| Return | Restore material against its original production issue and remaining original allocations | Remove original returned value |
| Conversion | No physical movement | Add explicitly approved actual labor, utility and overhead |
| Receipt | Add finished goods in the frozen output warehouse | Release the explicitly reviewed value into finished stock |
| ByProduct | Add an approved scrap/by-product in its reviewed company warehouse | Release the explicitly reviewed value into physical output stock |
| Loss | No fabricated scrap or by-product stock | Remove the reviewed loss value |
| Close | No movement; all other drafts must be posted or cancelled | Require zero WIP and freeze the execution |

Partial receipts are allowed up to the frozen planned quantity. Each receipt records its own actual output and reviewed WIP release; allocation is not inferred from the coefficient estimate. Close can accept a short-output execution only through its separate evidence-backed independent approval after all WIP is resolved; it does not automatically assert planned yield. A stale draft can be cancelled and recreated against the current session version. Money uses four decimal places and quantities six; API display and command amounts use exact decimal strings.

Stock quantities, immutable production movements, FIFO layers/average pools, cost journal, WIP entries, execution version, audit and outbox share the financial command transaction. Deferred PostgreSQL guards enforce source linkage, original-return bounds, immutable history and session/journal reconciliation. Period close includes production operations, physical movements and WIP checkpoints; pending drafts block close. Open WIP can carry into the next period through the immutable checkpoint.

## Configuration and routes

Manufacturing needs `ManufacturingReferences:InventoryBaseUrl` for scoped warehouse validation, in addition to its existing coefficient reference configuration. The financial host needs `ProductionReferences:ManufacturingBaseUrl`. Configure trusted origin URLs only. HTTPS is required unless the corresponding `AllowLoopbackHttp` setting explicitly permits a loopback development endpoint. Requests delegate the caller's bearer token; redirects, cookies, demo data and direct reads of another owner's database are not used.

- Manufacturing API: `/api/v1/companies/{companyId}/manufacturing/production-orders`, including submit, approve, cancel and execution-source routes.
- Financial API: `/api/v1/companies/{companyId}/inventory/production-executions`, including operation create, approve and cancel routes.
- Portal: `/app/manufacturing/execution`.

For the existing local service profiles, configure the Manufacturing process with `ManufacturingReferences__InventoryBaseUrl=http://127.0.0.1:5106/` and `ManufacturingReferences__AllowLoopbackHttp=true`. Configure the financial process serving Inventory with `ProductionReferences__ManufacturingBaseUrl=http://127.0.0.1:5107/` and `ProductionReferences__AllowLoopbackHttp=true`. These are explicit development settings, not production defaults; the code does not silently choose an endpoint.

Order commands use `manufacturing.request` / `manufacturing.approve`, and reads use `manufacturing.read`. Warehouse validation additionally delegates `inventory.read`. Financial import/drafts require `manufacturing.request` plus `costing.manage`; financial reads require `manufacturing.read` plus `costing.read`; posting requires `manufacturing.approve`, `costing.approve` and `inventory.post`. The approver must differ from the maker. Existing posting enablement and fiscal-period controls apply to financial operations.

New migrations: Manufacturing `20261031144502_VerifiedProductionOrders`; financial `20261031145050_ProductionWipPosting`, `20261031155101_ProductionInputUnits` and `20261031162622_PhysicalProductionOutputs`. Test migrations run only against disposable databases. Apply reviewed migrations to staging before enabling posting; no live database change is part of this code delivery.

## Remaining scope

This milestone supports one finished product plus [approved physical scrap/by-product outputs](PRODUCTION_PHYSICAL_OUTPUTS.md), [source-linked full and partial corrections](PRODUCTION_REVERSALS.md), independently approved closed-execution reopening and [approved actual-input unit ratios](PRODUCTION_INPUT_UNITS.md). Reopen changes only execution state and version; correction documents remain separate, and zero-WIP re-close is mandatory. BOM/planned-output alternative units, non-stock physical loss measurement, import/export documents, subcontract custody/service settlement and statutory production VAT adjustments remain separate work. Posted operations remain immutable.

The acceptance dataset uses two raw materials, a separate finished-goods warehouse, both costing methods, full receipt cost basis, material return, actual conversion cost, partial completion, WIP loss and close. Synthetic test tax values are not evidence of legal compliance.
