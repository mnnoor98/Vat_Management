<#
.SYNOPSIS
    Runs all 12 microservice database migrators and verifies database-per-service cutover.
.DESCRIPTION
    Executes each dedicated migrator in sequence against local PostgreSQL:
    - Vat.Identity.Migrator       -> vat_identity
    - Vat.Company.Migrator        -> vat_company
    - Vat.MasterData.Migrator     -> vat_masterdata
    - Vat.Purchase.Migrator       -> vat_purchase
    - Vat.Sales.Migrator          -> vat_sales
    - Vat.Inventory.Migrator      -> vat_inventory
    - Vat.Manufacturing.Migrator  -> vat_manufacturing
    - Vat.VatEngine.Migrator      -> vat_vatengine
    - Vat.Compliance.Migrator     -> vat_compliance
    - Vat.Workflow.Migrator       -> vat_workflow
    - Vat.Reporting.Migrator      -> vat_reporting
    - Vat.Integration.Migrator    -> vat_integration
#>

[CmdletBinding()]
param(
    [string]$SourceDatabase = "vat_management",
    [string]$HostServer = "127.0.0.1",
    [int]$Port = 5432,
    [string]$Username = "postgres",
    [string]$Password = "123456"
)

$ErrorActionPreference = "Stop"
$baseDir = Split-Path -Parent $PSScriptRoot
$toolsDir = Join-Path $baseDir "tools"

$migrators = @(
    @{ Name = "Identity";      Project = "Vat.Identity.Migrator/Vat.Identity.Migrator.csproj";           TargetDb = "vat_identity" },
    @{ Name = "Company";       Project = "Vat.Company.Migrator/Vat.Company.Migrator.csproj";             TargetDb = "vat_company" },
    @{ Name = "MasterData";    Project = "Vat.MasterData.Migrator/Vat.MasterData.Migrator.csproj";       TargetDb = "vat_masterdata" },
    @{ Name = "Purchase";      Project = "Vat.Purchase.Migrator/Vat.Purchase.Migrator.csproj";           TargetDb = "vat_purchase" },
    @{ Name = "Sales";         Project = "Vat.Sales.Migrator/Vat.Sales.Migrator.csproj";                 TargetDb = "vat_sales" },
    @{ Name = "Inventory";     Project = "Vat.Inventory.Migrator/Vat.Inventory.Migrator.csproj";         TargetDb = "vat_inventory" },
    @{ Name = "Manufacturing"; Project = "Vat.Manufacturing.Migrator/Vat.Manufacturing.Migrator.csproj"; TargetDb = "vat_manufacturing" },
    @{ Name = "VatEngine";     Project = "Vat.VatEngine.Migrator/Vat.VatEngine.Migrator.csproj";         TargetDb = "vat_vatengine" },
    @{ Name = "Compliance";    Project = "Vat.Compliance.Migrator/Vat.Compliance.Migrator.csproj";       TargetDb = "vat_compliance" },
    @{ Name = "Workflow";      Project = "Vat.Workflow.Migrator/Vat.Workflow.Migrator.csproj";           TargetDb = "vat_workflow" },
    @{ Name = "Reporting";     Project = "Vat.Reporting.Migrator/Vat.Reporting.Migrator.csproj";         TargetDb = "vat_reporting" },
    @{ Name = "Integration";   Project = "Vat.Integration.Migrator/Vat.Integration.Migrator.csproj";     TargetDb = "vat_integration" }
)

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " Running All 12 Microservice Database Migrations " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$successCount = 0

foreach ($m in $migrators) {
    $projectPath = Join-Path $toolsDir $m.Project
    Write-Host "`n>>> [MIGRATING] Service: $($m.Name) -> $($m.TargetDb) ..." -ForegroundColor Yellow
    
    $env:ConnectionStrings__VatDatabase = "Host=$HostServer;Port=$Port;Database=$SourceDatabase;Username=$Username;Password=$Password;"
    Set-Item -Path "env:ConnectionStrings__$($m.Name)Database" -Value "Host=$HostServer;Port=$Port;Database=$($m.TargetDb);Username=$Username;Password=$Password;"

    & dotnet run --project $projectPath --no-build
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Migration failed for $($m.Name) (Exit Code: $LASTEXITCODE)"
    } else {
        Write-Host ">>> [SUCCESS] $($m.Name) migration and ownership verification complete." -ForegroundColor Green
        $successCount++
    }
}

Write-Host "`n==========================================================" -ForegroundColor Cyan
Write-Host " Migration Completed: $successCount / $($migrators.Count) databases verified." -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Cyan
