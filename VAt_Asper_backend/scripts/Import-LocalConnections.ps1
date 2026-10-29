param([switch]$Force, [switch]$IncludeIdentityOwner, [switch]$IncludeWorkflowOwner, [switch]$IncludeComplianceOwner, [switch]$IncludeManufacturingOwner, [switch]$IncludeIntegrationOwner)
$ErrorActionPreference = 'Stop'
if ($env:OS -ne 'Windows_NT') { throw 'Local DPAPI credentials require their original Windows account. Supply environment secrets on other systems.' }
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$secretRoot = Join-Path $repoRoot 'artifacts/local-secrets'
$metadata = Get-Content -LiteralPath (Join-Path $secretRoot 'databases.json') -Raw | ConvertFrom-Json
$roles = @(
    @{ Role = 'vat_financial_app'; Key = 'ConnectionStrings__VatDatabase'; Database = $metadata.FinancialDatabase },
    @{ Role = 'vat_identity_app'; Key = 'ConnectionStrings__IdentityDatabase'; Database = $metadata.IdentityDatabase }
)
if ($metadata.PSObject.Properties.Name -contains 'WorkflowDatabase') { $roles += @{ Role = 'vat_workflow_app'; Key = 'ConnectionStrings__WorkflowDatabase'; Database = $metadata.WorkflowDatabase } }
if ($metadata.PSObject.Properties.Name -contains 'ComplianceDatabase') { $roles += @{ Role = 'vat_compliance_app'; Key = 'ConnectionStrings__ComplianceDatabase'; Database = $metadata.ComplianceDatabase } }
if ($metadata.PSObject.Properties.Name -contains 'ManufacturingDatabase') { $roles += @{ Role = 'vat_manufacturing_app'; Key = 'ConnectionStrings__ManufacturingDatabase'; Database = $metadata.ManufacturingDatabase } }
if ($metadata.PSObject.Properties.Name -contains 'IntegrationDatabase') { $roles += @{ Role = 'vat_integration_app'; Key = 'ConnectionStrings__IntegrationDatabase'; Database = $metadata.IntegrationDatabase } }
if ($IncludeIntegrationOwner) { $roles += @{ Role = 'vat_integration_owner'; Key = 'ConnectionStrings__IntegrationMigrationDatabase'; Database = $metadata.IntegrationDatabase } }
if ($IncludeManufacturingOwner) { $roles += @{ Role = 'vat_manufacturing_owner'; Key = 'ConnectionStrings__ManufacturingMigrationDatabase'; Database = $metadata.ManufacturingDatabase } }
if ($IncludeComplianceOwner) { $roles += @{ Role = 'vat_compliance_owner'; Key = 'ConnectionStrings__ComplianceMigrationDatabase'; Database = $metadata.ComplianceDatabase } }
if ($IncludeWorkflowOwner) { $roles += @{ Role = 'vat_workflow_owner'; Key = 'ConnectionStrings__WorkflowMigrationDatabase'; Database = $metadata.WorkflowDatabase } }
if ($IncludeIdentityOwner) { $roles += @{ Role = 'vat_identity_owner'; Key = 'ConnectionStrings__IdentityMigrationDatabase'; Database = $metadata.IdentityDatabase } }
foreach ($item in $roles) {
    if (!$Force -and [Environment]::GetEnvironmentVariable($item.Key)) { continue }
    $credential = Import-Clixml -LiteralPath (Join-Path $secretRoot ($item.Role + '.credential.xml'))
    $connection = [Data.Common.DbConnectionStringBuilder]::new()
    $connection['Host'] = $metadata.Server; $connection['Port'] = $metadata.Port
    $connection['Database'] = $item.Database; $connection['Username'] = $credential.UserName
    $connection['Password'] = $credential.GetNetworkCredential().Password
    [Environment]::SetEnvironmentVariable($item.Key, $connection.ConnectionString, 'Process')
}
