# Approved production input units

Production stock quantities, planned output, WIP allocation and valuation remain in each product's registered stock UOM. An order may now freeze explicit alternative units for actual material issue/return and finished receipt. This does not change the product master, coefficient quantities, cost policy or historical stock.

## Approval and exact conversion

Order creation accepts optional `uomConversions`: product ID, `inputUnitOfMeasure`, and `stockUnitsPerInputUnit` as an exact decimal string. For example, one BAG contains 25 KG; two BAG issue 50 KG. Ratios belong only to products in that order's verified BOM, including the finished product. An independent order approver reviews and freezes them with the existing source evidence and SHA-256 digest.

Unit codes are normalized to trimmed uppercase, maximum 20 characters. Ratios are positive decimal strings, at most 12 decimal places and below one trillion. Product/unit duplicates, unknown products and overrides of the stock unit are rejected. Registered stock units always have identity conversion. Omitting the alternative unit preserves existing stock-unit input, including older registered labels.

Input quantities use at most six decimal places. The server uses integer arithmetic to convert them, requiring a positive result exactly representable at stock quantity scale six and below one trillion. It rejects rounding, overflow and unapproved input units. For example, 250 G at 0.001 KG per G becomes 0.250000 KG; 0.000001 G cannot be posted because its stock result needs nine decimal places.

## Commands and retained evidence

- Material Issue/Return lines accept `quantity` in the selected `unitOfMeasure`. Persisted line `Quantity` is normalized stock quantity, accompanied by original input quantity/unit when explicitly supplied. Returned API lines expose stock `quantity` plus exact `inputQuantity` and `unitOfMeasure` strings. Returns still reference the original issue and restore its original cost by normalized stock quantity.
- Receipt accepts entered `outputQuantity` with optional `outputUnitOfMeasure`. Persisted/API `outputQuantity` is stock quantity; `outputInputQuantity` and `outputUnitOfMeasure` retain the actual entered evidence. Planned output limits use stock quantity. Reviewed WIP release amounts are unchanged by unit conversion.
- Receipt reversal derives original **stock** quantity and value from the immutable receipt; the entered alternative quantity is never converted twice.

Approval recalculates each retained conversion against the frozen order evidence. PostgreSQL triggers independently check input/stock quantity agreement at insert and update. Existing source immutability, expected versions, independent approval, fiscal periods, stock reservations, cost ledger, atomic rollback, audit and outbox remain applicable.

The portal exposes ratio entry and order review, offers only frozen units during actual entry, displays entered and stock quantities together during review, and copies drafts using their original input values. It does not calculate authoritative converted quantities or money using browser floating-point arithmetic.

## Compatibility and deployment

Older source JSON without conversions remains valid. The new optional source property is omitted when null, preserving existing typed source serialization and hashes. No approved document is backfilled or rewritten. New conversions are frozen per order; there is no mutable global conversion catalogue or automatic unit guessing. Approved [physical scrap/by-product outputs](PRODUCTION_PHYSICAL_OUTPUTS.md) may also carry frozen input-unit ratios.

Financial migration: `20261031155101_ProductionInputUnits`, adding nullable receipt input quantity/unit columns and conversion guards. Material input evidence uses the existing immutable JSON lines. Manufacturing needs updated code but no schema migration for its existing JSON evidence document. Deploy the updated financial owner and reviewed migration before enabling alternative-unit orders in Manufacturing. Test migrations run against disposable databases; no live migration, deployment or posting activation is part of this delivery. Migration rollback refuses to discard frozen execution conversion evidence.

## Remaining work

Coefficient/BOM quantity conversion, alternative units for planned output, partial corrections and closed-execution reopening remain separate contracts. Physical scrap/by-product inventory is now implemented through its separately approved output contract. These input conversions do not establish statutory production tax treatment or VAT compliance.

See [implementation evidence](../../EXPERT_IMPLEMENTATION_2026-10-31.md) and [next plan](../../NEXT_IMPLEMENTATION_PLAN_2026-10-31.md).
