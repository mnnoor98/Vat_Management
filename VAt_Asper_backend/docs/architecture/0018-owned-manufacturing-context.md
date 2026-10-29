# 0018: Owned Manufacturing context

Date: 2026-10-23

## Decision

Extract Manufacturing into independent Domain/Application/Infrastructure projects and `vat_manufacturing`. Retain the existing production-order metadata route, permissions, tenant/company filters, cursor limits and response shape. `ManufacturingDbContext` owns bom_headers, coefficient_versions, bom_lines, production_orders, material_issues, production_receipts and wastage_records with a separate migration history.

Retain composite scoped keys/internal foreign keys, unique references, maker/checker, positive-quantity, wastage-range, date/state and optimistic-version checks. Numeric fields retain precision 24,6. Product and warehouse UUIDs remain external references with scoped lookup indexes; BusinessDate indexes are retained. Company, MasterData and Inventory tables are not part of the owned database. No other source schema references Manufacturing through an incoming foreign key at inspection.

The API remains read only. This does not introduce inventory posting or production execution. Future writes need validated product/warehouse projections, fiscal-period/version admission, immutable stock journals, idempotency/outbox, and retry/reconciliation across the Inventory boundary. Financial posting and its current atomic guarantees remain with the existing APIs.

Financial layer source globs and hosting registration exclude Manufacturing. The optional aggregate host composes its own persistence explicitly. Shared Web/SharedKernel do not contain financial persistence.

## Cutover and runtime access

The operator migrator creates a distinctly named target, applies owned migrations, copies a repeatable-read/share-locked source snapshot in dependency order, and verifies every table's count and row fingerprint before commit. Identical reruns are accepted; a nonempty divergent target is refused. Source rows and the coordinated migration chain remain unchanged. Freeze legacy writers for cutover; no replication is provided.

Use `vat_manufacturing_owner` only for migrations and `vat_manufacturing_app` for production-order/history SELECT. Revoke public access to the owned database and financial runtime access to the historical Manufacturing schema. Local credentials use the existing Windows DPAPI profile; deployed hosts receive separate environment secrets. Each launched child receives only its own connection.

## Validation

Standalone tests use no Company/MasterData/Inventory tables. Validate metadata auth/scope/paging/privacy, seven-table/internal-FK ownership, cross-tenant issue rejection, invalid wastage and self-approval rejection. Populate all seven tables for binary transfer tests including decimal/date and quoted multiline Bengali text, repeatable imports, divergent-target refusal and source preservation. Check both model snapshots, backup restore rehearsals, role boundaries, independently published packages and all twelve hosts' readiness/restart isolation. Record observed results in the ownership guide.
