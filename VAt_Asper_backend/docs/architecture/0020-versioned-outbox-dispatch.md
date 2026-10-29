# 0020: Versioned outbox dispatch port and scoped lease settlement

Date: 2026-10-23

Follow-up: [decision 0021](0021-authenticated-outbox-source-transport.md) adds an optional authenticated remote adapter and separate technical host. The original local mode and the incomplete inbox/operator ownership described below remain applicable.

## Implemented boundary

`Vat.Messaging` contains the dispatch loop, broker publisher abstraction/configuration and V1 claim/receipt/source port. It depends on framework primitives and Options only, not EF, Npgsql, RabbitMQ, financial domain/application/infrastructure, or Integration metadata projects. Existing messaging namespaces are retained for source compatibility; assembly/project ownership is separate. `FinancialOutboxLeaseSourceV1` in financial Infrastructure owns SQL, leases, attempts, retry policy and quarantine. Hosting resolves that adapter through `IOutboxLeaseSourceV1`; the active worker uses the independent dispatcher. This is executing code, not an unused interface.

A V1 receipt carries dispatch protocol version, explicit source owner, event ID, tenant/company and lease token. A claim also carries aggregate/version, original event type and committed timestamp, exact stored payload bytes, SHA-256, lease expiry and attempt. The source owner is honestly named `financial-coordinated` until financial bounded contexts split. ContractVersion describes the dispatch protocol, not a replacement event schema. Existing JSON event envelopes retain their own schema version and RabbitMQ message ID/type/body are preserved. The publisher receives a payload copy so it cannot mutate the claim. Invalid identity/protocol/digest is rejected before publishing; event payload semantics still belong to the existing consumer.

Claims retain transactional SKIP LOCKED exclusivity, fresh tokens and expiring leases. Exhausted records are not claimed again. Confirm/fail must match owner/protocol/event/tenant/company/token, have an unexpired lease at the source owner's settlement attempt and remain undispatched/unquarantined. Wrong/stale/repeated settlement returns false without changing the row. Expired tokens are rejected even before a new worker reclaims the event. A later claim preserves event ID/payload and changes only lease/attempt state.

Retry delay and quarantine use the owner database's attempt count and configured limit, not a supplied claim attempt. Failure codes are restricted to safe operational names. Failed claim persistence rolls back lease/counter changes. Host cancellation leaves its lease for recovery. Publishing remains at least once: a confirmed broker publish with a lost/expired source acknowledgement can be republished with the same event ID; inbox deduplication remains required. False confirmation is not converted into a forced acknowledgement.

## What remains coordinated

The source port currently runs **in process** through the financial adapter. This does not add public/internal HTTP endpoints, service credentials or an RPC transport. Integration still needs the financial connection/hosting bridge; Reporting inbox/operator retry/replay still use their existing financial services. Broker transport and worker orchestration remain in financial Infrastructure. The port removes the dispatch algorithm's SQL dependency, not the entire host's dependency.

Next: implement authenticated source-owner claim/confirm/fail transport or source-owner publishers, including ownership routing, restricted service identity, request bounds, timeout and ambiguous-completion behavior. Move Reporting inbox/projections and operator operations through their own scoped/versioned contracts. Preserve existing audit/idempotency/reconciliation before removing the financial connection. Do not expose a global claim port to user JWTs without a service authorization boundary.

No database schema or stored event format change is required by this step. Project database count remains six and fully isolated API/core count remains four; Integration metadata is separately owned but messaging extraction is incomplete.

## Verification

Pure dispatch tests check protocol serialization/integrity, assembly dependency independence, unchanged wire identity/bytes, payload-copy behavior, safe failure codes, cancellation, bounded claims and stale-confirm handling. Real PostgreSQL tests check scoped/versioned receipts, wrong tenant/company/event/token/owner/version rejection, expiry before/after reclaim, retry/quarantine from owner state, exhausted records and failed-claim rollback. Existing outbox/inbox/metadata/posting tests and published process/readiness checks must pass before recording this milestone complete.

Verification completed: 11 independent dispatch tests and 51 PostgreSQL/API regression cases passed. The solution built with zero warnings/errors, and the financial model has no pending changes. All twelve freshly published APIs passed liveness, route ownership, authentication and database readiness with runtime credentials. Purchase restarted while Sales remained live; smoke processes were cleaned up. The four independent API packages exclude Vat.Messaging; the eight financial/transitional packages include it. These are focused suites, not a new full-suite or live RabbitMQ run.
