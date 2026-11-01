# Expert implementation batch — 31 October 2026

## 2026-11-01 milestone: production material-return reversal

Material-return reversal removes the exact original returned quantity/value from FIFO layers or the Moving Weighted Average pool and restores that value to WIP. The original Return remains immutable; the correction requires an open date, open execution, source-linked Reverse document and independent approval. Consumed, transferred or value-mismatched returned stock is rejected. Migration `20261101071316_ProductionMaterialReturnReversals` extends database movement, cost-entry and source-evidence guards. Focused domain/API validation passed **4/4** across both costing methods.

## 2026-11-01 milestone: Manufacturing execution-status inbox

Added a dedicated durable RabbitMQ queue and Manufacturing-owned inbox/checkpoint/dead-letter tables for financial production execution events. Inbox evidence, monotonic order projection and `Approved → Running → Completed` status changes commit atomically before broker acknowledgement. Exact redelivery is deduplicated; stale versions cannot roll status or totals backwards; malformed, unknown-order, conflicting-ID and second-session deliveries are quarantined without changing the projection. Migration: `20261101064158_ProductionExecutionInbox`. See [execution inbox guide](VAt_Asper_backend/docs/PRODUCTION_EXECUTION_INBOX.md).

Focused PostgreSQL tests passed **2/2**. The complete Manufacturing run passed 18 cases and had one existing transfer test time out during parallel fixture cleanup; that test passed **1/1** when rerun alone. Manufacturing EF model drift reported no pending changes. The complete solution Release build passed with zero warnings/errors. Live broker and deployment validation were not performed.

## 2026-11-01 milestone: physical scrap and by-product stock

Production orders now freeze separately referenced `Scrap`/`ByProduct` products for independent approval. Financial `ByProduct` operations post exact physical quantity and reviewed WIP value into the active FIFO or Moving Weighted Average ledger and company warehouse. Frozen UOM conversions apply to these outputs. Full source-linked reversal removes the original stock layer/pool value and restores WIP; consumed or reserved stock remains protected.

The new financial migration is `20261031162622_PhysicalProductionOutputs`. Deferred PostgreSQL guards verify source scope, output product/warehouse/quantity/value, exact conversion, cost evidence, WIP conservation, finished-output totals and reversal. Older evidence remains digest-compatible because optional output definitions are omitted when null. Portal order review, output entry, independent approval review and draft copying support the new contract. See [physical output guide](VAt_Asper_backend/docs/PRODUCTION_PHYSICAL_OUTPUTS.md).

Focused FIFO/MWA physical-output tests passed **2/2**. The final affected financial/stock/period regression passed **100/100**, and the complete Manufacturing owner suite passed **17/17**. Frontend API tests passed **167/167**; typecheck and Angular production build passed. The complete backend solution Release build passed with zero warnings/errors, and both financial and Manufacturing EF model-drift checks reported no pending changes. Test clusters stopped and were removed. No live migration, deployment or posting activation was performed.

Scope: cumulative execution evidence for the agreed Trading + Manufacturing VAT Management roadmap. Existing unrelated working-tree changes were preserved. Earlier milestone findings below are retained as historical evidence and are superseded by the latest delivered contract where noted.

## Latest milestone: approved production input units

Added order-frozen, independently reviewed product/input-unit ratios for actual material issue/return and finished receipt. Quantity/ratio inputs remain exact decimal strings; integer conversion rejects stock rounding and overflow. Financial operations retain entered input quantity/unit alongside normalized stock quantity. Approval recalculates conversions and PostgreSQL guards independently enforce their agreement with frozen source evidence. Valuation, planned output caps, returns and reversals continue to use stock quantities and original cost.

The portal supports ratio entry/review, frozen-only actual-unit choices and side-by-side entered/stock quantities. Draft copies preserve input values to avoid double conversion. Legacy stock-only source JSON omits the new null property, preserving existing source digests; existing registered stock-unit labels remain supported. No global conversion override, coefficient conversion or product-master UOM change is introduced. [Contract and rollout](VAt_Asper_backend/docs/PRODUCTION_INPUT_UNITS.md).

