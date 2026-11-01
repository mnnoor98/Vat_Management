# API route ownership

Base URLs are listed in [service architecture](SERVICE_ARCHITECTURE.md). Health endpoints belong to every host. Retired routes return 410.

## Identity

| Method | Path | Controller |
|---|---|---|
| GET | `/api/v1/companies/{companyId}/identity/roles` | IdentityController |
| GET | `/api/v1/identity/me` | ProfileController |
| GET | `/api/v1/auth/dev-token` | Retired |

## Company

| Method | Path | Controller |
|---|---|---|
| POST | `/api/v1/companies` | CompanyController |
| GET | `/api/v1/companies/{companyId}` | CompanyController |
| POST | `/api/v1/companies/{companyId}/branches` | CompanyController |
| GET | `/api/v1/companies/{companyId}/branches` | CompanyController |
| GET | `/api/v1/companies/{companyId}/branches/{branchId}` | CompanyController |
| POST | `/api/v1/companies/{companyId}/periods` | FiscalPeriodsController |
| GET | `/api/v1/companies/{companyId}/periods` | FiscalPeriodsController |
| GET | `/api/v1/companies/{companyId}/periods/{id}` | FiscalPeriodsController |
| POST | `/api/v1/companies/{companyId}/periods/{id}/request-close` | FiscalPeriodsController |
| POST | `/api/v1/companies/{companyId}/periods/{id}/close` | FiscalPeriodsController |
| POST | `/api/v1/companies/{companyId}/periods/{id}/cancel-close` | FiscalPeriodsController |

## MasterData

| Method | Path | Controller |
|---|---|---|
| GET | `/api/v1/companies/{companyId}/customers` | CustomerController |
| GET | `/api/v1/companies/{companyId}/customers/{id}` | CustomerController |
| POST | `/api/v1/companies/{companyId}/customers` | CustomerController |
| POST | `/api/v1/companies/{companyId}/customers/{id}/update` | CustomerController |
| POST | `/api/v1/companies/{companyId}/products` | MasterDataController |
| GET | `/api/v1/companies/{companyId}/products` | MasterDataController |
| GET | `/api/v1/companies/{companyId}/products/{id}` | MasterDataController |
| POST | `/api/v1/companies/{companyId}/suppliers` | MasterDataController |
| GET | `/api/v1/companies/{companyId}/suppliers` | MasterDataController |
| GET | `/api/v1/companies/{companyId}/suppliers/{id}` | MasterDataController |

## Purchase

| Method | Path | Controller |
|---|---|---|
| POST | `/api/v1/companies/{companyId}/purchases` | PurchaseController |
| GET | `/api/v1/companies/{companyId}/purchases` | PurchaseController |
| GET | `/api/v1/companies/{companyId}/purchases/{id}` | PurchaseController |
| POST | `/api/v1/companies/{companyId}/purchases/{id}/submit` | PurchaseController |
| POST | `/api/v1/companies/{companyId}/purchases/{id}/approve` | PurchaseController |
| POST | `/api/v1/companies/{companyId}/purchases/{id}/reject` | PurchaseController |
| POST | `/api/v1/companies/{companyId}/purchases/{purchaseId}/reversals` | PurchaseReversalsController |
| GET | `/api/v1/companies/{companyId}/purchases/{purchaseId}/reversals/{id}` | PurchaseReversalsController |
| POST | `/api/v1/companies/{companyId}/purchases/{purchaseId}/reversals/{id}/approve` | PurchaseReversalsController |
| POST | `/api/v1/companies/{companyId}/purchases/{purchaseId}/reversals/{id}/cancel` | PurchaseReversalsController |
| POST | `/api/v1/companies/{companyId}/purchases/{purchaseId}/cancel-unposted` | PurchaseReversalsController |
| POST | `/api/v1/companies/{companyId}/purchases/{purchaseId}/posting-plans` | PurchasePostingController |
| GET | `/api/v1/companies/{companyId}/purchases/{purchaseId}/posting-plans/{planId}` | PurchasePostingController |
| POST | `/api/v1/companies/{companyId}/purchases/{purchaseId}/posting-plans/{planId}/post` | PurchasePostingController |
| POST | `/api/v1/companies/{companyId}/purchases/{purchaseId}/posting-plans/{planId}/cancel` | PurchasePostingController |
| GET | `/api/v1/companies/{companyId}/purchases/{purchaseId}/posting-status` | PurchasePostingController |
| GET | `/api/v1/companies/{companyId}/purchases/{purchaseId}/reconciliation` | PurchasePostingController |
| POST | `/api/v1/companies/{companyId}/purchases/{purchaseId}/post-ledger` | Retired |

## Sales

