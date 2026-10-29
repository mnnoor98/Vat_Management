# Price-only credit allocation pools

Represent price credits in the sales financial journal with zero physical quantity and a distinct entry type. Share the existing credit-note workflow, one-pending-note constraint and source/version transaction boundary with goods returns. Require adjustment evidence without asserting a goods receipt.

Both adjustment types consume the same original line's remaining values. Preserve pure goods-return rounding. After a price adjustment, allocate subsequent goods against the remaining quantity/value pool. After a goods return, allocate consecutive price credits against the remaining monetary pool. Cumulative targets with exact rational rounding consume final remainders without reinterpreting earlier posted credits.

Keep physical completion separate from financial reduction: fully credited value can coexist with retained goods. Expose remaining/pending monetary values and price eligibility, include price credits in signed reporting and period reconciliation, and publish a separate event so goods-return consumers never infer stock receipt from a financial-only adjustment.
