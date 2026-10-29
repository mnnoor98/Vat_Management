# Integration backend

Integration has its own API, Domain/Application/Infrastructure projects and eight owned tables in `vat_integration`. It has no financial project reference or financial database connection. Port: **5112**. Supply `ConnectionStrings__IntegrationDatabase` plus the external OIDC authority/audience. Readiness checks only its own migration history.

Connection metadata reads retain their restricted runtime role. Public messaging URLs are unchanged, but operator calls delegate the caller JWT, original idempotency key and correlation ID to versioned owner endpoints. Configure `OperatorTargets__SourceBaseUrl` and `OperatorTargets__InboxBaseUrl` as trusted HTTPS origins. Development explicitly targets the local FinancialOutbox technical host on port 5113. An unavailable owner returns 503; Integration never falls back to financial SQL.

With messaging enabled, configure remote outbox/inbox adapters and distinct worker service credentials. Legacy Local mode is rejected. Default messaging remains disabled. Operator JWT permissions and worker service keys are separate capabilities.

The existing metadata migrator remains an operator-only snapshot/copy utility. No new migration or data copy is required for this host/core cutover. ERP batch processing and webhook workflows are separate future work.

See [operator contracts, configuration and limits](../../../docs/architecture/0025-independent-integration-operators.md), [service ownership](../../../docs/SERVICE_OWNERSHIP.md), and [routes](../../../docs/SERVICE_ROUTES.md).
