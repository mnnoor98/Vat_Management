# 0010: Scoped customers and immutable invoice snapshots

Status: Accepted

Introduce a tenant/company customer master with an immutable normalized code, optimistic version and active flag. Use existing masterdata read/manage policies, transactional audit/idempotency and scoped foreign keys. Do not hard-delete customer records.

Sales creation may link a customer ID explicitly. Capture the exact master version/details at creation and retain the original invoice reference. Later edits or deactivation affect future admission, not existing documents. Preserve reference-only invoices without inferring identities or rewriting history. Keep existing posting event contracts stable.

Optional identity permits compatible rollout but means reference-only documents cannot safely participate in future customer accounting without explicit identity assignment. Payment and balance functionality is deferred until its own posting and allocation rules are implemented.
