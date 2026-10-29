# P0 authentication, inventory scope and legacy transfer recovery

Date: 2026-10-24

## Scope and status

This implements the first stabilization batch from the [full review](../reviews/2026-10-24-full-project-review.md). It removes deployable embedded-key authentication, removes unsafe prototype execution, fixes Inventory read scoping/bounds, adopts legacy Inventory metadata through a tracked migration and reconstructs the two missing legacy transfer journals. It does not implement the complete coefficient/transfer approval workflows or complete the remaining service ownership programme.

## Authentication

Every environment now validates the configured external HTTPS OIDC issuer. The `Authority.Contains("localhost")` shortcut, embedded signing key, fixed issuer and disabled HTTPS metadata path were removed. Allowed signing algorithms are asymmetric RSA/PS/EC families; symmetric HMAC tokens cannot satisfy this validation. The existing issuer/audience/lifetime/signature checks and application-claim validation remain. Test factories explicitly substitute trusted discovery/signing metadata inside tests.

Local clients that previously minted tokens with the old shared key must use a real configured issuer. This change does not create an Identity password/token server. Production/Development localhost-authority regressions verify that no key/configuration shortcut is installed, valid trusted RSA tokens validate and forged symmetric tokens fail.

## Unsafe workflows are explicitly unavailable

Manufacturing coefficient GET/POST/approval no longer execute controller SQL, schema changes or fabricated actor/approver logic. They require company membership and named read/request/approve permissions, then return 501 `coefficient_workflow_unavailable`. New permissions are `manufacturing.request` and `manufacturing.approve`; they do not automatically grant issuer roles. The existing production-order metadata reader remains operational.

Inventory transfer POST no longer changes balances or invents a checker. It requires InventoryManage/company scope, then returns 501 `transfer_workflow_unavailable`. This is a deliberate unsupported capability, not a successful transfer implementation. A future approved workflow needs real source references, checker actions, expected versions, admission fencing, journal posting, audit/outbox and idempotency before this route is enabled.

Existing validated warehouse creation, financial posting and balance endpoints remain. Newer warehouse/all-balance reads join on complete tenant/company/ID keys. Their pages use bounded skip/take. Transfer history now goes through an application read service and scoped store, with bounded header/line reads and fully scoped related entities. Reused IDs across tenants are covered by a regression that would expose the previous joins/filter errors.

## Tracked schema and paired journal

Financial migration `20261024100358_TransferJournalRecovery` is migration 18. It adopts existing transfer metadata columns with compatible-type/lossless numeric checks and also creates them on a clean database. The fields are now mapped in the EF model. It adds `inventory.transfer_journal`, with scoped foreign keys (including transfer-plus-line identity), positive quantity, distinct warehouses and a unique source-line journal constraint.

One journal record represents both sides of a transfer line: negative quantity at FromWarehouse and positive quantity at ToWarehouse. Warehouse reconciliation includes these entries alongside purchase stock movements and goods sales postings. New fiscal-close snapshots include transfer IDs and entries in the ledger fingerprint. Existing snapshots are not rewritten; the new snapshot field is optional for old serialized data.

The new journal is runtime read-only in the local role provisioning/check scripts. The reconstruction CLI is operator-only and is not registered as an HTTP service. The migration refuses destructive Down execution; an approved rollback uses the verified pre-migration backup rather than dropping historical/adopted data columns.

Manufacturing's legacy financial prototype extension fields are not promoted into an active owned workflow by this migration. Their data is preserved in backup/source history. Owned coefficient fields, reference projections, migration/backfill and audited commands remain a separate delivery gate.

## Proven local reconstruction

The review found two existing Posted transfers, quantity 200 and 50, for the same product/source/destination. Their combined 250-unit movement exactly explained the two balance/journal differences: source balance 1150 versus base journals 1400; destination balance 250 without a base movement journal.

