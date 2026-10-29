# Enterprise VAT backend: current review and delivery plan

Review date: 24 October 2026 (Asia/Dhaka).

Stabilization update: [P0 fixes and proven transfer-journal reconstruction](docs/architecture/0023-p0-auth-inventory-recovery.md). The embedded-key authentication path and unsafe prototype execution were removed; the proven two-row quantity discrepancy is now reconciled through audited paired journals. The follow-up [transfer and coefficient workflows](docs/architecture/0024-transfer-coefficient-workflows.md) replace those 501 gates with controlled commands. The original findings below describe the pre-fix review snapshot.

The current source/database review is [Full project review and implementation plan](docs/reviews/2026-10-24-full-project-review.md).

Current state: twelve business API hosts, six persistent databases, five independent API/core/data owners. Purchase/Sales financial workflows and versioned messaging contracts are implemented; several other modules remain metadata/read foundations.

The original review found 2 unreconciled rows among 5 stock-balance rows and a deployable fixed-key authentication path. The subsequent P0 batch removed that path and reconstructed the proven missing paired transfer journal under verified backup and audit; the current quantity check has 0 mismatches. Production issue/receipt/wastage workflows, external issuer/operational acceptance and the later ownership programme remain release gates. The review itself was read-only; the separately documented remediation applied the correction.

Delivery order:

1. Backup/source baseline, reconciliation and authentication/unsafe-flow stabilization.
2. Delivered: tenant-scoped Inventory Draft/submit/approve/post/cancel commands, paired journals, idempotency and period admission.
3. Delivered: owned Manufacturing coefficient Draft/submit/approve/cancel revisions, API-verified reference snapshots, calculated costs, audit and restricted runtime access. Production issues/receipts/wastage remain future workflows.
4. Integration operator/host ownership cutover delivered; Reporting source-completeness, rebuild/export contracts and database cutover remain next.
5. Extract Company/MasterData reference owners, then coordinated financial participants with recovery/fencing contracts.
6. Complete remaining module business workflows and regulatory/external contracts.
7. Prove live broker/container/OIDC operations, performance, restored backups and a controlled reconciled pilot.

The detailed review contains the twelve-module matrix, database catalog counts, source-backed findings R01–R10, acceptance gates, first backlog B01–B08 and verification limitations. See also [current ownership](docs/SERVICE_OWNERSHIP.md).

Post-remediation full local baseline: **316 passed, 0 failed, 2 live-broker cases skipped** across eight test projects. Runtime roles, model checks, backup/restore and twelve-host readiness/restart checks passed. The proven stock discrepancy is reconciled; this historical baseline does not certify the subsequent workflow changes or external filing.

The superseded [PDF-only design-stage review](docs/reviews/2026-10-23-design-stage-review.md) is retained as history; its description of an empty source workspace is no longer current.

Latest workflow verification: **325 passed, 0 failed, 2 live-broker skips** across eight projects, followed by **13/13 standalone Manufacturing tests** after the description compatibility fix. All twelve published API readiness/route/auth checks and restart isolation passed; the final Manufacturing package was refreshed and independently probed. Local stock remains reconciled (5 rows, 0 mismatches). See the [workflow delivery evidence](docs/architecture/0024-transfer-coefficient-workflows.md).

Integration ownership delivery: the independent host uses only `vat_integration` and remote owner contracts; financial libraries/connection were removed. Final suite: **338 passed, 0 failed, 2 live-broker skips**; all twelve published API readiness/route/auth checks and restart isolation passed. Current isolation is **5/12** with **six persistent databases**. See [delivery proof](docs/architecture/0025-independent-integration-operators.md) and [next Reporting implementation plan](docs/architecture/0026-reporting-cutover-plan.md).
