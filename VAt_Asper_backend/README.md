Local development login fix (31 October 2026): the SPA posts to `/api/v1/auth/login` and accepts a session only after `/api/v1/identity/me` verifies the issued token. No offline/demo-token login fallback is used. Local password login is available only with `ASPNETCORE_ENVIRONMENT=Development` and the exact authority `https://localhost:5080`. Use the login screen's local credentials (for example `imran` / `Admin@123456`); incorrect passwords return 401. External-issuer and Production environments do not expose local password login. All local API hosts share a generated signing key under the ignored `.local/` directory; restart the hosts together if that file is removed. After building, run `./scripts/Test-LocalLogin.ps1` to verify four accounts, signed-token profile reads, wrong-password/fake-token rejection and the Production guard without connecting to a database. Restart the backend after changing authentication code.

Current stabilization status (30 October 2026): [implemented safety changes and remaining gates](../IMPLEMENTATION_STATUS_2026-10-30.md). Financial APIs now use the [coordinated transaction boundary and real close checkpoints](docs/architecture/0034-coordinated-financial-boundary.md). Extracted financial contexts remain staged; statutory filing remains unavailable. Committed posting settings default to disabled. Earlier delivery/test statements below are historical and do not certify the current extracted implementation.

Ownership update: **Identity, Workflow, Compliance, Manufacturing and Integration now have independent API/core/data ownership. Integration uses delegated owner APIs for message operations and no financial connection. Seven financial services remain coordinated.** [Current ownership and local operations](docs/SERVICE_OWNERSHIP.md), [Integration contract and cutover](docs/architecture/0025-independent-integration-operators.md).

Latest Integration ownership validation: **338 passed, 0 failed, 2 live-broker skips**; thirteen packages and all twelve business API readiness/route/authentication checks passed. [Delivery evidence and remaining Reporting work](docs/architecture/0025-independent-integration-operators.md).

See the [24 October full project review and prioritized delivery plan](PROJECT_REVIEW_AND_ROADMAP.md) for findings, remediation and acceptance gates. The workflow delivery full suite passed 325 tests with 0 failures and 2 live-broker cases skipped; all 13 standalone Manufacturing tests passed again after the final description-length alignment. The proven legacy stock discrepancy is reconciled. The subsequent [transfer and coefficient workflow delivery](docs/architecture/0024-transfer-coefficient-workflows.md) adds controlled execution and has separate validation evidence.

[P0 stabilization](docs/architecture/0023-p0-auth-inventory-recovery.md) removes the embedded-key authentication path, scopes/bounds Inventory reads and reconstructs the proven legacy transfer quantity journal. [Controlled transfer and coefficient workflows](docs/architecture/0024-transfer-coefficient-workflows.md) now replace those 501 gates with approval, version checks, audit and idempotency.

Integration dispatch and consumption require remote outbox/inbox owners when enabled; message operations use delegated owner endpoints. The independent host has no financial DSN or Local-mode fallback. [Remote transport and deployment](docs/architecture/0021-authenticated-outbox-source-transport.md).

The broker worker is now independent `Vat.Messaging.RabbitMq`; optional remote Reporting delivery requires a matching commit receipt before acknowledgement. Storage and audited operator operations remain coordinated. [Inbox delivery configuration and scope](docs/architecture/0022-reporting-delivery-commit-receipts.md).

The backend now has **12 independently runnable API projects** under `src/Services`, with service-owned controllers and source folders. See [service architecture and run/deploy commands](docs/SERVICE_ARCHITECTURE.md) and [route ownership](docs/SERVICE_ROUTES.md). Database ownership and the shared financial core remain coordinated; see the architecture guide for scope and retired development shortcuts.

# Enterprise VAT Backend

Executable modular backend for the [architecture roadmap](PROJECT_REVIEW_AND_ROADMAP.md). ASP.NET Core 10, EF Core 10 and PostgreSQL. Current business flow: Company -> Branch -> Purchase Draft -> Submitted -> ApprovedPendingPosting -> PostingPrepared -> Posted. Stock receipts, tax ledger, audit, outbox storage and final source status commit atomically.

Posting supports a constrained domestic, tax-exclusive goods scenario in BDT with registered supplier/product references, branch warehouses and published effective-dated percentage rules. No rates are seeded; purchase requests cannot supply a VAT rate. Company fiscal periods gate financial admission; closing freezes reconciled checkpoints and full reversals append linked correcting entries. RabbitMQ outbox delivery and a durable inbox now maintain eventually consistent financial reports. Posting and messaging are independently disabled by default. Statutory forms and partial returns remain subsequent milestones; this is not production compliance certification.

