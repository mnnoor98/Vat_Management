# 0036: Ordered source costing preview

Date: 2026-10-31. Status: implemented for original purchase/sales sources; authoritative valuation remains pending.

## Decision

Record company-sequenced source evidence inside original purchase receipt and original sales issue transactions. Serialize these supported sources and costing policy approval using the same company fence before stock-row locks. Do not reconstruct concurrent posting order from timestamps or business dates.

Expose a separate read-only `PurchaseNetOnly` cost preview. Resolve methods from the frozen approved source policies, replay FIFO or Moving Weighted Average and reconcile closing quantity against the same snapshot of physical stock. Do not promote this preview into the existing stock valuation fields.

## Evidence requirements

The company must have started with a provably empty stock history at ordered-source initialization. Legacy opening adoption requires a separate reviewed workflow. Every position source must be covered; returns, reversals and transfers block the preview, even when their net quantity is zero. Missing policy, changed methods, source mismatch, non-BDT currency, excess source count and reconciliation failure return an unavailable state without partial costs.

Source events are immutable and linked to their original scoped evidence. Deferred database constraints verify a dense newly committed sequence tail and event/head bounds. Failed source transactions roll back the clock together with stock, tax, audit, outbox and command replay evidence. Policy approval cannot alter captured receipt or ordered sales source history.

## Arithmetic and limits

Use integer quantity units at scale 6 and money units at scale 4, with BigInteger intermediates. Round partial allocation half away from zero; allocate all residual value on final depletion. Received quantity/value must equal issued plus closing quantity/value. At most 10,000 scoped sources are replayed, and a limit breach produces no calculation. Decimal quantities/money and company sequence use JSON strings for browser precision.

Only original purchase net amount is costed. VAT, landed charges, production cost, opening cost, returns/transfers, policy conversion and Import/Export/subcontract cost propagation remain separate work. Derived allocations are not yet an authoritative persisted cost posting ledger. The API is a current snapshot and does not support historical-as-of valuation.

See [implementation and rollout guide](../COSTING_POLICIES_AND_RECEIPTS.md).
