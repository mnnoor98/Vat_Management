# 0017: Owned Compliance context

Date: 2026-10-23

## Decision

Extract Compliance into independent Domain/Application/Infrastructure projects and `vat_compliance`. Preserve the existing returns metadata route and response. `ComplianceDbContext` owns form_templates, form_versions, return_periods, vat_returns, return_lines, reconciliation_runs, filing_evidence and period_close_requests, with its own migration history.

Retain tenant/company composite keys and internal foreign keys, scoped uniqueness, optimistic versions, numeric precision, JSONB snapshots, temporal/state checks, evidence-hash checks and maker/checker constraints. Company/fiscal-period IDs become external references without cross-database foreign keys; retain their scoped lookup indexes. No source schema has an incoming foreign key into Compliance at cutover inspection.

The API is read only. Do not interpret extraction as statutory filing support or as permission to close a Company fiscal period. Future writes need Company reference/version validation, frozen tax-ledger projections, evidence-storage integrity, idempotency/outbox and explicit cross-service close coordination. Existing financial period/posting guarantees remain in their original APIs.

The financial build no longer compiles/registers Compliance application or persistence source. The optional compatibility API composes it explicitly. Common HTTP/kernel libraries contain no financial persistence code.

## Cutover and operations

An operator utility creates a distinctly named target and applies only Compliance migrations. It holds repeatable-read/share source locks and an exclusive target lock, copies tables in dependency order using binary COPY and verifies canonical counts/fingerprints before commit. Identical reruns are accepted; nonempty divergent targets are refused. Self-referencing return corrections are verified in transfer tests. Source data and its migration chain remain unchanged. Freeze legacy writers during cutover; no dual writing or synchronization is provided.

Provision separate `vat_compliance_owner` and `vat_compliance_app` roles. Runtime SELECT is limited to vat_returns and migration history. Remove public access to the owned database and revoke financial runtime access to legacy Compliance tables/schema. Local development credentials use Windows DPAPI; deployments supply their own service secrets. Child API processes receive only their own connection.

## Verification required for cutover

Use a standalone test database without Company/financial tables. Verify permission/scope/cursor/limits, private metadata exclusion, scoped foreign keys, invalid evidence hashes and self-approval rejection. Populate every owned table and test complete graph copy, correction references, numeric/date/JSONB/text preservation, identical reruns and divergent-target refusal. Check source/owned model drift, runtime boundaries, fresh published packages, API readiness, independent restart and both backup restore rehearsals. Record actual results in the ownership guide.
