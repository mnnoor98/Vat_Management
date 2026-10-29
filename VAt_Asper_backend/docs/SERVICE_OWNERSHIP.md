# Service core and database ownership

Status on 2026-10-24: all twelve APIs are independently hosted. **Identity, Workflow, Compliance, Manufacturing and Integration have independent application/domain/persistence projects and owned databases. Five of twelve services now have independent core/data ownership; seven financial participants remain coordinated.** See [Integration operator cutover](architecture/0025-independent-integration-operators.md).

## Ownership matrix

Current financial chain update: migration **18** adds the paired transfer journal and adopts Inventory prototype metadata. Runtime access to this journal is append-only for supported transfer posting; UPDATE/DELETE/TRUNCATE remain denied. See [P0 stabilization and data recovery](architecture/0023-p0-auth-inventory-recovery.md); earlier verification paragraphs retain their historical 17-migration counts.

Migration **19** adds durable, runtime append-only source snapshots, initially for the explicit Purchase/reversal scope. See [Reporting source export V1](architecture/0027-reporting-source-export.md). Customer history now uses the same immutable store with a separate versioned scope. Reporting's persistent projection database cutover remains pending; ownership remains five independent business cores and seven coordinated financial APIs.

Reporting now additionally has a [separate staged financial snapshot core](architecture/0028-reporting-snapshot-generation-core.md), [customer history snapshot bootstrap/projections](architecture/0029-reporting-customer-history-snapshots.md), a [financial generation inbox](architecture/0030-reporting-financial-generation-inbox.md), and [source-export coverage proofs](architecture/0031-reporting-export-coverage-proof.md), with four owned migrations. Its live API is not switched to that core. Production ownership counts therefore remain five independent APIs and seven coordinated financial APIs; there is no persistent Reporting database cutover yet.

Financial migration **20** adds [source commit checkpoints and immutable export boundaries](architecture/0032-reporting-source-commit-checkpoints.md). Historical source rows have an explicitly marked legacy baseline; future financial outbox inserts receive transactionally ordered positions. Reporting's contiguous source cursor, customer live coverage and runtime cutover remain pending.

The [bounded journal read API](architecture/0033-reporting-bounded-source-journal.md) now exposes immutable financial positions with explicit epoch/boundary, original envelopes and byte/gap guards. This adds no migration or runtime owner cutover; the contiguous consumer cursor remains pending.

| Service | Core build | Database | Migration ownership |
|---|---|---|---|
| Identity | Independent Domain/Application/Infrastructure projects | `vat_identity` | `IdentityDbContext`, own history |
| Company | Existing financial layer build bridge | `vat_management` | `VatDbContext`, shared 20-migration chain |
| MasterData | Existing financial layer build bridge | `vat_management` | `VatDbContext`, shared 20-migration chain |
| Purchase | Existing financial layer build bridge | `vat_management` | `VatDbContext`, shared 20-migration chain |
| Sales | Existing financial layer build bridge | `vat_management` | `VatDbContext`, shared 20-migration chain |
| Inventory | Existing financial layer build bridge | `vat_management` | `VatDbContext`, shared 20-migration chain |
| Manufacturing | Independent Domain/Application/Infrastructure projects | `vat_manufacturing` | `ManufacturingDbContext`, own history |
| VatEngine | Existing financial layer build bridge | `vat_management` | `VatDbContext`, shared 20-migration chain |
| Compliance | Independent Domain/Application/Infrastructure projects | `vat_compliance` | `ComplianceDbContext`, own history |
| Workflow | Independent Domain/Application/Infrastructure projects | `vat_workflow` | `WorkflowDbContext`, own history |
| Reporting | Existing financial layer build bridge | `vat_management` | `VatDbContext`, shared 20-migration chain |
| Integration | Independent core; delegated owner operator/transport APIs | `vat_integration` only | `IntegrationDbContext`, own history |

## Extracted Identity boundary

```text
src/Services/Identity/
  Domain/Vat.Identity.Domain.csproj
  Application/Vat.Identity.Application.csproj
  Infrastructure/Vat.Identity.Infrastructure.csproj
    IdentityDbContext.cs
    IdentityDataTransfer.cs
    Migrations/
  Vat.Identity.Api/
```

