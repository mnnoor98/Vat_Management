Current ownership update: the project now has **six persistent databases**: `vat_management` (financial/coordinated contexts), `vat_identity` (independent Identity), `vat_workflow` (independent Workflow), `vat_compliance` (independent Compliance), `vat_manufacturing` (independent Manufacturing), and `vat_integration` (independent Integration metadata). Integration messaging delegates to financial owner APIs; Integration has no financial database connection. The original Identity/Workflow/Compliance/Manufacturing/Integration metadata tables remain in the source for historical compatibility. See [service ownership](SERVICE_OWNERSHIP.md). The counts below describe the original coordinated schema.

# Database coverage for the 12 planned modules

Current source schema update (24 October 2026): `vat_management` has **19 migrations and 98 application tables** (99 including EF history). Migration 19 adds runtime append-only `platform.reporting_source_exports`; migration 18 added the paired transfer journal. Both pre- and post-migration backup restores and the runtime role checks passed. Source snapshots cover Purchase/reversal bootstrap only; Reporting has not yet moved to its own database. See [source export V1](architecture/0027-reporting-source-export.md). The 23 October counts below are historical.

The original coordinated deployment used **one database, `vat_management`**, with module schemas. Seven API hosts still share this database/core in full; Integration delegates financial messaging through owner APIs; see [service architecture](SERVICE_ARCHITECTURE.md). The schema changes did not move existing data. There are 12 logical modules and 13 application schemas because `platform` holds shared infrastructure and `masterdata` remains a separate schema.

## Coverage and ownership

| Planned module | Existing authoritative persistence | Added foundations |
|---|---|---|
| Identity | External OIDC issuer and API permission claims | Company directory, roles, permissions, branch grants, menus, refresh-session metadata, security audit |
| Company | Companies, branches, fiscal periods and frozen close snapshots | Effective registrations, settings, branch access |
| Master Data | Products, suppliers; customers in `sales.customers` | Product versions, UOMs, effective HS codes and classifications, counterparty registrations |
| Purchase | Purchases/lines, posting plans, reversals, purchase stock movements | Attachment metadata and posting saga records |
| Sales | Invoices/lines, customer snapshots, fulfillments, posting journals, credits/debits, payments and corrections | Delivery evidence and internal challan number allocation/void history |
| Inventory | Warehouses, stock balances/movements; reservations in sales fulfillments | Transfer and adjustment headers/lines |
| Manufacturing | None previously | BOM headers/lines, effective coefficients, production orders, material issues, output receipts and wastage |
| VAT Engine | Published tax-rule versions, purchase input ledger; output tax in sales posting entries | Treatments, generic frozen calculations, posting batches and VDS entries |
| Compliance | Company fiscal periods and close reconciliation | Versioned form definitions, return periods/returns/lines, reconciliations, filing evidence and close requests |
| Workflow | Existing purchase/sales/payment maker-checker services | Versioned workflow definitions, instances, tasks, actions, delegation and escalation metadata |
| Reporting | Financial projections, checkpoints, inbox/dead letters; customer reports | Versioned templates, jobs, exports and reconciliation alerts |
| Integration | Shared platform outbox and reporting inbox | Connections, mapping versions, inbound batches/rows, external-ID deduplication, webhook subscriptions, attempts and dead letters |

Shared `platform.outbox_messages` and `reporting.inbox_messages` are deliberately reused. Purchase receipts/returns, tax reversals, stock reservations and period locks already have equivalents in the existing journals, reversal records, frozen fulfillments and period posting fences. There are no competing balances or duplicate tax ledgers.

## Persistence contract

All 63 new tables have `(TenantId, CompanyId, Id)` keys, a restrictive company foreign key, restrictive company-scoped references, UTC creation/update timestamps, creator, optimistic `Version`, and a scope/creation-time index. Tables with business dates or processing states also have company/date and company/state/creation-time indexes. Directory identities are company-scoped projections of `(Issuer, Subject)`, not local password accounts. Branch-specific grants cannot point at another company's branch. Nullable branch grants use PostgreSQL `NULLS NOT DISTINCT` uniqueness, so duplicate company-wide grants are rejected.

Money/quantity foundations use decimal `(24,6)`; existing posted money keeps its established precision. Effective dates, positive quantities/revisions, maker/checker separation, bounded decimal places, digest lengths and applicable state vocabularies are database constraints. JSONB is used for explicit configuration, mapping, parameters and frozen calculation/source snapshots, with relational keys outside JSON. Foreign keys are restrictive; deleting a company does not cascade financial/configuration history.

Integration batches bind a mapping to the same connection as well as the same company. External references and batch identifiers are unique within their connection. Connections store **secret references**, never provider secrets. Session metadata stores token digests only. Attachments and exports store object keys/digests; this migration neither uploads files nor sends webhooks.

Creation/update timestamps default to transaction time. Future services must explicitly advance `UpdatedAt` and `Version` on updates and enforce source-document lifecycle and cross-row business validation. The database does not provide row-level security: existing application company filters and permission checks remain required. These tables are not exposed through generic CRUD endpoints.

## Scope of completion

**Schema-ready does not mean executable module-ready.** Existing financial APIs continue to use their current domain services. New Manufacturing, Compliance, generic Workflow and Integration modules now have database foundations and bounded metadata read APIs. Their write workflows and background export execution remain unimplemented. Their commands, authorization, domain transitions, posting/reconciliation integration and workers still require implementation and independent tests. New identity grants are not automatically consulted by existing OIDC authorization, and refresh-session records do not introduce a local authentication server.

