<#
.SYNOPSIS
    Shows or applies schema migrations for the six active persistence owners.
.DESCRIPTION
    Default mode is a read-only plan. -Apply requires explicit migration-owner connections.
    This does not copy data, merge databases or certify an ownership cutover.
#>
[CmdletBinding()]
param(
    [switch]$Apply,
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release'
)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$steps = @(
    @{ Name = 'Financial'; Key = 'Vat'; Project = 'src/Vat.Infrastructure'; Startup = 'src/Vat.Api'; Context = 'VatDbContext' },
    @{ Name = 'Identity'; Key = 'Identity'; Project = 'src/Services/Identity/Infrastructure'; Startup = 'src/Services/Identity/Vat.Identity.Api'; Context = 'IdentityDbContext' },
    @{ Name = 'Workflow'; Key = 'Workflow'; Project = 'src/Services/Workflow/Infrastructure'; Startup = 'src/Services/Workflow/Vat.Workflow.Api'; Context = 'WorkflowDbContext' },
    @{ Name = 'Compliance'; Key = 'Compliance'; Project = 'src/Services/Compliance/Infrastructure'; Startup = 'src/Services/Compliance/Vat.Compliance.Api'; Context = 'ComplianceDbContext' },
    @{ Name = 'Manufacturing'; Key = 'Manufacturing'; Project = 'src/Services/Manufacturing/Infrastructure'; Startup = 'src/Services/Manufacturing/Vat.Manufacturing.Api'; Context = 'ManufacturingDbContext' },
    @{ Name = 'Integration'; Key = 'Integration'; Project = 'src/Services/Integration/Infrastructure'; Startup = 'src/Services/Integration/Vat.Integration.Api'; Context = 'IntegrationDbContext' }
)
foreach ($step in $steps) {
    Write-Output ("{0}: {1}; requires ConnectionStrings__{2}MigrationDatabase" -f $step.Name, $step.Context, $step.Key)
}
if (!$Apply) {
    Write-Output 'Plan only: no database connections or changes. Supply all six migration-owner settings and pass -Apply to apply schema migrations.'
    return
}
# Validate every owner setting before executing the first schema update. Never fall back to runtime credentials.
foreach ($step in $steps) {
    $ownerKey = 'ConnectionStrings__' + $step.Key + 'MigrationDatabase'
    if ([string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($ownerKey, 'Process'))) {
        throw "Configure $ownerKey before applying the migration plan."
    }
}
Push-Location $repoRoot
try {
    foreach ($step in $steps) {
        $runtimeKey = 'ConnectionStrings__' + $step.Key + 'Database'
        $previous = [Environment]::GetEnvironmentVariable($runtimeKey, 'Process')
        try {
            [Environment]::SetEnvironmentVariable($runtimeKey,
                [Environment]::GetEnvironmentVariable(('ConnectionStrings__' + $step.Key + 'MigrationDatabase'), 'Process'), 'Process')
            & dotnet ef database update --project $step.Project --startup-project $step.Startup --context $step.Context --configuration $Configuration --no-build
            if ($LASTEXITCODE -ne 0) { throw "Schema migration failed for $($step.Name)." }
        } finally {
            [Environment]::SetEnvironmentVariable($runtimeKey, $previous, 'Process')
        }
    }
    Write-Output 'Applied schema migrations for six active persistence owners. This does not certify existing-data transfer or reconciliation.'
} finally { Pop-Location }