The Identity API depends on its own core plus `Vat.Web` and `Vat.SharedKernel`; it does not reference the financial `Vat.Hosting`, `Vat.Application`, `Vat.Domain`, `Vat.Infrastructure` or compatibility API assemblies. Common HTTP hosting contains authentication, correlation/error handling and an injected readiness interface. The shared kernel contains primitives, verified scope claims, permission names and wire metadata records, not stock/tax/payment business code. Workflow, Compliance and Manufacturing follow the same independent dependency boundary. Seven APIs retain financial composition, and Integration retains it only for messaging. The aggregate compatibility API explicitly composes all five owned metadata contexts separately.

Identity owns nine tables in `vat_identity`: users, roles, permissions, user_roles, role_permissions, menu_items, role_menus, refresh_sessions and security_audit. Its migration history is `identity.__IdentityMigrationsHistory`, separate from the source history. Tenant/company keys, internal scope-preserving foreign keys, uniqueness, optimistic versions and existing checks are retained. Company and branch UUIDs are external references, not cross-database foreign keys. Any future Identity write workflow must validate those references through approved service contracts/reference projections.

The existing `identity` tables in `vat_management` are retained as historical compatibility data because `company.branch_access` still has references to them. They are not the new API's data source. The financial runtime role has no access to that legacy schema. No ongoing replication or dual-write mechanism is implied. Company access-reference projections must be implemented before enabling new cross-owner grant workflows or removing those old tables.

Role reads and verified identity profiles retain their routes and response contracts. OIDC remains external; local users/roles do not automatically change issuer claims. This extraction does not introduce a password server, role-management commands or refresh-token execution.

## Extracted Workflow boundary

Workflow owns Domain/Application/Infrastructure projects and seven tables in `vat_workflow`: workflow_definitions, workflow_versions, approval_instances, approval_tasks, approval_actions, delegations and escalation_rules. `WorkflowDbContext` uses `workflow.__WorkflowMigrationsHistory`. Every internal foreign key retains tenant/company scope. Company, issuer subject and source-document IDs are external references. The task metadata route is unchanged; source approve/reject commands still belong to the financial APIs. No new generic approval execution or replication is implied.

`tools/Vat.Workflow.Migrator` applies only the owned migration and transfers a locked, consistent snapshot using the same binary-copy/fingerprint/refusal rules described below. Legacy source Workflow tables remain historical compatibility data; the financial runtime role's access to them is revoked. Source migration history remains unchanged. Local provisioning uses `Initialize-LocalWorkflowRoles.ps1`, and owned updates use `Update-WorkflowDatabase.ps1`. The runtime role can SELECT tasks/history only; the owner manages migrations. Both credentials use the existing encrypted local profile. See [decision and verification](architecture/0016-owned-workflow-context.md).

The local project now has six persistent databases: `vat_management`, `vat_identity`, `vat_workflow`, `vat_compliance`, `vat_manufacturing`, and `vat_integration`. Full API/core isolation is now **5 of 12**; Integration owns only its metadata database and uses remote owner contracts for messaging.

The initial local Workflow cutover verified **7 tables and 0 existing rows**. The nonempty transfer integration test separately verifies the complete definition/version/instance/task graph and Unicode text. Its ownership migration is `20261023150120_WorkflowOwnership`; the source remains on its original 17 migrations. Operator verification manifests are under ignored `artifacts/ownership`.

## Extracted Compliance boundary

Compliance owns Domain/Application/Infrastructure projects and eight tables in `vat_compliance`: form_templates, form_versions, return_periods, vat_returns, return_lines, reconciliation_runs, filing_evidence and period_close_requests. `ComplianceDbContext` owns `compliance.__ComplianceMigrationsHistory` and migration `20261023151453_ComplianceOwnership`. Internal relationships preserve tenant/company keys; Company/fiscal-period IDs are external references with scoped lookup indexes. Maker/checker, state, temporal, version, evidence-hash, numeric precision and JSONB constraints are retained.

The return metadata route and response are unchanged. No filing/calculation/evidence-upload or financial-close execution is introduced. Future commands need Company period/version validation, frozen ledger projections, evidence integrity and cross-owner close coordination. See [Compliance decision](architecture/0017-owned-compliance-context.md).

