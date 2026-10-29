# Reporting delivery receipts and independent broker worker

Date: 2026-10-24

## Implemented boundary

`Vat.Messaging.RabbitMq` now owns RabbitMqTransport and MessagingWorker. Its dependencies are the neutral `Vat.Messaging` contract/dispatch project, RabbitMQ.Client and hosting abstractions. It has no financial application/domain/infrastructure, EF or Npgsql dependency. Existing namespaces are retained for source compatibility. Incoming message/processor/result types also moved into Vat.Messaging with their existing namespaces and JSON shapes.

The worker consumes through `InboxDeliveryHandler` and `IInboxDeliveryTargetV1`. Local mode resolves ReportingInbox; `InboxTarget:Mode=Remote` on Integration resolves HttpInboxDeliveryTargetV1. The Reporting API can explicitly enable an authenticated delivery endpoint. The processing request crosses the host boundary; inbox/projection storage still uses the coordinated financial database. This is not a Reporting database cutover or full Integration isolation.

## Commit and acknowledgement contract

The flow is broker delivery → bounded V1 delivery/hash → selected owner → inbox/projection/checkpoint or quarantine commit → matched V1 receipt → successful broker callback → broker acknowledgement. The receipt must use protocol 1, consumer `reporting-v1`, the exact delivery fingerprint and a terminal status. `Processed`, `Ignored` and `Duplicate` have no dead-letter ID; `Quarantined` requires a nonempty dead-letter ID. Missing, mismatched, pending or malformed receipts fail the callback. The existing transport leaves failed callbacks unacknowledged and reconnects/requeues.

ReportingInbox's delivery entry point refuses an existing outer transaction, so a broker receipt cannot attest to an uncommitted transaction. It returns only after its own inbox/projection/quarantine commit. Its original `IIncomingEventProcessor.Process` entry point still permits an ambient command transaction for audited/idempotent operator replay. Remote worker selection does not replace that operator processor. Replay, audit and idempotency retain their existing atomic transaction; routing replay over a separate HTTP transaction would have broken that guarantee.

Lost delivery responses are not retried automatically by the HTTP client. A committed message can be delivered again by the broker; existing event-ID/body-hash deduplication returns `Duplicate` without another projection. Invalid payloads return a committed quarantine receipt rather than a false success or an uncommitted acknowledgement. Delivery remains at least once, not exactly once.

## Bounded delivery and oversized quarantine

V1 carries the original message ID/type/content type, original body length, uppercase SHA-256 of the full body, at most 1 MiB of body prefix and SHA-256 of that prefix. Normal-size bodies must have matching full/prefix hashes. An oversized body carries only its bounded prefix over HTTP; Reporting rejects it as `message_too_large` and durably stores that prefix as truncated. Its fingerprint matches the original local inbox calculation, so local/remote quarantine uses the same record. The full-body digest and original length for truncated deliveries are trusted service metadata; the receiver cannot independently hash bytes intentionally omitted by the dispatcher.

The delivery contract caps each nullable metadata string at 4096 characters. Invalid transport metadata is rejected and cannot acknowledge a broker delivery. The HTTP request limit is 2 MiB before body binding (including Kestrel chunked reads), with strict unknown-member handling and JSON depth 16. The receipt client buffers at most 4096 bytes and validates its fingerprint/status before returning. HTTP client timeout is 1–30 seconds across send/response buffering (default 10). This bounds forwarding; the existing broker callback still receives/copies the broker's full delivery before creating the prefix.

## Service authentication and configuration

Reporting exposes `POST /internal/v1/inbox/reporting-v1/deliver` only when `InboxServer__Enabled=true`. Configure `InboxServer__ServiceKey` as a protected random 256-bit hexadecimal secret. Only the dedicated InboxService scheme and `X-Inbox-Service-Key` header satisfy this endpoint policy. Authenticated user principals and the outbox service header do not satisfy it. Use a different secret from the outbox capability; Integration rejects equal keys when both remote modes are selected.

Configure Integration with `InboxTarget__Mode=Remote`, `InboxTarget__BaseAddress=https://reporting-origin:port`, `InboxTarget__ServiceKey` and optional `InboxTarget__TimeoutSeconds`. The address must be an origin, with no user info/path/query/fragment. HTTPS uses normal platform certificate validation. Redirects/cookies/automatic retries are disabled and the service header is redacted from HttpClient logs. There is no local fallback on failure. Messaging worker/broker enablement remains separately controlled through Messaging settings.

