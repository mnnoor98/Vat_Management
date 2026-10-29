# Independent Identity core and database

Date: 2026-10-23. Status: implemented for Identity; other contexts remain coordinated.

Identity now builds through its own Domain/Application/Infrastructure projects, with a dedicated DbContext, migration history and database. Generic HTTP/claims/error concerns moved to `Vat.Web`/`Vat.SharedKernel` without financial application dependencies. Financial source-link build projects exclude Identity. The compatibility API composes Identity explicitly; independent financial hosts do not reference its core.

Cross-owner company/branch UUIDs are external references. Internal Identity foreign keys remain tenant/company scoped. Legacy source Identity tables are retained for existing Company access-reference foreign keys, and are not the owned API's database. New write workflows must validate external references and introduce projections/events before those historical links can be removed.

An operator-only transfer uses a source snapshot/share locks, transactional binary copy, row/checksum verification, repeat-safe equality checks and refusal to overwrite divergent target data. Local runtime and migration roles are separate; Identity runtime is read-only for its implemented API, and financial runtime cannot access its database. Passwords are generated and locally protected with Windows DPAPI, with deployment credentials supplied externally.

This completes one ownership cutover, not all twelve. Remaining financial boundaries still require contracts, posting sagas and reconciliation before independent database cutovers. See [ownership operations](../SERVICE_OWNERSHIP.md).
