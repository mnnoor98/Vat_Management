# VAT Management — Trading ও Manufacturing বাস্তবায়ন পরিকল্পনা

তারিখ: 31 October 2026, Asia/Dhaka.

Latest UOM delivery: actual issue/return/finished-receipt alternative unit ratios approved order-এ freeze হয়; exact conversion ছাড়া posting হয় না। Stock/BOM/planned quantity registered stock UOM-এ থাকে। [Input UOM guide](VAt_Asper_backend/docs/PRODUCTION_INPUT_UNITS.md)-এ contract ও scope দেখুন।

Latest correction delivery: open production execution-এ Conversion/Loss/Finished Receipt-এর full source-linked reversal যুক্ত হয়েছে। নতুন open-period compensating entry দিয়ে correction হয়; original ও closed-period snapshot বদলায় না। [Reversal guide](VAt_Asper_backend/docs/PRODUCTION_REVERSALS.md)-এ scope, migration ও সীমা আছে।

Latest delivery: approved production orders থেকে actual material issue/return, WIP, conversion cost, partial finished receipt এবং zero-WIP close-এর API/UI যুক্ত হয়েছে। [Production/WIP guide](VAt_Asper_backend/docs/PRODUCTION_WIP_EXECUTION.md) এবং [পরবর্তী plan](NEXT_IMPLEMENTATION_PLAN_2026-10-31.md)-এ delivered scope ও অবশিষ্ট কাজ দেখুন। পূর্ণ Import/Export/subcontract বা statutory production compliance এখনও সম্পন্ন নয়।

## নিশ্চিত scope

ব্যবহারকারীর সিদ্ধান্ত: শুধু VAT Management; Trading এবং Manufacturing দুটোই। HR, Payroll এবং পূর্ণ Accounting ERP অন্তর্ভুক্ত নয়। নিচের roadmap একটি প্রস্তাবিত implementation backlog; প্রতিটি capability সম্পন্ন হয়েছে এমন দাবি নয়।

পরবর্তী নিশ্চিত সিদ্ধান্ত: **Import/Export এবং subcontract production দুটোই প্রয়োজন।** Import declaration/supporting documents, import tax evidence, export shipment/evidence, subcontract material dispatch/return/consumption ও wastage reconciliation scope-এর অংশ। এই workflow-গুলোর legal treatment ও detailed contract এখনও review করতে হবে।

Stock costing-এর নিশ্চিত সিদ্ধান্ত: **FIFO এবং Moving Weighted Average দুটোই থাকবে; company/product অনুযায়ী নির্বাচন করা যাবে।** Effective-dated approved policy, frozen policy/version ও immutable cost journal প্রয়োজন। শুধু screen-এর method selector দিয়ে company/product policy পরিবর্তন হবে না। [Valuation boundary ও next implementation design](VAt_Asper_backend/docs/architecture/0035-stock-valuation-availability.md)।

Costing follow-up: approved opening adoption, persisted FIFO/Moving Weighted Average cost ledger, full-basis receipt cost, partial/full goods return, whole purchase reversal এবং warehouse transfer costing যুক্ত হয়েছে। নির্বাচিত basis: **net purchase + non-recoverable VAT + allocated landed cost**। Reconciled ledger থেকেই stock balance/summary value প্রকাশ পায়। [Stock valuation guide](VAt_Asper_backend/docs/STOCK_VALUATION_LEDGER.md)। Manufacturing production posting, Import/Export/subcontract workflows, late-cost correction ও historical valuation এখনও আলাদা backlog।

পরবর্তী execution order: controlled valuation pilot → Manufacturing material issue/WIP/finished-goods receipt → Import/Export → subcontract production → advanced cost correction/period snapshots → release hardening। বিস্তারিত acceptance criteria ও dependencies: [পরবর্তী implementation plan](NEXT_IMPLEMENTATION_PLAN_2026-10-31.md)।

## বর্তমান baseline

- Angular frontend এবং ASP.NET Core/EF Core/PostgreSQL backend বিদ্যমান।
- Active financial participants একটি coordinated VatDbContext ব্যবহার করে। Identity, Manufacturing, Compliance, Workflow এবং Integration-এর পৃথক persistence আছে। API process count দিয়ে database independence বিচার করা যাবে না।
- Purchase/Sales posting, corrections, stock, payments, tax journals ও period close-এর existing implementation আগে যাচাই ও পুনর্ব্যবহার করতে হবে।
- Compliance write/filing এবং independent reporting cutover সম্পূর্ণ বলে ধরা যাবে না।
- Working tree-তে পূর্ববর্তী পরিবর্তন আছে; এই পরিকল্পনা সেগুলো প্রতিস্থাপন করে না। পুরোনো review findings পরবর্তী implementation notes ও বর্তমান source মিলিয়ে যাচাই করতে হবে।

## বাস্তবায়নের আগে নিশ্চিত করার business decisions

