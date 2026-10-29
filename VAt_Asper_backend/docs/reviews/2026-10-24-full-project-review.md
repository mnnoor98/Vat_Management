# Full backend project review and implementation plan

Review date: 24 October 2026, Asia/Dhaka

Post-review remediation: [decision 0023](../architecture/0023-p0-auth-inventory-recovery.md) records applied authentication/scope fixes, explicit prototype containment and verified reconstruction of the missing legacy transfer journal. The findings and 300-test baseline below are the original review snapshot, not the latest remediation status.

Reporting extraction progress: [decision 0027](../architecture/0027-reporting-source-export.md) adds a durable Purchase/reversal source manifest/export with reconciliation, stable pages, audited preparation and bounded coverage checks. Reporting remains coordinated until generation-scoped projection and customer bootstrap are delivered. The source snapshot table stays in the financial owner's database.

## Decision

The backend has substantial working purchase/sales/posting and messaging infrastructure. It is not ready for production sign-off: the local database has unexplained quantity-journal differences, a repository-known signing-key authentication path remains reachable by configuration, and newer Manufacturing/Inventory flows bypass controls used by the established financial workflows.

Stabilize data and security before further database extraction. Retain the user's requirement for twelve separately runnable/deployable business backends. Complete genuine project/data ownership through explicit contracts and verified cutovers; an API folder, a database with tables, or a green readiness response is not a completed business module.

This review changes documentation and adds a read-only diagnostic. It does not repair business rows, apply migrations, change credentials or certify statutory filing.

## Scope and evidence

Reviewed the solution/project graph, all twelve service responsibilities, shared hosting/authentication, new Inventory/Manufacturing controller paths, financial command/posting/close infrastructure, owned contexts/migrations/roles, messaging contracts/transport, tests, CI and deployment scripts. Findings are a source/configuration audit plus local verification, not an exhaustive penetration test or line-by-line proof of every method.

The workspace has 51 .NET projects, 187 non-migration source C# files and 32 test source files. There are twelve business API hosts, one optional FinancialOutbox technical host and the optional aggregate compatibility API. Source hashes for the key reviewed files are recorded under ignored `artifacts/review-2026-10-24/source-manifest.json`; no Git repository/parent repository was available to supply a commit identifier.

The earlier PDF-only review is [archived](2026-10-23-design-stage-review.md). Its statement that there is no source code is historical and no longer describes this workspace. The current audit uses source and local database evidence. The PDFs remain requirements background; legal/regulatory mappings have not been independently validated in this review. No frontend repository or UI workflow is included in this backend review.

Verification:

- Latest solution build in this session: zero warnings/errors.
- Latest published probes in this session: all twelve business APIs passed liveness, route ownership, authentication and database readiness; Purchase restart left Sales live. Reporting delivery and FinancialOutbox probes passed. Their child processes were stopped.
- Runtime role checks re-run during this review: required reads, no administrative runtime privileges and cross-owner restrictions passed.
- Local catalog/history inventory re-read using runtime credentials, without schema/data changes.
- Read-only stock quantity comparison: 5 balance rows, **2 unreconciled rows** against stock movements plus goods sales postings. This does not identify the cause or reconcile reservations/valuation/tax.
- Full solution baseline completed across all eight test projects: **300 passed, 0 failed, 2 skipped (302 total)**. The two skipped cases require a live RabbitMQ broker. TRX evidence is in `TestResults/full-review-2026-10-24`; aggregate counters are in ignored `artifacts/review-2026-10-24/test-summary.json`. The disposable PostgreSQL process stopped cleanly. This supersedes the earlier 128-test focused baseline for test-count reporting, while untested prototype and actual-data findings remain open.
- Docker is not installed locally; container deployment, external OIDC integration, dependency advisory freshness, load/chaos behavior and production infrastructure are not certified here. CI has a broker-enabled job definition; no remote green run was available to inspect.

## Current capability and ownership

