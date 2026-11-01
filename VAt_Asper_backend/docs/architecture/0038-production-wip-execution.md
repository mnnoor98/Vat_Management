# 0038: Approved production evidence and actual WIP

Date: 2026-10-31. Status: implemented and verified; see the implementation report for test evidence and rollout limits.

Manufacturing owns approved order/BOM evidence. The financial owner imports that evidence through delegated authenticated HTTP and owns actual material, conversion, WIP and finished-stock posting. No coefficient estimate is posted as actual money, and neither service reads or writes the other service's database.

Each operation requires its own evidence, independent approval and current session version. Material issues and returns use the existing approved stock ledger; finished receipts carry reviewed WIP release values. Financial posting updates physical stock, cost journal, WIP, audit and outbox in one transaction. Deferred database guards check immutable sources and reconciliation. Period checkpoints include WIP.

The import receipt and outbox provide durable financial acknowledgement. Manufacturing remains the immutable source owner; automatic consumption of execution-status events is a later integration task. Corrections, UOM conversion, physical scrap/by-products and subcontract ownership need separate contracts.

See [workflow and rollout](../PRODUCTION_WIP_EXECUTION.md).
