# Customer receipts, invoice allocations and receivables

Deploy migration `CustomerPayments` after `CustomerMaster`. It adds receipts and immutable allocation rows; no old payment data or customer links are inferred. Keep this schema after posting receipts.

## Request and approval

POST `/api/v1/companies/{companyId}/payments` with `payment.request`, company access and a UUID `Idempotency-Key`:

```json
{
  "customerId":"00000000-0000-0000-0000-000000000001",
  "reference":"RECEIPT-001",
  "method":"Bank",
  "evidenceReference":"BANK-STATEMENT-001",
  "paymentDate":"2026-10-24",
  "amount":300,
  "allocations":[{"invoiceId":"00000000-0000-0000-0000-000000000002","amount":100}]
}
```

Method is exactly Cash or Bank; currency is BDT. Positive amounts support four decimals and a maximum 1,000,000,000,000,000. Reference is required, trimmed, limited to 100 characters and unique per tenant/company (case-sensitive). Evidence reference is required (1000 characters). This records externally evidenced collection; it does not initiate or verify a bank transfer. Existing inactive customers may settle their historical balances.

The response wraps payment, committed allocations, allocatedAmount and unallocatedAmount. Creation returns Pending/version 1, with requestedAllocations frozen on the receipt. Pending amounts have no financial effect and reserve no invoice budget. Pending receipts block close of their payment period. Zero to 200 distinct invoice allocations can be requested; null/omitted allocations mean a wholly unallocated receipt.

POST `/payments/{id}/approve` with `payment.approve` and `{"expectedVersion":1}`. The requester cannot approve. The financial posting gate and Open payment period are required. Approval rechecks all requested budgets and atomically posts the receipt (version 2), allocations, audit, outbox event and idempotency result. An intervening payment or credit may make a pending request unapprovable; cancel and recreate it.

POST `/payments/{id}/cancel` with `payment.request`, current expectedVersion and reason. Only Pending receipts can be cancelled; cancellation remains nonfinancial under the period fence. Original posted receipt facts and positive allocation rows are immutable; receipt source status/version may advance through correction review. [Receipt reversals and customer refunds](PAYMENT_CORRECTIONS.md) are available as separately approved compensating entries. Allocation-only reversals remain subsequent work.

GET `/payments/{id}` requires `payment.read` and returns a consistent receipt/allocation snapshot. GET `/payments?customerId={id}&take=25&before={lastId}` returns scoped receipt records; take is 1?100. Mutations use the existing actor-bound idempotency rules; failed commands do not consume their key.

## Allocate remaining receipt value

POST `/payments/{id}/allocate` with `payment.allocate`, UUID Idempotency-Key and:

```json
{"expectedVersion":2,"allocationDate":"2026-10-25","allocations":[{"invoiceId":"00000000-0000-0000-0000-000000000002","amount":110}]}
```

Select 1?200 distinct invoices. Multiple batches may allocate the same invoice later, while each batch records its paymentVersion. The payment must be Posted. Allocation date must be in an Open period and on/after both receipt date and each invoice date. An older receipt in a Closed period may be allocated in a later Open period. Original closed checkpoints remain frozen.

Invoices must be explicitly linked to the same tenant/company/customer, posted and reconciled, with no pending debit/credit. Reference-only and unposted sales cannot receive allocations. Requested amounts must fit both the receipt's unallocated budget and the invoice's current positive gross outstanding. Per-customer transaction locks serialize competing receipts/allocations across API instances; payment versions prevent stale decisions. Failed batches roll back completely. Allocation itself does not change cash, VAT, stock or invoice versions.

## Receivables

GET `/customers/{customerId}/receivables` with `payment.read` provides a repeatable-read customer snapshot:

- salesGrossAmount: original posted net/output VAT plus signed posted credit/debit adjustments.
- collectedAmount: Posted receipts only.
- allocatedAmount: immutable committed allocations; unallocatedAmount = netCollectedAmount minus net allocatedAmount.
- balance = salesGrossAmount minus netCollectedAmount; netCollectedAmount is gross collectedAmount less returnedAmount.
- Each invoice: grossAmount, allocatedAmount, outstandingAmount = grossAmount minus allocatedAmount.

For a gross 210 invoice and receipt 300, initially allocating 100 leaves invoice outstanding 110 and unallocated cash 200. Allocating another 110 settles the invoice and leaves unallocated cash 90; the customer balance is -90. Negative balances mean customer credit, not an executed refund.

A credit note after payment can make invoice outstanding negative; this is retained as customer credit. Credit/debit workflows remain available and do not silently rewrite or release old allocations. Later allocation must still fit current positive outstanding. Invoice and payment reconciliation failures block the report rather than silently showing incomplete amounts.

## Periods and notifications

Payment-period admission and allocation-period posting share existing company fences with fiscal close. Close checks receipt/initial-allocation/version reconciliation and the invoices touched by that period's allocations. Checkpoint Payments stores separate receipt/allocation IDs and amounts. Sales VAT and purchase totals retain their meaning; receipts do not create new revenue/output VAT. Receipt checkpoint material excludes mutable allocation version so later allocations do not alter the original receipt facts.

`CustomerPaymentPostedV1` and `CustomerPaymentAllocatedV1` use separate version-1 outbox contracts. The reporting inbox recognizes these as notifications without adding them to purchase projections. Events contain committed allocation rows, never provisional requested allocations.

This milestone does not provide bank-account reconciliation, reallocations, payment-provider integration, GL journals, statutory receipt numbering. [Due dates, statements and ageing](CUSTOMER_REPORTS.md) are now available as read-only business-date reports. The current receivables report returns all linked posted invoices; large-account paging/materialized reporting is subsequent work.

## Verification

Run `./scripts/Test-Local.ps1 -Filter FullyQualifiedName~PaymentApiTests` for the focused receipt/allocation suite, or omit Filter for the complete PostgreSQL-backed suite. The runner creates and removes a private temporary cluster.

[Payment corrections](PAYMENT_CORRECTIONS.md) add dated cash offsets and allocation releases. Gross collectedAmount remains the original collection total; netCollectedAmount, returnedAmount and net allocatedAmount explain current settlement.
