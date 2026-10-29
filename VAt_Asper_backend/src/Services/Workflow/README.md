# Workflow backend

Workflow owns independent Domain, Application and Infrastructure projects, `WorkflowDbContext`, seven workflow tables and `workflow.__WorkflowMigrationsHistory` in `vat_workflow`. The API on port **5110** references its own core, `Vat.Web` and `Vat.SharedKernel`; it has no financial host/core dependency.

Provide `ConnectionStrings__WorkflowDatabase` and `Authentication__Authority` through environment secrets. On this Windows development machine, `Import-LocalConnections.ps1` loads the encrypted runtime profile. The app can read task metadata and migration history only; the separate owner applies migrations through `scripts/Update-WorkflowDatabase.ps1`.

```powershell
dotnet run --project Vat.Workflow.Api --configuration Release
```

The current API lists scoped task metadata. It does not execute approval decisions, delegations or cross-service posting. Source document transitions still use their owning API and expected-version checks. Company/user/source IDs are external references requiring future service contracts before enabling writes.

The operator utility `tools/Vat.Workflow.Migrator` copies a consistent source snapshot, verifies row fingerprints, accepts identical reruns and refuses divergent targets. It does not modify source rows. Stop legacy writers for cutover; no replication is provided. Legacy source tables remain historical compatibility data and are inaccessible to the financial runtime role after provisioning.

See [ownership and operations](../../../docs/SERVICE_OWNERSHIP.md), [API routes](../../../docs/SERVICE_ROUTES.md) and [Workflow extraction](../../../docs/architecture/0016-owned-workflow-context.md).
