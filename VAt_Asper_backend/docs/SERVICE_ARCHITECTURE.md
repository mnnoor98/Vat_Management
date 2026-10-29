Current ownership update: **Identity, Workflow, Compliance, Manufacturing and Integration have independent API/core/data ownership. Integration delegates message operations to owner APIs without a financial connection. Seven financial services remain coordinated.** [Current ownership](SERVICE_OWNERSHIP.md), [Integration operator cutover](architecture/0025-independent-integration-operators.md).

# Twelve independently hosted backends

Each backend has its own ASP.NET Core API project, controllers, Program, appsettings, launch profile and Dockerfile under `src/Services/<Name>`. Each compiles into a separate executable and can be run, published, restarted and deployed independently. There is no proxy forwarding every request to a central API.

## Ports and responsibilities

| Backend | Local port | Current HTTP responsibility |
|---|---|---|
| Identity | 5101 | Verified identity profile and company role read model; external OIDC remains the token issuer |
| Company | 5102 | Companies, branches and fiscal periods |
| MasterData | 5103 | Products, suppliers and customers |
| Purchase | 5104 | Purchase workflow, preparation/posting, reconciliation and reversals |
| Sales | 5105 | Sales workflow, stock reservation/posting, credits/debits, payments and corrections |
| Inventory | 5106 | Warehouses, balances and approved journaled transfers |
| Manufacturing | 5107 | Production-order reads and owned coefficient revision/approval workflow |
| VatEngine | 5108 | Tax-rule management/publication and purchase input-tax ledger |
| Compliance | 5109 | Company-scoped VAT-return read model |
| Workflow | 5110 | Company-scoped generic approval-task read model |
| Reporting | 5111 | Financial reports, customer statements and ageing |
| Integration | 5112 | Connection read model and outbox/inbox operations; optional messaging worker |

An optional FinancialOutbox **technical adapter** uses port 5113 for authenticated source claim/confirm/fail. It is separate from these twelve business APIs and retains coordinated financial persistence. Integration now uses remote owner adapters for worker traffic and delegated JWT operator calls. See [configuration and deployment](architecture/0021-authenticated-outbox-source-transport.md).

The complete [route ownership inventory](SERVICE_ROUTES.md) identifies the API that owns each URL. Paths remain `/api/v1/...`; select the owning backend's base URL. Clients using the old single origin must configure those base URLs or add a gateway. `Vat.Api` remains an optional compatibility host compiling the same controller sources, primarily for existing clients and regression tests. The twelve backends do not depend on the compatibility host process or its executable.

## Folder and layer ownership

```text
src/
  Services/
    Company/
      Vat.Company.Api/
        Controllers/
        Program.cs
        appsettings.json
        Properties/launchSettings.json
        Dockerfile
      Application/
      Domain/
      Infrastructure/
    Purchase/ ...
    Sales/ ...
    ... 12 service folders
  Vat.Messaging/           independent dispatch loop and V1 source lease contract
  Vat.Messaging.Http/      authenticated V1 source client/endpoints; no financial persistence
  Vat.Messaging.RabbitMq/  independent broker transport/worker with receipt-gated delivery
  Vat.Web/                 common HTTP/authentication/errors without financial core
  Vat.SharedKernel/        scope claims, primitives and wire metadata contracts
  Vat.Hosting/             coordinated financial composition and command adapter
  Vat.Application/         shared contracts and transitional application build project
  Vat.Domain/              shared primitives and transitional domain build project
  Vat.Infrastructure/      shared database, migrations, command transaction and messaging
  Vat.Api/                 optional aggregate compatibility host
```

Existing business source files now live under their owning service's Application, Domain and Infrastructure folders where they have a clear owner. Existing namespaces/type identities are preserved. For the seven fully coordinated services, the three original layer projects still compile their owned files as linked sources. Identity, Workflow, Compliance and Manufacturing are excluded from those source globs and build through their own three core projects. Mixed concerns such as the reference-data facade, ledger entities, combined company/purchase store and shared posting model still cross service boundaries. Empty layers are not invented for read-only foundation modules.

