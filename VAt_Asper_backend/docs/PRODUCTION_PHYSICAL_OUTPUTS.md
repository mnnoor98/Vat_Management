# Approved physical scrap and by-product outputs

Implemented 2026-11-01. A production order may freeze up to 20 distinct physical output products, each classified as `Scrap` or `ByProduct`. Products are resolved through the scoped Master Data reference service, must be separate from the finished product and BOM materials, and are frozen in the independently approved order evidence and SHA-256 digest. Optional order UOM ratios can include these output products.

## Financial posting

The `ByProduct` operation records one approved physical output. It requires a frozen output product, a company warehouse, positive exact quantity, supporting evidence and a positive reviewed WIP value allocation. The server normalizes any frozen alternative input unit to the registered stock UOM without rounding. Approval requires a different actor, current operation/session versions, an open period, enabled posting, a reconciled cost ledger and an effective FIFO or Moving Weighted Average policy for the output product.

Posting adds physical stock and an immutable production receipt cost layer/pool using the reviewed value, while removing the same value from WIP. It does not infer value from a BOM estimate, market price or tax metadata. `Loss` remains the operation for reviewed residual WIP that creates no usable inventory.

A full source-linked reversal removes the original output quantity and value and restores its WIP entry. Existing reservation and cost-layer coverage rules can block reversal if that output has been consumed or reserved. Partial output correction and reopening a closed execution remain unsupported.

Database guards independently verify the frozen output scope, product/warehouse/quantity/value linkage, cost entry, WIP conservation, finished-output totals and exact reversal. By-product receipts use the existing production receipt movement/cost kinds; the linked financial operation retains the physical-output classification and product/warehouse evidence.

## Compatibility and deployment

Older order evidence omits `byProducts` when null, preserving its previous serialized digest. Financial migration `20261031162622_PhysicalProductionOutputs` adds nullable output product/warehouse columns and replaces the production reconciliation guards. Manufacturing stores the optional definition in its existing immutable JSON document and needs no schema migration.

Deploy the reviewed financial migration and code before creating Manufacturing orders with physical outputs. No live migration, deployment or posting activation was performed in this implementation batch. The workflow does not assign statutory VAT treatment to scrap or by-products; that requires reviewed business and tax mapping.

The next production milestone is the Manufacturing execution-status inbox/consumer with deduplication and delivery recovery, followed by advanced corrections and Import/Export.
