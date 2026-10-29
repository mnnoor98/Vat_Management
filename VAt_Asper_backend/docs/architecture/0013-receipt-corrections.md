# 0013: Append-only receipt corrections and explicit allocation releases

Status: Accepted

Keep original receipts and positive allocation rows immutable. Store correction notes with frozen release evidence, negative cash-adjustment rows and positive release rows linked to exact original allocations. Reversal offsets all remaining cash/allocation value; refund requires aggregate customer credit and explicit eligible invoice-credit releases or unallocated cash.

Use the source receipt's optimistic version and one pending note per receipt to serialize allocation/correction admission. Reuse per-customer transaction locks for shared credit budgets and company period fences for financial dates. Request and approval both validate budgets. Distinct reverse/refund policies and an independent checker govern cash-out admission.

Preserve original financial dates/amounts when source states become CorrectionPending, Refunded or Reversed. Reports identify original collections by PostedAt rather than current status, then apply dated cash offsets and allocation releases. Keep gross collection metrics stable and expose net collected separately. Current and business-date historical balances remain explainable by immutable signed entries.

Close reconciles frozen notes, source-version actions and linked cash/release rows, and captures separate correction checkpoint facts. Closed snapshots and original sale/VAT/stock history remain unchanged. Further correction of a posted cash-out entry requires a future explicit compensating workflow.
