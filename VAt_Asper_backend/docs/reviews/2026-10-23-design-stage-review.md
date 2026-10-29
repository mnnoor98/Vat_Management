Historical design-stage review. Superseded by the 24 October 2026 source review. This describes the earlier PDF-only workspace.

# Enterprise VAT Platform: Architecture Review and Delivery Plan

Review date: 23 October 2026

## Executive decision

The workspace contains twelve four-page backend design PDFs and their ZIP archive. It contains no application source, solution, migrations, automated tests, deployment configuration, or Git repository. This is a design-stage review, not a code audit or production certification. Statements below describe what is specified or missing in these documents; they do not establish runtime defects.

The documents provide a useful architectural baseline. They are not yet sufficient implementation contracts. Prioritize one complete, reconciled business transaction over creating twelve API shells. Preserve all twelve business boundaries, but decide the number of independently deployed services from team capacity, isolation needs, and measured load.

Recommended default for an initial small team: a modular core with explicit module ownership, an identity provider integration, and background workers. Keep business tables private to their module and prohibit cross-module writes. Extract services when a named owner, deployment benefit, and operational capacity justify the cost. If twelve independent services are a fixed requirement, retain the same vertical delivery order and fund the additional operational work explicitly.

## Evidence reviewed

All twelve PDFs contain four pages. Service-specific scope, tables, endpoints, events, rules, and security appear mainly in sections 1-7. Sections 8-17 provide implementation structure, illustrative envelopes, outbox pseudocode, schema principles, operations, verification, acceptance criteria, and a repeated sprint sequence.

| Document | Main evidence |
|---|---|
| [01 Identity](../../01_Identity_Service_Backend_Design.pdf) | Company-aware permissions, OIDC/PKCE, MFA and rotation; issuer/login ownership needs a decision. |
| [02 Company](../../02_Company_Service_Backend_Design.pdf) | BIN, branches and period locks; distributed closing protocol remains unspecified. |
| [03 MasterData](../../03_MasterData_Service_Backend_Design.pdf) | Versioned classifications and counterparties; historical snapshots are recognized. |
| [04 Purchase](../../04_Purchase_Service_Backend_Design.pdf) | Approval initiates stock and input-tax posting; saga tables are proposed without a full protocol. |
| [05 Sales](../../05_Sales_Service_Backend_Design.pdf) | Invoice approval, challan numbering and credit notes; reservation/posting sequence needs specification. |
| [06 Inventory](../../06_Inventory_Service_Backend_Design.pdf) | Append-only movements, atomic balance updates and negative-stock policy. |
| [07 Manufacturing](../../07_Manufacturing_Service_Backend_Design.pdf) | Frozen BOM/coefficient versions and batch reconciliation. |
| [08 VatEngine](../../08_VatEngine_Service_Backend_Design.pdf) | Effective rules, calculation snapshots, immutable ledger and reversals. |
| [09 Compliance](../../09_Compliance_Service_Backend_Design.pdf) | Return snapshots, reconciliation and filing evidence; external filing capability is not assumed. |
| [10 Workflow](../../10_Workflow_Service_Backend_Design.pdf) | Maker-checker, delegation and source-service revalidation. |
| [11 Reporting](../../11_Reporting_Service_Backend_Design.pdf) | Eventual consistency, projection checkpoints and freshness watermarks. |
| [12 Integration](../../12_Integration_Service_Backend_Design.pdf) | ERP deduplication, mappings, webhook signing and replay. |

## What to preserve

- Explicit ownership of service databases and business invariants.
- Transactional outbox and durable inbox, with local atomic writes.
- Historical classification/tax snapshots and correcting entries instead of changing posted records.
- Authorization inside each service, company/branch scope, and segregation of duties.
- Correlation IDs, reconciliation, restore drills and failure-oriented verification.
- Reporting freshness indicators and separation of reporting from transactional writes.

These are proposed controls, not verified implementations.

## Prioritized findings

P0 means resolve before financial posting or pilot data is trusted. P1 means resolve before a production pilot. P2 means expand after the core flow is proven.

