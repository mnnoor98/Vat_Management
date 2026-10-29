# 0012: Business-date statements and explicit due-date ageing

Status: Accepted

Store an optional immutable DueDate on sales creation and leave existing unknown terms null. Do not assume legacy invoices are immediately overdue. Keep the new field out of existing financial event contracts and closed journal checkpoints.

Compute statements from signed posted sales/adjustment documents and posted receipts. Allocation changes invoice settlement but has no customer balance effect, so it is excluded as a statement movement. Expose opening, running and closing balances using explicit half-open statement intervals.

Compute ageing using inclusive asOf cut-offs on sales business dates, receipt dates and allocation dates separately. Keep invoice credit and unapplied cash outside positive overdue buckets. Require report.read and explicit customer links. Read and reconcile each report under repeatable read; current corruption blocks historical queries too.

Reports describe committed data by business date and can restate after backdated admission. They are not wall-clock audit snapshots. Limit statement windows to 366 days and 1000 documents without silent truncation. Large-scale materialization and immutable exported statements are separate work.
