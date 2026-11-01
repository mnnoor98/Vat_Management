# Costing policies and immutable purchase receipt evidence

Later milestone: [Posted stock valuation ledger](STOCK_VALUATION_LEDGER.md) now provides approved opening adoption, persisted full-basis receipt/issue/return/transfer/reversal costing and reconciled stock values. The limitations below describe the earlier policy/preview milestone; use the newer guide for the active valuation contract and remaining production/import/subcontract scope.

Implemented 31 October 2026. Approved company/product policies and atomic original purchase receipts are followed by an ordered source journal and a purchase-net-only FIFO/Moving Weighted Average preview. This does not yet provide final inventory valuation, opening adoption or transfer/return/production cost propagation.

## Policy workflow and scope

Policies cover `[effectiveFrom, effectiveUntil)` and are Draft version 1 until independently Approved at version 2. Methods are `FIFO` and `WEIGHTED_AVERAGE`. `productId: null` means company default. A covering approved product policy overrides the company default; drafts never participate. Only one approved range may cover a date for each exact company/default or product scope. Adjacent ranges are allowed.

Source fields cannot be edited during approval, approved policies cannot be changed, and policy history cannot be deleted/truncated. Creating an approved policy directly is rejected by the database. A new non-overlapping range is required for future policy changes. Mid-range retirement and cost-policy conversion remain separate work.

Approval requires an open fiscal period covering the effective start, an expected version, a different actor, and no already captured purchase receipt or ordered sales source in the proposed scope/date range. This prevents retroactively assigning even previously unassigned source history. Initial adoption over existing stock requires a future reviewed opening/reconciliation milestone; approval does not backfill legacy data.

Dedicated permissions: `costing.read`, `costing.manage`, `costing.approve`. Grant them explicitly through the configured issuer. Local-development module filtering maps costing to Inventory. No general inventory-read grant authorizes policy mutation or cost-evidence access.

## APIs

All routes start with `/api/v1/companies/{companyId}/inventory` and enforce bearer authentication plus tenant/company grants.

| Method | Route | Permission | Contract |
|---|---|---|---|
| POST | `/costing-policies` | costing.manage | `{ productId: null or registered UUID, method, effectiveFrom, effectiveUntil }`; returns 201 draft and Location |
| POST | `/costing-policies/{id}/approve` | costing.approve | `{ expectedVersion: 1 }`; returns 200 approved policy |
| GET | `/costing-policies` | costing.read | `take` 1–100, optional `before` last UUID; descending ID cursor |
| GET | `/costing-policies/{id}` | costing.read | One scoped policy; absent/foreign tenant returns 404 |
| GET | `/costing-policies/resolve` | costing.read | Required registered `productId` and `businessDate`; absent effective policy returns 404 `cost_policy_unassigned` |
| GET | `/purchase-cost-receipts` | costing.read | `take` 1–100, optional `before`; original purchase receipt evidence |

Both POSTs use the existing command executor and require `Idempotency-Key` as a non-empty UUID. Same-actor/same-payload replay returns the saved response and `Idempotency-Replayed: true`; actor/payload conflicts are rejected. Draft/approval audit and idempotency receipt commit atomically. Main errors include 403 self-approval/company/permission denial, 404 missing reference, 409 stale version/overlap/frozen history and 422 invalid method/range/page.

UI: `/app/inventory/costing-policies`, linked from stock summary for users with costing.read. It supports a registered product or company default, method/date entry, cursor pages and reviewed approval. Failed decisions require reload and do not change local state. Unchanged failed-create retries reuse the same command key; changed payloads use another key. Company changes clear scope-bound data and subscriptions. Browser UAT remains required.

Product-specific UI creation/review also requires `masterdata.read` and loaded catalogue details; missing catalogue evidence disables product approval. A company-default policy can still be reviewed without product catalogue access. This screen reuses the existing bounded product catalogue; it does not claim a complete catalogue beyond that endpoint's supported scope.

## Receipt transaction and evidence

Original purchase receipt and original sales issue posting take the exclusive company costing fence before stock rows, after the period fence. They append ordered source events in the same transaction as their stock movement. Policy approval uses the same exclusive fence. Posting-first races preserve the original policy assignment and block retroactive approval; approval-first races capture the newly approved policy.

