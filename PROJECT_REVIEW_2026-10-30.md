# Full project review and delivery plan

Review date: 30 October 2026, Asia/Dhaka. Perspectives: software architect, senior developer, business analyst and end user.

Implementation follow-up: [stabilization delivery, validation and remaining gates](IMPLEMENTATION_STATUS_2026-10-30.md). Findings below describe the review snapshot; several authentication, unsafe-write, frontend outcome and test-isolation findings have since been addressed or contained. The complete roadmap remains pending.

## সিদ্ধান্ত

বর্তমান project-এ domain modelling, approval/version checks, audit, idempotency এবং messaging-এর ভালো ভিত্তি আছে। কিন্তু **বর্তমান source snapshot production rollout-এর জন্য প্রস্তুত নয়**। সবচেয়ে বড় সমস্যা নতুন service extraction-এর সময় financial guarantees হারিয়ে যাওয়া এবং frontend-এ ব্যর্থ কাজ সফল দেখানো। আগে correctness ও বাস্তব end-to-end flow ঠিক করতে হবে; নতুন module বা আরও database extraction তার পরে।

এই review source, configuration, deployment scripts, existing tests ও documentation পর্যালোচনা করেছে। এটি live production database audit, browser usability session বা বর্তমান VAT আইনের certification নয়। Source-এ প্রমাণিত বিষয়, সম্ভাব্য runtime প্রভাব ও এখনও যাচাই না হওয়া বিষয় নিচে আলাদা করা হয়েছে। পুরোনো test counts নতুন implementation-এর প্রমাণ হিসেবে ব্যবহার করা হয়নি।

## বর্তমান architecture: source বনাম documentation

- Frontend: Angular 22, standalone components, signals, RxJS; purchase, sales, inventory, manufacturing, compliance, workflow, reporting এবং administration screens আছে।
- Backend: .NET 10, EF Core, PostgreSQL; 12 business API projects, compatibility host এবং FinancialOutbox technical host আছে।
- `src/Services`-এ এখন Company, MasterData, Purchase, Sales, Inventory ও VatEngine-এর নিজস্ব persistence registration/context আছে। এগুলোকে এখনও সাতটি coordinated service বলা পুরোনো README-এর বর্ণনার সঙ্গে মেলে না।
- আলাদা context/connection থাকাই সম্পূর্ণ স্বাধীন business service হওয়ার প্রমাণ নয়। নতুন services-এ reference, period, stock, event delivery ও reporting-এর contract অসম্পূর্ণ।
- `src/Vat.Api/Program.cs`-এর compatibility host প্রথমে পুরোনো financial bridge register করে, পরে service-specific registrations যোগ করে। একই interface-এর একাধিক registration হওয়ায় কার্যকর implementation স্পষ্টভাবে নির্ধারণ ও পরীক্ষা করা জরুরি। এটি reverse proxy হিসেবে প্রমাণিত নয়; নামের মধ্যে Gateway থাকলেও একই process-এ business dependencies register হয়।
- Reporting host একই সঙ্গে financial bridge এবং নতুন Reporting persistence register করে; তার public report controller এখনও পুরোনো `IReportingService` ব্যবহার করে। নতুন database তৈরি হওয়া মানেই public report cutover সম্পন্ন নয়।
- README-এর “fixed key removed”, “startup never migrates” ও পুরোনো isolation/database counts বর্তমান source-এর জন্য নির্ভরযোগ্য baseline নয়।

## সর্বোচ্চ অগ্রাধিকারের findings

P0 = real-data pilot-এর আগে বন্ধ/সংশোধন; P1 = pilot completeness ও operations; P2 = পরবর্তী উন্নয়ন। নিচের paths repository root থেকে relative।