Sales invoice approval is also available: Draft -> Submitted -> ApprovedPendingPosting, with independent rejection, reasoned cancellation and period-close protection. See [Sales workflow](docs/SALES_WORKFLOW.md). Sales reservation, stock issue, separately published output VAT rules, ledger summaries and close reconciliation are available: see [Sales posting](docs/SALES_POSTING.md). Full goods-return credit notes are available with independent approval and linked stock/output-VAT corrections: see [Sales credit notes](docs/SALES_CREDIT_NOTES.md). Partial goods returns now support cumulative quantity/VAT allocation: see [Partial sales returns](docs/SALES_PARTIAL_RETURNS.md). Price-only credits now share the remaining value budget with goods returns while leaving stock unchanged: see [Sales price credits](docs/SALES_PRICE_CREDITS.md). Sales debit notes support positive price adjustments with independent approval: see [Sales debit notes](docs/SALES_DEBIT_NOTES.md). [Customer master and frozen invoice details](docs/CUSTOMERS.md) are available. [Payment collection, allocation and current customer balances](docs/CUSTOMER_PAYMENTS.md) are available. [Customer statements, invoice due dates and ageing](docs/CUSTOMER_REPORTS.md) are available. [Receipt reversals and customer refunds](docs/PAYMENT_CORRECTIONS.md) are available with immutable cash/release journals. Statutory forms remain later milestones.

## Structure

```text
src/Vat.Domain           Company, Branch, Purchase, Sales invoice and business invariants
src/Vat.Application      Use cases, permissions, DTOs and persistence abstractions
src/Vat.Infrastructure   PostgreSQL mappings, migrations, audit/outbox and idempotency
src/Vat.Api              Authenticated HTTP API, ProblemDetails and OpenAPI
tests/Vat.Tests          Domain tests and real-PostgreSQL HTTP integration tests
docs/architecture       Decisions and explicit scope limits
docs/contracts          Versioned integration-event schema
scripts/Test-Local.ps1   Disposable local PostgreSQL integration-test runner
```

## Prerequisites and configuration

- .NET SDK 10.0.402 (global.json permits later patches in this feature band).
- PostgreSQL 18 for development/testing.
- An HTTPS OIDC issuer configured to issue access tokens for this API.

Use environment variables or .NET user secrets. Never commit credentials. Required settings:

| Setting | Purpose |
|---|---|
| ConnectionStrings__VatDatabase | PostgreSQL connection string for the application database. |
| Authentication__Authority | HTTPS OIDC issuer/discovery authority. |
| Authentication__Audience | Expected access-token audience. |
| AllowedHosts | Production API hostname(s); default only permits localhost and 127.0.0.1. |
| Posting__Enabled | Defaults to false. Set true only in an environment with reviewed tax treatment and an agreed pilot scope. Preparation/read-only review remains available when disabled. |
| Messaging__Enabled / Messaging__Uri | Defaults disabled; enable background broker delivery/consumption with a secret AMQP(S) URI. Remote brokers require TLS. |

Access tokens need a stable `sub`, exactly one `tenant_id` UUID, repeated `company_id` UUID grants, and repeated `permission` claims. Claims are trusted only after bearer validation. Company creation requires a pre-granted company UUID and `company.manage`. The issuer/provisioning owner supplies those grants; API request headers cannot grant access. See the [identity decision](docs/architecture/0001-initial-modular-backend.md).

| Permission | Operations |
|---|---|
| company.manage | Create company and branches. |
| company.read | Read company and branches. |
| purchase.create | Create draft purchase. |
| purchase.read | Read/list purchase. |
| purchase.submit | Submit draft. |
| purchase.approve | Approve or reject submitted purchase; maker cannot decide their own document. |
| masterdata.manage / masterdata.read | Create/read immutable product and supplier references. |
| inventory.manage / inventory.read | Create warehouses; read warehouses and stock balances. |
| tax.rule.manage / tax.rule.publish | Draft / independently publish a dated tax-rule version. |
| tax.read | Read tax rules and the tax ledger. |
| purchase.prepare | Freeze or cancel a posting plan. |
| purchase.post | Authorize and atomically post a prepared plan. |
| period.manage / period.read | Create and read company fiscal periods. |
| period.close.request / period.close.approve | Request/cancel close review / independently finalize close. |
| purchase.reverse.request / purchase.reverse.approve | Request/cancel reversal / independently authorize corrections. |
| purchase.cancel | Cancel an approved, unposted source. |
| report.read / report.manage | Read financial projections / schedule backfill or rebuild. |
| messaging.read / messaging.retry | Inspect scoped message diagnostics / auditably retry or replay. |

