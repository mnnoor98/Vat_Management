# Durable Purchase-financial source export V1

The financial owner now prepares a frozen, reconciled bootstrap snapshot. Reporting still uses the coordinated financial context; this change does not create an empty Reporting database or claim Sales/customer reporting coverage. Source snapshots remain owned by the financial source after a future Reporting cutover.

Source persistence/service code lives under `Services/FinancialOutbox/Infrastructure`, separate from the future Reporting consumer core. The controller adapter stays in `Vat.Hosting/ReportingSourceEndpoints.cs`; reconciliation and snapshot construction stay in the service.

## Contract and operation

Neutral DTOs and the fingerprint helper live in `Vat.Messaging/ReportingSourceV1.cs`; no financial core reference is needed to consume them. The explicit scope is `purchase-financial-v1`, covering every posted Purchase plan and posted full Purchase reversal in one tenant/company, across all business dates. Drafts, pending reversal effects, Sales, customer balances and allocations are outside this scope.

Enable `ReportingSourceServer:Enabled=true` on the FinancialOutbox technical host (or compatibility host). Production defaults to disabled; the technical host's Development configuration enables it. This is an operator JWT boundary: the worker service key cannot authorize these endpoints.

| Method | Route below `/internal/v1/reporting-source/companies/{companyId}/exports` | Permission |
|---|---|---|
| POST | empty suffix, body `{ "reason": "Reviewed bootstrap" }`, UUID `Idempotency-Key` | `report.manage` |
| GET | `/{exportId}` | `report.read` |
| GET | `/{exportId}/rows?offset=0&take=100` | `report.read` |

POST returns the manifest. Every route checks the JWT tenant and granted company. Prepare uses the existing actor/payload-bound command executor, company exclusive admission fence, source reconciliation, audit and stored response in one transaction. Concurrent retries with the same key return one export; a changed actor or payload conflicts. Audit/save failure commits neither export nor receipt.

Preparation compares the complete posted source document set with every `FinancialEntryRecordedV1` outbox envelope. It checks metadata, schema, aggregate/version, exact authoritative signed amounts and transaction identity. Missing, duplicate, malformed, contradictory or orphan events fail closed with `source_coverage_conflict`; unreconciled ledgers fail with `reconciliation_failed`. Missing legacy events can be repaired through the existing owner-local audited/idempotent report backfill command, followed by a new preparation request. A contradictory event requires investigation; backfill must not overwrite it. Preparation never resets delivery attempts, leases or dispatch state.

The manifest identifies version, scope, export/tenant/company, capture time, exact event count, reconciled Purchase/reversal counts and SHA-256. `CoverageComplete` is true only after these checks. It describes this frozen scope; it is not a live downstream freshness claim. `CapturedAt`, UUID ordering and the latest event timestamp are **not** commit watermarks. A future generation consumer still needs a proven live catch-up/dedup boundary before promotion.

## Durability, bounds and verification

Migration `20261024122701_ReportingSourceExports` adds `platform.reporting_source_exports`, with composite tenant/company/export identity, immutable manifest text and ordered row text. The runtime financial role receives SELECT/INSERT and is denied UPDATE/DELETE/TRUNCATE by the local role initializer. Deployment grants must preserve the same restriction after blanket schema grants. The migration is forward-only; use a verified backup for rollback. Exports have no automatic deletion or expiry; establish retention before sustained production use.

V1 accepts at most 10,000 rows, a conservative source text budget of 2,097,152 characters and an 8 MiB UTF-8 snapshot. Source count and text length are bounded before payload materialization. Exceeding a limit returns `export_limit`, without partial state; use a future partitioned contract rather than silently truncate history. Pages accept offsets 0..10,000 and sizes 1..100. A page outside the snapshot returns no rows and no next offset. Each request reads at most the bounded stored snapshot.

Fingerprint bytes use the public V1 helper: web-default System.Text.Json, declared record property order, source decimal representation and rows ordered by ordinal lowercase D-format transaction UUID. The hash includes contract version, tenant, company, scope and every row (including event identity and aggregate version), excluding export ID/capture time. Consumers concatenate all pages in order and verify exact counts and this hash. New source writes cannot change previously stored pages. This is a bootstrap boundary, not a replacement for active-generation rebuild or existing inbox processing.

`ReportingSourceExportTests` covers real posting/reversal history, stable pages after a new reversal, signed totals/hash reproduction, unchanged delivery state, concurrent command replay, tenant/company/permission denials, page/reason bounds, missing-event backfill recovery, contradictory events, ledger gaps, source size refusal and audit rollback. Local verification logs use the `artifacts/reporting-source-*` prefix; test results use `TestResults/reporting-source-*`.

## Verified local result, 24 October 2026

Release build completed with zero warnings/errors. All 8 new export cases and all 18 existing reporting/operator cases passed in the final affected-scope run (26 total); all 109 tests in the other seven test projects passed. The original complete root regression retained 211 additional unaffected passing cases and two skipped live-broker cases. Latest outcomes per assembly/stable test ID therefore total **346 passed, 0 failed, 2 skipped**. The entire root suite was not repeated after the localized corrections. Raw failed attempts remain retained: the large-payload fixture first required valid JSON; that corrected fixture then exposed JSONB member-length translation bypassing the preflight. The final implementation explicitly queries parameterized `length("Payload"::text)` before materialization, and the regression now requires the `export_limit` problem code. `Test-Local.ps1` also preserves native test exit codes and complete TRX diagnostics under Windows PowerShell.

The local financial database has 19 applied migrations, 98 application tables (99 with history), no pending EF model changes, and no unvalidated constraints. Pre/post migration custom backups both restored successfully. Actual export grants are SELECT/INSERT only; role isolation checks passed. Stock reconciliation remains 5 balance rows and 0 quantity mismatches. All 13 packages were refreshed; all twelve business API readiness/route/auth checks and Purchase/Sales restart isolation passed. Final FinancialOutbox and Reporting inbox probes passed, including rejection of worker credentials at reporting export routes. Temporary test databases and probe processes were stopped.

`artifacts/reporting-source-export/test-summary.json` preserves each run, hashes and the latest-outcome calculation; `source-manifest.json` and `package-proof.json` identify the delivered source/package bytes. These are local ignored artifacts. Broker/real issuer and production load acceptance are not supplied by these local probes.

The staged financial snapshot core/bootstrap is delivered in [0028](0028-reporting-snapshot-generation-core.md), and separate complete bounded customer history export/bootstrap/projections in [0029](0029-reporting-customer-history-snapshots.md). Live inbox/catch-up, coordinated promotion and production cutover remain pending under 0026. Do not switch the Reporting DSN until complete live financial/customer coverage and replay transactions are implemented and compared against nonempty authoritative history.
