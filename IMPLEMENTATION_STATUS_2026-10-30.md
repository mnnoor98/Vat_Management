# Stabilization delivery — 30 October 2026

This is the first implementation batch from `PROJECT_REVIEW_2026-10-30.md`: containment, authentication, honest outcomes, test isolation and active CI. It does **not** complete the financial integration, regulatory mapping, live operations or pilot milestones.

Financial follow-up: the active financial APIs now use one coordinated transaction boundary, real reconciliation and close checkpoints. The staged financial contexts remain blocked and unregistered. See [financial execution decision and ownership map](VAt_Asper_backend/docs/architecture/0034-coordinated-financial-boundary.md). The follow-up section below supersedes the original pending financial gates.

## Implemented

- Removed the backend's embedded symmetric signing key and localhost string bypass. Every environment uses the configured HTTPS issuer and asymmetric token algorithms. Existing signed-token regression tests now pass.
- Replaced browser password accounts and pre-signed JWTs in both AuthService and the HTTP interceptor. Sign-in uses `oidc-client-ts` authorization code with PKCE. The API's `/api/v1/identity/me` validates the access token and supplies authoritative subject/company/permission grants. OIDC sessions use session storage; expired sessions require reauthentication. No synthetic refresh tokens or password grant are used.
- Added runtime provider configuration. Per the user's instruction, no provider was selected or provisioned. Local registration/password/admin mutations no longer create privileged browser accounts.
- Removed error-to-mock and error-to-success fallbacks from the API adapters. Server failure cannot mark purchase/sales Posted or a return Submitted. Live browser-only status updates and retired workflow decisions reject explicitly. Empty coefficient results stay empty.
- Live-mode demo arrays start empty; company/branch selectors are populated from granted API reads. Removed arbitrary company/branch UUID substitution. The return screen no longer claims an external filing or computes a final return from a first page of invoice data.
- Disabled extracted Purchase prepare/post/reversal, Sales final financial actions, Inventory posting/period admission and unreconciled period close through explicit 503 errors. This is containment, **not a replacement for complete financial execution**. Read/draft availability still depends on the corresponding service's schema and real reference configuration.
- Removed unsafe Compliance SQL handlers, cross-owner writes, invented opening balances and false external-filing success. Metadata listing remains available. Read users receive 403 on write endpoints; correctly authorized users receive explicit 503 until validated workflows exist. Company scope remains enforced. Added separate `compliance.manage` and `compliance.submit` policies.
- Removed privileged connection-string defaults from application settings and persistence registration. Missing/blank connection configuration fails explicitly. Removed startup migration execution from the extracted API hosts. Existing explicit owner/migrator workflows remain; this batch did not migrate or reconcile business databases.
- Added actor ownership checks to extracted command executor replay paths that previously only checked request hashes.
- All six PostgreSQL API test factories now override every application connection key with their disposable connection and assert the effective configuration before fixture migration. This prevents the missing-key fall-through discovered during review.
- Added the active CI workflow at repository-root `.github/workflows/ci.yml`, with frontend checks and the existing backend checks. Default frontend build is now production. A committed npm lockfile supports deterministic installs. CI execution on a remote repository has not been observed; existing incomplete business suites must still be resolved rather than treated as passing.

## Runtime configuration

Edit `Vat_Fontend/public/auth-config.json` (served as `/auth-config.json`):

```json
{
  "authority": "https://YOUR-ISSUER",
  "clientId": "YOUR-PUBLIC-SPA-CLIENT-ID",
  "scope": "openid profile YOUR-API-SCOPE"
}
```

Register `${frontendOrigin}/auth/callback` and `${frontendOrigin}/login` with the issuer, enable authorization code + PKCE for a public SPA, and configure its permitted browser origin. Do not add a client secret. Configure the issuer to supply the backend's required `sub`, `tenant_id`, `company_id` and `permission` grants, and an access token for the API audience.

Backend requires `Authentication__Authority`, `Authentication__Audience` and explicit `ConnectionStrings__<Owner>Database` variables. Compatibility host currently registers every owner and therefore requires all of its configured owner connections; the service host's own requirements remain visible in its persistence registration. Use separate runtime and migration credentials. The local `Run-AllBackend.ps1` launcher no longer supplies a default issuer or PostgreSQL administrator password; supply `VAT_OIDC_AUTHORITY`, `VAT_DB_USERNAME` and `VAT_DB_PASSWORD` before using the batch launcher. Per-service connection profiles remain preferable for least-privilege deployments. Do not use a superuser as the shared local runtime account.

