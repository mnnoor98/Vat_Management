param([ValidateSet('Debug', 'Release')][string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
if (!$env:ConnectionStrings__ManufacturingMigrationDatabase) {
    . (Join-Path $PSScriptRoot 'Import-LocalConnections.ps1') -IncludeManufacturingOwner
}
$previous = $env:ConnectionStrings__ManufacturingDatabase
try {
    $env:ConnectionStrings__ManufacturingDatabase = $env:ConnectionStrings__ManufacturingMigrationDatabase
    dotnet ef database update --project src/Services/Manufacturing/Infrastructure --startup-project src/Services/Manufacturing/Vat.Manufacturing.Api --context ManufacturingDbContext --configuration $Configuration --no-build
    if ($LASTEXITCODE -ne 0) { throw 'Manufacturing database migration failed.' }
} finally { $env:ConnectionStrings__ManufacturingDatabase = $previous }
