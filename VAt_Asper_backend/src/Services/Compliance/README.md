# Compliance backend

Compliance owns Domain, Application and Infrastructure projects, `ComplianceDbContext`, eight compliance tables and `compliance.__ComplianceMigrationsHistory` in `vat_compliance`. The API on port **5109** depends on its own core, `Vat.Web` and `Vat.SharedKernel`; it has no financial host/core dependency.

Provide `ConnectionStrings__ComplianceDatabase` and `Authentication__Authority` through environment secrets. On this Windows development machine, `Import-LocalConnections.ps1` loads the encrypted runtime profile. Runtime credentials read return metadata/history only; the owner applies migrations through `scripts/Update-ComplianceDatabase.ps1`.

```powershell
dotnet run --project Vat.Compliance.Api --configuration Release
```

The API lists scoped return metadata. Filing, return calculation, evidence upload and fiscal-close execution are not implemented. Company/fiscal-period IDs are external references; future write workflows require Company period/version contracts and frozen ledger projections. This extraction does not close financial periods or introduce statutory form mappings.

`tools/Vat.Compliance.Migrator` copies a consistent source snapshot, verifies row fingerprints, accepts identical reruns and refuses divergent targets. It never changes source rows. Stop legacy writers before cutover; no replication is provided. Legacy source tables remain historical compatibility data and are inaccessible to the financial runtime role after provisioning.

See [ownership and local operations](../../../docs/SERVICE_OWNERSHIP.md), [routes](../../../docs/SERVICE_ROUTES.md) and [Compliance ownership decision](../../../docs/architecture/0017-owned-compliance-context.md).