| ID | Priority | Source evidence | প্রভাব ও প্রয়োজনীয় পরিবর্তন |
|---|---|---|---|
| R01 | P0 | `VAt_Asper_backend/src/Vat.Web/VatHttp.cs`, JWT configuration | Authority string-এ `localhost` থাকলে embedded symmetric signing key সক্রিয় হয়; environment check নেই। Production-ও এই branch নিতে পারে। String containment-এর বদলে external issuer validation বাধ্যতামূলক করতে হবে; test signer শুধু test host-এ থাকবে। পরিচিত key দিয়ে signed token production configuration-এ প্রত্যাখ্যান করতে হবে। |
| R02 | P0 | `Vat_Fontend/src/app/core/services/auth.service.ts` | Login browser-এর plaintext demo users যাচাই করে; pre-signed token নির্বাচন করে; অজানা user-এর জন্য অন্য demo subject-এর token fallback আছে। Local user/role/refresh state বাস্তব issuer session নয়। OIDC login, verified profile/grants, expiry/logout এবং issuer-supported renewal বাস্তবায়ন করতে হবে। UI role ও JWT actor একই ব্যক্তি হতে হবে। |
| R03 | P0 | `.../core/services/api/purchase-api.service.ts: postPurchase`; `sales-api.service.ts: postSalesInvoice`; `compliance-api.service.ts: submitReturn91` | Server posting/return submit ব্যর্থ হলেও catchError local status পরিবর্তন করে success ফেরায়। ব্যবহারকারী ধরে নেবে stock/tax posting বা return submission হয়েছে। Production-এ এই success fallback সরাতে হবে; server receipt ছাড়া final success নয়। |
| R04 | P0 | `VAt_Asper_backend/src/Services/Purchase/Application/PurchasePostingService.cs`; `Infrastructure/PurchaseCommandExecutor.cs` | নতুন Purchase Prepare hardcoded tax snapshot তৈরি করে; Post source/plan status ও audit বদলায়, কিন্তু stock receipt/tax ledger coordination নেই। Event writer-এর Append body ফাঁকা। Posted document reporting/inventory-এ না পৌঁছানোর সরাসরি ঝুঁকি। Posting disable করে reviewed financial implementation পুনঃসংযোগ বা সম্পূর্ণ durable orchestration দিতে হবে। |
| R05 | P0 | `.../Company/Infrastructure/CompanyPeriodStore.cs`; `.../Sales/Infrastructure/SalesPersistence.cs`; `.../Inventory/Infrastructure/InventoryPersistence.cs` | Company HasPending সর্বদা false, BalancesReconcile true, close snapshot empty/zero। Sales/Inventory period guard no-op। Closed-period protection ও reconciled close কার্যকর বলে ধরা যায় না। বাস্তব admission/fence এবং owner reconciliation ছাড়া close/post বন্ধ রাখতে হবে। |
| R06 | P0 | `.../Sales/Infrastructure/SalesPersistence.cs`; `SalesPostingStore.cs` | Sales reference adapter product/tax rule-এর জন্য null এবং warehouse-এর জন্য placeholder ফেরায়; একই service stock SQL চালায় নিজের connection-এ `inventory.stock_balances`-এর বিরুদ্ধে। পৃথক Sales database-এ owner table অনুপস্থিত হলে failure; duplicate table থাকলে ভুল ownership। Versioned reference/Inventory contracts বা একই coordinated transaction boundary ফেরাতে হবে। |
| R07 | P0 | `.../Compliance/Vat.Compliance.Api/Controllers/ComplianceController.cs:47,114,197,334,491,606` | Return submit, VDS issue/deposit, ledger deposit ও credit/debit writes সবই ComplianceRead policy ব্যবহার করে। Controller raw multi-statement commands চালায়; command executor/idempotency/audit transaction ব্যবহার করে না। Read user write-path-এ প্রবেশ করতে পারে; runtime DB privilege outcome আলাদাভাবে verify করতে হবে। পৃথক permissions, typed validation, maker/checker, immutable audit ও atomic command boundary দিতে হবে। |
| R08 | P0 | একই Compliance controller: credit/debit handlers, AggregateLiveReturn, SaveReturnSubmission | Compliance connection-এ Company/Purchase/Sales tables query/write হয়; credit/debit source হিসেবে company-এর প্রথম invoice নেয়; fixed branch/form/period IDs, invented opening balance ও hardcoded rate আছে। Return response স্থানীয় insert-কে NBR submission হিসেবে বর্ণনা করে, কিন্তু method-এ external submission/acknowledgement নেই। Owner API/frozen snapshots ব্যবহার এবং LocalSubmitted বনাম FiledWithReceipt পৃথক করতে হবে। |
| R09 | P0 | `.../Company/Infrastructure/CompanyPersistence.cs`; `.../Purchase/Infrastructure/PurchasePersistence.cs`; Sales/Inventory/MasterData/VatEngine/Reporting registrations ও settings | Hardcoded superuser connection fallback আছে। একাধিক API startup-এ migration চালায় এবং exception warning দিয়ে চালিয়ে যায়। Secret rotation/removal, required configuration validation, least-privilege runtime role ও আলাদা migration job দরকার। Startup failure বনাম degraded readiness সুস্পষ্ট করতে হবে। রিপোর্টে credential value পুনরুৎপাদন করা হয়নি। |
| R10 | P1 | `.../Reporting/Vat.Reporting.Api/Program.cs`, `Controllers/ReportsController.cs`; `src/Vat.Hosting/VatBackend.cs` | Reporting host নতুন core/database register ও migrate করলেও public report queries পুরোনো financial IReportingService-এ যায় এবং readiness-ও financial context পরীক্ষা করে। Owned core ready হওয়া ও public report completeness আলাদা। Explicit generation selection/promotion, দুটো প্রয়োজনীয় dependency-এর readiness এবং source coverage ছাড়া cutover সম্পন্ন বলা যাবে না। |
| R11 | P1 | `.../Purchase/Infrastructure/PurchaseCommandExecutor.cs` বনাম `src/Vat.Infrastructure/Persistence/CommandExecutor.cs` | নতুন Purchase replay path payload hash দেখে, সংরক্ষিত actor ownership যাচাই করে না; পুরোনো executor তা করে। Company/operation/key জানা অন্য actor আগের receipt পেতে পারে। Executor semantics এক করতে হবে; same key/different actor negative test চাই। |
| R12 | P1 | `Vat_Fontend/src/app/core/services/api/workflow-api.service.ts`; backend `RetiredWorkbenchController.cs` | UI `/workflows/my-tasks` ও tasks approve/reject call করে; backend এসব route-এ 410 দেয়। UI demo task fallback দেখায় এবং HTTP success-এর আগেই local task mutate করে। Source document owner API + current version দিয়ে decision দিতে হবে; ব্যর্থ request-এ task/status/count অপরিবর্তিত থাকবে। |
| R13 | P1 | Purchase/Sales API mappers; `company-context.service.ts` | VAT পুনরায় fixed 15% হিসাব, fabricated HS/UOM/BIN/vehicle details, fallback company/branch/warehouse, current selected period দিয়ে historical invoice display আছে। Server-authoritative calculation snapshot ও real reference data ব্যবহার করতে হবে; missing data দৃশ্যমান থাকবে। এখানে কোনো hardcoded rate-এর আইনগত বৈধতা দাবি করা হয়নি। |
| R14 | P1 | Purchase/Sales list API adapters; backend list defaults; document mappers | Frontend একবার list GET করে; backend default 25 rows ফেরায়। Pagination cursor নেই, response version view model-এ বহন হয় না; transitions guessed version ব্যবহার করে এবং approve retry-তে version 1 পাঠায়। পূর্ণ pagination, actual version ও 409 reload/review UX চাই। |
| R15 | P1 | `VAt_Asper_backend/.github/workflows/ci.yml`; repository root | একমাত্র CI workflow Git repository root-এর `.github/workflows`-এ নেই। এই monorepo layout-এ GitHub সেটি workflow হিসেবে আবিষ্কার করবে না। Root workflow ও correct working-directory/path দিতে হবে; frontend checks এবং নতুন standalone service contracts অন্তর্ভুক্ত করতে হবে। |
| R16 | P1 | `Vat_Fontend/angular.json`; `package.json`; `tests/vat-calculations.test.ts` | সাধারণ npm build-এর default development; production build explicit চাই। Frontend tests-এর একটি অংশ test-file-এর নিজস্ব calculation/format functions পরীক্ষা করে, production implementation নয়। Build success ও 42 tests end-to-end correctness প্রমাণ করে না। Actual services/interceptors/components ও browser journeys পরীক্ষা দরকার। |
| R17 | P2 | `app.routes.ts`, feature components, reports screen | Routes eager-loaded; কয়েকটি component-এ বড় inline UI/business logic; reports-এর Download button আসলে navigation করে। Route lazy loading, ছোট feature services/components, role-specific navigation, সত্যিকার export ও accessibility QA দরকার। Performance improvement measurement অনুযায়ী হবে। |
| R18 | P0 | `VAt_Asper_backend/tests/Vat.Tests/PostgresApiFactory.cs:33`; compatibility host service registrations | Test fixture শুধু পুরোনো ছয় owned/financial connection override করে; Company/MasterData/Purchase/Sales/Inventory/VatEngine/Reporting-এর নতুন connection keys override করে না। ফলে disposable runner চালালেও নতুন adapters স্থানীয় appsettings/default database ব্যবহার করতে পারে। সব connection deny-by-default/disposable করা, test startup-এ target verification এবং business database-এ test writes হয়নি কি না যাচাই জরুরি। এই review-তে ঝুঁকি ধরা পড়ার পরে suite থামানো হয়েছে; clean full-suite result দাবি করা হয়নি। |