| Backend | Persistence/build ownership | Actual capability and remaining work |
|---|---|---|
| Identity | Independent core; `vat_identity` | Verified profile and scoped role reads. External OIDC issuer; application grant administration/lifecycle remains unfinished. |
| Company | Shared financial core; `vat_management` | Companies, branches, fiscal-period fencing/close. Reference projections and distributed closing contracts needed before extraction. |
| MasterData | Shared financial core | Products, suppliers, customers and historical snapshots in financial workflows. Own versioned reference APIs/projections needed. |
| Purchase | Shared financial core | Draft/approval, frozen posting plan, atomic stock/input-tax/audit/outbox, reversal and reconciliation. Distributed participant protocol not implemented. |
| Sales | Shared financial core | Invoice workflow, reservation/posting, credit/debit, payments, allocation/corrections and reporting. Participant ownership remains coordinated. |
| Inventory | Shared financial core | Established warehouse/balance paths plus newer transfer/read additions. Transfer invariants and scoped joins need repair before trusted use. |
| Manufacturing | Independent core/read foundation; `vat_manufacturing` | Scoped production-order metadata reads. New coefficient controller is incomplete and conflicts with the intended restricted owned runtime. Production execution is unfinished. |
| VatEngine | Shared financial core | Effective rules, frozen calculations and financial tax ledgers. Rule/domain validation and independent posting contracts needed. |
| Compliance | Independent core; `vat_compliance` | Scoped return metadata reads. Return calculation, statutory mappings, evidence and filing execution unfinished. |
| Workflow | Independent core; `vat_workflow` | Scoped task metadata reads. Version-bound decisions, delegation/escalation execution and source revalidation unfinished. |
| Reporting | Shared financial core | Financial/customer reports, freshness, inbox/projections, backfill/rebuild; optional authenticated delivery endpoint. Cross-owner joins and rebuild semantics block simple DB separation. |
| Integration | Independent metadata core; financial host bridge remains | Connection metadata, existing financial operator APIs, optional local/remote dispatch and inbox delivery. ERP processing/webhooks and full isolation unfinished. |

Four of twelve business APIs have independent API/core/data foundations. Integration owns a fifth module's metadata database but is not a fully independent host. Seven other APIs remain coordinated. Manufacturing's project isolation does not certify its newly added coefficient workflow.

Local database catalog snapshot:

| Database | Application tables | History tables | Applied migrations |
|---|---:|---:|---:|
| `vat_management` | 96 | 1 | 17 |
| `vat_identity` | 9 | 1 | 1 |
| `vat_workflow` | 7 | 1 | 1 |
| `vat_compliance` | 8 | 1 | 1 |
| `vat_manufacturing` | 7 | 1 | 1 |
| `vat_integration` | 8 | 1 | 1 |

Source financial tables include retained legacy copies of extracted metadata; these are physical table counts, not unique business entities or completion percentages. Main metadata read tables checked by runtime-role probes currently contain zero rows. That does not prove all historical/source tables are empty.

## Findings in priority order

### R01 — P0: Local stock quantity reconciliation is already failing

The read-only diagnostic returned 2 unmatched/different rows out of 5 stock balance rows. It uses the same journal basis as `src/Services/Company/Infrastructure/PeriodStore.cs:83`: stock movements plus goods sales postings. The cause is not established. Seeded/opening stock, manual adjustments and newer transfer behavior must be investigated against source evidence.

New transfer code changes source/destination balances directly (`InventoryController.cs:197` and `:206`) without corresponding movement records. A successful transfer through that path can cause warehouse-level journal/balance disagreement and interfere with the existing close reconciliation. This is a confirmed missing code path, not proof that it caused the two observed rows.

Required: verified backup, per-scope discrepancy report, approved opening/correction/transfer journal design, auditable repair and re-run quantity/reservation/financial-close reconciliation. Do not overwrite balances to force a green count or delete posted history.

### R02 — P0: Repository-known signing-key path is configuration-reachable

`src/Vat.Web/VatHttp.cs:61` selects local authentication using `Authority.Contains("localhost")`. That path uses a fixed repository-known symmetric signing key and does not check the hosting environment. A Production host configured into this branch can accept tokens minted by a holder of that key with application tenant/company/permission claims. This is a confirmed source path; no exploitation of a deployed service was attempted.

Required: remove the shared fixed-key path from deployable authentication. Use external OIDC validation in deployed environments; isolate test authentication in test hosts. If a developer issuer is retained, make it explicit, environment-restricted and use an externally supplied/generated secret. Reject unsafe production configuration at startup. Add production-mode negative token tests and normal issuer/audience/lifetime/signature cases.

### R03 — P0: Coefficient authorization and maker/checker identity are incomplete

The coefficient actions at `ManufacturingController.cs:55`, `:213` and `:334` lack the named module permission and actor/company checks used by the existing production-order read path. Fallback authentication only requires an authenticated user (`VatHttp.cs:111`); it is not authorization for a supplied company ID. SQL filters omit tenant scope. Create uses a constant tenant and creator (`:237`, `:240`); approval chooses a fabricated alternate approver (`:350`).

Required: typed commands/services, verified Actor-derived tenant/subject, explicit read/request/approve permissions, scoped lookups, real distinct checker action, expected versions, immutable approved snapshots, command idempotency and audit. A database maker/checker CHECK does not establish who actually approved when the application invents a different identity.

