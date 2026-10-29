# Added module tables: relational dictionary

Source: `PlannedModulesModel.Configure`. All tables below share required UUID `TenantId`, `CompanyId`, `Id`; required UTC `CreatedAt`, `UpdatedAt`; required `CreatedBy` (200 characters); and optimistic integer `Version` (default 1, positive). Their composite primary key is `(TenantId, CompanyId, Id)`. Every table references `company.companies` through `(TenantId, CompanyId)`; all other foreign keys also include both scope columns and restrict deletes. All have a `(TenantId, CompanyId, CreatedAt)` index.

`?` means nullable. Types: `g` UUID, `sN` string with maximum N characters, `n` decimal(24,6), `d` date, `t` UTC timestamp, `i` integer, `l` bigint, `b` boolean, `j` JSONB. Listed unique keys also include TenantId and CompanyId; nullable values compare equal for uniqueness. Common checks require Version > 0 and UpdatedAt >= CreatedAt. This lists new persistence foundations; see [coverage](DATABASE_COVERAGE.md) for existing canonical tables and runtime limitations.

## identity.users

| Column | Type | Reference |
|---|---|---|
| Issuer | `s500` |  |
| Subject | `s200` |  |
| DisplayName | `s200` |  |
| Disabled | `b` |  |

Unique: `Issuer,Subject`.

## identity.roles

| Column | Type | Reference |
|---|---|---|
| Code | `s100` |  |
| Name | `s200` |  |

Unique: `Code`.

## identity.permissions

| Column | Type | Reference |
|---|---|---|
| Code | `s200` |  |
| Description | `s500` |  |

Unique: `Code`.

## identity.user_roles

| Column | Type | Reference |
|---|---|---|
| UserId | `g` | `identity.users` |
| RoleId | `g` | `identity.roles` |
| BranchId | `g?` | `company.branches` |

Unique: `UserId,RoleId,BranchId`.

## identity.role_permissions

| Column | Type | Reference |
|---|---|---|
| RoleId | `g` | `identity.roles` |
| PermissionId | `g` | `identity.permissions` |

Unique: `RoleId,PermissionId`.

## identity.menu_items

| Column | Type | Reference |
|---|---|---|
| Code | `s100` |  |
| Label | `s200` |  |
| Route | `s500` |  |
| ParentId | `g?` | `identity.menu_items` |

Unique: `Code`.

Additional check:

```sql
("ParentId" IS NULL OR "ParentId" <> "Id")
```

## identity.role_menus

| Column | Type | Reference |
|---|---|---|
| RoleId | `g` | `identity.roles` |
| MenuId | `g` | `identity.menu_items` |

Unique: `RoleId,MenuId`.

## identity.refresh_sessions

| Column | Type | Reference |
|---|---|---|
| UserId | `g` | `identity.users` |
| TokenDigest | `s128` |  |
| FamilyId | `g` |  |
| ExpiresAt | `t` |  |
| RevokedAt | `t?` |  |
| ReplacedById | `g?` | `identity.refresh_sessions` |

Unique: `TokenDigest`.

Additional check:

```sql
("ExpiresAt" > "CreatedAt" AND ("RevokedAt" IS NULL OR "RevokedAt" >= "CreatedAt") AND ("ReplacedById" IS NULL OR "ReplacedById" <> "Id"))
```

## identity.security_audit

| Column | Type | Reference |
|---|---|---|
| UserId | `g?` | `identity.users` |
| Action | `s100` |  |
| Resource | `s500` |  |
| OccurredAt | `t` |  |
| CorrelationId | `s100` |  |
| Outcome | `s40` |  |

## company.registrations

| Column | Type | Reference |
|---|---|---|
| BranchId | `g?` | `company.branches` |
| RegistrationType | `s80` |  |
| Reference | `s100` |  |
| EffectiveFrom | `d` |  |
| EffectiveUntil | `d?` |  |
| ApprovedBy | `s200?` |  |

Unique: `RegistrationType,Reference,EffectiveFrom`.

Additional check:

```sql
("EffectiveUntil" IS NULL OR "EffectiveUntil" >= "EffectiveFrom") AND ("ApprovedBy" IS NULL OR "ApprovedBy" <> "CreatedBy")
```