No statutory rates, HS catalogue, return mappings, filing formats or legal challan rules are seeded. Compliance form mappings and tax classifications require approved source data before activation. Challan allocation here is an internal series registry, not a claim of statutory compliance. Workflow metadata does not bypass the existing maker/checker controls. Transfers, adjustments and production records do not post inventory by themselves.

## New table inventory

The [relational dictionary](DATABASE_SCHEMA.md) lists each new column, reference, unique key and additional check constraint.

### company

`company.registrations`, `company.company_settings`, `company.branch_access`.

### compliance

`compliance.form_templates`, `compliance.form_versions`, `compliance.return_periods`, `compliance.vat_returns`, `compliance.return_lines`, `compliance.reconciliation_runs`, `compliance.filing_evidence`, `compliance.period_close_requests`.

### identity

`identity.users`, `identity.roles`, `identity.permissions`, `identity.user_roles`, `identity.role_permissions`, `identity.menu_items`, `identity.role_menus`, `identity.refresh_sessions`, `identity.security_audit`.

### integration

`integration.integration_connections`, `integration.field_mappings`, `integration.inbound_batches`, `integration.inbound_rows`, `integration.external_references`, `integration.webhook_subscriptions`, `integration.delivery_attempts`, `integration.dead_letters`.

### inventory

`inventory.transfers`, `inventory.transfer_lines`, `inventory.adjustments`, `inventory.adjustment_lines`.

### manufacturing

`manufacturing.bom_headers`, `manufacturing.coefficient_versions`, `manufacturing.bom_lines`, `manufacturing.production_orders`, `manufacturing.material_issues`, `manufacturing.production_receipts`, `manufacturing.wastage_records`.

### masterdata

`masterdata.uoms`, `masterdata.hs_codes`, `masterdata.product_versions`, `masterdata.product_tax_classifications`, `masterdata.counterparty_registrations`.

### purchasing

`purchasing.purchase_attachments`, `purchasing.purchase_posting_sagas`.

### reporting

`reporting.report_templates`, `reporting.report_jobs`, `reporting.report_exports`, `reporting.reconciliation_alerts`.

### sales

`sales.delivery_references`, `sales.challan_numbers`.

### tax

`tax.tax_treatments`, `tax.tax_calculation_snapshots`, `tax.tax_posting_batches`, `tax.vds_entries`.

### workflow

`workflow.workflow_definitions`, `workflow.workflow_versions`, `workflow.approval_instances`, `workflow.approval_tasks`, `workflow.approval_actions`, `workflow.delegations`, `workflow.escalation_rules`.

## Operations

Use `scripts/Database-Local.ps1` with an in-process `PGPASSWORD`; never commit credentials. Defaults are loopback PostgreSQL 5432, user `postgres`, database `vat_management`. Override explicit parameters for another local target.

```powershell
# Set PGPASSWORD in the current process using your local secret mechanism first.
./scripts/Database-Local.ps1 -Action Status
./scripts/Database-Local.ps1 -Action Backup -VerifyRestore
dotnet build EnterpriseVat.sln --configuration Release --no-restore -m:1
./scripts/Database-Local.ps1 -Action Migrate
./scripts/Database-Local.ps1 -Action Verify
./scripts/Test-Local.ps1
```

Backups are custom-format dumps under ignored `artifacts/backups`, with SHA-256 sidecars. Treat them as sensitive company data and copy them to managed backup storage according to your retention policy. The optional rehearsal restores into an internally generated `vat_restore_<uuid>` database, verifies its catalogue, then removes only that temporary database. It never restores over the working database. A successful rehearsal establishes that PostgreSQL can restore the dump; it does not verify application workflows or substitute for recovery exercises.

Migrate uses compiled Release artifacts: always build first. Verify checks migration inventory and PostgreSQL constraint/index validation. The automated PostgreSQL tests additionally check EF model alignment, physical table availability, company fences, uniqueness and selected business checks. Run migration drift checks after changing mappings:

```powershell
dotnet ef migrations has-pending-model-changes --project src/Vat.Infrastructure --startup-project src/Vat.Api --configuration Release --no-build
```

For a rollback, retain a pre-change backup and rehearse recovery on a separately named target. Downgrading migrations removes the new module tables and loses their contents; there is no automatic destructive downgrade script.

## Verified local state ? 2026-10-23

- Project database: `vat_management`, local PostgreSQL 18, one persistent project database.
- 12 logical modules; 13 application schemas; **96 application tables**, including 63 new foundation tables. PostgreSQL also contains the EF migration-history table in `public` (97 tables including that infrastructure table).
- All **17 migrations** applied, ending at `20261023123901_ModuleQueryIndexesAndChallanValidation`. Source migration inventory matches the database; EF reports no pending model changes. No invalid indexes or unvalidated constraints were found.
- Release build passed with zero warnings/errors. Full foundation regression: **173 passed, 2 skipped** (live RabbitMQ broker tests). After the final index and nullable challan-reason hardening, all **6 focused database tests passed**, including physical availability of every new table and model/migration alignment. Existing runtime code was unchanged by this hardening.
- Pre-change (14 migrations) and post-change (17 migrations) custom-format backups were restored successfully into separate generated databases. Rehearsal databases were removed afterward. Dumps and SHA-256 sidecars remain under ignored `artifacts/backups`.
- Full regression evidence: `TestResults/full-suite-module-foundations.trx`; final focused checks: `TestResults/tests.trx`. These generated files are ignored by source control.

The module-ready limitations above still apply: this verifies persistence foundations and existing backend regressions, not newly implemented API workflows for the inactive modules.