`tools/Vat.Compliance.Migrator` applies only the owned migration, locks and copies all eight source tables in dependency order, verifies counts/fingerprints, accepts identical reruns and refuses divergent targets. Retain legacy source tables as historical compatibility data. The financial runtime role no longer has access to that schema. No replication or dual writing is provided. The initial local cutover verified **8 tables and 0 existing rows**; all-eight-table nonempty transfer tests separately cover correction references, dates/timestamps, decimal precision, JSONB and quoted/multiline Bengali text.

```powershell
# Provide operator source and target connections securely in this process:
# ConnectionStrings__VatDatabase -> vat_management
# ConnectionStrings__ComplianceDatabase -> vat_compliance
dotnet run --project tools/Vat.Compliance.Migrator --configuration Release --no-build --no-restore
# Then provision runtime/owner roles with the local administrator's PGPASSWORD:
./scripts/Initialize-LocalComplianceRoles.ps1
```

## Extracted Manufacturing boundary

Manufacturing owns Domain/Application/Infrastructure projects and seven tables in `vat_manufacturing`: bom_headers, coefficient_versions, bom_lines, production_orders, material_issues, production_receipts and wastage_records. `ManufacturingDbContext` uses `manufacturing.__ManufacturingMigrationsHistory` and migration `20261023155815_ManufacturingOwnership`. Internal foreign keys preserve tenant/company scope. Company, product and warehouse IDs are external references; product/warehouse lookup and business-date indexes are retained. Quantities retain numeric(24,6), positive-quantity/wastage checks, scoped uniqueness, maker/checker rules, state/date checks and optimistic versions.

The production-order metadata route remains unchanged. Production execution, stock issue/receipt and wastage posting commands are not implemented. They require MasterData/Inventory reference validation, fiscal-period admission, immutable stock journals and versioned posting/reconciliation contracts. See [Manufacturing decision](architecture/0018-owned-manufacturing-context.md).

`tools/Vat.Manufacturing.Migrator` migrates and verifies a locked source snapshot with dependency-ordered binary COPY, count/fingerprint verification, identical reruns and refusal to overwrite a divergent target. Historical source Manufacturing tables remain; the financial runtime role has no access to them after provisioning. No replication or dual writing is provided. The initial local cutover verified **7 tables and 0 existing rows**. Tests separately populate all seven tables and verify decimal/date and quoted multiline Bengali preservation.

```powershell
# Provide operator connections through the environment:
# ConnectionStrings__VatDatabase -> vat_management
# ConnectionStrings__ManufacturingDatabase -> vat_manufacturing
dotnet run --project tools/Vat.Manufacturing.Migrator --configuration Release --no-build --no-restore
# Then use the local administrator's PGPASSWORD for role provisioning:
./scripts/Initialize-LocalManufacturingRoles.ps1
```

## Integration owner boundary (updated 24 October 2026)

Integration owns independent metadata Domain/Application/Infrastructure projects and eight tables in `vat_integration`: integration_connections, field_mappings, inbound_batches, inbound_rows, external_references, webhook_subscriptions, delivery_attempts and dead_letters. `IntegrationDbContext` has its own `integration.__IntegrationMigrationsHistory` and `20261023162111_IntegrationMetadataOwnership`. Internal scoped foreign keys, same-connection mapping enforcement, JSONB, uniqueness, digest/attempt/status checks, maker/checker rules and optimistic versions are retained. Company/source/event IDs are external references. The typed metadata reader replaces the final generic module read store; the obsolete generic contract/store was removed.

**Integration now has independent API/core/data ownership.** Its public messaging routes delegate the original user JWT, command key and correlation ID to versioned owner APIs. Its enabled broker worker uses remote outbox/inbox contracts only. The host requires only `ConnectionStrings__IntegrationDatabase`; readiness checks only that owned history. The catalogue labels it `independent`, and the launcher strips the financial connection from its process. See [operator contracts and deployment](architecture/0025-independent-integration-operators.md).