## company.company_settings

| Column | Type | Reference |
|---|---|---|
| Key | `s100` |  |
| Value | `j` |  |

Unique: `Key`.

## company.branch_access

| Column | Type | Reference |
|---|---|---|
| UserId | `g` | `identity.users` |
| BranchId | `g` | `company.branches` |
| PermissionId | `g` | `identity.permissions` |

Unique: `UserId,BranchId,PermissionId`.

## masterdata.uoms

| Column | Type | Reference |
|---|---|---|
| Code | `s30` |  |
| Name | `s100` |  |
| DecimalPlaces | `i` |  |

Unique: `Code`.

Additional check:

```sql
("DecimalPlaces" BETWEEN 0 AND 6)
```

## masterdata.hs_codes

| Column | Type | Reference |
|---|---|---|
| Code | `s30` |  |
| Description | `s1000` |  |
| EffectiveFrom | `d` |  |
| EffectiveUntil | `d?` |  |

Unique: `Code,EffectiveFrom`.

Additional check:

```sql
("EffectiveUntil" IS NULL OR "EffectiveUntil" >= "EffectiveFrom")
```

## masterdata.product_versions

| Column | Type | Reference |
|---|---|---|
| ProductId | `g` | `masterdata.products` |
| Revision | `i` |  |
| Description | `s500` |  |
| UomId | `g` | `masterdata.uoms` |
| EffectiveFrom | `d` |  |
| EffectiveUntil | `d?` |  |
| ApprovedBy | `s200?` |  |

Unique: `ProductId,Revision`.

Additional check:

```sql
"Revision" > 0 AND ("EffectiveUntil" IS NULL OR "EffectiveUntil" >= "EffectiveFrom") AND ("ApprovedBy" IS NULL OR "ApprovedBy" <> "CreatedBy")
```

## masterdata.product_tax_classifications

| Column | Type | Reference |
|---|---|---|
| ProductVersionId | `g` | `masterdata.product_versions` |
| HsCodeId | `g` | `masterdata.hs_codes` |
| TaxCode | `s100` |  |
| EffectiveFrom | `d` |  |
| EffectiveUntil | `d?` |  |

Unique: `ProductVersionId,EffectiveFrom`.

Additional check:

```sql
("EffectiveUntil" IS NULL OR "EffectiveUntil" >= "EffectiveFrom")
```

## masterdata.counterparty_registrations

| Column | Type | Reference |
|---|---|---|
| CustomerId | `g?` | `sales.customers` |
| SupplierId | `g?` | `masterdata.suppliers` |
| RegistrationType | `s80` |  |
| Reference | `s100` |  |
| EffectiveFrom | `d` |  |
| EffectiveUntil | `d?` |  |

Additional check:

```sql
(("CustomerId" IS NULL) <> ("SupplierId" IS NULL)) AND ("EffectiveUntil" IS NULL OR "EffectiveUntil" >= "EffectiveFrom")
```

## purchasing.purchase_attachments

| Column | Type | Reference |
|---|---|---|
| PurchaseId | `g` | `purchasing.purchases` |
| ObjectKey | `s1000` |  |
| Sha256 | `s64` |  |
| ContentType | `s200` |  |
| SizeBytes | `l` |  |

Unique: `PurchaseId,Sha256`.

Additional check:

```sql
("SizeBytes" > 0 AND length("Sha256") = 64)
```

## purchasing.purchase_posting_sagas

| Column | Type | Reference |
|---|---|---|
| PurchaseId | `g` | `purchasing.purchases` |
| SourceVersion | `i` |  |
| State | `s40` |  |
| LastError | `s2000?` |  |
| NextAttemptAt | `t?` |  |
| AttemptCount | `i` |  |

Unique: `PurchaseId,SourceVersion`.

Additional check:

```sql
("SourceVersion" > 0 AND "AttemptCount" >= 0 AND "State" IN ('Pending','Running','Completed','Failed'))
```

## sales.delivery_references

| Column | Type | Reference |
|---|---|---|
| InvoiceId | `g` | `sales.sales_invoices` |
| Reference | `s100` |  |
| DeliveredAt | `t?` |  |
| EvidenceKey | `s1000?` |  |

