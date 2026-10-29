# Transfer and owned coefficient workflows

This change replaces the prototype 501 command gates with scoped application services and database transactions. Inventory remains a coordinated financial participant; Manufacturing continues to reference only its own core, SharedKernel and common HTTP packages.

## Inventory

POST `/api/v1/companies/{companyId}/transfers` creates a Draft. It requires `inventory.manage`, `Idempotency-Key` (UUID), an open fiscal period, two warehouses and 1–100 distinct company products. Quantities/prices have at most six decimal places. Quantity must be positive; stock is not changed at draft creation.

POST `/transfers/{id}/submit`, `/approve`, `/post` and `/cancel` accept `{ "expectedVersion": 1, "reason": "..." }`. Submit/cancel require `inventory.manage`; approve requires `inventory.approve`; post requires `inventory.post`. Approval must come from someone other than the document creator. Cancel requires a reason and is available before posting. Posted documents are immutable through these routes; this release does not introduce an accounting reversal route.

Every command requires a new UUID idempotency key; retrying the exact command with the same key returns the stored response. Changed payload/actor conflicts. Read history includes `version` and `approvedBy` so clients can submit the current version. Submitted and Approved transfers block period close until posted or cancelled.

Posting acquires the company financial fence exclusively before stock writes. It rechecks the open period and available source quantity excluding reservations. Source decrease, destination increase, immutable paired journal, document version/state, audit, outbox event and idempotency response commit together. A failure rolls everything back. The journal uses the existing evidence format, preserving legacy reconstruction verification. The financial runtime may insert/read journal rows but cannot update/delete/truncate them.

## Manufacturing

The owned migration `20261024103549_CoefficientWorkflow` adds three Manufacturing-only tables: `coefficient_documents` (immutable JSON snapshot and SHA-256), `coefficient_commands` (actor/payload-bound stored responses), and `coefficient_audit`. Existing BOM/coefficient/line data is retained. The migration is forward-only to preserve audit history. The legacy cutover tool refuses a target where the new workflow is already active.

GET `/api/v1/companies/{companyId}/manufacturing/coefficients` supports `before` and `take` (1–100). POST creates a Draft; POST `/{id}/submit`, PUT `/{id}/approve`, POST `/{id}/cancel` use expected versions and UUID idempotency keys. Permissions are `manufacturing.read`, `manufacturing.request` and `manufacturing.approve`. A different actor must approve. Cancel requires a reason and is allowed for Draft/Submitted only. Approved revisions with overlapping inclusive effective dates for one declaration are rejected. A declaration cannot change its finished product.

Create fields: `declarationNumber`, `finishedProductId`, `batchSize`, `effectiveFrom`, optional `effectiveUntil`, `directLaborCost`, `utilityCost`, `overheadCost`, and `materials` containing `productId`, `quantity`, `wastagePercent`, `unitPrice`. One declaration groups monotonically numbered revisions. Client display labels and fabricated HS/UOM defaults are not used. Material cost = quantity × (1 + wastagePercent / 100) × unitPrice, rounded to six places. Unit cost = total material/labor/utility/overhead cost divided by batch size. Submitted prices remain declared input, not a certified valuation or regulatory filing.

The reference adapter calls the scoped MasterData product endpoint with the caller's bearer token; the caller also needs `masterdata.read`. It checks returned product identity and response size. No financial SQL connection or cross-database join is introduced. The immutable snapshot records verified product descriptions/UOM and validation time; the existing product endpoint does not expose a revision number, so this is a point-in-time snapshot. Approval binds to that snapshot and rejects changed snapshot hashes. Legacy coefficients without snapshots remain readable but require a new verified revision for execution.

Configure `ManufacturingReferences:MasterDataBaseUrl` to a trusted HTTPS origin. Redirects/cookies are disabled. Development uses explicit loopback HTTP at port 5103; remote HTTP is rejected. An unavailable/misconfigured reference service fails closed with 503 and no command/audit/BOM writes. Production still requires the external OIDC issuer; the removed embedded signing key has not been restored.

Manufacturing runtime reads production metadata, inserts BOM/line/snapshot/command/audit rows, and inserts/updates coefficient state. It cannot write production orders/material issues or modify/delete immutable workflow history. Owner-only migrations and separate database ownership remain in place.

## Verification

Full suite: **325 passed, 0 failed, 2 live-broker cases skipped** across eight projects (`TestResults/pending-full`). The subsequent 500-character product-description alignment passed all **13 standalone Manufacturing tests** (`TestResults/pending-manufacturing-final`). Long descriptions remain complete in the immutable snapshot; the bounded BOM display name uses the product reference when necessary. Reference validation has a 20-second overall deadline.

All twelve published business APIs passed exact route/authentication/database readiness checks; Purchase restarted while Sales stayed live. The final Manufacturing package passed a separate readiness/route/authentication probe after refresh. FinancialOutbox and Reporting service-auth probes passed. Financial/Manufacturing models have no pending changes; runtime role/isolation checks passed, and the local stock diagnostic has 0 mismatches across 5 balances. Pre-change financial/Manufacturing and post-migration Manufacturing backup restore rehearsals passed. Temporary smoke processes and test clusters were stopped.

See `artifacts/pending-workflows/test-summary.json`, `artifacts/pending-workflows/source-manifest.json`, `artifacts/pending-build.log`, `artifacts/pending-financial-backup.log` and `artifacts/pending-manufacturing-backup.log` for this execution's evidence. Local external issuer and live-broker acceptance remain separate from application integration tests. Broader Reporting/Integration ownership cutovers and the remaining module programme are tracked in the project roadmap.