`tools/Vat.Integration.Migrator` transfers a locked source snapshot of the eight metadata tables with binary COPY and count/fingerprint verification. Identical reruns are accepted; divergent nonempty targets are refused. It does not move platform outbox or reporting inbox rows. Retain historical source metadata tables and revoke financial runtime access to that legacy schema. No replication/dual writing is provided. Freeze legacy writers before initial cutover.

```powershell
# Supply operator source/target connections through environment secrets:
# ConnectionStrings__VatDatabase -> vat_management
# ConnectionStrings__IntegrationDatabase -> vat_integration
dotnet run --project tools/Vat.Integration.Migrator --configuration Release --no-build --no-restore
./scripts/Initialize-LocalIntegrationRoles.ps1
./scripts/Update-IntegrationDatabase.ps1
```

The initial local metadata cutover verified **8 tables and 0 existing rows**. Nonempty transfer tests separately populate every metadata table and verify boolean/JSONB/date/text preservation, identical reruns and divergent-target protection.

The metadata runtime role is limited to connection/history SELECT; the owner runs migrations. The API separately retains its existing financial runtime credential for messaging, so the whole host is not read only. ERP import execution and webhook delivery commands remain unimplemented. [Decision and concrete next contracts](architecture/0019-owned-integration-metadata.md) define source-owner claims/confirmation/failure, Reporting inbox ownership, operator retry/replay audit/idempotency and required failure tests before removing the financial bridge.

## Initial Identity data cutover

The operator-only `tools/Vat.Identity.Migrator` reads administrator-provided source and target connection strings from the environment. It creates a distinctly named target, applies only Identity migrations, then copies the nine tables without writing/deleting source data. Source share locks and repeatable-read isolation hold a consistent snapshot. Target data imports run in one transaction, in dependency order, with PostgreSQL binary COPY. All table row counts and canonical row fingerprints must match before commit. An already identical target is accepted; a nonempty divergent target is refused rather than overwritten. No private row data is written to transfer logs or SQL files.

```powershell
# Supply administrator/migration connections securely for this initial operation.
# ConnectionStrings__VatDatabase -> vat_management
# ConnectionStrings__IdentityDatabase -> vat_identity
./scripts/Database-Local.ps1 -Action Backup -VerifyRestore
dotnet run --project tools/Vat.Identity.Migrator --configuration Release --no-build --no-restore
```

The initial local cutover verified 9 tables and **0 existing Identity rows**. Source financial data and its 17 migrations remain in place; Identity has its own initial ownership migration. Verification manifests are under ignored `artifacts/ownership`.

Use the transfer tool only for initial cutover or a deliberately verified repeat, not as a synchronization job. After Identity starts owning writes, source copies will be stale and must not be used to overwrite the owned database.

## Local credentials and SQL access boundaries

`Initialize-LocalDatabaseRoles.ps1` uses a local administrator supplied through `PGPASSWORD`. It provisions:

| Role | Purpose |
|---|---|
| `vat_identity_owner` | Owns the Identity database/schema/tables; used for owned migrations |
| `vat_identity_app` | Connects to Identity and reads only role metadata and migration history needed by the current API |
| `vat_financial_app` | Uses coordinated financial schemas; cannot access owned Identity/Workflow/Compliance/Manufacturing/Integration-metadata databases or their legacy schemas |
| `vat_workflow_owner` | Owns the Workflow database/schema/tables; used for owned migrations |
| `vat_workflow_app` | Reads Workflow task metadata and its migration history only |
| `vat_compliance_owner` | Owns the Compliance database/schema/tables; used for owned migrations |
| `vat_compliance_app` | Reads return metadata and its migration history only |
| `vat_manufacturing_owner` | Owns the Manufacturing database/schema/tables; used for migrations |
| `vat_manufacturing_app` | Reads production-order metadata and migration history only |
| `vat_integration_owner` | Owns Integration metadata database/schema/tables; runs owned migrations |
| `vat_integration_app` | Reads connection metadata/history; distinct from the financial messaging credential |

None has superuser, CREATEDB, CREATEROLE, replication or bypass-RLS privileges. Existing source users retain their existing access. All five owned metadata databases' public access is revoked; each grants access only to its own owner/runtime roles. The administrator remains available for recovery. Current financial runtime grants cover its existing schema tables; rerun provisioning after adding coordinated tables that require runtime access.