### R04 — P0: Actual schema drift and incompatible coefficient runtime access

The local financial catalog contains ad hoc transfer columns (`vehiclenumber`, `drivername`, `remarks`, `dispatchtime`, `unitprice`) and coefficient/BOM cost fields. Those names were not found in the checked-in financial/owned Manufacturing migration definitions. The owned `vat_manufacturing` catalog did not contain the queried ad hoc coefficient fields.

Coefficient GET/POST attempts request-time ALTER TABLE, references financial MasterData products, and accesses tables beyond the current owned runtime grant. `scripts/Initialize-LocalManufacturingRoles.ps1:65` intentionally grants only production-order/history reads. With that role these new paths cannot work; granting admin rights would defeat the boundary. The privileged financial fallback was removed earlier, but that does not implement the workflow.

Required: compare actual catalogs with a clean migration install, inventory historical/source versus owned data under operator controls, add reviewed typed owned migrations/backfill, update transfer verification for added fields, replace cross-database joins with validated reference snapshots/projections and grant only necessary DML after the workflow is ready. Keep DDL exclusively in operator migration tooling. EF pending-model checks alone compare model/snapshot and do not detect this manual database drift.

### R05 — P0: Transfer path bypasses financial command safeguards

`InventoryController.cs:125` posts directly through DbContext/SQL instead of the established command executor and domain workflow. It invents an approver (`:167`), sets Posted immediately (`:179`), prechecks availability before its transaction, and lacks the established period-admission, audit, outbox and immutable stock-journal path. It has no actor-owned idempotency key/result record for ambiguous retries.

Database nonnegative/reserved constraints still provide protection; the review does not claim that a concurrency oversell was reproduced. However, failed/racing requests lack a designed domain conflict/recovery contract, and successful operations bypass journal/close invariants.

Required: draft → submit → actual checker approval → atomic posting; validate/aggregate lines and scoped references, enforce period fence and expected versions, use guarded quantity updates/locks with affected-row checks, write paired immutable transfer movements, audit, outbox and idempotency atomically. Define cancellation/reversal and reconciliation. Prototype transport/display fields must be migrated, not added manually.

### R06 — P0: New Inventory queries do not preserve composite tenant scope

`ListTransfers` filters CompanyId only (`InventoryController.cs:278`, `:327`). Several warehouse/product/branch lookups and joins use IDs without complete tenant/company keys (`:137`, `:140`, `:143`, `:210` and the balance joins). Other established paths use Actor tenant plus company scope.

Required: enforce tenant/company on each lookup and join, validate branch grants where required and test identical company/product/warehouse UUIDs reused across tenants. Checking that the actor has a CompanyId does not repair a query that then reads that CompanyId across tenants. Bound list sizes and implement pagination instead of unrestricted materialization.

### R07 — P1: Business module completeness is below API/deployment completeness

The service catalogue correctly declares four independent modules and one partial metadata owner. Company/MasterData/Purchase/Sales/Inventory/VatEngine/Reporting do not yet have fully independent core project graphs and data ownership. Identity/Workflow/Compliance metadata tables and read APIs are not complete management, approval or filing systems.

Required: per-module capability/acceptance checklist; independent projects and owned migrations at the real cutover; versioned reference/command contracts before revoking the bridge. Preserve twelve deployable hosts without claiming that twelve hosts mean twelve complete microservices.

### R08 — P1: Integration/Reporting extraction needs operator and rebuild contracts

Remote source/inbox contracts and receipt-gated acknowledgement are implemented, but Integration still registers the financial backend (`Program.cs:9`) and checks its database (`:28`). MessagingController delegates operator calls to financial IReportingService/ICommandExecutor. Reporting summary/backfill/rebuild still joins or changes source financial data (`ReportingService.cs:18`, `:51`, `:82`).

Required: owner-executed scoped operator contracts with validated delegated user identity, permission and owner-side idempotency/audit; outbox operations remain at their source owner; reporting replay remains atomic with its own audit/inbox state. Define completeness watermarks/manifests, backfill and durable projection-generation rebuild before separating Reporting storage. Do not replace one local audit/replay transaction with an untracked pair of HTTP commits.

### R09 — P1: Deployment/verification and operational evidence have gaps

CI builds/tests, NuGet audit mode and all six model checks already exist. However, CI publication omits the explicit FinancialOutbox switch, does not execute the two new technical-host probes, and its standard host probe omits database-ready mode. Local Docker/live broker/production OIDC/load proof is missing; CI service configuration is not a verified remote run.