Do not point new empty databases at existing business traffic as a migration strategy. Existing financial participants still require an ownership/transfer/reconciliation design. No database cutover, credential rotation at the database server, external filing, deployment or data deletion was performed by this delivery.

OIDC library reference: [official UserManager documentation](https://authts.github.io/oidc-client-ts/classes/UserManager.html).

## Validation

- Frontend TypeScript check passed.
- Frontend production build passed after the return-screen changes; final token-origin regression run and rebuild also passed.
- Existing frontend validator/calculation tests: 42 passed.
- New tests against the actual API service implementations and authentication interceptor: 12 passed (failed posting/submission/list/create, retired workflow mutation, local status updates, empty-result behavior, token-origin protection and absent-session behavior).
- Backend authentication and financial containment tests: 5 passed, including all three formerly failing localhost/symmetric-token regression cases. Results: `VAt_Asper_backend/TestResults/stabilization-auth/`.
- Final solution Release build passed with zero warnings and zero errors (`--no-restore -m:1 -p:UseSharedCompilation=false`).
- Disposable PostgreSQL Compliance suite: 10 passed, including six new write-permission/no-mutation/foreign-company scenarios and four existing ownership tests. Effective connection assertions passed. Results: `VAt_Asper_backend/TestResults/stabilization-compliance/`. The temporary cluster shut down and its generated directory was cleaned up.
- No claim is made that the legacy financial integration suite now passes. Its previous failures and this batch's deliberate unavailable gates are remaining work. Full-suite completion, live issuer/broker testing, schema drift checks, nonempty data transfer/restore evidence, financial reconciliation and browser UAT remain release gates.

## Next implementation gate

1. Establish a single authoritative source/stock/tax/period transaction boundary and an executable route/context/database map. Decide the controlled compatibility path versus completed remote owner protocols before re-enabling writes.
2. Replace no-op period checks and reconciliation snapshots with real pending-operation/ledger proofs. Prove concurrent close/post behavior and correction/recovery paths.
3. Restore durable Purchase/Inventory outbox writes, correct reference/tax snapshots and guaranteed reporting source coverage. Reconcile existing data before any cutover.
4. Finish real versions, pagination, warehouse selection, historical totals and owner-backed workflow decisions in the UI.
5. Complete reviewed regulatory mappings and external filing receipts, then operations/restore/load/UAT evidence for a controlled pilot.

These were the original hand-off gates. The follow-up below records subsequent implementation; disabled or staged execution must not be described as an active posting or filing capability.

## Financial follow-up — coordinated execution

Implemented the next financial boundary gate:

- Company/Period, MasterData, Purchase, Sales/Payments, Inventory, VatEngine and public Reporting now resolve coordinated services/stores against `VatDbContext`. The compatibility host no longer registers their staged contexts. Independent metadata owners remain separate.
- Purchase posting and full reversal again commit actual stock/input-tax journals, source versions, audit, durable events and command receipts in one local transaction. Sales/corrections/payments and inventory transfers use the same financial context and durable writer. No staged no-op event writer participates in active posting.
- Restored company and fiscal use cases in the coordinated application layer. Admission uses the shared company transaction fence; closing uses the matching exclusive fence. Missing/closing/closed period admission returns 409.
- Replaced coordinated placeholder close checks with pending-work detection, per-document purchase/sales reconciliation, stock/reservation/payment checks and real immutable close checkpoints containing entry IDs, totals and a SHA-256 history fingerprint. Prior closed snapshots remain unchanged by later-period corrections.
- Restored the purchase reconciliation route, cancel-close route and receipt approve route while retaining existing aliases. Receipt allocation requires its explicit allocation permission.
- Inventory queries return real product/warehouse/branch metadata with scoped joins and bounded pages.
- Updated the executable service/connection catalogue, controller route catalogue, Compose financial connection and host documentation. Seven financial hosts require the same explicitly configured `VatDatabase`; staged financial persistence must not be treated as an automatic data cutover.
- Committed posting flags default to false. No live database migration, merge, repair or posting flag activation was performed.

Validation at this point:

- Full Release solution compilation passed with zero warnings/errors.
- All twelve built API hosts passed database-free liveness, exact route ownership and authentication probes. Purchase restarted while Sales remained live. Probe processes were cleaned up.
- EF reports no pending model changes for the coordinated financial context. This check used an unreachable dummy database connection and did not apply migrations.
- Final full-suite results are recorded after completion below. Earlier focused failures identified missing route aliases and incorrect 422/409 contracts; those application contracts were corrected without weakening the tests.

Final validation coverage (latest result per project):

| Project | Passed | Skipped |
|---|---:|---:|
| Vat.Tests (financial/security/reporting regressions) | 249 | 2 live RabbitMQ cases |
| Identity | 5 | 0 |
| Workflow | 4 | 0 |
| Compliance | 10 | 0 |
| Manufacturing, corrected importer rerun | 13 | 0 |
| Integration | 14 | 0 |
| Messaging | 28 | 0 |
| Messaging.Http | 41 | 0 |
| Reporting generation core | 70 | 0 |
| **Latest per-project coverage** | **434** | **2** |

The uninterrupted solution run recorded **433 passed, 1 failed, 2 skipped**. Its one failure exposed an existing Manufacturing import path that truncated divergent targets, swallowed lock/copy errors and tolerated fingerprint mismatch. That implementation was corrected to refuse divergent or active workflow targets, propagate errors, verify the exact copied graph and roll back on mismatch. The full Manufacturing suite was then rerun: **13 passed, 0 failed**, including preservation of a divergent target and refusal of a matching metadata target with active command history. Other project results above come from the completed solution run; a second whole-solution run was not performed after this isolated importer fix. TRX evidence is retained under `VAt_Asper_backend/TestResults/coordinated-financial-full/` and `coordinated-manufacturing-fixed/`.

Both disposable clusters stopped and their generated directories were removed. The active-owner migration runner was also corrected: default invocation shows a six-owner schema plan without connecting or changing data; `-Apply` requires all six explicit migration-owner connections. It no longer runs twelve data-transfer utilities or claims a database-per-service cutover. The planner syntax and read-only invocation passed; no live schema update or data transfer was executed.

Remaining delivery gates after the backend batch (frontend progress is recorded below): Reporting generation catch-up/promotion if independent cutover is required; validated statutory mapping/external filing; reconciliation of existing deployed data; live issuer/broker, least-privilege deployment, restored backups, load and UAT evidence.


## Frontend financial workflow follow-up ? implemented 2026-10-30

Scope: Purchase/Sales invoice registers, entry payload references, posting review, and the warehouse selector. This is not a claim that every legacy report or master-data screen has been migrated.

- Preserve the server document version and use the version returned by creation when submitting the draft. Approval/submission use the version of the loaded document. Removed fixed-version retries and local status fallback from both register screens. Drafts left by a failed automatic submission have an explicit Submit draft action.
- Added bounded 25-document cursor pages with Previous/Next navigation and last-document cursors. Filters and CSV exports explicitly apply to the current page; no total-record-count claim is made. Load failures clear stale page rows and expose Retry. UI updates explicitly notify Angular's zoneless change detection.
- Preserve PostingPrepared/Reserved and subsequent correction states. Resolve original immutable calculation snapshots through purchase posting-status and sales fulfillment. Tax rates, tax amounts, units, net and gross totals come from the server snapshot, including reduced/zero rates. Unprepared register tax/total cells and exports show Pending rather than an assumed 15 percent. Original posting totals are labelled separately from later corrections.
- Replaced hardcoded posting warehouses with paged warehouse selection limited to the invoice branch. The frontend now reads the active financial warehouse list's `id` contract correctly. Preparation/reservation and posting are separate user actions. Review shows published tax rule/version, legal basis/evidence and totals. Prepared/reserved documents can be reopened without preparing twice.
- Final posting uses the prepared plan's returned `sourceVersion` and purchase plan ID. Permission checks use the backend permission names. Maker/preparer self-posting and completed-plan reposting are blocked in the UI; backend authorization remains authoritative. Failed writes retain the review and require reload instead of a blind retry.
- Entry line payloads now send the selected product's registered reference rather than its GUID. Entry calculations remain explicitly labelled draft estimates. A submitted sales draft no longer navigates directly to the statutory challan preview or claims a challan was issued. Original invoice month is derived from its business date rather than the currently selected period.

Validation:

- `npm run typecheck`: passed.
- `npm run build`: production build passed.
- `npm run test:api`: **32 passed**, including 20 financial contract/screen regressions and the existing 12 failure/auth regressions.
- `npm test`: **42 passed**. Existing statutory formula examples are regression fixtures, not validation of current legal rates.
- `git diff --check -- Vat_Fontend`: passed.
- Build/test child processes initially received sandbox EPERM; the approved local execution completed. No dependency updates, live posting, database updates or posting-flag activation were performed in this batch.

Limits and remaining work: live browser end-to-end/UAT needs the chosen OIDC provider, configured runtime authority/client ID, real permissions and deployed test data. Legacy statutory reports/printouts, inventory valuation, other register pagination, master-data placeholders and transport/compliance fields not represented in the backend contract remain separate delivery work. No current-law or statutory-filing approval is implied by the UI changes.


## Master-data and stock integrity follow-up ? implemented 2026-10-30

- Replaced the legacy master-data page with forms and tables matching persisted API fields. Products use entered reference/name/unit/tax code, suppliers use reference/name, and customers use code/name/contact/address/tax registration. Unsupported product financial fields and supplier contact/tax fields are no longer offered as if they would persist. No edit action is offered for an unsupported update workflow.
- Product creation no longer substitutes a browser-generated ID for the entered reference or assumes `STD-15`. Product reads are explicitly metadata-only; no sample HS classification, price, stock or tax treatment is inferred. Supplier reads preserve the actual entity ID and reference separately. Purchase entry sends the supplier reference expected by the backend.
- Customer creation now translates the UI fields to `CreateCustomerRequest` and maps persisted registration, active status and version from the response. Customer lists use the backend cursor/size. The master-data page paginates the currently unpaged product/supplier endpoints locally in groups of 25; this does not add backend pagination or bound those two endpoint payloads. Search/export are explicitly page-scoped.
- Stock register requests explicit offset/size pages and resets its page after changing the warehouse. Stable display IDs derive from warehouse/product keys. Quantities, reservations and branch metadata come from the active financial API. Older in-flight stock responses cannot replace the latest selection; a failed stock reload clears stale rows and exposes Retry.
- Removed inferred stock unit costs, HS codes and valuations. Unknown values are displayed/exported as Unavailable, while actual supplied zero values remain zero. The inventory tab now says Live Stock Quantities. This is a correction to presentation, not an implementation of a costing/valuation engine.
- Sales entry keeps metadata-only products selectable without requiring an invented positive catalogue price; the user enters the actual unit price. Its customer selector can load pages beyond the first 25 and excludes inactive customers. Purchase product selection no longer substitutes a sample cost/tax rate.

Validation: `npm run typecheck` and the production `npm run build` passed. `npm run test:api` passed **44 tests** (32 previous plus 12 master-data/stock/customer regressions); `npm test` passed **42 tests**. Total frontend coverage in this run: **86 passed, 0 failed**. `git diff --check -- Vat_Fontend` passed. No backend/schema changes, dependency changes, live postings or deployment were made in this batch.

Remaining scope: provider-configured live login/browser UAT; a defined accounting costing policy plus authoritative cost data for real inventory valuation; statutory reporting/printing and transport-field persistence; transfer/manufacturing legacy field assumptions; backend product/supplier pagination and customer update UI. Existing reports are not claimed to have been certified by these frontend regressions.


## Transfer workflow frontend follow-up ? implemented 2026-10-30

- Replaced the legacy transfer entry and sample statutory preview with an operational draft form and paged transfer register. The register explicitly distinguishes Draft, Submitted, Approved, Posted and Cancelled; creating a draft no longer claims that stock moved or that a statutory challan was issued.
- Inventory API mapping now matches `TransferHistoryItem` and `TransferResult`: history preserves actual state/version, entity IDs, warehouse/branch metadata, transport fields and zero-valued declared prices. Create returns the server reference. No fabricated driver, vehicle, HS code, unit, date or completed-state fallback is used.
- Entry loads warehouses in bounded pages and source warehouse stock in separate bounded pages. A product from another warehouse is never substituted. Changing source clears product selection and ignores previous in-flight stock results. No default sample GUIDs, transfer reference, driver or inferred unit cost are supplied. Declared price is labelled separately from inventory cost.
- Draft validation checks loaded warehouse membership, distinct source/destination, selected source product, date, positive finite quantity, nonnegative finite price and displayed source availability. Backend validation and posting checks remain authoritative. An unchanged failed draft retry in the same form instance reuses its idempotency key; changing its payload creates a new key.
- Submit, approve, post and cancel are explicit server actions using the reviewed document version and their exact backend permissions. Approval is blocked for the maker. Cancellation requires a reason. Posting confirmation states that it moves stock. Terminal states have no further actions. A failed command requires a register reload before another action; successful commands reload authoritative history.
- The operational register shows persisted lines, declared value, maker/approver and transport fields without fabricated signatures or receipt claims. It does not expose the former uncertified PDF/print preview as a statutory document.

Validation: production `npm run build` and `npm run typecheck` passed. `npm run test:api` passed **55 tests** (44 previous and 11 transfer regressions); `npm test` passed **42 tests**. Total frontend result: **97 passed, 0 failed**. `git diff --check -- Vat_Fontend` passed. No backend/schema changes, live stock posting or posting-flag activation were performed.

Remaining delivery work: manufacturing declaration form/command contracts and remaining sample assumptions; authoritative statutory report/print mapping; live OIDC/browser UAT, actual inventory costing policy/data and deployment gates. Transfer drafts currently support one entered line through this form; the backend supports multiple lines. Idempotency retry retention is scoped to the current form instance, not a durable offline queue.


## Manufacturing declaration frontend follow-up ? implemented 2026-10-30

- Manufacturing API interfaces now match `CoefficientCreateRequest`, `CoefficientResult` and its nested immutable `CoefficientSnapshot`. Listing uses the server cursor and explicit take=25; IDs, BOM revision, document version, state, effective interval, maker/approver and server cost values are preserved without sample fallbacks.
- Replaced the prefilled sample declaration with a company-level draft form containing actual registered product selections, batch size, optional effective-until, material quantities/wastage/declared unit prices and labor/utility/overhead costs. It supports up to 100 unique material lines. Client validation rejects finished-product reuse, duplicates, invalid dates, unregistered selections, negative/out-of-range numbers and more than six decimal places. Backend reference and monetary checks remain authoritative.
- Removed fabricated product GUIDs, HS codes, branches, prices, margins and selling prices from this workflow. Product lookup errors remain visible without a mock fallback. Form fields reflect what the backend persists; unit cost is never labelled selling price. Zero costs/wastage remain zero.
- Draft creation is separate from submission and emits the reference returned by the server. Unchanged draft retries within the same form instance reuse the command idempotency key. Changed payloads receive a new key. The parent success banner states that only a draft was saved.
- Added paged operational review and a shared read-only BOM registry. They show the saved product/material reference snapshots, reference-check timestamp, effective dates, total batch cost, unit cost, current version and state. Page export preserves zero values and marks missing historical snapshots unavailable. They do not expose the former sample PDF/print document as a certified or filed statutory declaration.
- Submit/cancel use POST, approve uses PUT, and all send the reviewed version. Actions use exact manufacturing permissions. Maker self-approval, missing-snapshot mutation, read-only view mutation and terminal-state transitions are blocked in the UI. Cancellation requires a reason. Successful transitions reload server history; failed transitions retain the original state and require reload before another attempt.

Validation: `npm run typecheck` and production `npm run build` passed. `npm run test:api` passed **68 tests** (55 previous and 13 manufacturing contract/form/register regressions); `npm test` passed **42 tests**. Total frontend coverage: **110 passed, 0 failed**. The final API summary was captured separately because the first combined output was truncated. `git diff --check -- Vat_Fontend` passed.

No backend/schema changes, dependency updates, deployed declarations, live approvals or statutory filing were performed. Live browser end-to-end/UAT still requires the configured OIDC issuer and deployed MasterData/Manufacturing reference-service connectivity. Remaining delivery work includes statutory report/print mapping, reporting completeness across pages, backend product/supplier paging, real inventory costing policy/data and deployment gates. Coefficient approval is internal authorization, not external filing.


## Register report completeness follow-up ? implemented 2026-10-30

- Replaced legacy Purchase 6.1/Sales 6.2 previews with explicitly labelled original-posting register reviews. Removed sample opening quantities/values, fallback first-line quantities, hardcoded month filenames and assumed 15-percent headings from these report routes.
- A shared report scanner traverses invoice cursors in pages of 25 and emits only after reaching the final short/empty page. Failed later pages, repeated/missing document IDs, company changes or company-mismatched rows fail the whole scan. A 100-page safety cap rejects an uncompleted scan rather than exporting a capped total. Unsubscribing cancels in-flight pagination; reloading and leaving the report dispose the prior subscription.
- Totals and export remain disabled until scanning completes successfully. Posted records missing their calculation snapshot also block the report. Changing company after load or entering invalid dates disables review/export. Date filtering uses From inclusive / Until exclusive; prepared/reserved/draft records are excluded from original-posting totals.
- Registers use actual server calculation snapshots for original posted net/VAT/gross amounts, including reduced/zero rates. Export includes every actual invoice line with its own quantity/unit, rather than only the first line or a fake aggregate quantity. Branch IDs are shown without inferred branch names. Zero values remain zero.
- The screen explicitly states that this is a live scan, not a point-in-time database snapshot, and that later reversals, credit/debit notes and payments are separate. It does not claim to be a net financial ledger or certified statutory register. The previous sample statutory PDF/print output is not offered by these two routes.
- Report hub descriptions and action labels now describe operational source reviews; they no longer claim that clicking a source view produces a certified PDF, official invoice or external filing. The tax-invoice hub link leads to sales invoice selection.

Validation: `npm run typecheck` and production `npm run build` passed. `npm run test:api` passed **81 tests** (68 previous and 13 report scan/filter/export regressions); `npm test` passed **42 tests**. Total frontend result: **123 passed, 0 failed**. `git diff --check -- Vat_Fontend` passed. No backend/schema changes, live financial writes or external filing were performed.

Remaining reporting work: correction-inclusive net ledger/summary UI with freshness and reconciliation evidence; versioned point-in-time server report generation for larger datasets; authoritative statutory mappings and validated print/export layouts. Provider-configured live browser/UAT and deployment gates remain outstanding. Existing invoice list APIs are read live during this scan, so concurrent inserts/state changes are not represented as one consistent database snapshot.


## Correction-inclusive net ledger UI ? implemented 2026-10-30

- Added `/app/reports/net-ledger`, linked from the reports hub. Purchase and Sales are selected separately with a half-open business-date range. These are correction-inclusive ledger reviews, not payment statements, inventory valuation or statutory filings.
- Purchase reads the actual reporting financial-summary/financial-entries contracts. Signed reversals retain their original transaction link and recoverable-tax amounts. Both summary reads must report a caught-up projection with zero unprocessed financial events and zero quarantined messages. Incomplete projection errors show pending/quarantine counts; no final totals/export are made available.
- Sales reads the synchronous sales summary and all sales ledger pages. It applies the date interval to entry business dates, preserving signed goods returns, price credits and debit adjustments, correction IDs and reverse/adjust links. It does not infer sales freshness from the purchase reporting broker checkpoint.
- Both flows read a summary before scanning and again afterward, reject changed summary/date metadata, and reconcile signed net/VAT totals. Purchase additionally matches transaction count and recoverable tax; Sales matches gross amount and distinct original-invoice/credit/price-credit/debit counts. Missing zero-value corrections therefore cannot pass solely because their amount effect is zero.
- Cursor loops, duplicate/missing entry IDs, company changes, scan-cap exhaustion, unsafe numeric ranges and request failures stop the report. Monetary reconciliation uses integer micro-units with safe-integer guards. Successful CSV export preserves signed values and correction links; company/ledger/date filter changes invalidate the loaded report. Component teardown unsubscribes pending work.
- Purchase lists are date-scoped at the server. The current Sales ledger endpoint has no date parameters, so its bounded cursor scan covers ledger history before client-side date filtering. The safety cap is 100 pages of 100 entries; reaching it without a final page is an error, not a partial export. Larger reports require a server-generated report capability.
- The screen explicitly states that these are separately reconciled live reads, not a versioned point-in-time database snapshot. Matching totals/counts before and after cannot prove that no equal-value concurrent changes occurred; no such guarantee is claimed. CSV filenames include company, ledger and date interval.

Validation: `npm run typecheck` and production `npm run build` passed. Final `npm run test:api` result: **94 passed, 0 failed** (81 previous and 13 net-ledger freshness/reconciliation/UI regressions). `npm test`: **42 passed, 0 failed**. Total frontend coverage: **136 passed, 0 failed**. `git diff --check -- Vat_Fontend` passed. No backend/schema modifications, financial commands, backfill/rebuild commands or live filing were executed.

Remaining gates: versioned server-side report generation and reconciliation proof for large datasets; statutory mapping/print verification; payment/receivable UI; provider-configured live end-to-end/UAT and deployment checks. Report loading requires the actual report/tax read grants and deployed Reporting/Sales API connectivity.


## Payments and customer receivables UI ? implemented 2026-10-30

- Added `/app/sales/payments`, reachable from Sales invoices and the reports hub, with typed Payment, PaymentCorrection, Receivables, Statement and Ageing API contracts. Customer and receipt registers use explicit cursor pages; receipt detail loads actual allocation/release and correction history.
- Receipt creation submits a Pending request. The backend combines approval and posting, so the UI explicitly labels that combined action rather than inventing a separate approval state or automatic posting. Makers cannot approve their own receipts. Existing requested allocations on a Pending receipt are displayed before approval because approval also posts those allocations.
- Posted receipts can be allocated to an actual outstanding invoice with the loaded receipt version and explicit allocation date. The UI checks the displayed invoice/receipt budgets and the backend revalidates authoritative credit, period, receipt state and posting policy. This form allocates one invoice per command; it can be repeated after refreshing the returned version.
- Refund and full-reversal requests are separate from their approval/posting. Refunds accept an amount/method plus selected original allocation IDs and remaining release amounts. Previous releases are subtracted when checking a selected allocation's remaining amount. Full reversal delegates the frozen remaining receipt/allocation calculation to the backend. Neither request auto-approves.
- Pending correction decisions send the correction's `sourceVersion`, not its note `version`. They require the matching loaded receipt version/state, exact refund/reversal request/approve permissions, and a reason for cancellation. Both the correction requester and original receipt maker are blocked from correction approval. Terminal states expose no inappropriate commands.
- Every financial action has an explicit review/confirmation. Current customer/company/detail scope and versions are checked again at confirmation. Failed existing-receipt commands retain the original state and require a successful receipt reload before further commands. Unchanged creation retries within the current form instance reuse their idempotency key; edited payloads get a new key. Mock mode cannot issue payment commands.
- Customer selection changes clear previous receipts, allocation/release inputs, pending confirmations and report data. Receipt detail rejects mismatched document/customer/company responses. Customer and company/date changes invalidate exports; leaving the screen unsubscribes reads. Money input is positive with up to four decimals and constrained to the client's safely representable numeric range; backend validations remain authoritative.
- Receivables show server current invoice gross/allocated/outstanding, net cash, returned and unallocated amounts. Statements use server opening/debit/credit/closing balances and signed running entries for a half-open range of at most 366 days. Ageing preserves server buckets, negative credit balances and UnknownDueDate rather than fabricating due dates.
- Statement and ageing reports show their own server capture timestamps; no joint point-in-time snapshot is claimed. Separate line/invoice and summary CSV exports preserve signed amounts, unknown dates, zero values and customer-level balances even when no invoice rows exist. Exports are disabled during commands and after filter/scope changes.

Validation: `npm run typecheck` and production `npm run build` passed. `npm run test:api`: **114 passed, 0 failed** (94 previous plus 20 payment/receivables regressions). `npm test`: **42 passed, 0 failed**. Total frontend coverage: **156 passed, 0 failed**. The final requested-allocation review template was compiled again successfully after the full test run. `git diff --check -- Vat_Fontend` passed.

No backend/schema changes, dependency changes, live receipts, allocations, refunds, reversals, posting-flag activation or deployment were performed. Full browser end-to-end/UAT still requires a configured OIDC provider, real customer/receipt data, appropriate master-data/payment/report grants and deployed Sales/Reporting connectivity. Existing posting defaults remain disabled. Remaining work includes versioned server-generated reports, authoritative statutory mapping/print validation, deployed-data reconciliation and deployment/UAT evidence; no production-readiness claim is implied by these frontend tests.