## চারটি দৃষ্টিকোণ থেকে মূল্যায়ন

### Software architect

একই business capability-এর পুরোনো coordinated implementation এবং নতুন isolated implementation পাশাপাশি চলছে। কোন host কোন implementation-এর দায়িত্ব নেয় তা অস্পষ্ট হলে data ownership, deployment independence ও recovery নিশ্চিত হয় না। প্রথম deliverable হবে executable ownership map: route → service → application implementation → database → writer → outbox → report consumer। প্রতিটি critical write-এর transaction boundary ও failure recovery লিখতে হবে।

সুপারিশ: এখন extraction স্থগিত করে purchase/sales/stock/tax/period-এর একটি প্রমাণিত financial boundary স্থির করুন। সবচেয়ে দ্রুত নিরাপদ পথ হতে পারে বিদ্যমান coordinated core ব্যবহার; নতুন isolated path কেবল contract/reconciliation প্রমাণের পরে enable হবে। আলাদা database আবশ্যক হলে durable posting orchestration, reservation, idempotent steps, retry/recovery এবং close-fence protocol আগে নির্মাণ করতে হবে। Cross-database HTTP calls-কে local transaction-এর মতো গণ্য করা যাবে না।

### Senior developer

ভালো দিক: explicit domain state transitions, expected-version semantics, tenant/company scoping, parameterized SQL, transactional command executor ও কয়েকটি durable messaging contract আছে। এগুলো সংরক্ষণ করতে হবে।