Structured correlation/error handling and health endpoints exist. No established distributed tracing/metrics/SLO/load baseline or configured rate-limiting path was found in the reviewed source. Some new list endpoints are unbounded. FinancialOutbox still uses the broad financial runtime role; service keys need a documented deployment/rotation mechanism. Readiness currently measures local database migrations, not all remote dependencies.

Required: reproducible clean installs and role provisioning, technical host/package probes, live-broker fault tests, Docker build/start evidence, remote-mode integration tests, explicit dependency/degraded-health semantics, scoped secrets, narrow source SQL role and measured operational gates.

### R10 — P1: Change control, documentation and statutory acceptance need explicit gates

The workspace is not a Git working tree, so source/version provenance cannot currently be tied to a commit/PR. The previous root review described a PDF-only workspace. Concurrent coefficient/transfer additions demonstrate that controller routes, migration models and ownership documentation can drift independently.

Required: establish version control and reviewed change boundaries, keep route/migration/capability inventories current and preserve release/source manifests. Maintain the established purchase/sales financial tests when adding features. Domain reviewers must approve tax treatment/examples and statutory field/evidence mappings; plausible-looking defaults or a form label are not filing acceptance.

## Engineering standard for every backend

Use `src/Services/<Name>/{Domain,Application,Infrastructure,Vat.<Name>.Api}` with separate projects and feature-oriented application services. Controllers bind/authenticate/map HTTP; they do not contain SQL, DDL, fake actors or stock/tax mutation rules. Domain depends on primitives; Application owns use cases/ports; Infrastructure implements stores/clients/migrations; API is the composition root. SharedKernel and technical messaging must not accumulate unrelated business entities.

When extracting a module, remove its old wildcard compilation/financial dependencies in the same reviewed change. Own tables/history/roles; runtime never owns schema. No direct cross-owner database joins or writes. References use scoped versioned contracts and stored snapshots/projections. Every supported command defines permission, actor scope, expected version, idempotency, audit, event, transaction/commit boundary, retry/recovery and reconciliation behavior.

## Ordered delivery plan

Progress through acceptance gates, not an assumed percentage or a promise that every module fits one sprint. Proposed responsible roles below are work ownership, not named staffing commitments.

| Wave | Work packages | Proposed owner | Exit gate |
|---|---|---|---|
| 0 — P0 stabilization | Verified six-database backup/restore baseline; investigate 2 quantity mismatches; remove fixed-key production path; restrict unfinished endpoints; establish source/CI baseline. | Backend/security + database operator | No unexplained stock differences; unsafe production auth rejected; unsupported flows explicitly unavailable; no business rows changed without approved audit. |
| 1 — Financial workflow repair | Transfer domain/services, real checker, scoped queries, period admission, paired journals, idempotency/audit/outbox; reconcile schema with migrations. | Inventory + financial domain | Clean migrated DB supports complete transfer/reversal; duplicate/race/closed-period/tenant-negative tests pass; stock/reservations and close reconcile. |
| 2 — Owned coefficient workflow | Coefficient/BOM entities, commands/permissions, snapshots/reference validation, owned migrations/data reconciliation, role updates; remove controller SQL/DDL/fake defaults. | Manufacturing + domain reviewer | Full create/submit/approve/read on owned DB using restricted runtime; same maker cannot approve; no financial/master DB access; nonempty migration/restore checks pass. |
| 3 — Operator boundary and Reporting ownership | Delegated operator contracts; narrow outbox role; reporting watermark/backfill/rebuild design; own inbox/projections/audit/idempotency; metadata transfer and cutover; remove Integration financial bridge last. | Integration + Reporting | Remote dispatch/consume/operator recovery works without Integration financial DSN; reporting completeness/rebuild matches source; restore/rollback and broker faults demonstrated. |
| 4 — Reference owners | Company/MasterData APIs/owned migrations, versioned product/supplier/customer/company/branch reference projections; permissions/reference revocation and period protocol design. | Company + MasterData | Nonempty data transfer and scoped reference compatibility; consumers work with former cross-database access denied; close/admission contracts accepted. |
| 5 — Financial service cutovers | Inventory/VatEngine/Purchase/Sales core ownership and durable posting coordination, participant idempotency, source versions, period fencing, compensation/recovery and reconciliation. | Financial domain + service owners | Failure at each participant/commit boundary recovers; no silent partial Posted state; journal/tax/close equality maintained with separate DB credentials and processes. |
| 6 — Remaining module business scope | Identity grant lifecycle, Workflow decision/delegation/escalation, Manufacturing production execution, Compliance returns/evidence, Integration imports/webhooks. | Respective module owners + domain reviewers | Each agreed business slice works end to end, with permissions/versioning/recovery and approved external/regulatory contracts. |
| 7 — Release gate | Performance targets, observability/alerts, real OIDC/broker/container deployment, backup restoration, controlled pilot and operator runbooks. | QA/operations + business owner | Evidence for agreed load/SLO/RPO/RTO, zero critical findings, reconciled pilot data, approved regulatory scope and repeatable rollback. |

