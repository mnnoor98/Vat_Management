# Financial source-export coverage proof

The staged independent Reporting core can now verify its complete baseline-plus-live event set against a specific reconciled, frozen financial-owner export. This establishes **coverage of that export at its source capture**, not a source commit sequence, current-source freshness, customer coverage or production cutover. The supported scope remains explicitly `purchase-financial-v1`.

## Download and verification

`FinancialCoverageService.Verify` requires the verified operator's company scope, `ReportManage`, command/export/generation UUIDs, an expected local verification version and a bounded reason. A committed retry resolves from its actor/payload-bound receipt before contacting the source, including when the source is offline or the generation has subsequently changed. A different actor/body under the same key conflicts.

Bootstrap and coverage verification share `FinancialExportDownload`: one 20-second deadline, complete manifest/scope/coverage validation, stable bounded pages, exact counts/order/relationships and canonical SHA-256. The existing trusted-origin HTTP adapter uses the original delegated bearer token and refuses redirects/cookies. Download and validation take no target transaction or company lock. Source failure, partial pages or invalid evidence persist no proof or command receipt.

After downloading, verification acquires the **same target company advisory lock as delivery, replay and retirement**, then reads the latest state without tracked-state reuse. It refuses retired generations and stale expected verification versions. Pending originals, unresolved dead letters and rejected inbox dispositions all block verification. Resolving a dead-letter record alone cannot hide an inbox disposition that remains quarantined.

The target projection is the complete union of immutable snapshot entries and immutable live entries for that exact tenant/company/generation. Row count and SQL UTF-8 byte totals are bounded before materialization. Nothing is date-filtered or truncated: V1 supports at most 10,000 rows and 8 MiB. Rows sort by the wire contract's ordinal transaction UUID order. Count, full row equality and canonical fingerprint must all match the verified source. Equal signed totals alone cannot mask missing, extra, contradictory or substituted event identity/reference/date/amount evidence. If delivery advances the target during download, an old export no longer matches and verification fails safely.

## Durable evidence and status

Reporting migration `20261024162436_FinancialExportCoverage` adds `reporting.financial_coverage_verifications`, with scoped generation foreign keys, unique generation/version and generation/export identity, manifest JSON, exact fingerprint/count and verification time. PostgreSQL triggers prohibit UPDATE/DELETE/TRUNCATE; the migration is forward-only. Runtime access should be SELECT/INSERT only with a separate migration owner. No request performs DDL.

Proof, operator audit and command receipt commit together. Audit failure rolls everything back. Concurrent retries commit once. A new proof increments a **local verification version**, which is a concurrency token for accepted target proof history, not source commit ordering. Rechecking an already verified immutable export deduplicates its proof after current-state validation; it cannot rewrite an earlier proof or move the latest version backwards. A committed old receipt remains historical evidence and cannot overwrite newer proof history.

`Status` uses a repeatable-read transaction. It returns the latest local proof, whether the current projection still matches that proof with no unsettled work, and separate pending/quarantine/rejected counts. Later delivery makes a prior proof's current-projection match false; the original proof remains intact. Read requires `ReportRead` and the same company/tenant/generation scope. Source capture time is retained exactly from the immutable manifest; verification time is stable across PostgreSQL timestamp round trips.

Every proof exposes its `SourceScope`, `ExportCoverageVerified=true`, source capture time and **`LiveCoverageVerified=false`**. Status also keeps live verification false, even if its projection matches a proof. A source may commit after capture without delivering anything yet: the old export can still be completely covered while current-source freshness is unknown. An empty inbox, latest observed timestamp, largest UUID, or local verification version must never be used to claim live caught-up coverage or trigger production promotion.

## Remaining cutover gates

The financial source schema remains at 19 migrations; the owned Reporting chain now has four. This step needs no new source table, dispatch reset, deletion of active projections, permanent database provisioning or API connection change. Current runtime ownership is still five independent business APIs and seven coordinated APIs.

Required next work is a durable source commit/catch-up protocol with a verified continuing boundary, complete customer live movements and scope completeness, coordinated generation verification/promotion, production transport/routes, migration/least-privilege provisioning, and backup/restore/cutover rehearsals. The proof here is reusable historical evidence for those gates; it does not replace them.

## Verification

Final verification on 24 October 2026: the complete 56-project Release solution build passed with zero warnings/errors. All **89 selected affected tests passed**: 70 independent Reporting core/HTTP cases (including 14 new coverage cases) and 19 financial-source/customer API cases. The real-source parity case freezes an original Purchase export, posts a real reversal, imports the earlier baseline into a separate owned database, delivers the owner's actual event bytes and verifies exact event-set/fingerprint coverage against the later complete source export. Existing financial totals and customer statement/ageing parity remain valid with all four owned migrations.

Tests cover matching and mismatching identity/reference/counts, invalid source evidence, source unavailability, offline committed replay, later delivery invalidating a current-projection match, immutable historical proof/version behavior, rejected dispositions hidden behind resolved dead letters, concurrency, audit-failure rollback, source-download races with delivery/retirement, tenant/permission isolation and append-only triggers. Both financial and Reporting EF models have no pending migration changes. Dependency inspection retains only the own three projects, neutral messaging and SharedKernel. Temporary databases were removed and the PostgreSQL cluster stopped successfully.

Logs use `artifacts/financial-coverage-*`, final TRX files use `TestResults/financial-coverage-final`, and source/test/dependency hashes are under `artifacts/reporting-financial-coverage`. Seven other test projects had no matching cases under the explicit filter. This is not a full-suite rerun, live broker/container acceptance, permanent database migration or production runtime cutover.
