# Manufacturing backend

Manufacturing owns independent Domain/Application/Infrastructure projects, `ManufacturingDbContext`, twelve manufacturing tables and `manufacturing.__ManufacturingMigrationsHistory` in `vat_manufacturing`. The API on port **5107** depends on its own core, `Vat.Web` and `Vat.SharedKernel`; it has no financial host/core dependency.

Provide `ConnectionStrings__ManufacturingDatabase` and `Authentication__Authority` through deployment secrets. The local Windows profile loads encrypted runtime credentials with `Import-LocalConnections.ps1`. The runtime can read production metadata and execute the restricted coefficient workflow; the separate owner applies migrations through `Update-ManufacturingDatabase.ps1`.

```powershell
dotnet run --project Vat.Manufacturing.Api --configuration Release
```

The API lists scoped production orders and supports coefficient Draft/Submitted/Approved revisions with maker/checker, expected versions, immutable verified references, calculated costs, audit and idempotency. Material issues, production receipts, wastage posting and stock reconciliation commands remain future work. Company, product and warehouse IDs are external references. Future commands need MasterData/Inventory reference contracts, fiscal-period admission, immutable stock transactions and versioned posting coordination.

`tools/Vat.Manufacturing.Migrator` copies a locked source snapshot with verified binary transfer, accepts identical metadata reruns and refuses divergent or already-active workflow targets. Freeze legacy writers before cutover. Historical source tables remain for compatibility; no replication or dual writing is provided. Runtime access to that legacy schema is revoked after cutover.

See [ownership and local operations](../../../docs/SERVICE_OWNERSHIP.md), [routes](../../../docs/SERVICE_ROUTES.md) and [Manufacturing decision](../../../docs/architecture/0018-owned-manufacturing-context.md).

Approved production order creation and execution-source evidence are described in the [production/WIP contract](../../../docs/PRODUCTION_WIP_EXECUTION.md). Configure `ManufacturingReferences__InventoryBaseUrl` to the trusted Inventory origin for warehouse validation. Financial actual execution remains owned by the coordinated financial service and requires `ProductionReferences__ManufacturingBaseUrl` there. Deployment must grant the runtime access to the two new owner tables after the reviewed owner migration; no live migration or grants were applied by this code change.
## Coefficient execution

See [workflow contracts and permissions](../../../docs/architecture/0024-transfer-coefficient-workflows.md). Production must configure `ManufacturingReferences__MasterDataBaseUrl` to a trusted HTTPS origin. Development explicitly uses loopback port 5103. Creation delegates the caller token to MasterData and also requires `masterdata.read`; unavailable reference validation returns 503 without writes. New snapshots/commands/audit are append-only for the runtime. Historical coefficients without verified snapshots require a new revision before execution. This workflow does not constitute a regulatory filing.
