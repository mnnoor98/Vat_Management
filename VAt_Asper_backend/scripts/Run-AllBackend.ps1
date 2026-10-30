<#
.SYNOPSIS
    Starts all backend services (Unified Gateway + 12 Dedicated Microservices) concurrently.
#>

[CmdletBinding()]
param(
    [string]$Configuration = "Debug",
    [string]$Authority = $env:VAT_OIDC_AUTHORITY,
    [string]$HostServer = "127.0.0.1",
    [int]$DbPort = 5432,
    [string]$Username = $env:VAT_DB_USERNAME,
    [string]$Password = $env:VAT_DB_PASSWORD
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($Authority)) {
    throw "Configure VAT_OIDC_AUTHORITY or pass -Authority for the actual HTTPS identity provider."
}
if ([string]::IsNullOrWhiteSpace($Username) -or [string]::IsNullOrWhiteSpace($Password)) {
    throw "Configure VAT_DB_USERNAME and VAT_DB_PASSWORD or pass explicit local runtime credentials; no privileged defaults are provided."
}
$scriptRoot = $PSScriptRoot
$repoRoot = Split-Path -Parent $scriptRoot
$logsDir = Join-Path $repoRoot ("artifacts/services/" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $logsDir -Force | Out-Null

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " Starting Enterprise VAT Management Backend Services " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# Set Environment Variables
$env:Authentication__Authority = $Authority
$env:Authentication__Audience = "vat-api"
$env:ASPNETCORE_ENVIRONMENT = "Development"

$databases = @(
    "vat_identity", "vat_company", "vat_masterdata", "vat_purchase",
    "vat_sales", "vat_inventory", "vat_manufacturing", "vat_vatengine",
    "vat_compliance", "vat_workflow", "vat_reporting", "vat_integration", "vat_management"
)

$env:ConnectionStrings__VatDatabase = "Host=$HostServer;Port=$DbPort;Database=vat_management;Username=$Username;Password=$Password;Include Error Detail=true;"
$env:ConnectionStrings__IdentityDatabase = "Host=$HostServer;Port=$DbPort;Database=vat_identity;Username=$Username;Password=$Password;Include Error Detail=true;"
$env:ConnectionStrings__CompanyDatabase = "Host=$HostServer;Port=$DbPort;Database=vat_company;Username=$Username;Password=$Password;Include Error Detail=true;"
$env:ConnectionStrings__MasterDataDatabase = "Host=$HostServer;Port=$DbPort;Database=vat_masterdata;Username=$Username;Password=$Password;Include Error Detail=true;"
$env:ConnectionStrings__PurchaseDatabase = "Host=$HostServer;Port=$DbPort;Database=vat_purchase;Username=$Username;Password=$Password;Include Error Detail=true;"
$env:ConnectionStrings__SalesDatabase = "Host=$HostServer;Port=$DbPort;Database=vat_sales;Username=$Username;Password=$Password;Include Error Detail=true;"
$env:ConnectionStrings__InventoryDatabase = "Host=$HostServer;Port=$DbPort;Database=vat_inventory;Username=$Username;Password=$Password;Include Error Detail=true;"
$env:ConnectionStrings__ManufacturingDatabase = "Host=$HostServer;Port=$DbPort;Database=vat_manufacturing;Username=$Username;Password=$Password;Include Error Detail=true;"
$env:ConnectionStrings__VatEngineDatabase = "Host=$HostServer;Port=$DbPort;Database=vat_vatengine;Username=$Username;Password=$Password;Include Error Detail=true;"
$env:ConnectionStrings__ComplianceDatabase = "Host=$HostServer;Port=$DbPort;Database=vat_compliance;Username=$Username;Password=$Password;Include Error Detail=true;"
$env:ConnectionStrings__WorkflowDatabase = "Host=$HostServer;Port=$DbPort;Database=vat_workflow;Username=$Username;Password=$Password;Include Error Detail=true;"
$env:ConnectionStrings__ReportingDatabase = "Host=$HostServer;Port=$DbPort;Database=vat_reporting;Username=$Username;Password=$Password;Include Error Detail=true;"
$env:ConnectionStrings__IntegrationDatabase = "Host=$HostServer;Port=$DbPort;Database=vat_integration;Username=$Username;Password=$Password;Include Error Detail=true;"

$services = @(
    @{ Name = "Gateway";       Port = 5080; Project = "src/Vat.Api/Vat.Api.csproj"; Assembly = "src/Vat.Api/bin/$Configuration/net10.0/Vat.Api.dll" },
    @{ Name = "Identity";      Port = 5101; Project = "src/Services/Identity/Vat.Identity.Api/Vat.Identity.Api.csproj"; Assembly = "src/Services/Identity/Vat.Identity.Api/bin/$Configuration/net10.0/Vat.Identity.Api.dll" },
    @{ Name = "Company";       Port = 5102; Project = "src/Services/Company/Vat.Company.Api/Vat.Company.Api.csproj"; Assembly = "src/Services/Company/Vat.Company.Api/bin/$Configuration/net10.0/Vat.Company.Api.dll" },
    @{ Name = "MasterData";    Port = 5103; Project = "src/Services/MasterData/Vat.MasterData.Api/Vat.MasterData.Api.csproj"; Assembly = "src/Services/MasterData/Vat.MasterData.Api/bin/$Configuration/net10.0/Vat.MasterData.Api.dll" },
    @{ Name = "Purchase";      Port = 5104; Project = "src/Services/Purchase/Vat.Purchase.Api/Vat.Purchase.Api.csproj"; Assembly = "src/Services/Purchase/Vat.Purchase.Api/bin/$Configuration/net10.0/Vat.Purchase.Api.dll" },
    @{ Name = "Sales";         Port = 5105; Project = "src/Services/Sales/Vat.Sales.Api/Vat.Sales.Api.csproj"; Assembly = "src/Services/Sales/Vat.Sales.Api/bin/$Configuration/net10.0/Vat.Sales.Api.dll" },
    @{ Name = "Inventory";     Port = 5106; Project = "src/Services/Inventory/Vat.Inventory.Api/Vat.Inventory.Api.csproj"; Assembly = "src/Services/Inventory/Vat.Inventory.Api/bin/$Configuration/net10.0/Vat.Inventory.Api.dll" },
    @{ Name = "Manufacturing"; Port = 5107; Project = "src/Services/Manufacturing/Vat.Manufacturing.Api/Vat.Manufacturing.Api.csproj"; Assembly = "src/Services/Manufacturing/Vat.Manufacturing.Api/bin/$Configuration/net10.0/Vat.Manufacturing.Api.dll" },
    @{ Name = "VatEngine";     Port = 5108; Project = "src/Services/VatEngine/Vat.VatEngine.Api/Vat.VatEngine.Api.csproj"; Assembly = "src/Services/VatEngine/Vat.VatEngine.Api/bin/$Configuration/net10.0/Vat.VatEngine.Api.dll" },
    @{ Name = "Compliance";    Port = 5109; Project = "src/Services/Compliance/Vat.Compliance.Api/Vat.Compliance.Api.csproj"; Assembly = "src/Services/Compliance/Vat.Compliance.Api/bin/$Configuration/net10.0/Vat.Compliance.Api.dll" },
    @{ Name = "Workflow";      Port = 5110; Project = "src/Services/Workflow/Vat.Workflow.Api/Vat.Workflow.Api.csproj"; Assembly = "src/Services/Workflow/Vat.Workflow.Api/bin/$Configuration/net10.0/Vat.Workflow.Api.dll" },
    @{ Name = "Reporting";     Port = 5111; Project = "src/Services/Reporting/Vat.Reporting.Api/Vat.Reporting.Api.csproj"; Assembly = "src/Services/Reporting/Vat.Reporting.Api/bin/$Configuration/net10.0/Vat.Reporting.Api.dll" },
    @{ Name = "Integration";   Port = 5112; Project = "src/Services/Integration/Vat.Integration.Api/Vat.Integration.Api.csproj"; Assembly = "src/Services/Integration/Vat.Integration.Api/bin/$Configuration/net10.0/Vat.Integration.Api.dll" }
)

$runningProcesses = [System.Collections.Generic.List[object]]::new()

try {
    foreach ($svc in $services) {
        $assemblyPath = Join-Path $repoRoot $svc.Assembly
        $workingDir = Split-Path $assemblyPath
        $stdoutLog = Join-Path $logsDir "$($svc.Name).stdout.log"
        $stderrLog = Join-Path $logsDir "$($svc.Name).stderr.log"

        Write-Host ">>> [LAUNCHING] $($svc.Name) on http://127.0.0.1:$($svc.Port) ..." -ForegroundColor Yellow

        $pinfo = New-Object System.Diagnostics.ProcessStartInfo
        $pinfo.FileName = "dotnet"
        $pinfo.Arguments = "`"$assemblyPath`" --urls http://127.0.0.1:$($svc.Port)"
        $pinfo.WorkingDirectory = $workingDir
        $pinfo.RedirectStandardOutput = $true
        $pinfo.RedirectStandardError = $true
        $pinfo.UseShellExecute = $false
        $pinfo.CreateNoWindow = $true

        # Inject environment variables
        $pinfo.EnvironmentVariables["Authentication__Authority"] = $env:Authentication__Authority
        $pinfo.EnvironmentVariables["Authentication__Audience"] = $env:Authentication__Audience
        $pinfo.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"] = $env:ASPNETCORE_ENVIRONMENT
        $pinfo.EnvironmentVariables["ConnectionStrings__VatDatabase"] = $env:ConnectionStrings__VatDatabase
        $pinfo.EnvironmentVariables["ConnectionStrings__IdentityDatabase"] = $env:ConnectionStrings__IdentityDatabase
        $pinfo.EnvironmentVariables["ConnectionStrings__CompanyDatabase"] = $env:ConnectionStrings__CompanyDatabase
        $pinfo.EnvironmentVariables["ConnectionStrings__MasterDataDatabase"] = $env:ConnectionStrings__MasterDataDatabase
        $pinfo.EnvironmentVariables["ConnectionStrings__PurchaseDatabase"] = $env:ConnectionStrings__PurchaseDatabase
        $pinfo.EnvironmentVariables["ConnectionStrings__SalesDatabase"] = $env:ConnectionStrings__SalesDatabase
        $pinfo.EnvironmentVariables["ConnectionStrings__InventoryDatabase"] = $env:ConnectionStrings__InventoryDatabase
        $pinfo.EnvironmentVariables["ConnectionStrings__ManufacturingDatabase"] = $env:ConnectionStrings__ManufacturingDatabase
        $pinfo.EnvironmentVariables["ConnectionStrings__VatEngineDatabase"] = $env:ConnectionStrings__VatEngineDatabase
        $pinfo.EnvironmentVariables["ConnectionStrings__ComplianceDatabase"] = $env:ConnectionStrings__ComplianceDatabase
        $pinfo.EnvironmentVariables["ConnectionStrings__WorkflowDatabase"] = $env:ConnectionStrings__WorkflowDatabase
        $pinfo.EnvironmentVariables["ConnectionStrings__ReportingDatabase"] = $env:ConnectionStrings__ReportingDatabase
        $pinfo.EnvironmentVariables["ConnectionStrings__IntegrationDatabase"] = $env:ConnectionStrings__IntegrationDatabase

        $proc = [System.Diagnostics.Process]::Start($pinfo)
        $runningProcesses.Add([PSCustomObject]@{
            Name = $svc.Name
            Port = $svc.Port
            Process = $proc
            Url = "http://127.0.0.1:$($svc.Port)"
        })
    }

    Write-Host "`nWaiting for services to become healthy..." -ForegroundColor Cyan
    Start-Sleep -Seconds 3

    foreach ($inst in $runningProcesses) {
        $deadline = [DateTime]::UtcNow.AddSeconds(30)
        $ready = $false
        while ([DateTime]::UtcNow -lt $deadline) {
            if ($inst.Process.HasExited) {
                Write-Error "Service $($inst.Name) exited unexpectedly. Check $logsDir"
                break
            }
            try {
                $resp = Invoke-RestMethod -Uri "$($inst.Url)/health/live" -TimeoutSec 2
                if ($resp.status -eq "Healthy") {
                    $ready = $true
                    break
                }
            } catch {
                Start-Sleep -Milliseconds 500
            }
        }
        if ($ready) {
            Write-Host ">>> [HEALTHY] $($inst.Name) is active at $($inst.Url)" -ForegroundColor Green
        } else {
            Write-Warning ">>> [PENDING] $($inst.Name) did not report Healthy in time (may still be initializing)."
        }
    }

    Write-Host "`n==========================================================" -ForegroundColor Green
    Write-Host " ALL BACKEND SERVICES ARE RUNNING! " -ForegroundColor Green
    Write-Host "==========================================================" -ForegroundColor Green
    Write-Host "Gateway:       http://localhost:5080 (Proxied for Angular /api and /health)"
    Write-Host "Swagger UI:    http://localhost:5080/swagger"
    Write-Host "Scalar Docs:   http://localhost:5080/scalar/v1"
    Write-Host "Microservices: Ports 5101 to 5112"
    Write-Host "Logs Location: $logsDir"

    # Automatically launch Swagger UI in the default browser
    try {
        Start-Process "http://localhost:5080/swagger"
    } catch {
        # ignore browser launch errors in headless or background environments
    }

    # Keep alive
    while ($true) {
        Start-Sleep -Seconds 2
        foreach ($inst in $runningProcesses) {
            if ($inst.Process.HasExited) {
                Write-Error "Service $($inst.Name) stopped."
            }
        }
    }
} finally {
    Write-Host "`nStopping background service processes..." -ForegroundColor Yellow
    foreach ($inst in $runningProcesses) {
        if ($inst.Process -and !$inst.Process.HasExited) {
            Stop-Process -InputObject $inst.Process -Force -ErrorAction SilentlyContinue
        }
    }
}