Frontend **166/166** tests, typecheck and final Angular release build passed. Final full solution Release build passed with zero warnings/errors; both financial and Manufacturing model/snapshot checks passed. Focused financial/unit/source/reversal tests passed **22/22**, and the complete Manufacturing owner suite passed **16/16**. The final affected financial/stock/period regression suite passed **97/97**, with no failures or skips (`TestResults/production-uom-regression`); the focused 22 cases overlap this suite. The final ratio-review display change was also verified by the 166-test frontend suite and release build. No live migration, deployment or posting activation was performed.

The acceptance theory runs both FIFO and Moving Weighted Average with and without alternative units. PACK/BOX material issues, G material return and PAIR partial output normalize to the same stock quantities and values as the original stock-only dataset. Converted receipt reversal removes the complete original stock quantity and restores its WIP value before replacement receipt. Invalid aliases, fractional-stock rounding and direct SQL input/stock mismatch are rejected. Stock value remains 2,290, finished value 1,300 and final WIP zero; retry/rollback, original-cost returns and period checkpoints remain covered. Unit tests include micro-unit exactness, overflow, duplicate/identity overrides, legacy JSON omission and older registered stock labels.

## Previous milestone: immutable production reversals

Added evidence-backed full reversals for posted conversion charges, WIP losses and finished receipts in open executions. Each reversal references its immutable original operation and derives quantity/value server-side, requires a new open-period date and independent approval, and is protected against duplicate reversal. Receipt compensation preserves FIFO source layers / Moving Weighted Average pool coverage and reserved stock, restores original cost to WIP and reduces net output in the existing financial transaction.

The portal loads the original source for review before enabling reversal approval. Failed or mismatched reads clear financial decision state. Closed-period snapshots and original journals remain unchanged; no execution reopening or partial correction is introduced. Material issue correction continues to use original-cost material return. [Contract and limits](VAt_Asper_backend/docs/PRODUCTION_REVERSALS.md), [next plan](NEXT_IMPLEMENTATION_PLAN_2026-10-31.md).

Focused verification passed for both costing methods, covering conversion/loss/receipt reversal, closed-period source correction in a later open period, insufficient WIP, independent approval, invalid source/amount, duplicate denial, reserved and consumed stock, injected-save rollback, stable-key retry, immutable originals and final quantity/value reconciliation. Frontend **163/163** tests, typecheck and Angular release build passed; EF model/snapshot consistency passed. Full solution Release build passed with zero warnings/errors. Final affected financial/stock/period regression suite passed **89/89**, with no failures or skips (`TestResults/production-reversal-regression`). The two focused reversal cases overlap this suite. Initial validation-status test expectations were corrected from HTTP 400 to the project's HTTP 422; final runs include that correction. No live migration, deployment or posting activation was performed.

## Previous milestone: production order and actual WIP execution

Added immutable approved production order evidence in Manufacturing and a separate financial execution owner. Actual FIFO/Moving Weighted Average material issue/return, approved conversion costs, partial finished receipts, WIP losses and zero-WIP close share atomic financial stock/cost/WIP/audit/outbox posting. Drafts require independent approval and current execution versions; stale drafts can be cancelled. Period snapshots include production movements and WIP checkpoints.

Portal route: `/app/manufacturing/execution`. Source estimates are excluded from actual costing. Trusted authenticated reference clients validate source and warehouse scope; there is no direct cross-owner database access. The import receipt and financial outbox are durable; no reverse owner-status event consumer is implemented.

Verification: full solution Release build passed with zero warnings/errors; affected financial/stock/period regressions passed **87/87**; final Manufacturing owner suite passed **15/15**; frontend API suite passed **160/160** and Angular release build passed. Both EF model/snapshot drift checks passed. After adding exact evidence-payload SHA-256 comparison, the affected backend project rebuilt with zero warnings/errors and the final production/source-integrity subset passed **12/12**, including both costing methods and digest tampering. This subset overlaps the 87-test run; it is not 12 additional distinct tests. No live migration, deployment or posting activation was performed. [Production/WIP contract and limits](VAt_Asper_backend/docs/PRODUCTION_WIP_EXECUTION.md).

The production acceptance theory passed for FIFO and Moving Weighted Average: 2,200 material receipt value + 150 actual conversion − 60 approved loss = 2,290 closing stock, including 1,300 finished stock and zero final WIP. It also checked two raw materials, separate output warehouse, partial receipt, original issue return, independent approval, stale-draft cancellation, injected-save rollback, same-key retry, immutable movements, scoped permissions and period-close checkpoints. Delegated HTTP tests covered untrusted origins, owner denial, redirects, scope mismatch and malformed source evidence.