Unique: `InvoiceId,Reference`.

## sales.challan_numbers

| Column | Type | Reference |
|---|---|---|
| BranchId | `g` | `company.branches` |
| FiscalYear | `i` |  |
| Series | `s40` |  |
| Number | `l` |  |
| InvoiceId | `g?` | `sales.sales_invoices` |
| VoidedAt | `t?` |  |
| VoidReason | `s1000?` |  |

Unique: `BranchId,FiscalYear,Series,Number`.

Additional check:

```sql
("Number" > 0 AND "FiscalYear" BETWEEN 1900 AND 9999 AND (("VoidedAt" IS NULL AND "VoidReason" IS NULL) OR ("VoidedAt" IS NOT NULL AND "VoidReason" IS NOT NULL AND length(trim("VoidReason")) > 0)))
```

## inventory.transfers

| Column | Type | Reference |
|---|---|---|
| FromWarehouseId | `g` | `inventory.warehouses` |
| ToWarehouseId | `g` | `inventory.warehouses` |
| Reference | `s100` |  |
| BusinessDate | `d` |  |
| State | `s40` |  |
| ApprovedBy | `s200?` |  |

Unique: `Reference`.

Additional check:

```sql
("FromWarehouseId" <> "ToWarehouseId") AND ("ApprovedBy" IS NULL OR "ApprovedBy" <> "CreatedBy") AND "State" IN ('Draft','Pending','Approved','Rejected','Cancelled','Running','Completed','Failed','Submitted','Posted')
```

## inventory.transfer_lines

| Column | Type | Reference |
|---|---|---|
| TransferId | `g` | `inventory.transfers` |
| ProductId | `g` | `masterdata.products` |
| Quantity | `n` |  |

Unique: `TransferId,ProductId`.

Additional check:

```sql
"Quantity" > 0
```

## inventory.adjustments

| Column | Type | Reference |
|---|---|---|
| WarehouseId | `g` | `inventory.warehouses` |
| Reference | `s100` |  |
| BusinessDate | `d` |  |
| Reason | `s1000` |  |
| State | `s40` |  |
| ApprovedBy | `s200?` |  |

Unique: `Reference`.

Additional check:

```sql
("ApprovedBy" IS NULL OR "ApprovedBy" <> "CreatedBy") AND "State" IN ('Draft','Pending','Approved','Rejected','Cancelled','Running','Completed','Failed','Submitted','Posted')
```

## inventory.adjustment_lines

| Column | Type | Reference |
|---|---|---|
| AdjustmentId | `g` | `inventory.adjustments` |
| ProductId | `g` | `masterdata.products` |
| Quantity | `n` |  |

Unique: `AdjustmentId,ProductId`.

Additional check:

```sql
("Quantity" <> 0)
```

## manufacturing.bom_headers

| Column | Type | Reference |
|---|---|---|
| ProductId | `g` | `masterdata.products` |
| Code | `s100` |  |
| Name | `s200` |  |

Unique: `Code`.

## manufacturing.coefficient_versions

| Column | Type | Reference |
|---|---|---|
| BomId | `g` | `manufacturing.bom_headers` |
| Revision | `i` |  |
| OutputQuantity | `n` |  |
| EffectiveFrom | `d` |  |
| EffectiveUntil | `d?` |  |
| State | `s40` |  |
| ApprovedBy | `s200?` |  |

Unique: `BomId,Revision`.

Additional check:

```sql
("OutputQuantity" > 0) AND "Revision" > 0 AND ("EffectiveUntil" IS NULL OR "EffectiveUntil" >= "EffectiveFrom") AND ("ApprovedBy" IS NULL OR "ApprovedBy" <> "CreatedBy") AND "State" IN ('Draft','Pending','Approved','Rejected','Cancelled','Running','Completed','Failed','Submitted','Posted')
```

## manufacturing.bom_lines

| Column | Type | Reference |
|---|---|---|
| CoefficientVersionId | `g` | `manufacturing.coefficient_versions` |
| ProductId | `g` | `masterdata.products` |
| Quantity | `n` |  |
| WastagePercent | `n` |  |

Unique: `CoefficientVersionId,ProductId`.

Additional check:

