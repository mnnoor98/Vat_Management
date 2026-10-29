# MasterData backend

This backend owns `Vat.MasterData.Api`, its controllers and its own process on local port **5103**. Its current owned source folders are `Application`, `Domain`, `Infrastructure`. Shared transactional/build dependencies are described in the [architecture guide](../../../docs/SERVICE_ARCHITECTURE.md).

Supply `ConnectionStrings__VatDatabase` and `Authentication__Authority` through your environment, then run from this directory:

```powershell
dotnet run --project Vat.MasterData.Api --configuration Release
```

From the repository root, `./scripts/Publish-Services.ps1 -Service MasterData` publishes this backend after a Release build. Its Dockerfile uses the repository root as its build context. See the [route ownership inventory](../../../docs/SERVICE_ROUTES.md) and the architecture guide for current capabilities, permissions, external configuration and inactive write workflows.