জরুরি পরিবর্তন: no-op adapters অপসারণ/disable; shared command semantics; typed request validation; server DTO থেকে generated client/contracts; version ও snapshot অক্ষত রাখা; fail-closed errors; per-command stable idempotency key। Interceptor প্রতি নতুন request-এ key বানালে lost response-এর পরে manual retry নতুন operation হয়ে যেতে পারে। একই logical operation-এর key terminal outcome না আসা পর্যন্ত সংরক্ষণ করতে হবে।

### Business analyst

Screen থাকা আর accepted business capability থাকা আলাদা। Pilot-এর scope লিখিতভাবে নির্ধারণ করুন: domestic goods/BDT, company/branch/warehouse, supplier/customer/product references, কোন tax treatments, কোন correction flows এবং কোন forms শুধু preview। Import/export, services, supplementary duty, VDS, production, filing ইত্যাদি প্রতিটি capability-র Included/Excluded/Blocked সিদ্ধান্ত চাই।

একটি capability matrix-এ requirement, actor, permission, prerequisites, source data, command, accounting/stock/tax effect, exception path, audit evidence ও acceptance case থাকবে। Return-এর Draft → Reviewed → Approved → Exported/LocallySubmitted → ExternallyFiledWithReceipt আলাদা states দরকার; শুধু Submitted লেখা যথেষ্ট নয়। Regulatory mapping ও effective-date rules tax-domain specialist দিয়ে dated official sources-এর বিপরীতে approve করাতে হবে। এই code review বর্তমান আইন যাচাই করেনি।

