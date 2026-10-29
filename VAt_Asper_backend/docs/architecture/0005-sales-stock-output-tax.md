# Sales stock and output tax

Use the existing local PostgreSQL transaction boundary for invoice fulfillment. Frozen reservations make availability explicit and let a separate poster review output VAT before issuing stock. Keep sales output-tax/issue entries separate from purchase receipts and recoverable input-tax entries; shared on-hand balances reconcile across both ledgers.

Extend published tax rules with explicit Purchase/Sale treatment, preserving legacy rules as Purchase. Serialize publication by company/tax code, checking overlap within the treatment. Sales never falls back to a purchase rule.

Use a synchronous source-ledger summary for this milestone. Reusing the purchase-only financial projection would mislabel output tax as input credit. Keep its event contract unchanged and publish SalesPostedV1 separately. A later combined reporting projection will need its own versioned contract, backfill and freshness semantics.

Period snapshots retain legacy input-tax totals and add a separate sales checkpoint. Internal UUID-based document numbers are unique and stable; statutory numbering and credit-note corrections are outside this milestone.
