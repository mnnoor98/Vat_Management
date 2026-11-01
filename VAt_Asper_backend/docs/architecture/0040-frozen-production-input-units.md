# 0040: Frozen production input unit ratios

Date: 2026-10-31. Status: implemented and verified. Financial regressions: 97 passed; Manufacturing owner: 16 passed; frontend: 166 passed. Release builds and both model/snapshot checks passed.

Alternative actual-entry units are approved per production order and frozen in its source evidence. Financial stock and planned output remain in registered stock UOMs. Input quantities and ratios use exact decimal contracts; integer arithmetic rejects any conversion requiring stock rounding.

Financial operations retain both original input and normalized stock quantities. Approval and PostgreSQL guards verify agreement with the frozen source. Existing journal, original-cost return and reversal semantics operate on stock quantities, preserving valuation independently of entry units.

Null conversion evidence is omitted during serialization so older approved source hashes remain compatible. No global unit override, product UOM change or implicit ratio is introduced. See [contract and deployment](../PRODUCTION_INPUT_UNITS.md).