Company/BIN/branch সংখ্যা, central বনাম branch registration, transaction volume, opening-data source, import/export-এর document/process details, subcontract ownership/settlement rules, wastage/by-product treatment, return submission channel এবং hosting owner অমীমাংসিত। এগুলো ছাড়া সংশ্লিষ্ট business logic বা tax treatment নির্ধারণ করা যাবে না। প্রথম pilot-এর representative trading, manufacturing, import/export ও subcontract dataset দরকার।

## Roles ও অনুমতি

| Role | কাজ | সীমা |
|---|---|---|
| Administrator | User, role, company access configuration | Financial approval স্বয়ংক্রিয়ভাবে নয় |
| Maker | Master/document entry ও submission | নিজের document approve নয় |
| Checker | Supporting document যাচাই ও decision | Company/branch grant বাধ্যতামূলক |
| Posting Officer | Frozen calculation review ও posting | Backend segregation rules প্রযোজ্য |
| Production Officer | BOM, consumption, production entry | Approved version ও warehouse scope |
| VAT Officer | Tax rules, reconciliation, return preparation | Rule publication/return approval পৃথক |
| Auditor | Read, audit history, approved reports | Mutation নয় |
| Management Viewer | Scoped dashboard/report | Granted company/branch মাত্র |

প্রতিটি screen/API-তে read/create/submit/approve/post/reverse/export permission matrix তৈরি করতে হবে। Menu visibility কখনো backend authorization-এর বিকল্প নয়।

## End-to-end workflows

Trading: Purchase draft → submit → independent approval → immutable posting review → stock/input-tax posting → Sales approval/reservation/posting → output-tax → corrections → reconciliation → period close.

Manufacturing: Raw-material purchase → approved versioned BOM/coefficient → production order → material issue → actual consumption/wastage → finished-goods receipt → sales → stock/tax reconciliation.

Import: Import order/reference → declaration/supporting evidence → reviewed currency/tax basis → goods receipt → approved import tax/stock posting → reconciliation. Export: Sales reference → export/shipment evidence → reviewed tax treatment → dispatch/posting → adjustment/return reconciliation. Subcontract: Approved job → owned material dispatch → subcontractor custody tracking → consumption/unused material/wastage confirmation → finished-goods return → service invoice/tax review → quantity reconciliation and close. These are proposed workflows, pending business specification and legal mapping.

Compliance: Source completeness check → frozen period calculation → adjustments/VDS review → checker approval → reviewed export বা configured submission → acknowledgement সংরক্ষণ। Local approval এবং externally accepted filing পৃথক status হবে।

## Architecture ও data contracts

বর্তমান coordinated financial transaction boundary বজায় রেখে module ownership পরিষ্কার করা হবে। Financial correctness প্রমাণের আগে অতিরিক্ত database extraction নয়।

- Document + stock/tax journals + audit + outbox + idempotency receipt একই অনুমোদিত transaction boundary-তে commit হবে।
- Commands-এ company scope, actor permission, expectedVersion ও idempotency থাকবে; ভিন্ন actor/payload দিয়ে key replay প্রত্যাখ্যান করতে হবে।
- Tax calculation effective-dated published rule এবং frozen source snapshot ব্যবহার করবে। Historical document বর্তমান tax rate দিয়ে পুনরায় গণনা হবে না।
- Table dictionary-তে purpose, columns, precision, keys, constraints, indexes ও owner থাকবে। Cross-database foreign key ধরে design নয়।
- Manufacturing quantities/unit conversion, lot tracking প্রয়োজন, yield/wastage এবং costing boundary business review-এর পরে নির্ধারণ হবে। VAT scope-এর বাইরে পূর্ণ cost accounting ধরে নেওয়া যাবে না।
- Migration explicit owner job; runtime role least privilege। Existing-data transfer-এর আগে backup, reconciliation এবং rollback design।
- API inventory-তে route, owner, request/response, permissions, validation, errors ও transaction guarantees নথিভুক্ত হবে। Existing routes যাচাই ছাড়া নতুন parallel API তৈরি নয়।

## Screen plan

| Screen group | প্রয়োজনীয় screen/actions |
|---|---|
| Setup | Company/branch/BIN, warehouse, users/permissions, fiscal period |
| Masters | Product/raw-material/finished-goods, supplier/customer, unit/classification, published tax rules |
| Purchase | Paged register, entry/detail, approval, posting review, correction |
| Sales | Paged register, entry/detail, approval/reservation, posting, adjustments |
| Manufacturing | Versioned BOM/coefficient, production order, issue/consumption, wastage, finished-goods receipt |
| Inventory | Scoped balance/movement, transfer issue/receipt, reconciliation |
| Workflow | Real owner-backed pending tasks, source detail, versioned approve/reject |
| Compliance | Period readiness, VDS/adjustments, return preparation/review, submission receipts |
| Reports | Source registers, correction-inclusive ledgers, stock/tax reconciliation, audit history |

সব screen-এ required validation, loading/empty/error/retry, permission-aware actions, current version, historical snapshot এবং honest success/error state থাকবে। CSV/print export-এর scope ও freshness স্পষ্ট হবে।

