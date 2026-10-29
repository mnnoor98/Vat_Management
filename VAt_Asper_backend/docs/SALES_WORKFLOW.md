# Sales invoice approval workflow

This guide covers document admission and approval; fulfillment is implemented in [Sales posting](SALES_POSTING.md). Apply the `SalesInvoiceWorkflow` migration using the existing deployment procedure. It adds scoped invoice and line tables without modifying financial ledgers.

## API

Base: `/api/v1/companies/{companyId}/sales`. Every POST requires a UUID `Idempotency-Key`. All routes require authenticated tenant/company grants plus the permission below.

| Method/path | Permission | Body |
|---|---|---|
| POST `/` | `sales.create` | Draft below |
| GET `/` | `sales.read` | Cursor query `take=1..100`, optional `before` UUID |
| GET `/{id}` | `sales.read` | — |
| POST `/{id}/submit` | `sales.submit` | `{"expectedVersion":1}` |
| POST `/{id}/approve` | `sales.approve` | `{"expectedVersion":2}` |
| POST `/{id}/reject` | `sales.approve` | `{"expectedVersion":2,"reason":"Evidence missing"}` |
| POST `/{id}/cancel` | `sales.cancel` | `{"expectedVersion":3,"reason":"Order withdrawn"}` |

Example draft:

```json
{
  "branchId": "00000000-0000-0000-0000-000000000001",
  "externalReference": "SALES-REF-001",
  "customerReference": "CUSTOMER-001",
  "businessDate": "2026-10-23",
  "lines": [
    {"productReference":"ITEM-001","description":"Goods","quantity":2.5,"unitPrice":100}
  ]
}
```

Use an existing company branch. Products must be registered in that company before submission. Customer reference is currently an external identifier, validated as text; customer master records and registration checks are not implemented. Currency is fixed to BDT. Client VAT rates, tax totals and unknown request properties are rejected.

## State and transaction guarantees

Draft version 1 submits to version 2. An independent checker approves to `ApprovedPendingPosting` version 3 or rejects to `Rejected` version 3. The maker cannot approve or reject their invoice. An actor with `sales.cancel` can cancel any active unposted state with its current version and a reason. Reserved invoices can also be cancelled with version 4, releasing their stock atomically. Posted invoices cannot be cancelled. Cancelled and rejected documents are terminal; create a replacement with a new external reference. This stage has no edit/resubmit endpoint.

Quantities are positive, at most 1,000,000 with six decimal places. Unit prices are nonnegative, at most 1,000,000,000 with four decimal places. Net amounts round each line to four decimal places, midpoint away from zero, then sum. A document contains 1–200 lines. References are unique per tenant/company after trimming and remain case-sensitive.

Source state, audit, idempotency response and approval outbox notification commit in one transaction. Stale versions return 409. Concurrent approvals with different keys produce one successful transition. Reusing the same key and actor with the same request replays the exact original response; changed requests or actors conflict.

Submission and approval require an Open period and share the company transaction fence with period close. Approved invoices block both close request and final close. Cancellation is nonfinancial and remains available during/after close under the shared fence. Drafts and submitted documents do not block close; approval after the period enters Closing is rejected.

`SalesInvoiceApprovedV1` is an immutable approval notification. The reporting inbox durably records it as Ignored without adding financial amounts. Consumers must not treat it as evidence of a stock issue, VAT liability or a currently active order: later cancellation remains possible. Check source status for fulfillment readiness.

## Fulfillment boundary

Approved invoices can reserve and post through the [Sales posting API](SALES_POSTING.md). Customer reference remains external text. Posted invoices can be fully credited after goods return through the [credit-note workflow](SALES_CREDIT_NOTES.md). Partial quantities use the [partial-return workflow](SALES_PARTIAL_RETURNS.md); [price-only adjustments](SALES_PRICE_CREDITS.md) use a separate request without stock movement.

The external reference is supplied by the caller and is not a statutory invoice number. This stage generates no statutory form or compliance claim.
