# Customer statements and ageing

Apply migration `SalesInvoiceDueDates` after `CustomerPayments`. It adds nullable invoice DueDate with a database constraint requiring it to be on/after BusinessDate. Existing rows remain null; historical payment terms are not inferred. Closed financial checkpoints and posting events are unchanged.

## Invoice terms

The sales-create body accepts optional dueDate (`YYYY-MM-DD`). It is stored on the invoice and returned on sales reads. It cannot precede businessDate and is frozen at creation; there is no terms-edit endpoint. Omitting it means unknown payment terms, not an assumed immediate due date. Existing API clients and reference-only invoices remain compatible.

Null dueDate is omitted from typed request serialization so existing pre-field idempotency records can replay. Supplying a date changes the request hash and requires a fresh key.

These reports require an explicitly linked customer ID. Reference-only invoices are not automatically assigned to a master customer based on their text reference.

## Statement

GET `/api/v1/companies/{companyId}/customers/{customerId}/statement?from=2026-10-01&until=2026-11-01` requires report.read, tenant/company access and an existing scoped customer.

The interval is `[from, until)`: from is inclusive and until is exclusive. It must be non-empty and at most 366 days. Opening balance includes all currently posted customer activity with business dates before from. Closing balance includes activity before until.

Response fields: customerId, currency (BDT), from, until, capturedAt, openingBalance, totalDebit, totalCredit, closingBalance and lines. Each line includes documentId, optional invoiceId, businessDate, type, documentNumber, netAmount, outputTaxAmount, signed balanceChange and runningBalance.

Original invoice and debit-note gross amounts increase the customer balance. Credit-note gross amounts and posted receipts reduce it. Rows group the journal lines belonging to each financial document; posted zero-value goods returns can appear with zero balance change. Receipt rows have netAmount/outputTaxAmount zero because they do not create revenue or VAT. Allocation rows do not appear as new charges/payments: allocating previously collected cash changes invoice settlement, not customer balance.

Sort order is businessDate, ordinal document type, then document UUID; this gives deterministic ordering within each date rather than asserting an intraday cash chronology. Opening plus totalDebit minus totalCredit equals closingBalance. totalDebit/totalCredit are positive movement totals across all document kinds, not sales VAT figures.

A window with more than 1000 financial documents fails with report_too_large; use a narrower interval. No rows are silently truncated. Opening balance still covers earlier history. The report is read-only and creates no audit, stock, VAT or cash entries.

## Ageing as of a business date

GET `/api/v1/companies/{companyId}/customers/{customerId}/ageing?asOf=2026-10-24` uses the same permission and scope. asOf is required and inclusive. It is not a [from, until) interval.

Invoice gross uses signed sales journal values dated on/before asOf. Allocations use allocationDate on/before asOf; posted collections use paymentDate on/before asOf. A receipt known now but allocated later is unallocated at an earlier cut-off. Pending/cancelled receipts, pending sales adjustments and future-dated postings/allocations are excluded from monetary totals.

Positive invoice outstanding is placed in exactly one bucket:

| Bucket | Meaning |
| --- | --- |
| NotDue | Due today or later |
| Overdue1To30 | 1?30 days past due |
| Overdue31To60 | 31?60 days past due |
| Overdue61To90 | 61?90 days past due |
| OverdueOver90 | More than 90 days past due |
| UnknownDueDate | Positive outstanding with no recorded terms |

Days overdue use calendar-day differences, with no interest, business-day or grace-period assumptions. Zero outstanding invoices are omitted. Negative invoice outstanding uses Credit instead of an overdue bucket. invoices includes invoiceId/reference, businessDate, nullable dueDate/daysOverdue, bucket and gross/allocated/outstanding amounts. Sort order places known due dates first, then unknown terms, with UUID tie-breaking.

The response exposes positive outstandingDebitAmount and invoiceCreditAmount separately. Collected/allocated/unallocated amounts are cut off by their respective business dates. The reconciliation is:

`balance = outstandingDebitAmount - invoiceCreditAmount - unallocatedAmount`

The sum of the six positive buckets equals outstandingDebitAmount. Invoice credits and unapplied cash are not silently allocated to old debt or netted into overdue buckets. A negative balance means customer credit and does not execute a refund. For matching cut-offs, a statement's closing balance agrees with ageing balance (until is the day after asOf).

## Consistency and historical meaning

Each report reads reconciled sales/payment history in one PostgreSQL repeatable-read transaction. Missing or inconsistent current financial rows cause 409, including when the requested cut-off precedes the damaged row. Customer identity/grants and report.read are enforced independently of payment.read and masterdata.read.

These are business-date reports of the data committed when the query runs. Late-approved or backdated documents can restate an earlier cut-off. They do not recreate what an operator knew at an earlier wall-clock time, and capturedAt is the report-generation timestamp. Historical customer names/terms are not reconstructed by guessing; the invoice's recorded due date is used.

The reports currently reconcile/load a customer's full linked history before computing the requested window/cut-off. Large-account paging, archived statement artifacts, materialized summaries, company-wide ageing, terms changes, interest and statutory forms remain subsequent work. [Receipt reversals and refunds](PAYMENT_CORRECTIONS.md) use their separately approved financial workflow.

## Verification

Run `./scripts/Test-Local.ps1 -Filter FullyQualifiedName~CustomerReportingApiTests` for focused tests, or omit Filter for the complete PostgreSQL suite. Synthetic VAT rates in tests are calculation examples only.

Receipt corrections retain the original collection and add dated PaymentRefund/ReceiptReversal statement movements. Ageing exposes returnedAmount/netCollectedAmount and subtracts releases by their own business dates. Positive buckets and customer balance use net allocations/cash while collectedAmount remains gross original collections.