```sql
("Quantity" > 0 AND "WastagePercent" BETWEEN 0 AND 100)
```

## manufacturing.production_orders

| Column | Type | Reference |
|---|---|---|
| CoefficientVersionId | `g` | `manufacturing.coefficient_versions` |
| WarehouseId | `g` | `inventory.warehouses` |
| Reference | `s100` |  |
| BusinessDate | `d` |  |
| PlannedQuantity | `n` |  |
| State | `s40` |  |
| ApprovedBy | `s200?` |  |

Unique: `Reference`.

Additional check:

```sql
("PlannedQuantity" > 0) AND ("ApprovedBy" IS NULL OR "ApprovedBy" <> "CreatedBy") AND "State" IN ('Draft','Pending','Approved','Rejected','Cancelled','Running','Completed','Failed','Submitted','Posted')
```

## manufacturing.material_issues

| Column | Type | Reference |
|---|---|---|
| ProductionOrderId | `g` | `manufacturing.production_orders` |
| ProductId | `g` | `masterdata.products` |
| WarehouseId | `g` | `inventory.warehouses` |
| Quantity | `n` |  |
| BusinessDate | `d` |  |
| SourceReference | `s100` |  |

Unique: `SourceReference`.

Additional check:

```sql
"Quantity" > 0
```

## manufacturing.production_receipts

| Column | Type | Reference |
|---|---|---|
| ProductionOrderId | `g` | `manufacturing.production_orders` |
| ProductId | `g` | `masterdata.products` |
| WarehouseId | `g` | `inventory.warehouses` |
| Quantity | `n` |  |
| BusinessDate | `d` |  |
| SourceReference | `s100` |  |

Unique: `SourceReference`.

Additional check:

```sql
"Quantity" > 0
```

## manufacturing.wastage_records

| Column | Type | Reference |
|---|---|---|
| ProductionOrderId | `g` | `manufacturing.production_orders` |
| ProductId | `g` | `masterdata.products` |
| Quantity | `n` |  |
| Reason | `s1000` |  |
| ApprovedBy | `s200?` |  |

Additional check:

```sql
("Quantity" > 0) AND ("ApprovedBy" IS NULL OR "ApprovedBy" <> "CreatedBy")
```

## tax.tax_treatments

| Column | Type | Reference |
|---|---|---|
| Code | `s100` |  |
| Description | `s1000` |  |
| EffectiveFrom | `d` |  |
| EffectiveUntil | `d?` |  |
| ApprovedBy | `s200?` |  |

Unique: `Code,EffectiveFrom`.

Additional check:

```sql
("EffectiveUntil" IS NULL OR "EffectiveUntil" >= "EffectiveFrom") AND ("ApprovedBy" IS NULL OR "ApprovedBy" <> "CreatedBy")
```

## tax.tax_calculation_snapshots

| Column | Type | Reference |
|---|---|---|
| TreatmentId | `g` | `tax.tax_treatments` |
| RuleVersionId | `g` | `tax.tax_rule_versions` |
| SourceType | `s80` |  |
| SourceId | `g` |  |
| SourceVersion | `i` |  |
| Calculation | `j` |  |

Unique: `SourceType,SourceId,SourceVersion`.

Additional check:

```sql
"SourceVersion" > 0
```

## tax.tax_posting_batches

| Column | Type | Reference |
|---|---|---|
| SourceType | `s80` |  |
| SourceId | `g` |  |
| SourceVersion | `i` |  |
| BusinessDate | `d` |  |
| State | `s40` |  |
| ExpectedTax | `n` |  |

Unique: `SourceType,SourceId,SourceVersion`.

Additional check:

```sql
"SourceVersion" > 0 AND "State" IN ('Draft','Pending','Approved','Rejected','Cancelled','Running','Completed','Failed','Submitted','Posted')
```

## tax.vds_entries

| Column | Type | Reference |
|---|---|---|
| BatchId | `g` | `tax.tax_posting_batches` |
| SupplierId | `g` | `masterdata.suppliers` |
| BusinessDate | `d` |  |
| Amount | `n` |  |
| EvidenceReference | `s500` |  |
| ReversalOfId | `g?` | `tax.vds_entries` |

## compliance.form_templates

