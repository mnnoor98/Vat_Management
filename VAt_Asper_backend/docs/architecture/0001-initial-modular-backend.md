# ADR 0001: Modular backend for the first delivery

Status: implemented foundation; service extraction remains a later decision.

The later [atomic posting decision](0002-atomic-purchase-posting.md) supersedes the initial implementation limits below concerning master data, calculation snapshots and local stock/tax posting. This document retains the reasoning for the original foundation.

## Context

The twelve supplied service specifications describe desired business boundaries, but there was no executable application. Team size, scale and hosting constraints have not been established. An initial purchase approval flow needs a reliable local transaction before introducing distributed posting.

## Decision

Use one ASP.NET Core API with Domain, Application and Infrastructure projects. Domain code has no framework dependency. Application owns use cases, DTOs, permission names and storage abstractions. Infrastructure owns PostgreSQL mappings and transactions. API owns bearer validation, HTTP policies and transport concerns.

Company and Purchasing have separate database schemas. Platform records contain audit, idempotency and outbox storage. Composite foreign keys prevent a purchase from referencing a branch in another company/tenant. Stores require tenant/company arguments; application services also enforce signed company grants. This iteration does not use PostgreSQL RLS.

Every mutation requires an idempotency UUID. A PostgreSQL transaction advisory lock serializes concurrent retries of that tenant/company/operation/key. The owner subject and canonical typed payload hash must match. Domain changes, audit, outbox and successful HTTP response commit together. Failed commands roll back and do not consume a key. Keys currently have no expiry; a retention policy must preserve financial duplicate protection before cleanup is introduced.

Purchase Version is an optimistic concurrency token. Approval is valid only in Submitted state, against the expected document version and by someone other than the maker. Approval persists ApprovedPendingPosting and PurchaseApprovedV1. No dispatcher or financial participant is included yet; this is not a completed financial posting.

## Identity contract

Use an external HTTPS OIDC authority. Validate token signature, issuer, audience, expiry, one tenant_id UUID and one stable sub. Repeated company_id UUID claims grant company access; repeated permission claims grant operations. Company management additionally requires a grant for the supplied company UUID. An administrative provisioning workflow must pre-grant that UUID in the issuer. No password store, login endpoint, test-token endpoint or header-based authentication exists in the API.

Grants currently apply to all branches of a granted company. Branch-specific grants, live permission revocation and issuer provisioning are subsequent identity work. Short token lifetime and issuer policy must be configured by the deployment owner.

## Consequences

- The first slice can be tested with real PostgreSQL without an identity-provider network dependency in tests; tests still validate RSA-signed JWTs.
- Transactional approval is simpler, while module ownership and event schemas leave a path to service extraction.
- Shared deployment/database availability is an accepted initial tradeoff.
- Extracting modules will require removing local cross-module foreign keys and introducing validated snapshots, sagas and period fences.
- Supplier/product references are draft snapshots, not validated master-data entities. Currency is BDT only. Net amounts round per line to four decimals, midpoint away from zero; this does not define VAT rounding.
- There is no VAT calculation, stock posting, period close, return generation, RabbitMQ delivery or inbox processing in this delivery. These must be implemented before any production financial workflow.

## Next acceptance milestone

Reviewed master-data/tax snapshots -> durable posting coordinator -> inventory/tax participants -> broker outbox/inbox delivery -> reconciliation -> period-close fence. Define the participant protocols and approved calculation examples before implementing their financial effects.

## Technical references

- [ASP.NET Core bearer token validation](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication)
- [Npgsql EF Core 10 provider](https://www.npgsql.org/efcore/release-notes/10.0.html)
- [RabbitMQ confirms and acknowledgements](https://www.rabbitmq.com/docs/confirms)
