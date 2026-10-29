# Independent Reporting snapshot generation core

Reporting now has separate `Vat.Reporting.Domain`, `Vat.Reporting.Application` and `Vat.Reporting.Infrastructure` projects under `src/Services/Reporting/Core`. They reference their own layers, the neutral messaging contract and SharedKernel; they do not reference the financial application/domain/infrastructure or hosting assemblies. The existing Reporting API still uses the coordinated financial implementation. These projects are the staged snapshot core, not a completed API/database ownership cutover.

## Supported boundary

The core consumes the immutable `purchase-financial-v1` export from decision [0027](0027-reporting-source-export.md). `ReportingGenerationService.Bootstrap` downloads and validates a complete export before opening the target write transaction. It checks manifest version, tenant/company/export identity, declared coverage, exact document/event counts, page offsets and continuation, deterministic order, unique transaction/event identities, full signed reversal relationships, lossless money precision and SHA-256. Input is bounded to 10,000 rows and 8 MiB; HTTP manifests are bounded to 32 KiB and pages to 1 MiB. A single 20-second download deadline spans all pages. Cancellation, partial pages and invalid source evidence leave no partial generation or receipt.

The HTTP adapter uses a fixed trusted HTTPS origin (explicit loopback HTTP opt-in only), the original operator bearer token, disabled redirects/cookies and no automatic retry. It does not store tokens, use worker credentials or query financial SQL. A future HTTP host must construct `Actor` from its verified principal and bind the source adapter to that same request's original bearer token; these library methods do not replace authentication middleware.

`ReportingDbContext` has its own migration chain and `reporting.__ReportingMigrationsHistory`. The initial migration creates five tables:

| Table | Purpose |
|---|---|
| `snapshot_generations` | Verified immutable manifest, export ID, fingerprint, source capture time and exact event count |
| `snapshot_entries` | Generation-scoped transaction/event identity, original row JSON and signed dated amounts |
| `snapshot_heads` | Company/tenant snapshot pointer and optimistic version |
| `snapshot_command_receipts` | Operation/key, verified operator, request hash and committed typed response |
| `snapshot_operator_audit` | Operator, command key, generation, action, reason and occurrence time |

Composite foreign keys prevent cross-tenant/company pointers. Generation/event and transaction uniqueness prevent double counting inside a generation; repeated exports deduplicate by scoped export ID and immutable manifest. PostgreSQL triggers prohibit UPDATE/DELETE/TRUNCATE of generation, entry, receipt and audit evidence. Only the snapshot head changes. The migration is forward-only.

## Transactions and reads

Bootstrap and snapshot activation each own their transaction, company advisory lock, audit and actor/payload-bound receipt. Concurrent retries commit once. A committed bootstrap retry resolves locally even when the source is offline. The same key with a different actor or payload conflicts. Download work holds no target write transaction or company lock, so an existing snapshot remains readable while its replacement downloads. Generation rows, audit and receipt commit together after full verification.

`ActivateSnapshot` selects a verified static snapshot using an expected head version. Pointer change, audit and command receipt commit together; stale versions cannot replace the current head. Old generations remain intact. Replaying an old activation receipt returns its original response without moving the current pointer backwards. These are **snapshot reads only**: responses explicitly carry `SnapshotOnly=true` or `LiveCoverageVerified=false`. Source capture time is not a commit watermark or a caught-up checkpoint.

`ReadSnapshot` reads the head and dated rows in a repeatable-read transaction, preserves `[from, until)` boundaries and groups totals by currency. It returns `snapshot_not_ready` without an activated snapshot. It does not silently fall back to financial SQL, add different currencies together, erase the active generation, or reset source outbox leases.

## Use and verification

The infrastructure package exposes `AddReportingGenerationCore(connection)` for scoped services and context registration. Deployments must apply the separate Reporting migration with a migration identity before enabling a host. The eventual runtime role needs SELECT/INSERT on immutable snapshot tables, SELECT/INSERT/UPDATE on heads, and SELECT on its history; it must not own schemas or have DDL/administrative rights. No request performs DDL.

Run the isolated tests using `scripts/Test-Local.ps1 -Target tests/Vat.Reporting.Tests/Vat.Reporting.Tests.csproj`. Tests cover signed source totals/date boundaries, command concurrency and offline replay, scope/coverage/hash/page failures, stale activation, old snapshot readability during rebuild, restart persistence, audit rollback, cancellation, append-only triggers, per-currency totals, HTTP origin validation, redirect refusal and known/unknown-length body limits. A separate real-source parity test in `ReportingSourceExportTests` posts Purchase/reversal through the existing API, reads the export through the production HTTP adapter, migrates a separate temporary Reporting database, imports/activates the snapshot and compares all signed totals with existing reports for original, reversal and combined date windows.

Logs use `artifacts/reporting-core-*`; final TRX results use `TestResults/reporting-core-final`. These tests create and remove disposable databases. No persistent `vat_reporting` database or live API DSN switch is part of this stage; the six existing persistent databases and source financial migration chain remain unchanged. The Reporting core's initial migration is independent of financial migration 19.

## Remaining cutover work

Final verification on 24 October 2026: the complete 56-project Release solution build passed with zero warnings/errors. All **50 affected tests passed**: 23 independent Reporting core/HTTP cases and 27 source/reporting/operator cases, including the separate-database real Purchase/reversal parity test. The owned EF model has no pending migration changes. Other test projects were excluded by the explicit filter; the full regression suite and production API runtime probes were not repeated for these staged library changes. Temporary parity/core databases and the disposable PostgreSQL cluster were stopped/removed. `artifacts/reporting-generation-core/test-summary.json` retains per-run results and hashes.

Customer Sales/credit/debit/payment/refund/allocation-release source coverage and independent static projections are now implemented in [0029](0029-reporting-customer-history-snapshots.md), with a second owned migration. Generation-scoped live inbox/deduplication/quarantine, a verified live catch-up boundary, checkpoint/freshness semantics and atomic promotion for complete supported report scopes are not implemented by a frozen snapshot head. Complete those contracts and compare nonempty customer/financial history before replacing the Reporting API's financial core reference, provisioning/migrating the persistent owned database, rehearsing backup/restore, switching worker/API owners and revoking cross-owner access. Follow [0026](0026-reporting-cutover-plan.md); do not interpret a static snapshot as a live reporting cutover.

The subsequent financial-only generation inbox and atomic replay are delivered in [0030](0030-reporting-financial-generation-inbox.md), adding the third owned migration. Its arrival metrics still do not certify source completeness or implement customer live coverage/promotion.