Generated passwords are stored as Windows DPAPI-encrypted PSCredential XML under ignored `artifacts/local-secrets`, readable/decryptable by the original Windows account. They are never printed or placed in source configuration. This is a local-development convenience, not a portable secret store or an isolation boundary against other processes running as the same Windows account. For containers/servers, provide separate service credentials through your deployment secret system. DPAPI files cannot be moved to another machine/account as plaintext recovery credentials; restore data and provision new local credentials there.

```powershell
# After initial migration, with the administrator's PGPASSWORD in this process:
./scripts/Initialize-LocalDatabaseRoles.ps1
# After Workflow migration/cutover:
./scripts/Initialize-LocalWorkflowRoles.ps1
# After Compliance migration/cutover:
./scripts/Initialize-LocalComplianceRoles.ps1
# After Manufacturing migration/cutover:
./scripts/Initialize-LocalManufacturingRoles.ps1
# After Integration metadata migration/cutover:
./scripts/Initialize-LocalIntegrationRoles.ps1
. ./scripts/Import-LocalConnections.ps1 -Force
./scripts/Test-LocalDatabaseRoles.ps1
# Set Authentication__Authority and optional audience/CORS before starting APIs:
./scripts/Start-Services.ps1 -Published
```

The start script loads the local encrypted profile when present, without overriding explicitly supplied environment values. Each child receives only its declared runtime connection settings (Integration receives only its owned metadata connection); other database/migration connection strings and PostgreSQL operator password variables are removed before process creation and restored in the parent afterward. Do not override runtime connections with administrator credentials.

Owned schema updates use the owner credential transiently, restoring the runtime connection afterward:

```powershell
./scripts/Update-IdentityDatabase.ps1
./scripts/Update-WorkflowDatabase.ps1
./scripts/Update-ComplianceDatabase.ps1
./scripts/Update-ManufacturingDatabase.ps1
./scripts/Update-IntegrationDatabase.ps1
```

`Database-Local.ps1` continues to migrate/verify only the financial context; it refuses to apply that chain to `vat_identity`, `vat_workflow`, `vat_compliance`, `vat_manufacturing` or `vat_integration`. Status and backup can inspect all six databases. Source, Identity, Workflow, Compliance, Manufacturing and Integration metadata backups were restored into separate temporary databases successfully; those rehearsal databases were then removed. Dumps/verification files remain under ignored `artifacts/backups`.

## Packaging and deployment

`services.json` records each API's database connection key and current ownership state. Identity uses `ConnectionStrings__IdentityDatabase`, Workflow uses `ConnectionStrings__WorkflowDatabase`, Compliance uses `ConnectionStrings__ComplianceDatabase`, Manufacturing uses `ConnectionStrings__ManufacturingDatabase`, Integration metadata uses `ConnectionStrings__IntegrationDatabase`, and seven coordinated APIs use `ConnectionStrings__VatDatabase`. Integration has no financial connection; configure its trusted operator origins and enabled remote worker adapters instead. Compose requires `VAT_IDENTITY_DATABASE_CONNECTION`, `VAT_WORKFLOW_DATABASE_CONNECTION`, `VAT_COMPLIANCE_DATABASE_CONNECTION`, `VAT_MANUFACTURING_DATABASE_CONNECTION` and `VAT_INTEGRATION_DATABASE_CONNECTION` separately from `VAT_DATABASE_CONNECTION`. The optional compatibility host needs all six connections.

Publishing uses a fresh staging directory, checks independent services for forbidden financial assemblies, writes a manifest, and swaps the completed package into place. Previous generated packages are retained as hidden `.previous-*` directories for rollback. This avoids leaving old financial DLLs in an independently extracted service's package. Build/publish before restarting a deployment; do not treat package replacement as a zero-downtime deployment mechanism.

## Verification

