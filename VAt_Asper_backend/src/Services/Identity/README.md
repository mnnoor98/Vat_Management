# Identity backend

Port 5101. This API owns its Domain, Application and Infrastructure projects and `vat_identity`, using `IdentityDbContext` and its own migration chain. It references only its own core, `Vat.Web` and `Vat.SharedKernel`.

Supply `ConnectionStrings__IdentityDatabase` and the external OIDC settings. Use the runtime role for the API and the separate owner role for migrations. The API exposes verified profiles and scoped role metadata reads. Local Development mode with the exact authority `https://localhost:5080` also supports authenticated local account creation/listing at `/api/v1/companies/{companyId}/identity/users` and local password login. Apply the `LocalDevelopmentAccounts` Identity migration explicitly. Creation requires `identity.manage`; list reads require `identity.read`. Passwords use ASP.NET Core's salted password hasher and are never returned. Server-owned role mappings intersect with assigned modules; usernames are normalized and unique, and seeded login names are reserved. Each creation writes a security audit in the same transaction. Production/external-issuer account provisioning remains with the identity provider.

After updating a running local environment, sign out and sign back in as `imran` to obtain the new `identity.manage` grant. Use Admin > Users & Logins Management to create users with passwords of 8-128 characters. The selected branch is a UI default; backend financial authorization remains company scoped as documented in the financial API.

See [ownership and migration operations](../../../docs/SERVICE_OWNERSHIP.md) and [routes](../../../docs/SERVICE_ROUTES.md).