## Previous milestone: persisted stock valuation

Implemented an approved opening cutover and persisted FIFO / Moving Weighted Average ledger for the coordinated financial owner's current stock flows. The selected full receipt basis is **net purchase + non-recoverable VAT + independently approved allocated landed cost**. The UI defaults to this basis; missing allocation, including an unreviewed zero-landed-cost assumption, blocks receipt posting.

- Opening adoption requires complete nonzero warehouse/product quantities and reviewed values, effective policies, an open period and independent approval. Activity fingerprints reject stale preparation, and active ledgers cannot be reset. Empty companies can explicitly adopt an empty opening.
- Landed reviews allocate a declared BDT total across every frozen prepared-receipt line. Independent approval freezes the allocation; only one approved allocation may exist per plan. The UI provides source references and line review rather than requiring raw JSON.
- Receipt/issue cost positions, FIFO layers / weighted-average pools and original issue allocations persist. Partial/full goods returns restore original issue cost; transfer legs preserve company value; whole purchase reversals remove original receipt value subject to remaining original-origin/pool coverage. Sales price-only notes do not change inventory cost.
- All cost changes share physical posting's idempotency, stock/tax/audit/outbox transaction and period/company fencing. Deferred database guards protect immutable evidence, source linkage, approved methods, allocation totals, balanced transfers, dense sequence clocks and physical/journal/position/layer conservation. Active source movements cannot commit without cost coverage.
- Stock balance and summary values are promoted only for an approved reconciled ledger in a Repeatable Read snapshot. Unadopted nonzero stock remains unavailable. Summary covers every position, uses actual top-valued items, and reports mixed policies without letting the old method selector override cost state.
- Added exact quantity/value strings and integer page totals for browser/CSV precision; numeric compatibility fields remain. UI: `/app/inventory/valuation`, linked from stock balances. Review/source/journal pages enforce scope, independent approval, failed-read clearing and stable draft retry keys.
- Financial migrations: `20261031135250_ActiveStockCostLedger` and `20261031135906_FullReceiptCostBasis`; database guards are an embedded migration resource. No live business database migration, automatic opening activation, posting-flag change or deployment occurred.

Guide: [Posted stock valuation ledger](VAt_Asper_backend/docs/STOCK_VALUATION_LEDGER.md). Decision: [0037](VAt_Asper_backend/docs/architecture/0037-approved-stock-cost-ledger.md). Next work: [Implementation plan](NEXT_IMPLEMENTATION_PLAN_2026-10-31.md).

Verification: initial **8 focused cases passed**; expanded affected PostgreSQL suite **96 passed, 0 failed** (`VAt_Asper_backend/TestResults/full-stock-regressions`, `tmp/full-stock-regressions.log`). After exact-value fields, additional database validation and concurrent/multiline/mixed-policy assertions, the final targeted run **33 passed, 0 failed** (`VAt_Asper_backend/TestResults/full-stock-final`, `tmp/full-stock-final-tests.log`). Counts overlap and must not be added. Tests include full-basis FIFO/Moving Average, reserved stock, partial/final cost returns, transfer out/back, supplier reversal, injected rollback with same-key recovery, immutable evidence/projection/clock writes, stale/incomplete openings, source preparation, exact summary values, mixed product policies and concurrent receipts. All disposable clusters stopped and were removed. No full-backend-suite rerun is claimed.

Frontend typecheck passed; final API regression suite **153 passed, 0 failed** (`tmp/full-stock-frontend-final-tests.log`), including exact arithmetic beyond JavaScript number precision and failed/foreign coherent-review reloads. Final production frontend build passed (`tmp/full-stock-frontend-final-build.log`). The final affected backend build and final complete-solution Release build passed with **0 warnings/errors** (`tmp/full-stock-final-build.log`, `tmp/full-stock-final-solution-build.log`). Migration model drift check passed (`tmp/full-stock-model-drift.log`).

Remaining limits: opening is one reviewed cutover layer per position, not historical FIFO reconstruction; pre-adoption issue-cost returns, late landed-cost true-ups, nonzero method conversion and historical-as-of/closed cost snapshots need separate reviewed workflows. Manufacturing currently has coefficient approval and order reads, not production issue/receipt posting. WIP/conversion/scrap, Import/Export currency/tax/evidence and subcontract flows are explicitly next-plan work. Live browser UAT, business opening sign-off, production identity/broker/migration/restore checks and high-volume company-clock load evidence remain release gates. This is not a claim that the whole ERP/VAT roadmap is complete.

