# Full goods-return credit notes

Preserve the original invoice posting version and immutable fulfillment. Use an independent, reasoned credit-note workflow and append corrections linked to each original stock/output-tax entry. A full return negates the frozen original amounts and restores original warehouse quantities; it does not recalculate VAT from current rules.

Use the correction date's Open period so a later return can adjust a previously closed invoice without reopening or rewriting that period. Pending credit requests block their correction period. Sales reconciliation validates the original plus credit-note history and correcting rows; close discovers posted notes even when their correcting ledger rows are missing.

Keep the existing purchase financial projection separate. Extend synchronous sales summaries with signed corrections and a separate credit-note count, and publish SalesCreditPostedV1 for explicit downstream consumers. Full stock returns are the bounded scope; price-only adjustments and partial returns require separate quantity/tax allocation and evidence rules.