## Build and migrate

Set the settings above in your shell first. Create a dedicated application database and database credentials using your PostgreSQL administration workflow. Migrations are explicit; API startup never creates or changes database schema.

```powershell
dotnet restore EnterpriseVat.sln
dotnet tool restore
dotnet build EnterpriseVat.sln --no-restore
dotnet ef database update --project src/Vat.Infrastructure --startup-project src/Vat.Api
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project src/Vat.Api --no-launch-profile --urls http://localhost:5080
```

Use HTTPS termination for deployed traffic and an explicit production host configuration. Development OpenAPI JSON: `http://localhost:5080/openapi/v1.json`. No Swagger UI is bundled. `/health/live` checks process availability; `/health/ready` checks database access and pending migrations. Health responses do not expose connection details. JWT authority and audience must be configured even to start the application.

## API flow

All business endpoints require a bearer access token. Every POST also requires `Idempotency-Key: <non-empty UUID>`. An optional UUID `X-Correlation-ID` is returned in the response; invalid input is replaced. Request bodies reject unknown properties.

1. `POST /api/v1/companies` with `{ "id": "<pre-granted UUID>", "name": "Example", "registrationReference": "DRAFT-REG-001" }`.
2. `POST /api/v1/companies/{companyId}/branches` with `{ "name": "Dhaka" }`.
3. `POST /api/v1/companies/{companyId}/purchases` with the draft below.
4. Register an Open fiscal period covering the business date using `POST /api/v1/companies/{companyId}/periods` with `{ "name": "October 2026", "from": "2026-10-01", "until": "2026-11-01" }`. Submit the purchase with `POST /api/v1/companies/{companyId}/purchases/{id}/submit` and `{ "expectedVersion": 1 }`.
5. A different subject with approval permission calls `.../{id}/approve` with `{ "expectedVersion": 2 }`, or `.../{id}/reject` with `{ "expectedVersion": 2, "reason": "Source document missing" }`.

```json
{
  "branchId": "<branch UUID>",
  "externalReference": "PURCHASE-001",
  "supplierReference": "SUPPLIER-001",
  "businessDate": "2026-10-23",
  "lines": [
    {
      "productReference": "ITEM-001",
      "description": "Purchased materials",
      "quantity": 2.5,
      "unitPrice": 100
    }
  ]
}
```

`GET /api/v1/companies/{companyId}/purchases/{id}` reads current state. List with `?take=25`; pass the last returned UUID as `before` for the next page. Maximum page size is 100. Branch listing currently returns at most 100 entries. Company grants cover every branch in that company in this iteration.

Approval returns `200` with `ApprovedPendingPosting`, version 3 and one stored event. It does not launch an asynchronous process. Before financial posting, register the referenced supplier/product, a warehouse in the purchase branch, and an independently published tax rule covering the business date. Then prepare and review an immutable posting plan. See the [posting API guide](docs/PURCHASE_POSTING.md) for requests, state transitions, calculations and recovery.

Use the [period and reversal guide](docs/PERIODS_AND_REVERSALS.md) for closing checkpoints, full correcting entries, unposted cancellation and the new admission requirements. Missing/Closing/Closed periods block new financial admission. A posted purchase may become ReversalPending and then Reversed, retaining its original positive ledgers and posting version.

Use the [messaging and reporting guide](docs/MESSAGING_AND_REPORTING.md) to enable confirmed RabbitMQ delivery, inspect projection freshness, replay quarantined work, and backfill/rebuild reports. Source financial transactions continue to commit locally; reporting is asynchronous and exposes incomplete state explicitly.

Successful same-actor retries with the same typed request return the original response and Location, with `Idempotency-Replayed: true`. Reusing a key with different data or a different actor returns 409. Scope is tenant/company/operation/key. Failed commands do not consume the key. External purchase references are unique per tenant/company. Response errors use ProblemDetails; conflict and domain errors include a stable code.

## Verification

Domain checks only:

```powershell
dotnet test EnterpriseVat.sln --filter FullyQualifiedName~PurchaseDomainTests
```