| Column | Type | Reference |
|---|---|---|
| Code | `s100` |  |
| Name | `s200` |  |

Unique: `Code`.

## compliance.form_versions

| Column | Type | Reference |
|---|---|---|
| TemplateId | `g` | `compliance.form_templates` |
| Revision | `i` |  |
| EffectiveFrom | `d` |  |
| EffectiveUntil | `d?` |  |
| Mapping | `j` |  |
| ApprovedBy | `s200?` |  |

Unique: `TemplateId,Revision`.

Additional check:

```sql
"Revision" > 0 AND ("EffectiveUntil" IS NULL OR "EffectiveUntil" >= "EffectiveFrom") AND ("ApprovedBy" IS NULL OR "ApprovedBy" <> "CreatedBy")
```

## compliance.return_periods

| Column | Type | Reference |
|---|---|---|
| FiscalPeriodId | `g` | `company.fiscal_periods` |
| Code | `s100` |  |
| From | `d` |  |
| Until | `d` |  |

Unique: `Code`.

Additional check:

```sql
("Until" >= "From")
```

## compliance.vat_returns

| Column | Type | Reference |
|---|---|---|
| ReturnPeriodId | `g` | `compliance.return_periods` |
| FormVersionId | `g` | `compliance.form_versions` |
| Revision | `i` |  |
| State | `s40` |  |
| LedgerWatermark | `t?` |  |
| SubmittedAt | `t?` |  |
| ApprovedBy | `s200?` |  |
| CorrectsId | `g?` | `compliance.vat_returns` |

Unique: `ReturnPeriodId,FormVersionId,Revision`.

Additional check:

```sql
("CorrectsId" IS NULL OR "CorrectsId" <> "Id") AND "Revision" > 0 AND ("ApprovedBy" IS NULL OR "ApprovedBy" <> "CreatedBy") AND "State" IN ('Draft','Pending','Approved','Rejected','Cancelled','Running','Completed','Failed','Submitted','Posted')
```

## compliance.return_lines

| Column | Type | Reference |
|---|---|---|
| ReturnId | `g` | `compliance.vat_returns` |
| LineCode | `s100` |  |
| Amount | `n` |  |
| SourceSnapshot | `j` |  |

Unique: `ReturnId,LineCode`.

## compliance.reconciliation_runs

| Column | Type | Reference |
|---|---|---|
| ReturnId | `g` | `compliance.vat_returns` |
| StartedAt | `t` |  |
| CompletedAt | `t?` |  |
| State | `s40` |  |
| Differences | `j` |  |

Additional check:

```sql
"State" IN ('Draft','Pending','Approved','Rejected','Cancelled','Running','Completed','Failed','Submitted','Posted')
```

## compliance.filing_evidence

| Column | Type | Reference |
|---|---|---|
| ReturnId | `g` | `compliance.vat_returns` |
| ObjectKey | `s1000` |  |
| Sha256 | `s64` |  |
| FilingReference | `s200` |  |
| FiledAt | `t` |  |

Unique: `ReturnId,Sha256`.

Additional check:

```sql
(length("Sha256") = 64)
```

## compliance.period_close_requests

| Column | Type | Reference |
|---|---|---|
| FiscalPeriodId | `g` | `company.fiscal_periods` |
| ReturnId | `g?` | `compliance.vat_returns` |
| State | `s40` |  |
| RequestedBy | `s200` |  |
| ApprovedBy | `s200?` |  |
| DecisionAt | `t?` |  |

Additional check:

```sql
("ApprovedBy" IS NULL OR "ApprovedBy" <> "CreatedBy") AND "State" IN ('Draft','Pending','Approved','Rejected','Cancelled','Running','Completed','Failed','Submitted','Posted')
```

## workflow.workflow_definitions

| Column | Type | Reference |
|---|---|---|
| Code | `s100` |  |
| Name | `s200` |  |

Unique: `Code`.

## workflow.workflow_versions

| Column | Type | Reference |
|---|---|---|
| DefinitionId | `g` | `workflow.workflow_definitions` |
| Revision | `i` |  |
| State | `s40` |  |
| Definition | `j` |  |
| EffectiveFrom | `d` |  |
| EffectiveUntil | `d?` |  |
| ApprovedBy | `s200?` |  |

