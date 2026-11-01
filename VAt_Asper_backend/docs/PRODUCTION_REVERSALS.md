# Immutable production corrections

This batch adds full reversals for posted conversion charges, WIP losses, material returns and finished-goods receipts in an **open execution**. Original documents, stock movements, cost entries and WIP entries remain unchanged. Corrections use a new operation, business date, reason and independent approval.

## API and review

Use the existing production operation endpoints. Create a draft with `kind: "Reverse"`, `reversesOperationId` identifying the original operation, current `expectedSessionVersion`, a correction `businessDate`, supporting `evidence`, empty `lines`, and zero amounts/output/charges. The service derives the complete quantity and value from the original posted source. The original must be a Return, Conversion, Loss or Receipt in the same tenant, company and execution, dated on or before the correction.

The usual idempotency key, permissions, independent maker/checker, posting enablement, open-period and expected-version checks apply. Creation and approval both verify the source; only one posted reversal per original is permitted. A failed approval leaves its draft unchanged. Cancel stale or blocked drafts before closing an execution. Review the original source in the portal before approving its reversal.

| Source | Reversal |
|---|---|
| Material Return | Remove the exact original returned quantity/value from raw-material stock and restore it to WIP |
| Conversion | Remove the original actual charge from unreleased WIP; reject insufficient WIP |
| Loss | Restore the original loss value to WIP |
| Receipt | Remove the complete original finished quantity/value from its warehouse and return that value to WIP; reduce net received output |

Material-return and finished-receipt reversal use their original source cost, not a current price or new estimate. FIFO requires the original returned/receipt layers to remain in the original warehouse. Moving Weighted Average requires enough physical and cost-pool quantity/value to remove the original amount; if removing all quantity would leave a residual value, reversal is rejected. Both methods preserve reserved stock. Resolve consumption/transfers and cost-pool discrepancies through their approved workflows before retrying; reversal never creates negative stock.

## Fiscal periods and persistence

The **correction date** must be in an open fiscal period. An original from a closed period may be reversed in a later open period while its execution is still open. Closed-period snapshots and original entries are retained; this is a current-period compensating entry, not a historical rewrite. Execution close is still final.

Physical stock, receipt compensation, cost layers/pool, cost journal, WIP, net output, version, audit and outbox share one financial transaction. Deferred PostgreSQL checks match each reversal to its original entry and, for finished goods, its exact original movement. A scoped foreign key, a unique posted-source index and immutable-source guards protect the linkage. The original `OriginalIssueId` column is reused internally on `ReceiptReversal` stock movements as the original receipt movement reference; the public operation contract uses `reversesOperationId`.

Base migration: `20261031152514_ProductionOperationReversals`; material-return reversal migration: `20261101071316_ProductionMaterialReturnReversals`. Earlier migration SQL resources remain frozen. The new migration replaces validation functions and expands constraints; rollback refuses to discard posted return-reversal evidence. Apply through the reviewed staging migration process before deploying writers/readers that require it. No live migration or posting activation is performed by this batch.

## Boundaries and next work

Material issue corrections use the existing partial/full original-cost material return workflow; an erroneous posted Return can now be reversed in full while its original evidence remains immutable. The correction date and execution must remain open. A later [input-unit batch](PRODUCTION_INPUT_UNITS.md) adds frozen alternative units while preserving stock-unit reversal quantities. Partial output correction, reversing a reversal and execution reopening remain separate work. Replacement documents must receive their own approvals after correction.

Acceptance covers FIFO and Moving Weighted Average, closed-period preservation, original charge/loss/receipt values, independent approval, insufficient WIP, duplicate reversal, reserved and consumed finished goods, rollback after physical SQL, safe retry and final stock/WIP reconciliation. Final results are in the [implementation report](../../EXPERT_IMPLEMENTATION_2026-10-31.md).
