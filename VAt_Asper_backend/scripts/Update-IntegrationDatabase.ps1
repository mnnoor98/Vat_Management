param([ValidateSet('Debug', 'Release')][string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
if (!$env:ConnectionStrings__IntegrationMigrationDatabase) {
    . (Join-Path $PSScriptRoot 'Import-LocalConnections.ps1') -IncludeIntegrationOwner
}
$previous = $env:ConnectionStrings__IntegrationDatabase
try {
    $env:ConnectionStrings__IntegrationDatabase = $env:ConnectionStrings__IntegrationMigrationDatabase
    dotnet ef database update --project src/Services/Integration/Infrastructure --startup-project src/Services/Integration/Vat.Integration.Api --context IntegrationDbContext --configuration $Configuration --no-build
    if ($LASTEXITCODE -ne 0) { throw 'Integration database migration failed.' }
} finally { $env:ConnectionStrings__IntegrationDatabase = $previous }
