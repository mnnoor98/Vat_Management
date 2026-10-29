param([ValidateSet('Debug', 'Release')][string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
if (!$env:ConnectionStrings__IdentityMigrationDatabase) {
    . (Join-Path $PSScriptRoot 'Import-LocalConnections.ps1') -IncludeIdentityOwner
}
$previous = $env:ConnectionStrings__IdentityDatabase
try {
    $env:ConnectionStrings__IdentityDatabase = $env:ConnectionStrings__IdentityMigrationDatabase
    dotnet ef database update --project src/Services/Identity/Infrastructure --startup-project src/Services/Identity/Vat.Identity.Api --context IdentityDbContext --configuration $Configuration --no-build
    if ($LASTEXITCODE -ne 0) { throw 'Identity database migration failed.' }
} finally { $env:ConnectionStrings__IdentityDatabase = $previous }