| Method | Path | Controller |
|---|---|---|
| POST | `/api/v1/companies/{companyId}/payments/{paymentId}/reversals` | PaymentCorrectionController |
| POST | `/api/v1/companies/{companyId}/payments/{paymentId}/refunds` | PaymentCorrectionController |
| POST | `/api/v1/companies/{companyId}/payments/{paymentId}/reversals/{id}/approve` | PaymentCorrectionController |
| POST | `/api/v1/companies/{companyId}/payments/{paymentId}/reversals/{id}/cancel` | PaymentCorrectionController |
| POST | `/api/v1/companies/{companyId}/payments/{paymentId}/refunds/{id}/approve` | PaymentCorrectionController |
| POST | `/api/v1/companies/{companyId}/payments/{paymentId}/refunds/{id}/cancel` | PaymentCorrectionController |
| GET | `/api/v1/companies/{companyId}/payments/{paymentId}/corrections` | PaymentCorrectionController |
| GET | `/api/v1/companies/{companyId}/payments/{paymentId}/corrections/{id}` | PaymentCorrectionController |
| POST | `/api/v1/companies/{companyId}/payments` | PaymentController |
| GET | `/api/v1/companies/{companyId}/payments` | PaymentController |
| GET | `/api/v1/companies/{companyId}/payments/{id}` | PaymentController |
| POST | `/api/v1/companies/{companyId}/payments/{id}/approve` | PaymentController |
| POST | `/api/v1/companies/{companyId}/payments/{id}/cancel` | PaymentController |
| POST | `/api/v1/companies/{companyId}/payments/{id}/allocate` | PaymentController |
| GET | `/api/v1/companies/{companyId}/customers/{customerId}/receivables` | PaymentController |
| GET | `/api/v1/companies/{companyId}/sales/{invoiceId}/returnable-lines` | SalesCreditController |
| POST | `/api/v1/companies/{companyId}/sales/{invoiceId}/price-credits` | SalesCreditController |
| POST | `/api/v1/companies/{companyId}/sales/{invoiceId}/credit-notes` | SalesCreditController |
| GET | `/api/v1/companies/{companyId}/sales/{invoiceId}/credit-notes` | SalesCreditController |
| GET | `/api/v1/companies/{companyId}/sales/{invoiceId}/credit-notes/{id}` | SalesCreditController |
| POST | `/api/v1/companies/{companyId}/sales/{invoiceId}/credit-notes/{id}/approve` | SalesCreditController |
| POST | `/api/v1/companies/{companyId}/sales/{invoiceId}/credit-notes/{id}/cancel` | SalesCreditController |
| POST | `/api/v1/companies/{companyId}/sales/{invoiceId}/debit-notes` | SalesDebitController |
| GET | `/api/v1/companies/{companyId}/sales/{invoiceId}/debit-notes` | SalesDebitController |
| GET | `/api/v1/companies/{companyId}/sales/{invoiceId}/debit-notes/{id}` | SalesDebitController |
| POST | `/api/v1/companies/{companyId}/sales/{invoiceId}/debit-notes/{id}/approve` | SalesDebitController |
| POST | `/api/v1/companies/{companyId}/sales/{invoiceId}/debit-notes/{id}/cancel` | SalesDebitController |
| POST | `/api/v1/companies/{companyId}/sales` | SalesController |
| GET | `/api/v1/companies/{companyId}/sales` | SalesController |
| GET | `/api/v1/companies/{companyId}/sales/{id}` | SalesController |
| POST | `/api/v1/companies/{companyId}/sales/{id}/submit` | SalesController |
| POST | `/api/v1/companies/{companyId}/sales/{id}/approve` | SalesController |
| POST | `/api/v1/companies/{companyId}/sales/{id}/reject` | SalesController |
| POST | `/api/v1/companies/{companyId}/sales/{id}/cancel` | SalesController |
| POST | `/api/v1/companies/{companyId}/sales/{id}/reserve` | SalesPostingController |
| POST | `/api/v1/companies/{companyId}/sales/{id}/post` | SalesPostingController |
| GET | `/api/v1/companies/{companyId}/sales/{id}/fulfillment` | SalesPostingController |
| GET | `/api/v1/companies/{companyId}/sales/{id}/reconciliation` | SalesPostingController |
| GET | `/api/v1/companies/{companyId}/sales/ledger` | SalesPostingController |
| GET | `/api/v1/companies/{companyId}/sales/summary` | SalesPostingController |
| POST | `/api/v1/companies/{companyId}/sales/{id}/post-ledger` | Retired |

## Inventory