Controllers bind HTTP input and authorize a named permission, then call application services. Transactional commands keep `ICommandExecutor`, idempotency keys, optimistic versions, audit/outbox writes and exact replay bytes. Read controllers never accept table or column names from requests. Foundation readers select approved metadata only, enforce both tenant and company filters, and use bounded UUID cursor pages (`take` 1?100, default 25). Invalid business page limits return the existing 422 domain-validation response.

## What is independently deployed, and what remains shared

**All twelve APIs are separately runnable/deployable; five have independent core/database ownership: Identity, Workflow, Compliance, Manufacturing and Integration.** Seven financial business hosts use `vat_management`, `VatDbContext` and the shared 19-migration chain. Integration owns only `vat_integration` and delegates financial message operations to owner APIs; its enabled broker worker uses remote adapters. Purchase/Sales posting still commits source, inventory, tax, audit and outbox atomically in the coordinated database. Further financial extraction needs reference, posting/recovery and reconciliation contracts; an empty per-service database cannot replace those dependencies.

Manufacturing, Compliance, generic Workflow and ERP Integration expose foundation read APIs. Their complete write commands, approvals, production/stock reconciliation, filing workflows, ERP import execution and webhooks are not implemented by this host extraction. Identity does not become a home-built OIDC provider. No tax rate/form mapping is guessed or seeded.

For full data ownership isolation, extract one bounded context at a time: define versioned contracts, introduce remote/reference projections, replace cross-schema writes with explicit posting sagas, implement retry/compensation/reconciliation, backfill owned data, then cut over a separately owned database and migration runner. Financial posting must retain its current reconciliation guarantees throughout that change. Creating 12 empty databases or copying the DbContext would not complete that work.

## Configuration and local execution

Use .NET 10 and PostgreSQL 18 as in the current solution. Secrets are supplied through environment variables or your deployment secret store. Required settings for every host:

- `ConnectionStrings__VatDatabase`: required only by the seven coordinated business hosts and the optional FinancialOutbox technical owner. Identity, Workflow, Compliance, Manufacturing and Integration each require only their corresponding owned connection key. Integration operator origins and remote worker credentials are configured separately; it receives no financial connection.
- `Authentication__Authority`: HTTPS URL of your real OIDC issuer.
- `Authentication__Audience`: issued API audience (default `vat-api`; can be overridden per backend).
- `Cors__AllowedOrigins__0` and further numbered values: browser origins you explicitly allow. No origins are permitted by default.

Do not commit credentials. New service appsettings files contain no database passwords or signing keys. The compatibility host requires both financial and Identity connection settings. Every host validates its configuration on startup, verifies signed JWT issuer/audience/lifetime, reads application scope claims, and enforces permission policies. New read permissions are `identity.read`, `manufacturing.read`, `compliance.read`, `workflow.read` and `integration.read`; grant them through the external issuer where appropriate.

```powershell
dotnet restore EnterpriseVat.sln
dotnet build EnterpriseVat.sln --configuration Release --no-restore -m:1
# Supply the required environment variables securely in this process first.
./scripts/Start-Services.ps1                     # all twelve on ports 5101?5112
./scripts/Start-Services.ps1 -Service Company,Sales
# Or run one service through its own launch profile:
dotnet run --project src/Services/Company/Vat.Company.Api --no-build -c Release
```

The start script checks the chosen ports, logs to ignored `artifacts/services/<run-id>`, keeps the processes alive, and stops only its own child processes when it exits. Each host has anonymous `/health/live` and `/health/ready`. Readiness checks migration availability against the configured database; it is not a complete dependency health check. Development exposes `/openapi/v1.json`; production does not expose it by default. Local launch profiles bind loopback HTTP; terminate HTTPS at the deployment edge or configure Kestrel certificates.

Run migrations once with the existing database tooling. Individual API startup never automatically migrates the shared database. Only Integration (or the optional compatibility host) registers the messaging worker. Keep `Messaging:Enabled=false` until broker settings are configured. Do not unintentionally run both deployments as your messaging owner.

## Publishing and containers

```powershell
./scripts/Publish-Services.ps1                   # all twelve; build first
./scripts/Publish-Services.ps1 -Service Sales
./scripts/Start-Services.ps1 -Published -Service Sales
# Each directory deploys separately and requires the ASP.NET Core 10 runtime:
dotnet artifacts/publish/Sales/Vat.Sales.Api.dll --urls http://127.0.0.1:5105
```