```powershell
./scripts/Test-Local.ps1 -Target tests/Vat.Identity.Tests/Vat.Identity.Tests.csproj -ResultsDirectory TestResults/identity
./scripts/Test-Local.ps1 -Target tests/Vat.Workflow.Tests/Vat.Workflow.Tests.csproj -ResultsDirectory TestResults/workflow-owned-verified
./scripts/Test-Local.ps1 -Target tests/Vat.Compliance.Tests/Vat.Compliance.Tests.csproj -ResultsDirectory TestResults/compliance-owned
./scripts/Test-Local.ps1 -NoBuild -Target tests/Vat.Manufacturing.Tests/Vat.Manufacturing.Tests.csproj -ResultsDirectory TestResults/manufacturing-owned
./scripts/Test-Local.ps1 -NoBuild -Target tests/Vat.Integration.Tests/Vat.Integration.Tests.csproj -ResultsDirectory TestResults/integration-metadata-owned
./scripts/Test-Local.ps1 -NoBuild -ResultsDirectory TestResults/ownership-full
./scripts/Test-LocalDatabaseRoles.ps1
./scripts/Test-ServiceHosts.ps1 -Published -DatabaseReady
```

Identity tests run with no Company/financial tables. They verify schema/migration ownership, authorization, tenant/company fences, cursor reads, null-aware grant uniqueness, internal foreign keys, binary transfer of Unicode/quoted/multiline data, repeatable imports, and refusal to overwrite divergent target data. The full run after core/HTTP extraction passed **191 existing tests plus 5 Identity tests**, with 2 live RabbitMQ tests skipped locally. Runtime-role SQL checks and all twelve published hosts' database readiness also passed. Temporary test/smoke databases and processes were cleaned up.

Workflow extraction verification passed **4 standalone integration tests** (`TestResults/workflow-owned-verified`) and **23 existing module API/foundation database tests** (`TestResults/workflow-compatibility`). The final solution builds with zero warnings/errors. Financial and Workflow contexts report no pending model changes; runtime-role boundary checks and owner migration checks passed. All twelve freshly published APIs passed liveness, exact route ownership, authentication and database readiness with runtime credentials. Purchase restarted while Sales remained live; smoke processes were cleaned up. Ten financial packages contain no Workflow core assemblies, and the independent Workflow package contains no financial bridge assemblies. The prior 196-test full run above predates Workflow extraction; the current change was verified with the focused suites.

Compliance extraction verification passed **4 standalone integration tests** (`TestResults/compliance-owned`) and **23 existing API/foundation tests** (`TestResults/compliance-compatibility`). The solution builds with zero warnings/errors. Financial and Compliance contexts report no pending model changes. Local role provisioning is repeatable, runtime permission checks passed, and owner migration execution reports the owned database up to date. Both the source backup and new Compliance backup passed restore rehearsals; their temporary databases were removed. The new package contains only its own core plus common Web/SharedKernel assemblies; the nine financial packages contain no Compliance core assemblies. All twelve freshly published hosts passed liveness, exact routes, authentication and database readiness with runtime credentials; Purchase restarted while Sales remained live. Temporary smoke processes were cleaned up. These are focused checks for this extraction; the earlier full-suite result predates it.

Manufacturing extraction verification passed **4 standalone integration tests** (`TestResults/manufacturing-owned`) and **25 existing regression tests** (`TestResults/manufacturing-compatibility`): 23 API/foundation cases plus purchase stock/tax posting and sales stock posting/fiscal-close checkpoint flows. The solution builds with zero warnings/errors. Financial and Manufacturing model snapshots report no pending changes. Owner migration execution and repeatable local role provisioning passed; all five profile entries remain intact. Runtime access is limited to order/history reads and cross-owner access is denied. Source and new Manufacturing backup restore rehearsals passed and their temporary databases were removed. The new package has no financial bridge libraries; eight financial packages have no Manufacturing core assemblies. All twelve freshly published APIs passed liveness, exact route ownership, authentication and database readiness with runtime credentials. Purchase restarted while Sales remained live; temporary smoke processes were cleaned up. These are focused checks; earlier full-suite results predate this extraction.