CI/observability/security improvements run throughout the waves. A production pilot does not wait until Wave 7 to discover missing logs or backups; Wave 7 consolidates the evidence. Reference extraction and financial protocol design can overlap after P0 stabilization, but persistent financial cutovers remain dependency-gated.

Company reference-data extraction can precede financial participants; moving fiscal-period authority cannot. Keep the coordinated period path until participant admission/close fencing is implemented and tested. A remote or cached "Open" response alone cannot authorize a later posting commit.

For each financial cutover, define a durable process ID and document version, participant-local inbox/result/outbox commits, observable Pending/Posted/RecoveryRequired states, and status queries for ambiguous timeouts. Mark the source Posted only after required matching participant results. Compensation uses approved correcting/reversal entries, never deletion of posted journals. Test crashes before/after every participant commit and coordinator acknowledgement before moving live data.

### First implementation backlog

1. **B01:** Preserve a source/data snapshot; run read-only scoped quantity/reservation reconciliation and approve the explanation/repair for every difference.
2. **B02:** Remove fixed-key deployable authentication and add Production-mode negative tests.
3. **B03:** Explicitly restrict incomplete coefficient endpoints (including GET with DDL) and unsafe transfer mutation/list paths until their support/authorization contracts are ready; keep existing validated read/posting paths usable.
4. **B04:** Compare local and clean migration catalogs, capture untracked fields/data and deliver compatible migrations/backfill with a restore rehearsal.
5. **B05:** Implement tenant-complete Inventory queries, transfer command/audit/journals and replay-safe posting.
6. **B06:** Implement the owned coefficient workflow with genuine actor/checker identity and scoped permissions.
7. **B07:** Add technical-host/remote-mode and actual-role CI coverage; require a green full suite including live broker on the designated CI runner.
8. **B08:** Deliver the operator/rebuild contract design and a bounded extraction spike before attempting Reporting/Integration data cutover.

This ordering deliberately places current correctness/security issues ahead of adding another remote port. Do not start by creating empty databases or copying every financial table into twelve stores.

### Required failure/acceptance matrix

| Area | Required evidence |
|---|---|
| Authentication | Production cannot accept a token signed with a repository-known key; wrong issuer/audience/signature/expiry rejected; application claims and service capabilities remain separate. |
| Tenant/company/branch | Reused IDs across tenants do not leak; unauthorized company/branch gives 403/404 and no writes; every join/reference is scoped. |
| Idempotency/checker | Same actor/key/body repeats once; changed payload/actor conflicts; actual maker cannot approve; forged requested identity cannot supply authority. |
| Stock | Two competing transfers/reservations cannot violate available stock; duplicate product lines aggregate; exactly paired movements; no partial write on failure; affected-row counts checked. |
| Fiscal close | Transfer/posting/correction with a closed period fails; close races do not admit late commits; all quantity/reservation/tax/payment journals reconcile. |
| Schema/roles | Clean install and upgrade both support declared endpoints; no request DDL; app role cannot migrate/access another owner; real catalog drift checked separately from EF snapshot drift. |
| Messaging/operator | Lost claim/confirmation/delivery responses, duplicates, quarantine, stale leases, replay failures and audit/idempotency commit failure preserve recoverability. |
| Ownership/cutover | All new fields survive nonempty transfer; divergent target refusal; original data retained; owned backup restores; former shared connection can be removed without fallback. |
| Statutory/export | Approved field/rule examples and effective versions; no invented defaults standing in for evidence; actual filing/integration capability tested against an agreed contract. |

## Planning assumptions and sign-off

The twelve independently deployable business backends are a fixed requirement. Team capacity, pilot date, data volumes, statutory filing scope and external provider/ERP contracts are not supplied, so this review does not invent a reliable overall completion date. Size the first stabilization/workflow tickets after reproducing the discrepancies; estimate distributed financial cutovers after the protocol spike. Any calendar plan must reserve QA/domain review, migration rehearsal and recovery testing, not just coding time.

Production sign-off requires R01–R06 resolved or the affected capability explicitly unavailable, the agreed pilot scope complete, a fresh full verification baseline, restored backups and domain acceptance. A passing suite only covers its implemented scenarios; it does not certify untested prototype routes or real-data reconciliation.