Published directories are under ignored `artifacts/publish/<Name>`. Deploy one directory and its environment settings per process. Dockerfiles use the repository root as build context, select the SDK version pinned in `global.json`, and publish only the selected API. Images run as a non-root numeric UID and listen on container port 8080.

`compose.yaml` defines all twelve images, exposing ports 5101?5112 on the Docker host's loopback interface. Supply `VAT_DATABASE_CONNECTION`, `VAT_IDENTITY_DATABASE_CONNECTION`, `VAT_OIDC_AUTHORITY` and optionally `VAT_OIDC_AUDIENCE` externally, then run `docker compose up --build -d`. A PostgreSQL endpoint used from a container must be reachable from that container; `127.0.0.1` inside the container refers to the container itself. The compose file does not create/migrate a database, deploy an OIDC issuer, enable messaging or publish an Internet-facing ingress. Configure those deployment dependencies explicitly.

## Intentional retirement of unsafe shortcuts

The old anonymous `/api/v1/auth/dev-token` endpoint used a fixed signing key and granted all permissions. It now returns 410; obtain tokens from the configured external issuer. Purchase/sales `post-ledger` shortcuts synthesized different maker/checker identities; they now return 410 with guidance to use the actual prepare/reserve and versioned posting actions. The old `/workflows/my-tasks` and `/workflows/tasks/{id}/approve|reject` workbench adapter mixed guessed tax amounts with source-document approvals; it also returns 410. Use the new `/workflow/tasks` read resource for generic workflow records and use each source API's explicit approval/rejection commands with permissions and ExpectedVersion.

These are deliberate behavior changes. Existing frontend workbench/login integrations must be updated. The canonical source-document workflows, journal logic and database schema remain in place.

## Verification

```powershell
./scripts/Test-ServiceHosts.ps1
./scripts/Test-ServiceHosts.ps1 -Published       # verify the actual deployment directories
./scripts/Test-ServiceHosts.ps1 -Published -DatabaseReady # requires your configured database connection
./scripts/Test-Local.ps1
```

The host smoke test starts all twelve real API processes on temporary loopback ports, verifies liveness, exact route ownership and authentication, stops/restarts Purchase while checking Sales stays live, and cleans up its child processes. By default it uses no real database or OIDC service. The optional `-DatabaseReady` mode additionally checks each host against the database connection provided in the current process; it does not contact or verify an external OIDC issuer. PostgreSQL integration tests exercise real database migrations, HTTP controllers, authorization, tenant/company isolation, idempotency, financial posting and reporting. Container builds require a Docker environment and image access and must be verified separately if unavailable locally.

## Historical host-extraction verification ? 2026-10-23 (before owned Identity cutover)

- Release solution build: passed, zero warnings/errors, including all twelve API executables.
- All twelve APIs started as separate processes with exact owned routes and authentication checks. The same checks passed from the independently published deployment directories. Each package contains its own API assembly without another backend executable.
- Every published API passed readiness against the local `vat_management` database. Purchase was stopped/restarted while Sales remained live. Temporary smoke processes were removed afterward.
- EF reports no pending model changes. This host extraction created no new database migration; the existing 17 migrations remain authoritative.
- Initial full regression: 180 passed, 2 skipped live RabbitMQ tests, 1 required-date binding regression. Explicit required query binding corrected that compatibility issue. The subsequent focused run passed all **24 tests**, covering all ten required-query cases, module permissions/scope/pagination/secret omission and the entire customer-report test class. The entire long-running suite was not repeated after that localized fix.
- Evidence: `TestResults/full-suite-service-hosts-before-binding-fix.trx` records the initial full run; `TestResults/tests.trx` records the final focused run. Generated evidence and deployment directories are ignored by source control.
- CI now builds, publishes all twelve packages, checks their process/route isolation and runs the full PostgreSQL/RabbitMQ-backed suite. The updated remote CI has not been executed in this workspace.
- Docker was unavailable locally; container image builds were not executed. Windows process launch was verified; the Linux PowerShell launch branch is configured for CI and has not been exercised locally.