Historical metadata extraction verification (before the independent operator cutover) passed **4 standalone tests** (`TestResults/integration-metadata-owned`) and **38 existing API/foundation/reporting-messaging tests** (`TestResults/integration-metadata-compatibility`). Standalone metadata works without Company/financial tables; combined host readiness correctly returns 503 without financial migrations. Regressions cover outbox claims, confirmation/failure/retry/lease recovery and inbox duplicate handling, quarantine/replay/audit. Both model snapshots report no pending changes. The final solution builds with zero warnings/errors. The initial local cutover verified eight tables/zero rows; runtime boundaries and owner migrations passed. Source and metadata backup restores passed and temporary databases were removed. Other eleven published APIs contain no Integration metadata core assemblies; the Integration package intentionally retains financial libraries for messaging. All twelve freshly published APIs passed liveness, exact route ownership, authentication and database readiness with runtime credentials, including Integration's two-database readiness. Purchase restarted while Sales remained live and the smoke processes were cleaned up. Full API isolation remains incomplete for Integration.

The concurrent Inventory additions were preserved, their three nonexistent Product-set references were corrected, and route inventories were synchronized with current controller attributes. The new transfer business flow was not validated by these tests; see the scope note in [decision 0019](architecture/0019-owned-integration-metadata.md).

## Versioned dispatch source port

The active dispatch loop now lives in independent `src/Vat.Messaging`, with no EF/Npgsql/financial project dependency. It uses `IOutboxLeaseSourceV1`; financial Infrastructure supplies `FinancialOutboxLeaseSourceV1`, and hosting wires the worker to that adapter. V1 receipts carry source owner, event/tenant/company IDs, protocol version and lease token. Claims include aggregate/version, original event type/timestamp, stored payload bytes and SHA-256. RabbitMQ still receives the original event ID/type/JSON bytes, with a payload copy protecting the claim from publisher mutation.

Source settlement now rejects expired leases even before reclaim, wrong tenant/company/event/token/owner/protocol, and repeated/stale confirmations. Retry/quarantine derives from the owner row's attempt state. Failed claim persistence rolls back; host cancellation leaves its lease for recovery. Broker confirmation with a lost/expired source acknowledgement still permits same-event redelivery, so inbox deduplication remains required. See [decision 0020 and remaining transport work](architecture/0020-versioned-outbox-dispatch.md).

This is an executing **in-process port**, not remote source-owner transport. Integration's financial connection, Reporting inbox and operator retry/replay bridge remain. Database schema/count and the 4-of-12 fully isolated API/core status are unchanged. The financial source still has its 17 migrations, with no pending model changes from this step.

Verification passed **11 independent dispatch tests** (`TestResults/outbox-port-unit`) and **51 PostgreSQL/API regression cases** (`TestResults/outbox-port-postgres`), including 11 source lease cases plus metadata/foundation, outbox/inbox and purchase/sales posting/close paths. The solution builds with zero warnings/errors. These are focused suites; they do not constitute a new full-suite or live RabbitMQ run.

All twelve freshly published APIs passed liveness, exact route ownership, authentication and database readiness with runtime credentials. Purchase restarted while Sales remained live; smoke processes were cleaned up. Package checks confirmed that the four independent APIs exclude Vat.Messaging and the eight financial/transitional hosts include it.

## Authenticated remote dispatch source

Integration can now select `OutboxSource:Mode=Remote` and resolve `HttpOutboxLeaseSourceV1` from independent `Vat.Messaging.Http`. The separate FinancialOutbox technical host exposes V1 claim/confirm/fail over HTTPS with a dedicated service credential; user JWTs do not satisfy its authentication policy. It is an adapter for the existing coordinated source, not a new business module/database. The twelve business API catalogue entries remain unchanged. The launcher scopes server settings to FinancialOutbox and client settings to Integration, and publish/start support the explicit `-IncludeFinancialOutbox` switch.

Local mode remains the default. Inbox processing and operator operations still use the financial connection, so this does not increase the four fully independent APIs or six persistent databases. See [transport configuration, failure semantics and remaining ownership](architecture/0021-authenticated-outbox-source-transport.md).

Remote transport verification passed **83 focused tests**: 19 HTTP cases, 11 dispatch cases and 53 PostgreSQL/API regressions including two real loopback HTTP/source settlement cases. The solution built with zero warnings/errors. The published technical host passed service-authentication, controller-isolation and runtime database-readiness checks; published package boundaries and changed PowerShell syntax checks passed. This does not claim a live broker/Docker or full-suite run.

All twelve freshly published business APIs passed liveness, exact routes, authentication and database readiness with runtime credentials. Purchase restarted while Sales remained live; technical/business smoke processes were cleaned up and probe environment restored.

