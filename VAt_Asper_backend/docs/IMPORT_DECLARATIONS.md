# Import declaration and landed-cost linkage

Implemented 1 November 2026 as the first Import/Export workflow slice.

An import declaration belongs to one prepared purchase posting plan. The maker records the declaration number/date, foreign currency invoice amount, reviewed BDT exchange rate, recoverable and non-recoverable tax evidence, and classified Duty/Freight/Insurance/Port/Clearing/Other charges. Each charge has its own evidence and an explicit capitalizable flag. The system calculates and freezes the functional invoice amount and capitalizable charge total; it never guesses a rate or charge.

A different user must approve the unchanged declaration while the receipt date remains open and the plan remains prepared. One approved declaration is allowed per receipt. Approved evidence cannot be updated, deleted or truncated.

The landed-cost review may reference the approved declaration. When linked, its plan and total must exactly match the declaration's prepared receipt and capitalizable charge total. The API and database triggers both enforce this reconciliation. The existing complete line allocation then capitalizes the approved BDT total through the FIFO or Moving Weighted Average stock ledger. Recoverable/non-recoverable tax remains separately evidenced so a tax component already present in the purchase calculation is not silently capitalized again.

Domestic purchases can continue using the existing landed-cost review without an import declaration. Late charges after receipt posting remain part of the advanced correction backlog. Export shipment/evidence linkage is documented in [EXPORT_SHIPMENTS.md](EXPORT_SHIPMENTS.md).
