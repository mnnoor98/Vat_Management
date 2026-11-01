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
| Receipt / By-product | Remove a reviewed partial quantity or all remaining original output from its warehouse and return original cost to WIP; finished corrections reduce net received output |

Material-return and output reversal use original source cost, not a current price or new estimate. For an output source, submit a positive `outputQuantity`; zero means all remaining original output. Multiple independently approved corrections may reference the same output until its original quantity/value is exhausted. Intermediate cost is derived proportionally at BDT scale 4 and the final correction takes the exact residual, so cumulative corrections equal the immutable receipt. FIFO requires the corrected origin layers to remain in the original warehouse. Moving Weighted Average requires enough physical and cost-pool quantity/value to remove the original amount. Both methods preserve reserved stock. Resolve consumption/transfers and cost-pool discrepancies through their approved workflows before retrying; reversal never creates negative stock.

## Fiscal periods and persistence

The **correction date** must be in an open fiscal period. An original from a closed period may be reversed in a later open period while its execution is still open. Closed-period snapshots and original entries are retained; this is a current-period compensating entry, not a historical rewrite. Execution close is still final.

Physical stock, receipt compensation, cost layers/pool, cost journal, WIP, net output, version, audit and outbox share one financial transaction. Deferred PostgreSQL checks match each reversal to its original entry and, for finished goods, its exact original movement. A scoped foreign key, a unique posted-source index and immutable-source guards protect the linkage. The original `OriginalIssueId` column is reused internally on `ReceiptReversal` stock movements as the original receipt movement reference; the public operation contract uses `reversesOperationId`.

Base migration: `20261031152514_ProductionOperationReversals`; material-return reversal: `20261101071316_ProductionMaterialReturnReversals`; partial output: `20261101074904_ProductionPartialOutputCorrections`; reviewed reopening: `20261101081403_ProductionExecutionReopening`. Earlier migration SQL resources remain frozen. These migrations replace validation functions, allow guarded cumulative output corrections and permit only sequenced `Closed → Open` reopening. Rollback refuses to discard correction or reopening evidence. Apply through the reviewed staging migration process before deploying writers/readers that require it. No live migration or posting activation is performed by this batch.

## Boundaries and next work

Material issue corrections use the existing partial/full original-cost material return workflow; an erroneous posted Return can be reversed in full while its original evidence remains immutable. Output receipts support cumulative partial correction. The correction date must be open. A closed execution requires a separate `Reopen` draft and independent approval before any correction; after WIP is reconciled it requires another approved `Close`. Original close and posting evidence remain immutable. A later [input-unit batch](PRODUCTION_INPUT_UNITS.md) adds frozen alternative units while preserving stock-unit reversal quantities. Reversing a reversal remains separate work. Replacement documents must receive their own approvals after correction.

Acceptance covers FIFO and Moving Weighted Average, closed-period preservation, original charge/loss/receipt values, independent approval, insufficient WIP, duplicate reversal, reserved and consumed finished goods, rollback after physical SQL, safe retry and final stock/WIP reconciliation. Final results are in the [implementation report](../../EXPERT_IMPLEMENTATION_2026-10-31.md).
