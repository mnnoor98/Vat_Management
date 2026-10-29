# Partial sales return allocation

Freeze selected return quantities and their allocations at request time. Allocate net and output VAT from exact original posting values with rounded cumulative targets, subtracting prior posted credit. This prevents independent per-return rounding from exceeding the original tax and consumes the final residual exactly. Use integer rational arithmetic for decimal boundary cases.

Retain one pending request per invoice and optimistic source/decision versions. Allow multiple posted notes and multiple corrections per original entry, with a unique note/original link. Preserve historical cancelled allocations while excluding them from consumed capacity. Derive invoice completion from returned quantity rather than the number of notes or remaining monetary value.

Backfill legacy full-credit line snapshots without changing original ledger rows or past event bodies. Publish a distinct partial-credit event so full-return consumers do not infer whole-invoice cancellation from a partial return. Keep signed sales reporting and period reconciliation over the complete correction history.