Before schema/data work, all six databases were backed up and each dump passed a restore rehearsal. Evidence is under ignored `artifacts/p0-remediation/backups-20261024T100111Z/manifest.json`. No credentials were persisted in the tool/source changes.

`tools/Vat.TransferJournal.Repair` defaults to preview and requires an operator connection through `ConnectionStrings__VatMigrationDatabase`, explicit tenant/company, operator identity and reason. Its transaction:

1. Serializes with company financial-period fencing and locks the relevant document/balance/journal tables during proof.
2. Reads only scoped Posted source transfers/lines, verifies complete evidence and rejects divergence from already-recorded journal entries.
3. Requires missing journal dates to be in an Open period; Closing/Closed history is refused.
4. Computes base movements plus all documented paired transfers and requires exact equality with all current scoped balances. Unexplained differences produce no writes.
5. On explicit apply, writes missing journal rows and reconstruction audit entries atomically; it does not update stock balances or reapprove the historical documents.

The local preview found 2 missing entries and a stable quantity-evidence digest. Apply inserted **2 paired journal entries totalling 250 units** and **2 audit entries**. A second apply found 0 missing entries and wrote nothing. Preview/apply/repeat manifests are under `artifacts/p0-remediation/repair-*.json`.

The updated read-only diagnostic then reported **5 stock-balance rows, 0 unreconciled quantity rows**, compared with 2 unreconciled rows in the original review. This fixes the proven quantity-journal omission; it does not retroactively certify the prototype's claimed checker identity or all tax/valuation evidence. The audit reason explicitly records that limitation.

Reproduction after migration:

```powershell
# Supply the operator DSN securely in this process; omit --apply for preview.
dotnet tools/Vat.TransferJournal.Repair/bin/Release/net10.0/Vat.TransferJournal.Repair.dll <tenant> <company> <operator> "Documented repair reason"
# Inspect the preview; the service rechecks all proof under its apply transaction.
dotnet tools/Vat.TransferJournal.Repair/bin/Release/net10.0/Vat.TransferJournal.Repair.dll <tenant> <company> <operator> "Documented repair reason" --apply
# Use tools/diagnostics/StockQuantityReconciliation.sql with the runtime read role.
```

Do not use the tool to force an unexplained adjustment, repair closed history automatically, or substitute for the missing new-transfer business workflow.

## Verification and remaining work

The solution built with zero warnings/errors. Three authentication regressions and 43 focused database/API cases passed, including reconstruction preview/apply/repeat, unchanged balances, audit rollback/recovery, changed-evidence rejection, Closing-period refusal, tenant-ID reuse isolation, unavailable-route authorization and existing fiscal-close/module cases. Runtime role checks passed after the local migration, including read-only access to the reconstruction journal. CI now includes FinancialOutbox publication and both technical-host authentication probes.

Final verification passed **316 tests, 0 failures, 2 live-RabbitMQ cases skipped** across all eight test projects (`TestResults/p0-full`, aggregate `artifacts/p0-remediation/test-summary.json`). The 16 new cases cover the P0 authentication, containment, scoped-read and reconstruction changes. Temporary PostgreSQL stopped cleanly.

The post-repair financial backup passed another restore rehearsal (`artifacts/p0-remediation/post-repair-backup-restore.log`); the financial EF model reports no pending changes. All thirteen business/technical packages were freshly published. Reporting inbox and FinancialOutbox probes passed service authentication and database readiness. All twelve business APIs passed liveness, exact route ownership, authentication and runtime-credential database readiness; Purchase restarted while Sales stayed live. Smoke child processes were stopped and probe environments restored. Static controller/authentication checks and changed PowerShell syntax checks passed; the verified source snapshot remained unchanged.

Live broker/Docker/external issuer deployment and the remaining business/module extraction programme remain separate acceptance gates. The historical review's stock/signing-key findings are remediated as described; unavailable workflows remain visibly unsupported, not certified complete.