For loopback development only, explicitly enable `InboxServer__AllowLoopbackHttp` and `InboxTarget__AllowLoopbackHttp`, and use `http://127.0.0.1:5111`. The server requires the actual peer IP to be loopback and does not trust forwarded headers as TLS/loopback proof. Proxy deployments must retain HTTPS to the Reporting listener. The service launcher passes InboxServer settings only to Reporting and InboxTarget settings only to Integration.

Local inbox mode remains the default and the Reporting internal endpoint is disabled by default. No persistent deployment key or mode change is made by tests. Existing readiness still checks owned/local database migrations; it does not attest to remote inbox availability or a live broker. Remote failure is handled by the worker's deferred/requeue behavior.

## Verification and remaining ownership

Focused tests cover V1 integrity/fingerprint compatibility, bounded oversized delivery, receipt gating, service-only authentication, malformed/unsafe responses, no automatic retry, PostgreSQL commit visibility/deduplication, oversized local/remote quarantine identity, failed owner commit rollback and ambient-transaction refusal. The regression selection also covers existing source lease, reporting/outbox/inbox/operator and posting paths.

Verification completed: the solution built with zero warnings/errors, and **128 focused tests passed** with no skips: 28 neutral messaging tests (`TestResults/inbox-receipt-unit`), 41 HTTP tests (`TestResults/inbox-receipt-http`) and 59 PostgreSQL/API regressions (`TestResults/inbox-receipt-postgres`). The latter include six new inbox contract/assembly cases plus existing source leases, reporting/outbox/inbox/operator, API/foundation and purchase/sales posting/close cases. The disposable PostgreSQL process stopped cleanly. Reflection checks confirmed the broker/worker assembly has no financial/EF/Npgsql dependency.

Fresh published package checks excluded broker/HTTP dependencies from the four independent business hosts and confirmed the expected technical assemblies in Integration, Reporting and FinancialOutbox. Changed PowerShell scripts passed syntax checks. The published Reporting delivery probe passed dedicated service authentication, invalid-contract rejection without data writes, and runtime database readiness.

The FinancialOutbox published probe also passed service authentication/controller isolation and runtime database readiness. After synchronizing the concurrent Manufacturing routes and removing its privileged fallback (scope below), the final solution rebuild passed with zero warnings/errors and Manufacturing was freshly republished. All twelve business APIs then passed liveness, exact route inventory, authentication and database readiness with runtime credentials. Purchase restarted while Sales remained live. All probe child processes were stopped and environment restored.

`scripts/Test-ReportingInboxHost.ps1 -Published -DatabaseReady` starts a fresh published Reporting host with an ephemeral credential, checks its enabled route/authentication/readiness, submits only an invalid protocol (no persistent inbox/quarantine write), then stops its child and restores environment. Re-run the twelve business hosts and FinancialOutbox probes after publishing.

There is no schema or data migration in this step. Persistent databases remain six; fully isolated business API/core count remains four of twelve. Integration still retains its financial connection for operator reads/retry/replay and the shared financial composition. Reporting still requires financial joins/backfill/rebuild and customer reporting. Next work is scoped operator contracts and Reporting storage ownership, preserving audit/idempotency, completeness/rebuild guarantees and a rehearsed data cutover before removing either financial connection. Live RabbitMQ/Docker and a new full-suite run are separate verification scopes.

## Concurrent Manufacturing scope

The published route probe found coefficient GET/POST/approval routes added to Manufacturing outside this messaging change, while the route inventory still listed only production-order reads. Those additions were preserved and both route inventories synchronized. Their connection helper had a privileged financial fallback; it now requires only `ConnectionStrings:ManufacturingDatabase` and fails closed if absent.

These coefficient flows are not validated by the 128 messaging/regression tests or readiness probes. They still need a separate review of tenant/company fences, write permissions, maker/checker identity, owned migrations instead of request-time DDL, removal of financial/master-data joins and business validation before use. Keep the existing restricted runtime role; do not supply a migration/admin credential to make these paths execute. Four independent packages/core projects and the owned production-order read foundation remain established; the concurrent coefficient implementation is not evidence of a completed owned write workflow. The published probes do not invoke coefficient SQL or perform a schema migration.