## Earlier corrective batch

## Changes

- Manufacturing coefficient register now uses saved API snapshots. Removed fabricated declaration fallback on empty/failed reads, local approval success after rejected commands, invented cost/profit figures, and the unvalidated statutory print/PDF presentation. Replaced that presentation with a saved-snapshot review showing maker, approver, version, effective dates, material quantities/costs and server batch/unit costs.
- Commands retain the reviewed version and maker/checker checks. Failed decisions preserve source state and require a successful reload before another command. Empty results remain empty; failed loads clear stale declarations and expose retry.
- Restored cursor Previous/Next navigation. CSV exports current-page server snapshot values, preserves real zero costs and marks missing snapshots Unavailable.
- Inventory screen/CSV no longer replaces missing valuation with zero or missing HS code with a sample classification. A page with any unknown valuation has an unavailable total. Real zero costs remain zero.
- Updated the stock request test for the supported default costing-method parameter. Added a Manufacturing empty/failed-load regression without weakening approval assertions.
- Backend baseline exposed warehouse/all-balances public page-size regression. Restored the public 1–100 limit in the controller before query execution; internal valuation-summary batching remains unchanged.
- Six HTTP inbox/outbox integration cases also attempted to bind the copied application's fixed port 5080: `UseUrls` did not override the named Kestrel endpoint. Their test configurations now override that endpoint with loopback port 0. Existing application processes were left running.
- Identity ownership test now expects the already-implemented tenth entity and explicitly verifies `LocalAccount` maps to `identity.local_accounts`; schema isolation/migration checks remain intact.
- Compliance baseline revealed six safety regressions: fake success, missing correction routes and unimplemented filing execution. Removed sample financial responses and local filing success; restored permission/company-scoped 503 gates for unimplemented calculation and write routes, including credit/debit notes. Saved metadata listing remains available. Added four read-path regression cases to prevent fabricated financial responses.

## Active runtime ownership map

Source registrations in `src/Vat.Hosting/VatBackend.cs`, `src/Vat.Api/Program.cs` and individual service `Program.cs` files establish this composition:

| Runtime area | Active persistence / registration |
|---|---|
| Company/Periods, MasterData, Purchase, Sales/Payments, Inventory, VatEngine, public Reporting | Coordinated `AddVatBackend` / `VatDbContext` / `ConnectionStrings:VatDatabase` |
| Identity | `AddIdentityPersistence` / independent owner |
| Manufacturing | `AddManufacturingPersistence` / independent owner |
| Compliance | `AddCompliancePersistence` / independent owner; filing completeness is a separate gate |
| Workflow | `AddWorkflowPersistence` / independent owner |
| Integration | `AddIntegrationPersistence` / independent owner |
| Compatibility API | Coordinated financial bridge plus five independent registrations |
| FinancialOutbox host | Financial bridge; worker behavior must be checked against runtime flags and host registration |

This is a source composition map, not evidence that deployed connection strings, grants or existing data are reconciled. Staged service contexts are not automatically active owners.

## Verification

- Frontend typecheck passed.
- Frontend API suite: **132 passed, 0 failed**, including the seven previously failing tests and the new Manufacturing load-failure regression. Evidence: `tmp/vat-corrective-api-2026-10-31.log`.
- Production Angular build passed. An unused pipe warning from the first build was removed and the final build completed without that warning.
- Baseline backend full-solution integration run used `scripts/Test-Local.ps1` and pre-change Release binaries: **421 passed, 15 failed, 2 skipped** across 438 cases. The failures were two pagination cases, six HTTP test-port conflicts, one outdated Identity entity-count assertion and six Compliance safety cases. Results: `VAt_Asper_backend/TestResults/expert-baseline-2026-10-31/`; log: `tmp/vat-backend-tests-2026-10-31.log`.
- That disposable cluster shut down and its generated directory was removed. No live business database was used.
- Post-change full solution Release build passed with **0 warnings, 0 errors**. Log: `tmp/vat-corrective-build-2026-10-31.log`.
- Post-change affected-suite rerun: **42 passed, 0 failed** (23 main financial/HTTP/containment cases, 5 Identity, 14 Compliance including four new no-sample read cases). Results: `VAt_Asper_backend/TestResults/expert-corrective-2026-10-31/`; log: `tmp/vat-corrective-backend-tests-2026-10-31.log`. All 15 baseline failing cases are covered by this rerun. Projects outside the filter correctly reported no matching tests; they were not counted as newly passing cases. A second full-solution test run has not been performed.
- The corrective-run disposable PostgreSQL cluster also stopped and its generated directory was removed. Live broker cases remain unverified; the baseline skipped two such cases.