| ID | Priority | Design gap and consequence | Required deliverable |
|---|---|---|---|
| R01 | P0 | Purchase approval triggers stock and tax, but participant contracts, terminal states and compensation are absent. Partial success can be mistaken for completion. | Posting sequence diagram, durable saga state, timeout/retry policy, participant result schemas and reconciliation queries. |
| R02 | P0 | Company owns period locks while posting happens elsewhere. An asynchronous lock notification alone leaves a race with in-flight writes. | Closing barrier protocol with posting admission fencing, participant acknowledgements and reconciliation. |
| R03 | P0 | Tax rules and snapshots are named, but precedence, inclusive/exclusive calculations, rounding and correction examples are not defined. | Approved rule decision tables, precision policy and independently checked example calculations. |
| R04 | P0 | Company scope is required, but tenant versus legal company, identity membership and trusted tenant resolution are unspecified. | Tenant/company/branch model; composite ownership constraints; cross-tenant negative-test matrix. |
| R05 | P0 | Workflow and document APIs both expose approval, without complete ownership and document-version binding. | Source document service owns transitions; approvals bind to document version/hash and workflow version. |
| R06 | P1 | Endpoint lists and event names lack complete typed schemas. The example data:{} envelope is explicitly illustrative. | Versioned OpenAPI, event JSON schemas, examples, errors, producer/consumer map and compatibility policy. |
| R07 | P1 | Idempotency is stated but scope, payload mismatch, retention and concurrent requests are not specified. | Tenant + operation + key scope; request hash; atomic claim/result persistence; replay and conflict behavior. |
| R08 | P1 | OIDC/PKCE and custom login/refresh endpoints coexist without naming the issuer or credential owner. | Identity architecture decision, provider selection, client flows, key rotation and permission-revocation expectations. |
| R09 | P1 | Operational controls have no workload model, numeric targets, capacity budget or responsible owner. | Agreed SLOs, load profile, alert routing, RPO/RTO and restore evidence. |
| R10 | P1 | Regulator mappings remain explicitly unvalidated. Form generation cannot establish correct filing. | Authoritative source register, field-level mappings, effective dates, reviewer sign-off and filing evidence model. |
| R11 | P1 | ERP replay and webhooks lack a full trust and recovery contract. | Credential isolation, signature/replay checks, outbound destination validation, per-row import outcomes and audited replay. |
| R12 | P2 | Twelve services repeat the same four-sprint sequence without platform dependencies or end-to-end milestones. | Dependency-based backlog with vertical acceptance gates and named owners. |

## Architecture decisions to record before scaffolding

1. **Deployment topology:** modular core or independent services, with the staffing and operating-cost reason. No cross-service database queries in the independent model; reporting consumes events or supported exports.
2. **Identity:** select an established OIDC provider; distinguish authentication from application permissions and navigation. Define browser session/token handling and service-to-service credentials.
3. **Tenant model:** distinguish SaaS tenant, legal company, registration and branch. Never treat a request companyId as authorization. Define how an authorized active company is selected.
4. **Posting ownership:** Purchase/Sales/Manufacturing own their document processes; Inventory owns quantity movements; VatEngine owns tax postings; Workflow records decisions; Company owns period governance.
5. **Financial representation:** decimal precision by quantity, price, rate and total; currency, conversion, rounding stage and business-date semantics. The generic NUMERIC(20,4) statement is insufficient for every field.
6. **Contract evolution:** event naming, schema versions, additive-change rules, deprecation period and producer/consumer compatibility checks.
7. **Document storage:** private object storage for source evidence and exports; metadata, checksum, access audit, retention and expiring downloads.
8. **Infrastructure baseline:** supported runtime/database/broker versions, pinned dependencies, reproducible development setup and environment separation. Verify support at implementation time.

## First vertical slice: domestic purchase to reconciled tax ledger

Use a narrowly agreed domestic-purchase scenario with an approved tax treatment. Do not assume every purchase is a stock receipt or every input tax amount is creditable.

1. Authorized user selects company/branch and creates a draft with supplier/product snapshots.
2. Server validates fields and obtains a deterministic tax calculation snapshot, including rule version and input hash.
3. Submit freezes the approvable document version. Amendments invalidate the previous approval request.
4. Eligible checker approves that version; maker cannot approve their own document.
5. Source module enters PostingPending, records a durable process identifier, and emits the posting request atomically.
6. Inventory records the applicable receipt, and VatEngine records the eligible tax treatment. Each participant persists inbox, result and outgoing event in its local transaction.
7. Source module marks Posted only after all required participant results match the process and document version. Reporting catches up and exposes freshness.
8. Reconciliation compares source totals and participant references. Partial results remain visible and recoverable; no automatic deletion of posted ledger rows.