### End user

প্রথম প্রয়োজন বিশ্বাসযোগ্য status ও totals। Maker বুঝবেন draft save হয়েছে নাকি submit; Checker দেখবেন current version, source document ও difference; Tax Manager দেখবেন applied rule/date ও posting receipt; Auditor দেখবেন immutable history এবং original/correction linkage।

Company/branch/period সবসময় দৃশ্যমান থাকবে। Unavailable service, empty result, stale report, permission denied ও validation error পৃথকভাবে দেখাতে হবে। Server failure-এ sample invoice/return দেখানো যাবে না। Unsaved changes, duplicate click, interrupted request, keyboard navigation, বাংলা label/number, date boundary ও A4 print পরীক্ষা করতে হবে। Browser-এ ব্যবহারকারীর session চালিয়ে এই review-তে usability test করা হয়নি; এগুলো source-backed UX backlog ও UAT criteria।

## Module capability matrix

| Module | বর্তমান source পর্যবেক্ষণ | Pilot gate |
|---|---|---|
| Identity | Backend external issuer-এর ওপর নির্ভর; frontend local demo login | Real identity lifecycle, correct subject/grants, expired/revoked session handling |
| Company/Period | Owned context আছে; close checks placeholder | Actual pending operations, balances, snapshot ও close/post race proof |
| MasterData | Owned context ও frontend enrichment-এ mock dependency | Company-scoped real reference snapshots এবং no invented identifiers |
| Purchase | Draft/approval আছে; নতুন posting আর complete stock/tax transaction নয় | Receipt + stock + tax + audit + durable event reconcile |
| Sales/Receivables | Workflow/payment/correction code আছে; reference ও period adapters অসম্পূর্ণ | Stock reserve/issue, tax, payment/correction history ও fiscal admission end-to-end |
| Inventory | Transfers/journal code আছে; isolated period guard no-op | Stock conservation, reservation safety, period fence ও event delivery |
| Manufacturing | Controlled coefficient workflow আছে; production issue/receipt/wastage পরবর্তী কাজ | Real reference contracts; প্রয়োজন হলে পূর্ণ production stock lifecycle |
| VatEngine | Rules/domain আছে; নতুন Purchase path rules bypass করে | Published effective-dated rules সব posting path-এ enforce |
| Compliance | Local SQL-based extra writes; cross-owner queries; misleading filing success | Read/write separation, trusted snapshots, reconciled forms এবং external receipt |
| Workflow | Metadata read + retired workbench routes | Owner-backed approval queue, versioned actions, delegation policy if required |
| Reporting | New generation core ও legacy public financial service পাশাপাশি | Proven export/cursor catch-up, freshness, reconciliation ও controlled promotion |
| Integration | Remote messaging contracts আছে; নতুন Purchase/Inventory event writers ফাঁকা | প্রত্যেক active source-এর durable event থেকে consumer receipt পর্যন্ত proof |

