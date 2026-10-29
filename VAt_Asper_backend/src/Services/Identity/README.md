# Identity backend

Port 5101. This API owns its Domain, Application and Infrastructure projects and `vat_identity`, using `IdentityDbContext` and its own migration chain. It references only its own core, `Vat.Web` and `Vat.SharedKernel`.

Supply `ConnectionStrings__IdentityDatabase` and the external OIDC settings. Use the runtime role for the API and the separate owner role for migrations. The API currently exposes verified profiles and scoped role metadata reads; it is not an OIDC/password server.

See [ownership and migration operations](../../../docs/SERVICE_OWNERSHIP.md) and [routes](../../../docs/SERVICE_ROUTES.md).