Suggested states: Draft -> Submitted -> Approved -> PostingPending -> Posted. Add Rejected, CancelledBeforePosting, RecoveryRequired, ReversalPending and Reversed with explicit allowed transitions. A temporary timeout is not evidence that a participant failed to commit.

For every transition specify actor, preconditions, expected version, persisted changes, emitted event, retry behavior and audit evidence. Corrections after posting follow an approved compensating process with original-entry linkage.

## Period-close protocol

Treat closing as a coordinated financial operation, not a broadcast boolean.

- Open -> Closing stops new posting admission using a durable period fence/version.
- Every participant must enforce the fence at its local commit boundary. A cached Open flag cannot authorize a new commit after the barrier. Document how fence installation and active transactions are serialized.
- Drain or explicitly resolve previously admitted operations; collect per-participant checkpoints and acknowledgements.
- Reconcile document, inventory, tax and return totals. A timeout, unresolved posting or missing participant blocks Closed.
- Freeze the return snapshot and record the checkpoint set, rule/form versions and approval evidence.
- Reopening requires a separate privileged, audited process and an explicit correction policy.

The protocol itself needs concurrency and crash-recovery tests before acceptance.

## Contract and persistence standards

- Use typed business requests. Return 201 for a created resource, 200/204 for completed synchronous work, and 202 plus Location/status resource for asynchronous work.
- Define ProblemDetails codes for validation, scope denial, version conflict, invalid state and idempotency payload conflict.
- Use expected versions/ETags for conflicting document changes. Database constraints remain authoritative under concurrency.
- Scope business uniqueness to its true boundary: tenant/company, document kind, numbering scope, source system and external ID as appropriate.
- Store event consumer identity with event ID for inbox deduplication when multiple independent consumers share a database. Add business-operation uniqueness because different event IDs can represent the same external operation.
- Specify ordering per aggregate, stale-version handling, gap recovery, bounded retries, dead-letter ownership and replay authorization.
- Mark outbox dispatch complete only after the publication policy succeeds; retain enough metadata for diagnosis. Configure and handle unroutable publications.
- Use publisher confirms and acknowledge consumed messages after durable local processing. These are separate mechanisms; neither proves completion of the entire business process. See [RabbitMQ acknowledgements and confirms](https://www.rabbitmq.com/docs/confirms).
- Evaluate PostgreSQL row-level security as an additional tenant boundary. Use an application role that cannot bypass the intended policy and test pooled-connection tenant context. Table owners normally bypass RLS. See [PostgreSQL row security](https://www.postgresql.org/docs/current/ddl-rowsecurity.html).
- Share technical primitives sparingly; do not share mutable domain entities or a universal repository across all modules.

## Service-specific next deliverables

| Boundary | Next concrete specification |
|---|---|
| Identity | Permission catalogue, membership model, issuer integration and revocation behavior. |
| Company | Tenant/company distinction, branch grants, period barrier and registration-change history. |
| MasterData | Versioned product/counterparty schemas, effective-date overlaps and immutable transaction snapshots. |
| Purchase | Purchase categories, receipt versus invoice treatment, approval/posting saga and returns. |
| Sales | Number allocation/void history, stock reservation, posting failures and credit/debit corrections. |
| Inventory | UOM precision/conversion, reservation concurrency, valuation policy if in scope and transfer recovery. |
| Manufacturing | Batch/BOM version binding, substitutions, yield deviation approval and input/output reconciliation. |
| VatEngine | Rule precedence, calculation explanation, snapshot reproducibility and posting uniqueness. |
| Compliance | Field-level source mapping, frozen return inputs, amendment policy and filing evidence. |
| Workflow | Document-version binding, expiry/delegation, duplicate decisions and callback authorization. |
| Reporting | Projection schemas, event dependencies, freshness limits and rebuild/checkpoint procedure. |
| Integration | External ID mapping, schema validation, import row outcomes and safe replay/delivery contract. |

## Phased roadmap

Indicative planning envelope: 18-24 weeks for a constrained pilot with 3-4 experienced backend engineers, frontend support, QA/DevOps capacity and an available VAT domain reviewer. This is a planning assumption, not an estimate validated against team velocity. Manufacturing and additional integrations can materially extend delivery. Do not promise all twelve production services in this envelope.

| Phase | Indicative duration | Deliverables | Exit gate |
|---|---|---|---|
| 0: Decisions and scope | 1-2 weeks | ADRs, pilot scenario, domain glossary, legal-source review backlog, typed contracts and threat model. | Engineering and domain owners approve sample data, transaction states and boundaries. |
| 1: Foundation | 2-3 weeks | Solution/modules, identity integration, tenant authorization, PostgreSQL migrations, CI, telemetry, outbox/inbox and development setup. | Two-company isolation checks and message/crash recovery pass. |
| 2: Purchase vertical slice | 4-5 weeks | Master data subset, approval, purchase, inventory receipt, tax posting and reconciliation screen. | Duplicate/reordered events, partial failure and concurrent approval do not duplicate financial effects. |
| 3: Sales and corrections | 3-4 weeks | Invoice/challan flow, stock reservation, posting, return/credit correction and numbering audit. | Concurrent sales respect stock policy; correction preserves original history. |
| 4: Close and compliance | 3-4 weeks | Closing barrier, reconciled return snapshot, validated form mappings, evidence and exports. | Concurrent close/post tests pass; domain reviewer approves agreed reference cases. |
| 5: Pilot hardening | 3-4 weeks | ERP import subset, operating dashboards, load checks, restore drill, runbooks, migration rehearsal and UAT. | Pilot acceptance checklist passes with signed owners and recoverable data. |
| Later expansion | Separately estimated | Manufacturing, additional ERP adapters, richer workflow, analytics and justified service extraction. | Each feature meets its own domain and operational acceptance gates. |

## Initial implementation backlog

| Order | Work item | Accountable role | Acceptance evidence |
|---|---|---|---|
| 1 | Confirm pilot scope, deployment topology and domain glossary | Product + technical lead | Recorded decisions and explicit exclusions. |
| 2 | Define tenant/company/branch and permission matrix | Technical lead + security owner | Two-company access cases and trusted context specification. |
| 3 | Approve calculation examples and rule source register | VAT domain owner | Reviewed expected outputs with rule/effective-date references. |
| 4 | Specify purchase states, participant schemas and close fence | Backend lead | Reviewed sequence/state diagrams and failure table. |
| 5 | Establish build, migration and CI baseline | Backend + DevOps | Clean checkout builds; isolated database migrates. |
| 6 | Implement idempotency and outbox/inbox foundation | Backend | Concurrent retry and crash-boundary verification. |
| 7 | Deliver purchase flow through reporting | Backend + frontend + QA | Demonstrable reconciled transaction with audit history. |
| 8 | Add recovery dashboard and repair runbook | Backend + operations | Operator resolves a deliberately interrupted posting. |

## Pilot acceptance gates

- Zero cross-company data disclosure in agreed read/write/export/background-job tests.
- No self-approval; stale document approval cannot post an edited document.
- No duplicate stock/tax business effect after retries, redelivery, process restarts or imported duplicates.
- Outbox/inbox and migrations verified against real PostgreSQL and the chosen broker, not only mocks.
- Tax results match all approved reference examples; historical recalculation uses recorded versions.
- Closing cannot complete while admitted posting remains unresolved; no unauthorized post-close commit.
- Source documents, participant ledgers and frozen return totals reconcile with explainable differences only.
- A backup restores successfully, with measured recovery evidence and owner approval.
- OpenAPI/event compatibility checks, secret scanning and relevant dependency/security checks pass.
- Operators can diagnose a failed process from correlation ID and follow an audited recovery procedure.

Proposed targets for stakeholder agreement: p95 ordinary interactive reads below 500 ms under the agreed load; p95 ordinary posting completion below 30 seconds under healthy dependencies; projection lag below 60 seconds; recovery point within 15 minutes and recovery time within 4 hours. These are candidate requirements, not observed performance or existing guarantees. Define availability and peak-load targets before infrastructure sizing.

## Regulatory and review limits

No current VAT rate, legal deadline, form correctness or government API capability is asserted by this review. Before pilot use, the domain reviewer must validate authoritative NBR law/rules/notices, their effective dates, applicability and form mappings. Store the reviewed evidence with each published rule/form version. Manual filing with recorded evidence remains a valid scoped workflow until an actual integration contract is verified.

No build, application test, load test or vulnerability scan could be run because there is no application code in this workspace. The next engineering milestone is an approved contract set and one executable vertical slice, followed by evidence-based review of the actual implementation.