## বাস্তবায়ন পরিকল্পনা

সময়সীমা indicative: 2 backend developer, 1 frontend developer, 1 QA এবং part-time architect/BA/tax specialist ধরে। Legacy data বা regulatory scope বড় হলে পুনঃestimate করতে হবে। Phase gates পূরণ না হলে পরের dependent phase শুরু নয়।

| Phase | আনুমানিক সময় | দায়িত্ব | Deliverables ও exit gate |
|---|---|---|---|
| 0: Baseline ও containment | 2–3 working days | Tech lead + QA | Current commit/route/context inventory; isolated backups/restore plan; unsafe posting/filing paths disabled; sample/live distinction; P0 regression scenarios তালিকা |
| 1: Authentication ও honest outcomes | 1–2 weeks | Backend + frontend | R01–R03/R07/R09 fixes; OIDC integration; runtime least privilege; server receipt ছাড়া success নয়; read-only user write denied; demo token rejected |
| 2: Financial correctness | 2–4 weeks | Architect + backend + QA | R04–R06/R08; coordinated core restoration অথবা completed owner protocols; stock/tax/source/audit/event reconcile; closed period ও interrupted posting tests pass |
| 3: Frontend/business integration | 2–3 weeks | Frontend + backend + BA | Real company/master data; versions/states/totals; workflow owner actions; pagination; retry recovery; historical snapshot এবং reliable print/export |
| 4: Reporting ও compliance scope | 2–4 weeks | Backend + tax specialist + QA | Source coverage/catch-up/freshness; ledger→register→return reconciliation; approved mappings; local/export/external filing states; supported correction paths |
| 5: Operations ও controlled pilot | 1–2 weeks | QA + operations + business owner | Active root CI; real issuer/broker tests; performance baseline; restore rehearsal; alerts/runbooks; role-based UAT; opening/closing balance sign-off |

Phases 3/4-এর independent preparation কিছু overlap করতে পারে। মোট calendar estimate approximately 9–15 weeks; এটি commitment নয়। Full manufacturing/import/export বা নতুন statutory filing integration প্রয়োজন হলে আলাদা scope/estimate লাগবে।

## প্রথম actionable backlog

| Work item | Depends on | Acceptance |
|---|---|---|
| B01: Embedded signer ও local auth অপসারণ | None | Production configuration-এ known demo tokens rejected; authentic issuer token-এর correct subject/profile |
| B02: Frontend false-success fallback সরানো | None | 401/403/409/500/timeout-এ Posted/Submitted নয়; retryable error ও correlation ID দৃশ্যমান |
| B03: Critical command gate | None | অসম্পূর্ণ posting/close/compliance writes explicit unavailable; কোনো final state বা partial financial write নয় |
| B04: Ownership/composition map | None | প্রতিটি route-এর একটিমাত্র authoritative implementation; standalone DI/route/readiness checks |
| B05: Financial boundary পুনঃস্থাপন | B03, B04 | Source, stock, tax, audit, outbox একই reconciled outcome; failure/restart-এ recoverable state |
| B06: Period admission ও close fencing | B04, B05 | Pending operation close block করে; closing/closed period নতুন financial work block করে; race পরীক্ষা |
| B07: Real DTO/version/master data integration | B01, B04 | Actual expectedVersion; immutable rule/customer snapshot; no default warehouse/company substitution |
| B08: Compliance commands redesign | B03, B05, B06 | Separate permissions; maker/checker; idempotency; immutable source linkage; external filing-এর receipt |
| B09: Reporting coverage ও cutover | B05 | সব active source events accounted; missing sequence blocks promotion; stale/incomplete status visible |
| B10: CI ও UAT | এখন setup; B01–B09 for release | Root workflow runs both stacks; supported end-to-end journeys ও recovery gates pass |