Each receipt contains tenant/company, purchase/line/plan and stock movement IDs, warehouse/product, business date, quantity, source net/tax/recoverable-tax amounts, currency, creation time, and optional approved policy ID/version/method. No policy produces explicit null policy fields; it neither guesses a default nor blocks the existing non-costing purchase flow. Source amounts are preserved separately rather than assuming a reviewed landed-cost formula.

Unique source-movement keys prevent duplicates. Database guards enforce original movement identity/quantity/date, effective policy choice and frozen policy version/method; update/delete/truncate are rejected. The source posting, stock balance, stock/tax journals, cost receipt, audit, events and idempotency record share the same coordinated transaction. An error rolls them back together.

Purchase receipt evidence preserves original purchases. The additional ordered source journal covers original purchases and original sales issues; it is a supported subset of stock activity. Reversals, returns and transfers explicitly block the scoped preview. Manufacturing/import/subcontract cost coverage remains pending. Stock/summary valuation therefore remains unavailable as established by decision 0035.

## Migration and rollout

Migration: `20261031092959_CostingPolicyReceiptEvidence` in the coordinated financial owner. It creates two tables, constraints, indexes and database evidence guards. No live database was migrated by this development batch. Apply through the existing reviewed financial migration-owner process before deploying code that captures receipts. All coordinated financial hosts use that reviewed shared database; do not create per-service empty replacements.

No automatic policy activation, historical source rewrite, opening-cost import, posting-flag change or production deployment is included. Existing schema readiness will report a pending migration until the operator applies it. Runtime credentials require the new owner-table privileges; migration credentials remain separate.

Next: approved opening/checkpoint adoption, immutable cost layers and issue allocations for both methods, correction/transfer/production/Import/Export/subcontract coverage, end-to-end value reconciliation and validated valuation read promotion.

## Ordered source preview

`GET /api/v1/companies/{companyId}/inventory/cost-preview?warehouseId={UUID}&productId={UUID}` requires `costing.read` and scoped warehouse/product references. `/app/inventory/cost-preview` is linked from each stock balance position. Methods come from the approved policies frozen on sources; there is no client-side method override.

`inventory.cost_source_heads` holds a company sequence and immutable opening proof. The first supported posting proves the company has no prior nonzero balances, stock movements, quantity sales entries or transfer journals. Existing history, including receipts captured before the ordered journal migration, conservatively requires reviewed opening adoption. No historical order is invented from timestamps. Within a posting, lines use product/source-line ID order. The sequence is posting order, not business-date order, and covers only supported original receipt/issue sources.

`inventory.cost_source_events` links each receipt or original sales issue with quantity, position and frozen policy identity/version/method. Company fencing orders concurrent supported postings. Failed transactions roll back source events and sequence advancement with stock, tax, audit and idempotency evidence. Database triggers reject event mutation/truncation, source mismatches, non-forward clocks, clock gaps and events beyond the committed head. Policy approval cannot reassign captured sales-source history either.

The reader uses a Repeatable Read snapshot. It refuses calculations for unproven openings, unsupported quantity movements, coverage gaps, missing policies, changed methods, unsupported currency, mismatched evidence or closing quantity that differs from physical on hand. It reads at most 10,001 events and refuses positions above the 10,000-source limit rather than returning a partial calculation. `sourceCount` is the bounded number of sources inspected; early rejection can report zero, and limit rejection reports 10,001 rather than claiming a full position count. Error responses carry `state: Unavailable`, a reason and `calculation: null`.

Successful responses carry `state: Preview`, `basis: PurchaseNetOnly`, a supported company sequence, source count and capture time. Receipt value uses the original net amount only: tax, landed charges and production costs are excluded. FIFO consumes actual receipt layers; Moving Weighted Average updates the pool after each receipt. Integer micro-quantity and ten-thousandth-money arithmetic avoids floating point drift and intermediate overflow. Partial allocations round half away from zero; complete depletion takes the exact remaining value. Each replay conserves received value = issued value + closing value. The implementation has no historical-as-of contract.

Quantities (6 places), BDT amounts (4 places) and sequence are JSON strings to preserve exact values in JavaScript. The UI renders these strings without recalculation and labels the first 100 displayed issue allocations when there are more. An unavailable or failed read clears previous numbers. Calculation outputs are derived read-only previews; cost layers/issue allocations are not yet persisted as an authoritative posting ledger.

Migration `20261031105204_OrderedCostSources` adds the two ordered-source tables and guards. Apply both reviewed financial-owner migrations before deploying source-capture code. This development work has not migrated a live business database or deployed the application.
