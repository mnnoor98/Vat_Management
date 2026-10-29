# ADR 0004: Confirmed broker delivery and immutable reporting transactions

Status: implemented; live broker tests require an external RabbitMQ test instance.

## Decision

Keep financial effects inside the existing local transaction and add outbox dispatch asynchronously. Claim rows with expiring ownership tokens instead of holding a database transaction open while publishing. Use mandatory persistent publications and tracked publisher confirmations; mark delivery with a conditional lease-owner update. Unknown outcomes intentionally permit replay. Bound outbox retries and retain quarantined work for authorized recovery.

Use a durable reporting inbox with exact event-byte identity and a second business uniqueness boundary on financial transaction ID. Store immutable signed financial transactions so reversal-before-original arrival is safe. Inbox, projection and checkpoint commit together. Broker acknowledgement follows durable processing/quarantine. Transient storage failure closes the consumer session without acknowledging and reconnects with backoff.

Introduce FinancialEntryRecordedV1 instead of deriving monetary totals from notification events that lack historical inputs. Its original/reversal transaction IDs, signed frozen values and business dates are self-contained. Reporting does not query mutable source state while consuming. Explicit local backfill reconciles old sources before scheduling missing events. Rebuild resets only reporting state and requeues existing financial events, preserving ledgers and period snapshots.

Read reporting freshness conservatively: dispatched is not processed; maximum observed timestamp is not a contiguous watermark. Legacy source gaps and unresolved quarantine also prevent IsCaughtUp. These are operational financial reports, not statutory returns.

## Deployment and limits

Workers run in the API host, disabled by default. Separate worker deployment can reuse these scoped services later. A durable topic exchange and quorum reporting queue cover all current notification routes. Use a dedicated vhost, least-privilege broker credentials and TLS outside loopback. A broker outage accumulates outbox work without undoing committed financial transactions.

Schema failures are stored in PostgreSQL application quarantine, not a broker DLQ. Unscoped malformed payloads remain visible only to infrastructure owners. Retry/replay and company-scoped projection rebuild are authorized and audited; payload editing is not exposed. Monitoring/alerts, retention tuning and independent reporting storage remain future operational work.

## References

- [RabbitMQ .NET client API](https://www.rabbitmq.com/client-libraries/dotnet-api-guide)
- [Publisher confirmations and consumer acknowledgements](https://www.rabbitmq.com/docs/confirms)