## Remaining findings

`FinancialInventoryQueries.cs` still contains hardcoded HS/standard-cost fallbacks, uses non-cancelled purchase documents rather than a complete immutable valuation journal, and its summary is capped at 1000 rows. The frontend fixes above cannot establish authoritative backend valuation. Do not treat this estimate as reconciled inventory accounting. Replacing it requires reviewed valuation rules, posted receipt/correction/transfer coverage and a completeness contract; it is the next inventory work item.

The paragraph above describes the first-batch handoff; the availability follow-up below supersedes its query/fallback findings. Authoritative costing itself remains pending.

Live browser UAT, production identity/broker checks, reviewed statutory mapping, opening-data reconciliation and restore/deployment evidence remain pending. This batch does not complete the whole roadmap or certify production use.

## Stock valuation availability follow-up

User confirmed both FIFO and Moving Weighted Average with company/product selection, in addition to Import/Export and subcontract scope. See [decision 0035](VAt_Asper_backend/docs/architecture/0035-stock-valuation-availability.md) for the current contract and ordered cost-journal implementation.

- Removed hardcoded HS/standard-cost dictionaries and all draft/purchase-price-based valuation inference from the active financial inventory query.
- Nonzero stock retains on-hand/reserved/available quantities with null valuation and explicit `Unavailable` / `cost_journal_unavailable` metadata. Zero on-hand stock has zero value and `NoStock`; it does not claim a computed unit cost. Tax code remains available without inventing an HS classification.
- Unknown costing methods receive 422; recognized methods remain requested methods until persisted company/product policies and a reconciled cost journal are implemented.
- Summary uses scoped PostgreSQL aggregates over every balance position rather than the first 1000 rows. A Repeatable Read transaction keeps warehouse and product aggregates consistent. Summary quantities include reserved stock; no invented top-valued ranking is returned.
- Frontend/API models accept nulls. Screen totals explicitly describe the current page; loaded warehouse count no longer invents a warehouse. Export includes valuation status/reason and is disabled during failed/loading reads.
- Added a 1001-position PostgreSQL regression covering summary completeness, reserved/on-hand distinction, zero/unknown costs, missing HS, both requested methods, tenant/company/permission boundaries and invalid method rejection. Frontend regressions cover null metadata, CSV and export guards.

Validation: frontend typecheck and production build passed; **133 frontend API tests passed**. Full solution Release build passed before final test adjustments; final affected-project rebuild passed with 0 warnings/errors after the query ordering and test adjustments. The initial database-test attempt stopped at an xUnit analyzer error before running cases; its assertion style was corrected. Final PostgreSQL run: **8 passed, 0 failed**, including the 1001-position summary regression, inventory tenant isolation and public pagination/authorization checks. Results: `VAt_Asper_backend/TestResults/valuation-availability-2026-10-31/`; log: `tmp/vat-valuation-backend-tests-final-2026-10-31.log`. The generated cluster shut down and was removed. No second full backend test run, schema changes, business-data migration, posting activation or deployment occurred. Clients expecting numeric valuation fields must be updated for the nullable contract.

## Approved costing policy and purchase receipt foundation

