# 0039: Production compensating reversals

Date: 2026-10-31. Status: implemented and verified. Affected backend regressions: 89 passed; frontend: 163 passed; full solution Release and Angular release builds passed.

Production correction appends a separately approved operation referencing one original posted Conversion, Loss or Receipt. It does not update the original's state or amounts. Only an open execution can receive a correction; a later open fiscal period may hold the compensating entry for a closed-period source.

Finished receipt compensation removes its original quantity/value through the existing cost ledger and physical stock fence, returning its original released cost to WIP. Exact-source FIFO coverage, Moving Weighted Average pool coverage and reserved stock checks can reject reversal. WIP charge reversal cannot create negative WIP.

Source references, expected versions, independent approval, unique posted reversal, immutable journal, deferred source/value constraints and the existing financial transaction preserve traceability and retry safety. Partial production reversals, material-return reversal and execution reopening need separate contracts.

See [workflow and rollout](../PRODUCTION_REVERSALS.md).
