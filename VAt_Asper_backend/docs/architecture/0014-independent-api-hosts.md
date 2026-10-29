# Independent API hosts with coordinated financial persistence

Date: 2026-10-23. Status: implemented for host/process separation.

The requested deployment shape is twelve backends that can run and deploy independently. Each now has its own executable API project, owned controllers, configuration, launch port and container definition in `src/Services`. Existing domain/application/persistence source files have been assigned to owning service folders while preserving their compiled type identities. `Vat.Hosting` provides common HTTP configuration. The old API is an optional compatibility composition over the same controller sources; individual hosts do not proxy to it.

The current purchase and sales posting paths atomically update stock, tax, source versions, audit and outbox in one PostgreSQL transaction. Splitting those tables into twelve databases during a controller/hosting extraction would change financial guarantees. This change therefore retains the shared database, migration chain and layer-library build bridge. Process independence is achieved; data ownership, migration releases and shared financial code still require coordination.

Only Integration, or the explicitly retained compatibility host, owns the optional messaging worker. Other APIs expose their own HTTP surface. No host migrates the database automatically. Authentication uses the configured external OIDC issuer; legacy fixed-key token issuance and impersonating automatic posting/workbench shortcuts are retired with 410 responses. Canonical permission-checked and versioned source commands remain available.

Next extraction work must define contracts and reference projections, replace cross-owner writes with reconciled posting sagas, implement compensation/replay, backfill data, and then migrate a bounded context to an independently owned database. Foundation read APIs do not imply that missing manufacturing, filing or ERP write workflows are implemented.

See [architecture and operations](../SERVICE_ARCHITECTURE.md) and [route ownership](../SERVICE_ROUTES.md). Existing monetary invariants and posting/reversal decisions continue to apply. This supersedes the single-process hosting portion of the earlier modular-monolith decision, while retaining its coordinated transactional persistence for this stage.
