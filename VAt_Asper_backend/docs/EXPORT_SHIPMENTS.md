# Export shipment evidence and dispatch-cost linkage

Implemented 1 November 2026 as the second Import/Export workflow slice.

An export shipment references one posted sales invoice and its posted fulfillment. The maker selects a posted dispatch with a complete active stock-cost ledger, then records the export declaration/date, destination country, transport document, foreign proceeds, reviewed BDT exchange rate, tax treatment and customs/tax evidence.

The workflow does not calculate another stock issue. It resolves every original sales goods entry to its immutable `Issue` cost-journal entry and freezes their exact total as the export dispatch cost. A missing cost entry blocks preparation. The functional proceeds use the explicit reviewed rate; no rate is guessed. Zero-rated or exempt treatment is accepted only when the posted invoice has zero output tax. This is evidence enforcement, not a claim that a particular legal rate is currently correct.

A different user approves the unchanged draft while the declaration period remains open. Approval rechecks the posted sales entries, cost entries, output tax and source fingerprint. Only one approved shipment is allowed per sales invoice. Database triggers independently reconcile the fulfillment, sales-entry count, cost-entry count, dispatch value, output tax, currency calculation and tax shape, and prevent update/delete/truncate of approved evidence.

Sales returns remain separate immutable corrections in the existing stock ledger. Late export evidence corrections and statutory form/report output are later workflow slices.