Unique: `DefinitionId,Revision`.

Additional check:

```sql
"Revision" > 0 AND ("EffectiveUntil" IS NULL OR "EffectiveUntil" >= "EffectiveFrom") AND ("ApprovedBy" IS NULL OR "ApprovedBy" <> "CreatedBy") AND "State" IN ('Draft','Pending','Approved','Rejected','Cancelled','Running','Completed','Failed','Submitted','Posted')
```

## workflow.approval_instances

| Column | Type | Reference |
|---|---|---|
| WorkflowVersionId | `g` | `workflow.workflow_versions` |
| SourceType | `s80` |  |
| SourceId | `g` |  |
| SourceVersion | `i` |  |
| RequestedBy | `s200` |  |
| State | `s40` |  |

Unique: `SourceType,SourceId,SourceVersion`.

Additional check:

```sql
"SourceVersion" > 0 AND "State" IN ('Draft','Pending','Approved','Rejected','Cancelled','Running','Completed','Failed','Submitted','Posted')
```

## workflow.approval_tasks

| Column | Type | Reference |
|---|---|---|
| InstanceId | `g` | `workflow.approval_instances` |
| AssignedTo | `s200` |  |
| StepNumber | `i` |  |
| State | `s40` |  |
| DueAt | `t?` |  |
| DecisionAt | `t?` |  |

Unique: `InstanceId,StepNumber,AssignedTo`.

Additional check:

```sql
("StepNumber" > 0 AND "State" IN ('Pending','Approved','Rejected','Cancelled'))
```

## workflow.approval_actions

| Column | Type | Reference |
|---|---|---|
| TaskId | `g` | `workflow.approval_tasks` |
| Actor | `s200` |  |
| Decision | `s40` |  |
| Reason | `s1000` |  |
| OccurredAt | `t` |  |
| IdempotencyKey | `s100` |  |

Unique: `TaskId,IdempotencyKey`.

Additional check:

```sql
("Decision" IN ('Approved','Rejected','Cancelled'))
```

## workflow.delegations

| Column | Type | Reference |
|---|---|---|
| Delegator | `s200` |  |
| Delegate | `s200` |  |
| StartsAt | `t` |  |
| ExpiresAt | `t` |  |
| RevokedAt | `t?` |  |

Additional check:

```sql
("Delegator" <> "Delegate" AND "ExpiresAt" > "StartsAt")
```

## workflow.escalation_rules

| Column | Type | Reference |
|---|---|---|
| WorkflowVersionId | `g` | `workflow.workflow_versions` |
| StepNumber | `i` |  |
| AfterMinutes | `i` |  |
| EscalateTo | `s200` |  |

Unique: `WorkflowVersionId,StepNumber,AfterMinutes`.

Additional check:

```sql
("StepNumber" > 0 AND "AfterMinutes" > 0)
```

## reporting.report_templates

| Column | Type | Reference |
|---|---|---|
| Code | `s100` |  |
| Revision | `i` |  |
| Definition | `j` |  |
| ApprovedBy | `s200?` |  |

Unique: `Code,Revision`.

Additional check:

```sql
"Revision" > 0 AND ("ApprovedBy" IS NULL OR "ApprovedBy" <> "CreatedBy")
```

## reporting.report_jobs

| Column | Type | Reference |
|---|---|---|
| TemplateId | `g` | `reporting.report_templates` |
| Parameters | `j` |  |
| State | `s40` |  |
| RequestedBy | `s200` |  |
| Watermark | `t?` |  |
| StartedAt | `t?` |  |
| CompletedAt | `t?` |  |
| Error | `s2000?` |  |

Additional check:

```sql
"State" IN ('Draft','Pending','Approved','Rejected','Cancelled','Running','Completed','Failed','Submitted','Posted')
```

## reporting.report_exports

| Column | Type | Reference |
|---|---|---|
| JobId | `g` | `reporting.report_jobs` |
| ObjectKey | `s1000` |  |
| Sha256 | `s64` |  |
| ContentType | `s200` |  |
| SizeBytes | `l` |  |
| ExpiresAt | `t` |  |

Unique: `JobId,ObjectKey`.

Additional check:

