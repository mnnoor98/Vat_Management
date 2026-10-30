Current runtime (30 October 2026): this API uses the coordinated financial core and `ConnectionStrings__VatDatabase`. Supply the same reviewed financial database to all seven financial hosts. The extracted context is staged, not the active write owner. See [transaction boundary, configuration and validation](../../../docs/architecture/0034-coordinated-financial-boundary.md). Earlier ownership/migration notes below describe historical or staged implementations; do not treat changing connections as a data migration.

# Inventory backend

This backend owns `Vat.Inventory.Api`, its controllers and its own process on local port **5106**. Its current owned source folders are the API project. Shared transactional/build dependencies are described in the [architecture guide](../../../docs/SERVICE_ARCHITECTURE.md).

Supply `ConnectionStrings__VatDatabase` and `Authentication__Authority` through your environment, then run from this directory:

```powershell
dotnet run --project Vat.Inventory.Api --configuration Release
```

From the repository root, `./scripts/Publish-Services.ps1 -Service Inventory` publishes this backend after a Release build. Its Dockerfile uses the repository root as its build context. See the [route ownership inventory](../../../docs/SERVICE_ROUTES.md) and the architecture guide for current capabilities, permissions, external configuration and inactive write workflows.
## Transfer execution

New transfers support Draft → Submitted → Approved → Posted, with pre-post cancellation. Commands require UUID idempotency keys and expected versions; approval requires a different actor. Separate `inventory.approve` and `inventory.post` permissions are required. Posting changes both warehouse balances and the paired journal atomically, excludes reserved stock, and respects fiscal-period close fences. Submitted/Approved transfers block closing. Read history exposes current version/checker. See [workflow contracts](../../../docs/architecture/0024-transfer-coefficient-workflows.md). Inventory still shares the coordinated financial database; it has not been independently extracted.