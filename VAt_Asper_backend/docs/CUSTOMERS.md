# Customer master and invoice snapshots

Apply migration `CustomerMaster` after `SalesDebitNotes`. It adds `sales.customers` and nullable customer ID/snapshot columns on invoices. Existing invoices keep their original reference and remain unlinked; no customer identity or tax registration is inferred from old references.

## Customer API

Base: `/api/v1/companies/{companyId}/customers`.

- POST `/`: `masterdata.manage`, UUID Idempotency-Key; creates an active customer.
- GET `/` and `/{id}`: `masterdata.read`. List accepts take 1?100 (default 25) and before equal to the last returned UUID.
- POST `/{id}/update`: `masterdata.manage`, UUID Idempotency-Key and expectedVersion. Updates details/active status; code remains immutable. No deletion endpoint is provided.

Create body:

```json
{"code":"CUS-001","name":"Example customer","contact":"01700000000","address":"Dhaka","taxRegistrationReference":"REG-001"}
```

Code is trimmed and uppercased, unique within tenant/company, and limited to 100 characters. Name is required (200 characters). Contact/address/tax reference are optional plain text limited to 300/1000/100 characters. These values are stored references, not verified identities or tax registrations.

Update body:

```json
{"expectedVersion":1,"name":"Updated customer","contact":"01700000000","address":"Dhaka","taxRegistrationReference":"REG-001","isActive":false}
```

Updates replace the supplied details; null optional fields become empty strings. Re-enable with isActive true and the current version. Stale/concurrent updates return 409; failed writes roll back audit and idempotency records. Successful identical same-actor/key retries replay the committed result. Company/tenant access and scoped database foreign keys apply to customer and invoice links.

## Sales integration

The existing sales-create body accepts optional customerId. When supplied, customerReference must match that customer's code (case-insensitive, trimmed). The customer must belong to the same tenant/company and be active at the creation read. The response includes customerId and customerSnapshot containing ID, version, code, name, contact, address and tax-registration reference. The stored customerReference uses canonical code.

The snapshot is frozen at invoice creation. Later customer edits/deactivation do not change existing drafts, approvals, postings or corrections. In-flight creation uses the version read within its transaction; deactivation does not retroactively revoke an admitted document. Customer details are not refreshed during submission or posting.

Omitting customerId preserves the existing reference-only path, with null ID/snapshot. Such an invoice is not implicitly associated with a master even when codes happen to match. Clients needing customer accounting should always supply customerId. Migrating reference-only sales to verified customer identities requires a separate explicit workflow.

This milestone provides customer identity and frozen invoice details. [Payment collection/allocation and current customer balances](CUSTOMER_PAYMENTS.md) are available. [Customer statements, due dates and ageing](CUSTOMER_REPORTS.md) are available. Credit limits remain subsequent work. Existing VAT calculations, stock movements, financial event contracts and closed snapshots are unchanged.