## Ordered delivery backlog

| ID | অগ্রাধিকার | Deliverable | Dependency | Acceptance evidence |
|---|---|---|---|---|
| B01 | P0 | Current build/test baseline, route/context/owner map | Existing source | Commands/results ও unresolved failures নথিভুক্ত |
| B02 | P0 | Pilot requirements, roles ও opening-data plan | Business decisions | Reviewed scope, permission matrix, representative data |
| B03 | P0 | Trading financial regression ও reconciliation | B01/B02 | Purchase/Sales/stock/tax/close agree; retries/concurrency safe |
| B04 | P0 | Manufacturing gap assessment ও workflow specification | B02/B03 | BOM version, issue/receipt/wastage invariants ও approved contracts |
| B05 | P1 | Manufacturing API/UI integration | B04 | বাস্তব production scenario থেকে traceable stock/tax evidence |
| B06 | P1 | Owner-backed approval inbox ও screen completeness | B03/B05 | Failed/stale decision-এ false success নয়; role-based UAT |
| B07 | P1 | Compliance calculation ও reviewed form mapping | B03/B05 | Source-to-form reconciliation, effective rule evidence, reviewed examples |
| B08 | P1 | Durable report generation/freshness | B03/B07 | Coverage proof, immutable version, totals/entries reconciliation |
| B09 | P0 release gate | Deployment, broker/identity, backup restore ও load verification | Completed pilot scope | Staging evidence, restore rehearsal, scoped access, recovery runbook |
| B10 | P0 release gate | Controlled pilot ও sign-off | B09 | Trading/manufacturing users accept reconciled opening/closing balances |

B01 প্রথম execution batch। B04-এর আগে সম্পূর্ণ Manufacturing module নতুন করে লেখা হবে না; বিদ্যমান implementation inventory করে শুধু gap পূরণ করতে হবে। Duration ও staffing estimate B02/B04-এর পরে নির্ধারণ হবে।

## Verification strategy

1. Frontend typecheck, production build, existing validator/API tests।
2. Backend Release build; disposable PostgreSQL-এ relevant integration suites, পরে full solution checks।
3. Negative permission/tenant/company tests, stale versions, duplicate commands, rollback, close/post race এবং correction reconciliation।
4. Configured live identity/broker tests; skipped test-কে passing evidence নয়।
5. Trading এবং manufacturing browser UAT; export/print totals ও historical values যাচাই।
6. Backup restore, deployment rollback, migration drift, load ও monitoring evidence।

বর্তমান আইন/rate/form correctness এই document certify করে না। Compliance implementation-এর সময় authoritative current sources এবং business review দরকার।

## Execution evidence

31 October-এর actual working-tree baseline:

- Frontend `npm run typecheck`: passed.
- Frontend `npm run build`: production build passed.
- Frontend `npm test`: 42 validator/calculation tests passed.
- Frontend `npm run test:api`: 131 tests; 124 passed, 7 failed। Log: `tmp/vat-baseline-api-2026-10-31.log`.
- পাঁচটি Manufacturing component test `PdfExportService` provider না পাওয়ায় component construction-এ ব্যর্থ। Test injector এবং নতুন component dependencies একসঙ্গে review করতে হবে; শুধু assertion দুর্বল করে pass নয়।
- দুটি Inventory contract test ব্যর্থ: request-এ নতুন `method: WEIGHTED_AVERAGE` parameter এবং unknown valuation-এর expected null বনাম actual zero। Backend valuation contract মিলিয়ে data semantics নিশ্চিত করার পরে implementation/test সংশোধন করতে হবে।
- Sandbox-এ Node/esbuild child-process `spawn EPERM` ছিল; approved local execution-এ test ও frontend build চালানো হয়েছে। উপরের সাতটি failure sandbox failure নয়।
- Backend `dotnet build EnterpriseVat.sln --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`: passed; 0 warnings, 0 errors। এই batch-এ database-dependent backend tests চালানো হয়নি।

পরবর্তী corrective batch: Inventory valuation semantics এবং Manufacturing test dependency setup review; এরপর affected tests এবং full frontend API suite rerun। Application business rules এই planning/baseline batch-এ পরিবর্তন করা হয়নি। আগের implementation report-এর test counts এই run-এর ফল নয়।

## Corrective execution follow-up

ব্যবহারকারীর implementation নির্দেশনায় প্রথম corrective batch শুরু হয়েছে। [Execution evidence](EXPERT_IMPLEMENTATION_2026-10-31.md)-এ changes ও results আছে। Frontend-এর সাতটি baseline failure সংশোধনের পরে API suite 132/132 passed। Manufacturing-এ rejected approval-এর false success এবং fabricated declaration/cost presentation সরানো হয়েছে; inventory missing-value presentation সংশোধিত। Backend baseline ও public pagination fix-এর ফল execution document-এ সংরক্ষণ করা হবে। পূর্ণ roadmap এখনও pending।