## Mandatory acceptance journeys

1. Maker purchase তৈরি/submit করবেন, অন্য actor approve/post করবেন; stock receipt, input-tax entry, source version, audit এবং durable event মিলবে। Same key retry duplicate তৈরি করবে না।
2. Sales reservation/posting একই stock দুইবার ব্যবহার করতে পারবে না; failed tax/reference validation কোনো partial issue করবে না।
3. Timeout-এর পরে একই logical command retry original receipt ফেরাবে; different payload/actor দিয়ে replay প্রত্যাখ্যান হবে।
4. Closed/closing period-এর admission এবং concurrent close/post safety বাস্তব service/database boundary-তে পরীক্ষা হবে।
5. Return/credit/debit/VDS action-এ read-only user denied; original source/period/rule linkage যাচাই; step failure-এ rollback/recovery।
6. Broker unavailable হলেও financial event durable থাকবে; restored delivery duplicate financial effect করবে না; Reporting freshness ও coverage দৃশ্যমান থাকবে।
7. First page-এর বেশি documents, company switch, restricted branch, rejected/cancelled/reversed/prepared states এবং historical period সঠিক দেখাবে।
8. Filing-এর external acknowledgement না থাকলে UI/API দাবি করবে না যে NBR-এ filed হয়েছে। Print/export values frozen server data-র সঙ্গে মিলবে।
9. Runtime DB role DDL/other owner writes করতে পারবে না; backup থেকে isolated restore করে business reconciliation pass হবে।

## Verification performed for this review

- Frontend TypeScript check: passed (`npm run typecheck`).
- Frontend production build: passed (`npm run build -- --configuration production`); initial bundle approximately 1.17 MB raw / 202.27 kB estimated transfer. Sandbox initially blocked compiler worker spawning; rerun with authorized process access succeeded.
- Frontend tests: 42 passed using `node --experimental-strip-types --test --test-isolation=none tests/nbr-validators.test.ts tests/vat-calculations.test.ts`. Default npm test hit sandbox child-process EPERM; no-isolation rerun passed. Coverage remains limited as R16 explains.
- Backend Release build: passed (`dotnet build EnterpriseVat.sln --no-restore --configuration Release -m:1`), zero warnings and zero errors. No online package vulnerability audit was performed by this no-restore build.
- Database-free focused authentication regression rerun: **3 failed, 0 passed**, using `dotnet test tests/Vat.Tests/Vat.Tests.csproj --configuration Release --no-build --no-restore --filter FullyQualifiedName~AuthenticationConfigurationTests --logger trx --results-directory TestResults/review-2026-10-30-auth`. TRX evidence is retained in that directory. The assertions show configured external authority is replaced by the embedded-key configuration; this run does not depend on R18's database fixture.
- Backend full-suite attempt was interrupted after detecting R18. Before interruption, authentication configuration tests failed for Production `https://localhost:5080`, Production `https://localhost.attacker.example`, and Development localhost; transfer and sales regression failures were also observed. These are diagnostic observations, not an aggregate pass/fail count. Two live-broker cases reported skipped. Do not treat this attempt as an isolated baseline: the fixture did not override all newly added service connections. Local default databases may have received test operations; no claim of unchanged business data can be made from this run. Verify against backups/audit before relying on their balances; do not delete suspected rows without provenance.
- No application source code was edited. Only this review artifact was added; the pre-existing `open-swagger.bat` modification was preserved. A temporary PostgreSQL cluster was created; process inspection after interruption showed its PID had exited. Its exact generated directory was then removed after verifying the absolute workspace path and absence of its recorded process. The standalone Company liveness probe also started and was stopped; it was configured with an unreachable dummy database and did not exercise business operations.
- Live OIDC provisioning, RabbitMQ transport, deployment, performance/load, current database balances and official tax-rule correctness were not certified by these checks.
