# 0037: Approved stock cost ledger

Date: 2026-10-31. Status: implemented for the coordinated owner's current stock flows.

The earlier purchase-net replay remains a diagnostic. Authoritative current stock values now require an independently approved complete opening cutover and a persisted cost journal. No automatic legacy backfill, timestamp ordering or source-price estimate is promoted into stock value.

The selected receipt basis is net purchase + non-recoverable tax + independently approved allocated BDT landed cost. Missing landed cost, including an unreviewed zero-charge assumption, blocks receipt posting for this basis. Source net/tax evidence remains separate.

Cost posting shares the physical command transaction, idempotency key, period/company fences, audit and outbox. FIFO layers / Moving Weighted Average pools and original issue allocations persist. Partial returns restore original issue cost, transfers preserve company value, and whole purchase reversals remove original receipt cost subject to remaining stock/value coverage. Price-only sales notes have no stock-cost quantity effect.

Deferred database guards check source linkage, approved method, allocation amounts, balanced transfers, dense valuation sequences and physical/journal/position/layer agreement. Read models use one Repeatable Read snapshot and fail closed on disagreement. Exact decimal strings are exposed for browser display, totals and CSV; legacy numeric fields remain compatible.

Opening values define a reviewed cutover layer per position. Historical FIFO reconstruction, legacy pre-adoption returns, nonzero method conversion, late charges, production/WIP/import/subcontract integration and historical-as-of reports require separate reviewed workflows. Company serialization and whole-company reconciliation still require representative load/UAT evidence before production rollout.

See [implementation and rollout](../STOCK_VALUATION_LEDGER.md) and [next plan](../../../NEXT_IMPLEMENTATION_PLAN_2026-10-31.md).