## Receipt-gated Reporting delivery

The broker transport and worker now live in independent `Vat.Messaging.RabbitMq`, without financial/EF/Npgsql dependencies. Integration's worker uses a V1 inbox target and accepts only a terminal commit receipt matching the original delivery fingerprint before completing the broker callback. `InboxTarget:Mode=Remote` selects authenticated HTTP delivery to Reporting's explicitly enabled service-only endpoint. Local mode remains available to the aggregate compatibility host; independent Integration requires Remote. The internal Reporting endpoint is disabled by default.

ReportingInbox returns broker receipts only after its own transaction commits and refuses an outer transaction on that entry point. Operator replay retains the original processor inside its audit/idempotency transaction. Oversized broker messages use a bounded prefix plus original length/hash, preserving the original quarantine fingerprint and truncation policy. The launcher scopes InboxServer secrets to Reporting and InboxTarget secrets to Integration. See [delivery contracts, configuration and remaining storage ownership](architecture/0022-reporting-delivery-commit-receipts.md).

That transport milestone moved the processing call and broker worker boundary without moving Reporting tables. The subsequent operator cutover removes Integration's financial connection; current counts are six persistent databases and five independent business API/core/data owners.

Verification passed **128 focused tests**: 28 messaging, 41 HTTP and 59 PostgreSQL/API cases (`TestResults/inbox-receipt-unit`, `inbox-receipt-http`, `inbox-receipt-postgres`). These cover durable receipt visibility, duplicate delivery, failed commit rollback, oversized quarantine compatibility, ambient-transaction refusal, broker assembly independence and existing posting/operator paths. The solution built with zero warnings/errors; disposable PostgreSQL stopped cleanly. Published package boundaries, changed PowerShell syntax checks and the Reporting delivery host's service-authentication/database-readiness probes passed. These are focused results, not a live RabbitMQ/Docker or new full-suite run.

The FinancialOutbox probe also passed. The final twelve-host run passed liveness, exact route inventory, authentication and database readiness; Purchase restarted while Sales stayed live and smoke processes were cleaned up. A concurrent Manufacturing coefficient route mismatch was resolved by synchronizing inventories, and its privileged financial connection fallback was removed in favor of the owned runtime key. The final solution rebuild passed and Manufacturing was republished. Coefficient business writes remain outside the 128-test certification and need a separate security/ownership review; see the [scope note](architecture/0022-reporting-delivery-commit-receipts.md#concurrent-manufacturing-scope).

## Remaining extraction work

Earlier Identity composition validation also passed 17 existing module API tests and all 5 independent Identity tests (`TestResults/identity-cleanup-verified`). Those Identity/financial contexts reported no pending model changes. The Identity transfer test explicitly verifies that a divergent target remains unchanged and that all target connections close before its temporary database is dropped.

Seven financial business services remain coordinated; Integration has been isolated through owner APIs. Those seven services are not fully isolated yet. Their foreign keys, transactional stores, read projections and posting/closing fences must be replaced with versioned service contracts before data cutover. Financial posting spans source documents, inventory, tax, audit and outbox; it needs explicit posting sagas, retry/compensation, source-version checks and reconciliation while preserving the existing journals and fiscal-close guarantees. Merely copying those tables or pointing each current API at an empty database would break existing behavior.

Next candidates should be evaluated by their actual dependency graph. The remaining Company/MasterData and financial contexts require reference projections and cross-owner write contracts. Source outbox persistence and customer reporting remain financial participants and require explicit owner/export/rebuild contracts for further extraction. Each cutover must include nonempty transfer tests, least-privilege runtime access, owned migrations, compatibility validation and a rehearsed backup/recovery path.

## Transfer/coefficient workflow update — 24 October 2026

Manufacturing now owns ten application tables and a second migration. Its runtime can execute the restricted coefficient workflow; snapshot, command and audit history is append-only. Source financial tables remain inaccessible. Inventory transfer commands use the coordinated financial transaction and an append-only paired journal; Inventory has not been extracted. See [workflow contracts and current limitations](architecture/0024-transfer-coefficient-workflows.md). Earlier read-only role/test counts above are historical extraction milestones.
