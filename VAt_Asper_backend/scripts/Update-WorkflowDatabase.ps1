param([ValidateSet('Debug', 'Release')][string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
if (!$env:ConnectionStrings__WorkflowMigrationDatabase) {
    . (Join-Path $PSScriptRoot 'Import-LocalConnections.ps1') -IncludeWorkflowOwner
}
$previous = $env:ConnectionStrings__WorkflowDatabase
try {
    $env:ConnectionStrings__WorkflowDatabase = $env:ConnectionStrings__WorkflowMigrationDatabase
    dotnet ef database update --project src/Services/Workflow/Infrastructure --startup-project src/Services/Workflow/Vat.Workflow.Api --context WorkflowDbContext --configuration $Configuration --no-build
    if ($LASTEXITCODE -ne 0) { throw 'Workflow database migration failed.' }
} finally { $env:ConnectionStrings__WorkflowDatabase = $previous }
