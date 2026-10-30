# Coordinated financial execution and close checkpoints

Date: 30 October 2026 (Asia/Dhaka).

## Decision

Company/Period, MasterData, Purchase, Sales/Payments, Inventory, VatEngine and public Reporting use the coordinated `Vat.Application` / `Vat.Infrastructure` implementation and **one configured `VatDbContext` database**. Their standalone hosts and the compatibility host resolve the same financial store contracts. The independent Identity, Workflow, Compliance, Manufacturing and Integration owners keep their existing persistence boundaries.

The extracted financial contexts are staged implementations. They are not registered by the active financial hosts, and their unavailable execution guards remain in place. Separate process/port does not imply a separate financial transaction or database. This restores a local transaction boundary rather than introducing unverified remote compensation across databases.

No existing database was migrated, merged, repaired or cut over by this implementation. If data already exists in multiple financial databases, reconcile and transfer it through a reviewed migration plan before changing a deployed connection. Do not switch existing traffic to a different database as an implicit data migration.

## Executable ownership map

| Route family / host | Application services | Active persistence | Commit participants |
|---|---|---|---|
| Companies, branches, periods / Company | CompanyService, PeriodService | CompanyStore, PeriodStore | Company/fiscal state, audit, close event, command receipt |
| Products, suppliers, customers / MasterData | ReferenceDataService, CustomerService | ReferenceStore, CompanyStore, CustomerStore | Scoped immutable references/customer revisions, audit, receipt |
| Purchases, plans, reversals / Purchase | PurchaseService, PostingService, ReversalService | PurchaseStore, PostingStore, ReversalStore | Source version, stock journal/balance, input tax, audit, outbox, receipt |
| Sales, credits/debits, receipts/corrections / Sales | SalesInvoiceService, SalesPostingService, SalesCreditService, SalesDebitService, PaymentService, PaymentCorrectionService | Coordinated sales/payment stores | Source/reservation/issue, output tax, allocation/release/cash history, audit, outbox, receipt |
| Warehouses, balances, transfers / Inventory | ReferenceDataService, PostingService, FinancialInventoryQueries, TransferService | Coordinated stock/reference/transfer stores | Paired transfer journal, both balance changes, version/checker, audit, outbox, receipt |
| Tax rules and input-tax ledger / VatEngine | ReferenceDataService, PostingService | ReferenceStore, PostingStore | Published rule version/overlap fence, audit and receipt |
| Public summaries/history and Reporting inbox / Reporting | IReportingService, CustomerReportingService, ReportingInbox | Coordinated source/projection/inbox storage | Durable projection/inbox commit and service receipt |

`services.json` records the shared financial connection key, active core projects, and staged core projects. `FinancialCompositionTests` asserts that coordinated stores are resolved and staged Company/Purchase/Sales/Inventory contexts are absent from the compatibility runtime. The controller route catalogue is synchronized with the actual HTTP attributes.

## Posting, replay and recovery

All financial controllers resolve the coordinated command executor. Its transaction covers the source version, immutable journals, balance mutations, audit, stored outbox events and idempotency receipt. Faulted commands roll back; a retry with the same command key can recover. Successful same-actor/same-payload retries replay the original response. Different actor or payload cannot reuse the receipt.

Purchase preparation reads registered supplier/product/warehouse references and an independently published, effective-dated tax rule. Posting freezes and uses that calculation snapshot. Purchase posting writes stock receipts, input tax and versioned events together. Full reversal writes linked correcting stock/tax entries and events. Sales uses real references, stock reservations/issues and published output-tax rules; its correction and receipt workflows retain immutable history. Transfers use paired journals and a stored event, rather than the staged no-op event writer.

Public financial reporting now reads the same financial source whose commands produce the coordinated outbox. Existing durable export, source checkpoints, inbox deduplication and projection behavior remain. The staged Reporting generation core/database is not a public API cutover and is not registered by the public Reporting host.

## Fiscal admission and close

Financial admission holds the shared company advisory transaction lock before checking the fiscal period. Close/create/cancel-close take the matching exclusive lock. Posting and close therefore cannot both admit conflicting work. Missing, Closing and Closed periods reject new financial admission with HTTP 409. Financial writes still require configured posting policy and explicit permissions.

Close review checks pending approved/prepared purchases, reversals, approved/reserved sales, credits/debits, receipts/corrections, and submitted/approved transfers in the period. Final close also reconciles:

- Purchase snapshots against their original/correcting stock and input-tax journals.
- Sales snapshots against stock/output-tax and adjustment history.
- Warehouse/product balances against purchase/sales movements and paired transfer journals.
- Reservations against outstanding reserved fulfillment snapshots.
- Receipt/allocation/correction/cash/release consistency.

Only after these checks does close store a dated, immutable checkpoint with actual purchase/stock/tax IDs and totals, sales entries/totals, payment/allocation/cash/release IDs and totals, transfer journal IDs, and a SHA-256 fingerprint of ordered period history. Later-period corrections do not rewrite the earlier checkpoint. Maker/checker and expected-version checks still apply to closing.

## Configuration and limits

Supply the **same reviewed financial database** as `ConnectionStrings__VatDatabase` to all seven financial hosts. Compose uses `VAT_FINANCIAL_DATABASE_CONNECTION` for those hosts. The compatibility host additionally requires the five independent owner connections. No embedded credentials or startup migrations were introduced.

`Posting__Enabled` defaults to false in committed application settings. Set it true only in a configured environment with approved business scope and reconciled opening data. Compliance calculation/filing remains explicitly unavailable; a local transaction does not establish legal filing acceptance. Real OIDC/broker provisioning, deployed least-privilege roles, load/restore evidence, existing-data reconciliation and a controlled pilot remain operational gates.

`scripts/Migrate-AllDatabases.ps1` now shows the six active owner schema updates by default. Applying them requires `-Apply` and explicit `ConnectionStrings__<Owner>MigrationDatabase` settings (Financial uses the `Vat` key). This schema planner does not invoke staged data-transfer tools. The Manufacturing metadata transfer tool was separately repaired to refuse divergent or active workflow targets, preserve source rows, propagate lock/COPY errors and require exact row fingerprints before committing.

## Validation

Validation results are recorded in `IMPLEMENTATION_STATUS_2026-10-30.md` at repository root after the final runs. Tests use a private disposable PostgreSQL cluster and assert effective connection isolation. No live business database is used for these runs.

Latest per-project coverage: **434 passed, 2 live-broker skips**. The full solution run had one Manufacturing importer failure; its fix was verified by a separate **13/13 Manufacturing rerun**. The coordinated main suite passed **249 tests**. All twelve API process/route/authentication probes and restart isolation passed. EF reports no pending financial model changes. These results do not certify live broker delivery, existing deployed balances or external filing.