| Method | Path | Controller |
|---|---|---|
| POST | `/api/v1/companies/{companyId}/warehouses` | InventoryController |
| GET | `/api/v1/companies/{companyId}/warehouses/{id}` | InventoryController |
| GET | `/api/v1/companies/{companyId}/inventory/balances` | InventoryController |
| GET | `/api/v1/companies/{companyId}/warehouses` | InventoryController |
| GET | `/api/v1/companies/{companyId}/inventory/all-balances` | InventoryController |
| POST | `/api/v1/companies/{companyId}/transfers` | InventoryController |
| POST | `/api/v1/companies/{companyId}/transfers/{id}/submit` | InventoryController |
| POST | `/api/v1/companies/{companyId}/transfers/{id}/approve` | InventoryController |
| POST | `/api/v1/companies/{companyId}/transfers/{id}/post` | InventoryController |
| POST | `/api/v1/companies/{companyId}/transfers/{id}/cancel` | InventoryController |
| GET | `/api/v1/companies/{companyId}/transfers` | InventoryController |

Current Inventory controller additions are inventoried here; the new transfer flow has not been validated by the Integration metadata extraction.

## Manufacturing

| Method | Path | Controller |
|---|---|---|
| GET | `/api/v1/companies/{companyId}/manufacturing/production-orders` | ManufacturingController |
| POST | `/api/v1/companies/{companyId}/manufacturing/production-orders` | ManufacturingController |
| GET | `/api/v1/companies/{companyId}/manufacturing/production-orders/{id}` | ManufacturingController |
| GET | `/api/v1/companies/{companyId}/manufacturing/production-orders/{id}/execution-source` | ManufacturingController |
| POST | `/api/v1/companies/{companyId}/manufacturing/production-orders/{id}/{submit,approve,cancel}` | ManufacturingController |
| GET | `/api/v1/companies/{companyId}/manufacturing/coefficients` | ManufacturingController |
| POST | `/api/v1/companies/{companyId}/manufacturing/coefficients` | ManufacturingController |
| PUT | `/api/v1/companies/{companyId}/manufacturing/coefficients/{id}/approve` | ManufacturingController |
| POST | `/api/v1/companies/{companyId}/manufacturing/coefficients/{id}/submit` | ManufacturingController |
| POST | `/api/v1/companies/{companyId}/manufacturing/coefficients/{id}/cancel` | ManufacturingController |

Financial execution routes use `/api/v1/companies/{companyId}/inventory/production-executions`: POST import, GET list/detail, POST/GET `{id}/operations`, GET `{id}/operations/{operation}`, POST `{id}/operations/{operation}/approve` and `/cancel`. See [production/WIP ownership, permissions and configuration](PRODUCTION_WIP_EXECUTION.md).

The coefficient routes reflect concurrent controller additions. Their business flow is not certified by the messaging/readiness checks; see the [scope note](architecture/0022-reporting-delivery-commit-receipts.md#concurrent-manufacturing-scope).

## VatEngine

| Method | Path | Controller |
|---|---|---|
| POST | `/api/v1/companies/{companyId}/tax/rules` | TaxController |
| POST | `/api/v1/companies/{companyId}/tax/rules/{id}/publish` | TaxController |
| GET | `/api/v1/companies/{companyId}/tax/rules/{id}` | TaxController |
| GET | `/api/v1/companies/{companyId}/tax/ledger` | TaxController |

## Compliance

| Method | Path | Controller |
|---|---|---|
| GET | `/api/v1/companies/{companyId}/compliance/returns` | ComplianceController |

## Workflow

| Method | Path | Controller |
|---|---|---|
| GET | `/api/v1/companies/{companyId}/workflow/tasks` | WorkflowController |
| GET | `/api/v1/companies/{companyId}/workflows/my-tasks` | Retired |
| POST | `/api/v1/companies/{companyId}/workflows/tasks/{id}/approve` | Retired |
| POST | `/api/v1/companies/{companyId}/workflows/tasks/{id}/reject` | Retired |

## Reporting

| Method | Path | Controller |
|---|---|---|
| GET | `/api/v1/companies/{companyId}/customers/{customerId}/statement` | CustomerReportingController |
| GET | `/api/v1/companies/{companyId}/customers/{customerId}/ageing` | CustomerReportingController |
| GET | `/api/v1/companies/{companyId}/reports/financial-summary` | ReportsController |
| GET | `/api/v1/companies/{companyId}/reports/financial-entries` | ReportsController |
| POST | `/api/v1/companies/{companyId}/reports/backfill` | ReportsController |
| POST | `/api/v1/companies/{companyId}/reports/rebuild` | ReportsController |

## Integration

| Method | Path | Controller |
|---|---|---|
| GET | `/api/v1/companies/{companyId}/messaging/outbox` | MessagingController |
| POST | `/api/v1/companies/{companyId}/messaging/outbox/{id}/retry` | MessagingController |
| GET | `/api/v1/companies/{companyId}/messaging/dead-letters` | MessagingController |
| POST | `/api/v1/companies/{companyId}/messaging/dead-letters/{id}/replay` | MessagingController |
| GET | `/api/v1/companies/{companyId}/integration/connections` | IntegrationController |