For full tests on Windows with installed PostgreSQL binaries:

```powershell
./scripts/Test-Local.ps1
# Or pass -PostgreSqlBin 'path/to/PostgreSQL/bin'
```

The runner creates a private cluster on a free loopback port with a random password, runs tests, stops it and removes only that generated directory. It does not touch your existing PostgreSQL service/databases. Restore packages first. Results are written under `TestResults/`.

Alternatively set `VAT_TEST_POSTGRES` to a disposable server connection (database `postgres`, role with CREATEDB), then run `dotnet test EnterpriseVat.sln`. The fixture creates a randomly named database, applies committed migrations, and drops only that database afterwards. The tests use real RSA-signed bearer tokens, real PostgreSQL transactions/constraints, concurrent requests, tenant boundaries and atomic approval/outbox/audit assertions. A PostgreSQL server is required; the suite does not silently substitute an in-memory provider.

GitHub CI restores with NuGet auditing, builds Release, checks migration drift, and provisions PostgreSQL 18 plus RabbitMQ for integration checks. The workflow is provided but has not been run on a remote repository. Live transport tests run only when VAT_TEST_RABBITMQ_URI is configured; otherwise they are explicitly skipped. Local cache-only restore cannot establish current package vulnerability status.

Latest local verification (23 October 2026): Release compilation succeeded with zero warnings/errors; 168 tests passed and 2 live RabbitMQ transport tests were explicitly skipped because no test broker was configured. All 12 focused receipt reversal/refund cases passed, including correction-date credit eligibility, shared-credit concurrency, rollback/retry, immutable releases, historical reporting and closed-period checkpoints. The complete suite also covers earlier purchase/sales/security/messaging/customer workflows and legacy idempotency replay. EF reported no pending model changes; all 14 migrations are applied to the local vat_management database. Full results are retained in `TestResults/tests.trx` and `TestResults/full-suite-payment-corrections.trx`. Temporary database cleanup and cluster shutdown completed successfully. Live broker confirmation/requeue/unroutable behavior still requires the configured broker tests or CI. Synthetic tax examples do not assert legal rates. Runtime use requires configured OIDC/database settings and, when messaging is enabled, a RabbitMQ broker.

If NuGet is unreachable and the required package versions already exist in the local cache, the environment used for this delivery can restore with the command below. This is an offline fallback only; run an online audited restore before release.

```powershell
dotnet restore EnterpriseVat.sln --source "$env:USERPROFILE/.nuget/packages" -p:NuGetAudit=false --disable-parallel -m:1
```

## Next work

The [messaging/reporting decision](docs/architecture/0004-outbox-inbox-reporting.md) extends the [period/reversal milestone](docs/architecture/0003-period-fencing-and-reversals.md). Next implement additional business flows and validated statutory mappings. Production use additionally needs issuer provisioning, branch-level access where required, live grant revocation policy, protected retention, broker/worker monitoring, deployment observability and restore evidence.

Sales price increases are implemented through [debit notes](docs/SALES_DEBIT_NOTES.md), with separate permissions, frozen VAT, mixed-adjustment reconciliation and correction-period reporting. See [decision 0009](docs/architecture/0009-sales-debit-notes.md).

Database coverage for all 12 planned modules and local backup/migration commands: [Database coverage](docs/DATABASE_COVERAGE.md).

Reporting extraction has a [durable Purchase/reversal source export](docs/architecture/0027-reporting-source-export.md), a [separate snapshot generation core](docs/architecture/0028-reporting-snapshot-generation-core.md), [customer statement/ageing snapshots](docs/architecture/0029-reporting-customer-history-snapshots.md), a [financial generation inbox](docs/architecture/0030-reporting-financial-generation-inbox.md), and [exact source-export coverage proofs](docs/architecture/0031-reporting-export-coverage-proof.md). The staged core has four owned migrations; financial migration 20 adds [source commit checkpoints and immutable export boundaries](docs/architecture/0032-reporting-source-commit-checkpoints.md). Consumer cursor/catch-up verification, customer live coverage, coordinated promotion and production API/database cutover remain pending. Runtime ownership is still five independent and seven coordinated APIs.

The [bounded source journal API](docs/architecture/0033-reporting-bounded-source-journal.md) now reads original envelopes through an explicit epoch/checkpoint with count, byte and sequence-gap guards. It introduces no additional migration; Reporting's durable consumer cursor remains pending.