- Added scoped, effective-dated company-default and product-override policies for FIFO and Moving Weighted Average. Independent approval freezes version 2; same-scope approved date ranges cannot overlap. Existing captured receipt history cannot be retroactively reassigned.
- Added `costing.read`, `costing.manage`, `costing.approve`, permission/company-checked APIs and `/app/inventory/costing-policies`. Product review requires loaded registered-product details. Failed approval requires reload; source responses must match the reviewed scope/version. Failed draft retries retain their typed command key until the payload changes.
- Original Purchase posting now appends immutable `purchase_cost_receipts` in the existing source/stock/tax/audit/outbox/idempotency transaction. The receipt preserves original quantities and net/tax/recoverable-tax components, source movement/plan IDs and the selected approved policy/version/method. Missing policies stay explicitly unassigned. Shared/exclusive company fences serialize capture against policy approval.
- Added financial migration `20261031092959_CostingPolicyReceiptEvidence`: two owner tables, constraints, scoped FKs, source uniqueness and database guards against approved-policy/evidence mutation and history reassignment. No live database was migrated. Apply the reviewed financial-owner migration before deploying receipt-capture code.
- Corrected local-development module filtering so costing grants follow Inventory rather than the unknown-prefix Admin fallback.
- Final valuation remains unavailable: original purchase evidence alone does not establish FIFO issue layers, moving-average issue costs, corrections, transfer/production/import/subcontract coverage or opening stock adoption.

Guide, API contracts and rollout limits: [Costing policies and receipts](VAt_Asper_backend/docs/COSTING_POLICIES_AND_RECEIPTS.md).

### Ordered source costing continuation

The receipt foundation is extended by [decision 0036](VAt_Asper_backend/docs/architecture/0036-ordered-source-cost-preview.md). Original purchase and original sales postings now capture an immutable supported-source sequence under an exclusive company fence. Clock, events and stock/tax/audit/outbox/idempotency evidence commit or roll back together. Database guards validate source links, freeze events and opening proof, and reject clock gaps or events beyond the head. Approved policy history protection now includes ordered sales sources.

Added an exact FIFO/Moving Weighted Average replay engine and a separate `PurchaseNetOnly` API/UI preview. The preview checks empty opening proof, source coverage, method/policy assignment, supported movements, BDT currency and closing physical quantity within a Repeatable Read snapshot. It refuses positions above 10,000 source events and returns no partial amounts. Decimal values and source sequence are strings; the UI does no floating-point recalculation. Stock balance rows link to the preview, which clears old results on failed or mismatched reads.

Migration `20261031105204_OrderedCostSources` adds two financial-owner tables plus database guards. No live database migration or deployment occurred. Source replay remains a derived purchase-net preview: opening adoption, return/reversal/transfer allocations, policy conversion, landed and production costs, Import/Export/subcontract propagation and authoritative valuation promotion remain pending. Standard stock valuation remains unavailable for nonzero stock.

Verification: **57 backend cases passed** in the first disposable PostgreSQL run (`VAt_Asper_backend/TestResults/cost-replay-preview`, `tmp/cost-replay-preview-tests.log`). After the final source-mismatch check and additional opening-adoption/source-clock rollback assertions, the complete solution Release rebuild passed with **0 warnings/errors** (`tmp/cost-preview-final-solution-build.log`), and **21 final affected cases passed** (`VAt_Asper_backend/TestResults/cost-preview-final`, `tmp/cost-preview-final-tests.log`). These rerun counts overlap and must not be added. Both generated clusters stopped and were removed. No whole-backend-suite rerun is claimed.

Frontend typecheck and **144 API regressions passed** (`tmp/cost-preview-typecheck.log`, `tmp/cost-preview-frontend-tests.log`). Final production build passed after guidance/label updates (`tmp/cost-preview-frontend-final-build.log`). Migration model drift check passed (`tmp/cost-preview-model-drift.log`). Browser UAT, high-volume company-clock load testing and production migration/identity/broker validation remain pending.

### Earlier policy-foundation verification

Verification: **46 PostgreSQL integration cases passed**, covering new policy/receipt behavior plus original Purchase/Sales posting, period/reversal, transfer and stock isolation/summary regressions. New concurrency cases covered overlapping approval and approval-versus-capture boundaries. Receipt replay, immutable update/delete attempts and rollback recovery passed. Results: `VAt_Asper_backend/TestResults/costing-policy-journal/`; log: `tmp/costing-policy-journal-tests.log`. The private cluster stopped and was removed. Migration model drift check passed using a nonconnecting design-only connection. Final frontend typecheck, production build and **140 API regressions passed**. Final complete solution Release build passed with **0 warnings/errors** (`tmp/costing-solution-final-build.log`); **3 pure domain/module-permission cases passed** on that build (`TestResults/costing-domain-permissions/`). The integration run preceded the final required-query binding annotations and local module filter adjustment; those final changes compiled and the permission adjustment passed the pure checks. No full-backend-suite or live browser/UAT claim is made.