```sql
("SizeBytes" > 0 AND length("Sha256") = 64 AND "ExpiresAt" > "CreatedAt")
```

## reporting.reconciliation_alerts

| Column | Type | Reference |
|---|---|---|
| SourceType | `s80` |  |
| SourceId | `g` |  |
| Severity | `s40` |  |
| Message | `s2000` |  |
| ResolvedAt | `t?` |  |
| ResolvedBy | `s200?` |  |

## integration.integration_connections

| Column | Type | Reference |
|---|---|---|
| Code | `s100` |  |
| Provider | `s100` |  |
| Endpoint | `s2000` |  |
| SecretReference | `s1000` |  |
| Enabled | `b` |  |

Unique: `Code`.

## integration.field_mappings

| Column | Type | Reference |
|---|---|---|
| ConnectionId | `g` | `integration.integration_connections` |
| DocumentType | `s100` |  |
| Revision | `i` |  |
| Mapping | `j` |  |
| ApprovedBy | `s200?` |  |

Unique: `ConnectionId,DocumentType,Revision`.

Additional check:

```sql
"Revision" > 0 AND ("ApprovedBy" IS NULL OR "ApprovedBy" <> "CreatedBy")
```

## integration.inbound_batches

| Column | Type | Reference |
|---|---|---|
| ConnectionId | `g` | `integration.integration_connections` |
| MappingId | `g` | `integration.field_mappings` |
| ExternalId | `s200` |  |
| State | `s40` |  |
| PayloadKey | `s1000` |  |
| PayloadDigest | `s64` |  |
| ReceivedAt | `t` |  |

Unique: `ConnectionId,ExternalId`.

Additional check:

```sql
(length("PayloadDigest") = 64) AND "State" IN ('Draft','Pending','Approved','Rejected','Cancelled','Running','Completed','Failed','Submitted','Posted')
```

## integration.inbound_rows

| Column | Type | Reference |
|---|---|---|
| BatchId | `g` | `integration.inbound_batches` |
| RowNumber | `i` |  |
| ExternalId | `s200` |  |
| State | `s40` |  |
| Error | `s2000?` |  |
| SourceId | `g?` |  |

Unique: `BatchId,RowNumber`.

Additional check:

```sql
("RowNumber" > 0) AND "State" IN ('Draft','Pending','Approved','Rejected','Cancelled','Running','Completed','Failed','Submitted','Posted')
```

## integration.external_references

| Column | Type | Reference |
|---|---|---|
| ConnectionId | `g` | `integration.integration_connections` |
| SourceType | `s80` |  |
| ExternalId | `s200` |  |
| SourceId | `g` |  |

Unique: `ConnectionId,SourceType,ExternalId`.

## integration.webhook_subscriptions

| Column | Type | Reference |
|---|---|---|
| ConnectionId | `g` | `integration.integration_connections` |
| EventType | `s200` |  |
| Endpoint | `s2000` |  |
| SigningSecretReference | `s1000` |  |
| Enabled | `b` |  |

Unique: `ConnectionId,EventType,Endpoint`.

## integration.delivery_attempts

| Column | Type | Reference |
|---|---|---|
| SubscriptionId | `g` | `integration.webhook_subscriptions` |
| EventId | `g` |  |
| AttemptNumber | `i` |  |
| State | `s40` |  |
| NextAttemptAt | `t?` |  |
| LeaseUntil | `t?` |  |
| HttpStatus | `i?` |  |
| Error | `s2000?` |  |

Unique: `SubscriptionId,EventId,AttemptNumber`.

Additional check:

```sql
("AttemptNumber" > 0 AND ("HttpStatus" IS NULL OR "HttpStatus" BETWEEN 100 AND 599)) AND "State" IN ('Draft','Pending','Approved','Rejected','Cancelled','Running','Completed','Failed','Submitted','Posted')
```

## integration.dead_letters

| Column | Type | Reference |
|---|---|---|
| ConnectionId | `g` | `integration.integration_connections` |
| BatchId | `g?` | `integration.inbound_batches` |
| EventId | `g?` |  |
| PayloadKey | `s1000` |  |
| Error | `s2000` |  |
| FailedAt | `t` |  |
| ReplayedAt | `t?` |  |

