# 0011: Approved receipts and immutable allocation batches

Status: Accepted

Treat customer collection as a separately approved receipt, not a change to sales revenue or VAT. Pending requests freeze intended allocations but reserve no money. Approval revalidates budgets and atomically posts receipt, initial allocation rows, audit and outbox. Permit unapplied collection and later append-only allocation batches.

Serialize receipt/allocation mutations per tenant/company/customer with transaction advisory locks and optimistic payment versions. Use the fiscal company shared fence before the customer lock so close cannot race admission. Allocation cannot exceed remaining receipt value or currently positive invoice gross outstanding. Customer identity must be explicit and scoped; reference-only invoices are excluded.

Later sales credits may create negative invoice outstanding/customer credit. Preserve original receipt and allocation evidence; do not silently move cash or invent a refund. Receipt and allocation reversals require a subsequent explicit workflow.

Read receivables under repeatable read, calculate signed sales minus posted receipts, and expose unallocated cash independently. Close reconciles payment/allocation history and includes dated receipt/allocation checkpoints separately from VAT. Sales/payment events are known notifications outside purchase projections. Old closed snapshots remain immutable.
