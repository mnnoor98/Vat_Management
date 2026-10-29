# Independent Integration and owner-committed message operations

Integration now runs with its own core and `vat_integration` only. It no longer references `Vat.Hosting`, `Vat.Application`, `Vat.Domain` or `Vat.Infrastructure`, and its process is not given `ConnectionStrings__VatDatabase`. The existing eight-table metadata model, migration chain and stored data are retained. Five of twelve business APIs now have independent core/persistence graphs: Identity, Workflow, Compliance, Manufacturing and Integration. Seven financial participants remain coordinated.

## Operator contract V1

The public Integration URLs and permission names are unchanged. Outbox reads/retry and dead-letter reads/replay use a versioned, delegated HTTP boundary at the current data owner:

| Method | Owner route suffix |
|---|---|
| GET | `/outbox?before={cursor}&take={1..100}` |
| POST | `/outbox/{id}/retry` |
| GET | `/dead-letters?before={cursor}&take={1..100}` |
| POST | `/dead-letters/{id}/replay` |

Prefix: `/internal/v1/message-operations/companies/{companyId}`. Command body: `{ "reason": "Reviewed recovery" }`. Commands require a UUID `Idempotency-Key`. Integration validates company scope, permission, bounds and key before forwarding. The owner independently validates the original signed bearer token, tenant/company membership and `messaging.read`/`messaging.retry` policy. Actor identity is never accepted from a JSON body. Original bearer token, idempotency key and correlation UUID are forwarded; the owner response status/body and replay header are preserved. Credential headers are redacted from HTTP client logs.

Currently these endpoints belong to the optional FinancialOutbox technical host because outbox and inbox/projection data still share the financial database. They are enabled only with `OperatorServer:Enabled=true` (explicitly enabled in local Development). Operator endpoints use delegated user JWTs, while claim/confirm/fail continue to use their dedicated worker service key. A worker key cannot authorize operator commands. Production must configure the real HTTPS OIDC issuer and enable the owner adapter explicitly.

Retry/replay run the existing owner-local command executor. The operation names and JSON request hash are preserved, so existing compatibility-host retry receipts are recognized. Projection/inbox effects, dead-letter resolution, audit and stored idempotent response commit together. An injected audit failure rolls back prior replay writes. Integration does not create a second receipt or distributed transaction. It never automatically retries an ambiguous response: clients retry the same command key. Successful mutation responses require the owner's `Idempotency-Replayed` header. Redirects, malformed/unbounded replies and unavailable owners fail closed.

## Deployment

Integration settings:

- `ConnectionStrings__IntegrationDatabase`: owned runtime credential only.
- `Authentication__Authority` and `Authentication__Audience`: the same delegated issuer/audience accepted by the owners.
- `OperatorTargets__SourceBaseUrl`: trusted HTTPS origin for outbox operations.
- `OperatorTargets__InboxBaseUrl`: trusted HTTPS origin for inbox operations; this can move with the Reporting owner later.
- `OperatorTargets__AllowLoopbackHttp`: explicit local-development opt-in; remote HTTP is always rejected.

Local Development points both operator origins to `http://127.0.0.1:5113/`. Start the FinancialOutbox technical host separately or with `Start-Services.ps1 -IncludeFinancialOutbox`; it still needs its dedicated OutboxServer service secret. Missing/down owners return 503 for operator calls while Integration's owned-database readiness remains independent. There is no fallback to financial SQL. Production Compose no longer supplies Integration with the financial database secret; supply `VAT_OPERATOR_SOURCE_BASE_URL` and `VAT_OPERATOR_INBOX_BASE_URL` for trusted owner endpoints.

When `Messaging:Enabled=true`, Integration registers the neutral broker worker with remote `OutboxSource` and `InboxTarget` adapters only. Configure their HTTPS origins and separate 256-bit service keys as documented in the earlier transport decisions. Legacy Local mode is rejected. When messaging is disabled, no dispatch/consumer loops or remote calls run. Remote adapter settings are required only when this independent worker is enabled; explicit adapter registrations elsewhere retain their original strict startup validation. Existing receipt-gated acknowledgement and source lease fencing remain unchanged.

The optional aggregate `Vat.Api` keeps an explicit compatibility controller with the original local owner service. This compatibility adapter is not compiled into the independent Integration API and adds no financial dependency to it. The technical owner exposes no `/api/*` business controllers.

## Scope and verification

This change needs no database migration, row copy or credential rotation. Schema ownership remains six persistent databases. Reporting still uses the coordinated financial database: freshness/source completeness, backfill/rebuild, customer statement/ageing data, generation fencing and cutover/export contracts must be addressed before its own database can be safely separated. This change does not claim Reporting isolation, ERP ingestion completion or live broker/OIDC/container acceptance.

Evidence is recorded under `artifacts/ownership-*.log` and `TestResults/integration-ownership`. Owner tests cover authorization/scoping, receipt interoperability, single audit on retries, replay commit and full rollback on injected audit failure. Standalone Integration tests verify only owned tables are present, no financial API/core references remain, gateway validation and forwarding, fail-closed missing owners, and refusal of Local worker mode.

## Next Reporting cutover gates

1. Define source completeness/export manifests and a durable event replay range; a missing source event must prevent an `IsCaughtUp` claim.
2. Replace cross-database rebuild/reset with owner-local generation changes, source replay receipts and recovery fences.
3. Provide customer/payment/credit/refund coverage before moving statement and ageing reads off their current transactional source.
4. Create the owned Reporting core/database, preserve inbox deduplication/quarantine and atomic operator audit/idempotency, then verify source/target counts and financial fingerprints under a controlled cutover.
5. Rehearse backups/restores, revoke cross-owner runtime access, and prove lag, duplicate delivery, replay, partial failure and recovery before enabling the isolated owner.

The source-backed implementation sequence is documented in [Reporting cutover plan](0026-reporting-cutover-plan.md); it is explicitly not yet implemented.

## Completed local validation

Final full suite: **338 passed, 0 failed, 2 live-broker skips** across eight projects (`TestResults/ownership-full`, this run dated 24 October 2026 at 17:24?17:35 Asia/Dhaka). The final standalone Integration suite passed **14/14**; the earlier targeted owner/Reporting/Integration batch passed **30/30**. Final build has zero warnings/errors. All thirteen packages were refreshed; Integration package dependency/catalogue/Compose checks prove no financial libraries or financial connection. All twelve published APIs passed exact route, authentication and owned-database readiness checks; Purchase restarted while Sales stayed live. FinancialOutbox and Reporting technical-host auth/readiness probes passed, including denial of operator access using a worker key. Runtime role/isolation checks passed; Financial/Integration EF models have no pending changes. Local quantity reconciliation remains **5 balances, 0 mismatches**. Temporary API/test PostgreSQL processes were stopped.

Machine-readable evidence: `artifacts/integration-ownership/test-summary.json`, `package-proof.json` and `source-manifest.json`; detailed logs use `artifacts/ownership-*.log`. Historical TRX files in the results folder are excluded from this run summary. No new schema migration, production data change, database copy or credential rotation was required. Live broker, real external OIDC and container/load acceptance remain unverified locally.
