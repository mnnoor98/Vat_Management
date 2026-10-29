# Authenticated financial outbox source transport

Date: 2026-10-23

Follow-up: [decision 0022](0022-reporting-delivery-commit-receipts.md) adds receipt-gated Reporting delivery and extracts the broker worker into an independent technical project. Operator and storage ownership still remain coordinated.

## Implemented boundary

`Vat.Messaging.Http` adds an HTTP implementation of the V1 source lease port. It references only `Vat.Messaging` and ASP.NET Core, with no financial, EF, Npgsql or broker dependency. The Integration API selects it when `OutboxSource:Mode=Remote`; `Local` remains the compatible default, and unknown modes fail startup. The active dispatcher resolves the selected source through DI.

`src/Services/FinancialOutbox/Vat.FinancialOutbox.Api` is a separate technical executable for the existing `financial-coordinated` source, conventionally port 5113. It is not a thirteenth business module or a newly isolated financial domain. It hosts the existing financial adapter and readiness check, has no business controllers or messaging worker, and retains the financial connection. The twelve business API catalogue entries remain unchanged. Their hosts do not expose the internal source endpoints.

The source path is `/internal/v1/outbox/financial-coordinated`, with POST operations:

| Operation | Request | Response |
|---|---|---|
| `claim` | No body | 200 V1 claim, or 204 when none available |
| `confirm` | V1 receipt | 204 accepted, 409 stale/scoped mismatch, 400 invalid contract |
| `fail` | `{receipt,errorCode}` | Same settlement statuses; only four safe dispatch error codes accepted |

The source adapter still owns SQL/transactions, tenant/company/token fencing, owner-clock lease expiry, attempt counters, backoff and quarantine. The original event ID/type/payload bytes and digest cross the transport unchanged. No schema or data migration is needed.

## Service authentication and bounds

The endpoints require the dedicated `OutboxService` authentication scheme, using `X-Outbox-Service-Key`. The credential must be 32 cryptographically random bytes encoded as 64 hexadecimal characters. Comparison uses fixed-time byte comparison. User bearer tokens and application permissions do not satisfy this policy. The key grants dispatch access across this coordinated source; it is not a per-company user credential. Supply it through protected deployment secrets, and rotate both server/client settings together with a controlled restart. There is no built-in default credential or overlapping-key rotation window.

HTTPS is required on both sides. Local tests/development can explicitly enable `AllowLoopbackHttp` on both sides; the server also checks the actual peer IP is loopback. Forwarded headers do not establish this exception or TLS identity. For deployments using a proxy, retain TLS from proxy to this host; remote plaintext container traffic is rejected. Do not enable broad forwarded-header trust to bypass this boundary.

Server request bodies are capped at 4096 bytes before binding, including Kestrel chunked bodies; JSON rejects unknown members and depth above 16. Claims return one event, never an unbounded batch. The client caps buffered response bodies at 4 MiB and validates claim integrity/source owner before returning them. A source event above that transport limit will not be dispatched remotely until the event/transport sizing is addressed; it remains under source lease recovery/quarantine policy. Configured client timeout is 1–30 seconds, including claim response buffering. Redirects, cookies and automatic request retries are disabled; the service header is redacted in HttpClient logging.

## Ambiguous completion

A lost claim response may leave a lease until expiry. A lost confirm response may follow a committed confirmation. The client does not repeat either request automatically. Settlement returns false on 409 and never forces an acknowledgement. The dispatcher's existing failure/recovery policy and source token fences remain in effect. Broker publication is still at least once; duplicate event IDs must remain safe in the inbox. This transport does not create exactly-once delivery.

## Running and deployment

Build the solution and publish with `./scripts/Publish-Services.ps1 -IncludeFinancialOutbox`. Use `-Service Integration,FinancialOutbox` with that switch to publish only these packages. The standard fresh-staging/manifest/rollback package flow applies. A Dockerfile is provided for the technical host; Docker verification is separate from local .NET verification.

Required source host settings: `ConnectionStrings__VatDatabase` (financial runtime credential), `Authentication__Authority`, `Authentication__Audience`, `OutboxServer__ServiceKey`, and an HTTPS Kestrel listener/certificate. The shared HTTP foundation still validates OIDC configuration, although internal endpoints use only service authentication. The existing financial runtime role remains broader than an outbox-only role; introducing a dedicated restricted SQL role is a remaining hardening step.

Integration remote settings: `OutboxSource__Mode=Remote`, `OutboxSource__BaseAddress=https://source-host:port` (origin only), `OutboxSource__ServiceKey`, optional `OutboxSource__TimeoutSeconds` (default 5), and `OutboxSource__SourceOwner=financial-coordinated` (default). There is no silent local fallback when remote requests fail. Enable the existing messaging worker separately through `Messaging__Enabled` and valid broker settings. Keep `Messaging__LeaseSeconds` sufficiently above publish and source transport timeouts. Ensure only the intended deployments run dispatch workers during rollout.

For a local loopback-only run, set the two `AllowLoopbackHttp` settings explicitly, use `http://127.0.0.1:5113` as the source origin, and run `Start-Services.ps1 -IncludeFinancialOutbox -Service FinancialOutbox,Integration -Published`. The launcher passes server secrets only to FinancialOutbox and client settings only to Integration. Its normal twelve-service behavior is unchanged. No shared credential is generated or persisted by that launcher.

`Test-FinancialOutboxHost.ps1 -Published -DatabaseReady` checks a fresh process with an ephemeral credential and imported runtime DSN. It checks readiness, controller isolation, anonymous/user-token rejection and authenticated invalid-receipt rejection, without claiming or settling persistent events. The child process is stopped and environment restored afterward. Real claim/settlement tests use disposable PostgreSQL and loopback Kestrel.

## Remaining ownership work

Integration still needs its financial connection for Reporting inbox processing and audited/idempotent operator outbox/inbox operations. Its readiness still checks both databases. Remote mode moves the dispatch source calls only; it does not remove the bridge assembly, SQL role or all financial access. The persistent database count remains six and fully independent business API/core count remains four of twelve. Local configuration defaults were not switched to Remote, no persistent events were claimed by deployment probes, and no live broker cutover is implied.

Next: extract Reporting inbox/projection ownership and operator contracts, introduce the narrower source SQL role, then remove Integration's financial bridge after compatibility/recovery verification. Financial source-owner decomposition and credential federation/rotation can follow their actual bounded-context rollout.

## Verification

The final solution build passed with zero warnings/errors. Focused suites passed **83 tests**: 19 HTTP transport/authentication/validation cases (`TestResults/outbox-http-final`), 11 independent dispatch cases (`TestResults/outbox-http-dispatch`) and 53 PostgreSQL/API regressions (`TestResults/outbox-http-postgres`). The PostgreSQL suite includes two real loopback Kestrel-to-source settlement cases plus source lease fences, reporting/outbox/inbox, API/foundation and purchase/sales posting/close regressions. Its disposable PostgreSQL process stopped cleanly.

The freshly published FinancialOutbox host passed runtime-credential database readiness, dedicated service authentication and business-controller isolation; its probe submitted only an invalid receipt and did not claim persistent events. Published package checks excluded the HTTP adapter from the four independent business hosts and included it in Integration/FinancialOutbox. Changed PowerShell scripts passed syntax checks. Live RabbitMQ, Docker and a new full-suite run are not covered by these focused results.

All twelve freshly published business APIs passed liveness, exact route ownership, authentication and runtime-credential database readiness. Purchase restarted while Sales remained live. Technical/business smoke child processes were stopped and probe environment restored.
