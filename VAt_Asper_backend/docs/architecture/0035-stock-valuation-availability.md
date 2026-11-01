# Stock valuation availability and costing-policy boundary

Date: 31 October 2026. Status: availability contract implemented; cost journal and policy persistence pending.

## Confirmed business scope

VAT Management covers Trading, Manufacturing, Import/Export and subcontract production. The user has explicitly requested both FIFO and Moving Weighted Average, selected by company/product. Neither method may be inferred from a sample product code or silently substituted for an unknown method.

## Current read contract

The previous query inferred cost from non-cancelled purchase documents across the company, included unposted purchases, guessed HS codes and standard prices, valued available rather than on-hand quantity, and capped company summaries at 1000 positions. Those values were not an authoritative cost ledger.

The stock API now preserves scoped on-hand/reserved/available quantities and existing product tax-code metadata. `standardCost` and `hsCode` are nullable and unavailable until authoritative sources exist. Tax code is not automatically an HS classification. Nonzero on-hand positions return `totalValuation: null`, `valuationStatus: Unavailable`, and `valuationReason: cost_journal_unavailable`. Zero on-hand positions return zero value with `NoStock`; this does not claim a costing method was executed. FIFO/WEIGHTED_AVERAGE are validated requested methods, not activated persisted policies. Unknown methods receive 422.

The summary aggregates all scoped positions in PostgreSQL without a 1000-row cutoff. Warehouse counts refer to warehouses with balance positions. Quantity fields retain their physical units; aggregating quantities across different units is not a normalized physical measure. Summary quantities use on-hand stock, including reservations. Company/warehouse valuations stay null if any position needs a missing cost journal. No speculative top-valued ranking is returned. Multiple summary reads share a Repeatable Read transaction; `capturedAt` is the response capture timestamp within that transaction, not a statutory cutoff or a reusable report generation ID.

The UI labels loaded warehouses, current-page quantities/values and the requested method. CSV preserves unavailable values and valuation status/reason. No migration or live ledger rewrite is required by this change. Consumers must handle nullable valuation fields; zero and unavailable have different meanings.

## Next costing implementation

1. Persist an independently approved effective-dated company default and product override. Resolve policy by business date and freeze policy/version in each posted costing command. Switching methods requires an explicit migration/reconciliation boundary, not a UI dropdown.
2. Add immutable warehouse/product cost journal entries and source coverage checkpoints. Use original posting-plan snapshots and actual source commits rather than draft documents. Opening stock requires approved quantity/value evidence.
3. FIFO needs ordered receipt layers and immutable issue allocations. Moving Weighted Average needs deterministic posting order, receipt value/quantity updates and stored issue unit costs. A complete journal must reconcile quantity and value before exposing a final valuation.
4. Transfer moves original inventory cost between locations; it cannot substitute a user-entered transfer price. Returns/reversals link the original issue/receipt allocations. Reservations affect availability but do not remove on-hand asset value.
5. Import receipts require reviewed landed-cost components, currency conversion/evidence and tax recoverability mapping. Export issues retain the original costing source. Production consumes actual material cost and uses reviewed conversion-cost allocation; incomplete production remains explicitly incomplete.
6. Subcontract dispatch retains ownership and tracks offsite custody. Consumption, unused material return, wastage and finished-goods return must reconcile to the job and original cost layers. Service invoice/VAT treatment is a separate reviewed contract.
7. Test backdated commands, simultaneous receipt/issue, zero-cost receipts, insufficient source coverage, cross-warehouse transfers, adjustments, closed periods, tenant isolation, failed commits and replay before enabling authoritative valuation.

Detailed landed-cost, by-product/wastage and policy-change business rules remain to be reviewed. This decision does not assert legal VAT rates or certified statutory form mapping.
