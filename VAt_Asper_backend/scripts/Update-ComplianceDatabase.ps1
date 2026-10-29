param([ValidateSet('Debug', 'Release')][string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
if (!$env:ConnectionStrings__ComplianceMigrationDatabase) {
    . (Join-Path $PSScriptRoot 'Import-LocalConnections.ps1') -IncludeComplianceOwner
}
$previous = $env:ConnectionStrings__ComplianceDatabase
try {
    $env:ConnectionStrings__ComplianceDatabase = $env:ConnectionStrings__ComplianceMigrationDatabase
    dotnet ef database update --project src/Services/Compliance/Infrastructure --startup-project src/Services/Compliance/Vat.Compliance.Api --context ComplianceDbContext --configuration $Configuration --no-build
    if ($LASTEXITCODE -ne 0) { throw 'Compliance database migration failed.' }
} finally { $env:ConnectionStrings__ComplianceDatabase = $previous }
