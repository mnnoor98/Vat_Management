# Reporting backend

The optional service-only inbox delivery endpoint is enabled through `InboxServer__Enabled=true` plus a dedicated secret and HTTPS listener. It returns V1 receipts after inbox/projection/quarantine commit; storage still uses the financial database. See [delivery configuration and ownership limits](../../../docs/architecture/0022-reporting-delivery-commit-receipts.md). The endpoint is disabled by default.

This backend owns `Vat.Reporting.Api`, its controllers and its own process on local port **5111**. Its current owned source folders are `Application`, `Infrastructure`. Shared transactional/build dependencies are described in the [architecture guide](../../../docs/SERVICE_ARCHITECTURE.md).

The financial owner exposes a [durable Purchase/reversal source export V1](../../../docs/architecture/0027-reporting-source-export.md), with reconciled coverage, stable pages and SHA-256. Financial migration 20 adds [commit checkpoints and immutable export boundaries](../../../docs/architecture/0032-reporting-source-commit-checkpoints.md). This source boundary is hosted on FinancialOutbox; the staged consumer below adds static financial snapshot bootstrap. Consumer contiguous-cursor verification and customer live coverage remain pending.

The owner also exposes a [bounded source journal API](../../../docs/architecture/0033-reporting-bounded-source-journal.md): fixed epoch/through-sequence pages, original envelopes, byte limits and fail-closed gap detection. Consumer HTTP adaptation, durable contiguous progress and live coverage proof are still pending.

The staged [Reporting snapshot core](../../../docs/architecture/0028-reporting-snapshot-generation-core.md) lives under `Core/Domain`, `Core/Application` and `Core/Infrastructure`, with four owned migrations and a test project. It imports verified Purchase/reversal exports with atomic audit/receipts and static snapshot activation. [Customer history snapshots](../../../docs/architecture/0029-reporting-customer-history-snapshots.md) support dated statement/ageing projections. The [financial generation inbox](../../../docs/architecture/0030-reporting-financial-generation-inbox.md) supports deduplication, reorder handling, quarantine and atomic replay; [export coverage verification](../../../docs/architecture/0031-reporting-export-coverage-proof.md) proves exact event coverage of a frozen source export. The live API above still uses the financial core; continuing source commit/catch-up verification, customer live coverage, coordinated promotion and persistent database cutover remain pending.

Supply `ConnectionStrings__VatDatabase` and `Authentication__Authority` through your environment, then run from this directory:

```powershell
dotnet run --project Vat.Reporting.Api --configuration Release
```

From the repository root, `./scripts/Publish-Services.ps1 -Service Reporting` publishes this backend after a Release build. Its Dockerfile uses the repository root as its build context. See the [route ownership inventory](../../../docs/SERVICE_ROUTES.md) and the architecture guide for current capabilities, permissions, external configuration and inactive write workflows.
