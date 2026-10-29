# 0019: Owned Integration metadata with explicit financial messaging bridge

Date: 2026-10-23

## Boundary

Extract integration_connections, field_mappings, inbound_batches, inbound_rows, external_references, webhook_subscriptions, delivery_attempts and dead_letters into independent Domain/Application/Infrastructure projects and `vat_integration`. Use `IntegrationDbContext` and its own migration history. Preserve internal scoped keys, same-connection mapping enforcement, uniqueness, digest/status/attempt checks, JSONB, maker/checker and optimistic versions. Company/source/event UUIDs are external references. No source schema has an incoming foreign key into these metadata tables at inspection.

Do not move platform.outbox_messages or reporting inbox/projections. Financial posting commits events atomically with source/stock/tax/audit. Existing Integration routes also retry/replay those rows through the financial command executor. Its RabbitMQ worker publishes financial events and maintains reporting inbox data. Repointing that code at an empty Integration database would break delivery and recovery.

Therefore only metadata core/persistence is independently owned in this phase. The API retains Vat.Hosting for messaging and explicitly composes its own metadata persistence. Other financial hosts do not depend on Integration metadata projects. Remove the obsolete generic module read store now that every foundation reader has a typed owned store. The compatibility host composes metadata separately.

## Runtime and cutover

Declare both IntegrationDatabase and VatDatabase on the Integration host in the catalogue/compose/launcher. Readiness requires both contexts' migration histories. All other children retain their single declared runtime connection. Operator/migration credentials and other service connections are stripped before child startup.

Create the owned database with the operator migrator, then apply a consistent locked source snapshot via binary COPY and verify all row fingerprints. An identical target is accepted; a divergent nonempty target is refused. Retain source historical tables without replication/dual writes. The metadata runtime role reads only connection/history data; an independent owner runs migrations. The separate financial runtime credential remains necessary for messaging. Revoke financial access to the legacy integration schema. Source migration history stays unchanged.

## Dispatch port progress

The active dispatcher and V1 claim/confirm/fail contract now live in an independent technical project, with financial SQL behind the source adapter. Expiry/scope/owner/protocol fencing and original event bytes are tested. This remains an in-process port; remote authorization/transport, Reporting inbox ownership and operator contracts below are still required. See [decision 0020](0020-versioned-outbox-dispatch.md).

## Required next contracts for full Integration isolation

1. Keep event creation transactionally at each financial posting owner. Define an explicit event envelope with event/schema version, source owner, tenant/company, aggregate/version, event ID, occurrence time and payload digest. Do not regenerate event IDs on retries.
2. Replace Integration's direct financial dispatcher reads/writes with source-owner claim/confirm/fail operations or owner-side publishers. Preserve exclusive expiring leases, broker confirmation, attempt limits and recoverable failures; a lease token must fence stale acknowledgements.
3. Move reporting inbox/projection ownership to Reporting. Define versioned processing/replay/query contracts, business-duplicate detection, ordering/reconciliation and durable quarantine before replacing direct replay paths.
4. Replace public operator outbox/dead-letter calls with authenticated scoped contracts. Preserve reason, expected version, idempotency key, exact replay behavior and source-owner audit. Test tenant/permission failures and repeated calls across network failures.
5. Test nonempty cutover, broker outage, duplicate/out-of-order events, lease expiry, source commit vs broker confirmation, retry/replay audit and financial close reconciliation. Only then remove the financial connection/Vat.Hosting and mark the entire API independent.

These are requirements, not implemented remote APIs. No network gateway or hidden thirteenth backend is introduced by this extraction.

## Verification

Standalone metadata tests run with no Company/financial tables and demonstrate scoped private-metadata reads while combined readiness correctly reports unavailable financial state. Test all-eight-table nonempty transfer, same-connection mapping enforcement, cross-tenant rejection, invalid digest/self-approval, JSONB/boolean/text preservation, reruns and divergent-target protection. Regression-check existing metadata routes and financial outbox/inbox processing, role boundaries, both migration snapshots, backup restore, and published host readiness/restart isolation.

The Inventory controller had concurrent additions outside this extraction. Its nonexistent db.Products accesses were corrected to the existing Product set and the route inventory reflects its current attributes. This does not validate the new inventory transfer business flow; that code requires its own scoped review and integration tests.
