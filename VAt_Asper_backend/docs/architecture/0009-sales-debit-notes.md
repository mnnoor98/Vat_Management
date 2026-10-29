# 0009: Separate sales debit notes with shared adjustment history

Status: Accepted

Price increases require their own permissions, positive immutable allocations and evidence without changing stock or original postings. Store debit notes separately from credit notes and link PriceDebit entries to the original invoice/line. Use the invoice optimistic version to serialize admission across both note tables; a pending debit blocks credit/return admission and vice versa.

Replay a combined adjustment history ordered by sourceVersion during reconciliation. Debit VAT uses the original frozen rate and cumulative rounding within consecutive debit runs. Each change of adjustment kind establishes the relevant remaining-value pool. Later credits/returns can consume increased value without rewriting prior allocations. Retained quantity and the supported line-value cap bound increases.

Period fences, maker/checker, idempotency, transaction boundaries and audit/outbox remain shared. Financial summaries and period checkpoints aggregate signed entries synchronously; sales notifications stay outside purchase reporting projections. Internal DN numbering does not assert statutory compliance.
