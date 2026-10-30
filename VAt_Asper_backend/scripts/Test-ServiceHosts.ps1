param([ValidateSet('Debug', 'Release')][string]$Configuration = 'Release', [switch]$Published, [switch]$DatabaseReady)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Service-Processes.ps1')
$catalogue = @(Get-VatServices)
$routeOwners = Get-Content -LiteralPath (Join-Path $script:ServicesRepoRoot 'service-routes.json') -Raw | ConvertFrom-Json
$logs = Join-Path $script:ServicesRepoRoot ('artifacts/service-smoke/' + [Guid]::NewGuid().ToString('N'))
$instances = [Collections.Generic.List[object]]::new()
$previousEnvironment = $env:ASPNETCORE_ENVIRONMENT
$previousConnection = $env:ConnectionStrings__VatDatabase
$previousIdentityConnection = $env:ConnectionStrings__IdentityDatabase
$previousWorkflowConnection = $env:ConnectionStrings__WorkflowDatabase
$previousComplianceConnection = $env:ConnectionStrings__ComplianceDatabase
$previousManufacturingConnection = $env:ConnectionStrings__ManufacturingDatabase
$previousIntegrationConnection = $env:ConnectionStrings__IntegrationDatabase
$previousAuthority = $env:Authentication__Authority
$previousAudience = $env:Authentication__Audience
$previousMessaging = $env:Messaging__Enabled
if ($DatabaseReady -and (!$previousConnection -or !$previousIdentityConnection -or !$previousWorkflowConnection -or !$previousComplianceConnection -or !$previousManufacturingConnection -or !$previousIntegrationConnection)) { throw 'Set ConnectionStrings__VatDatabase, ConnectionStrings__IdentityDatabase, ConnectionStrings__WorkflowDatabase, ConnectionStrings__ComplianceDatabase, ConnectionStrings__ManufacturingDatabase and ConnectionStrings__IntegrationDatabase before checking database readiness.' }
try {
    # Default probes do not connect to a database; -DatabaseReady explicitly opts in.
    # These probes never contact an external OIDC issuer.
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:ConnectionStrings__VatDatabase = if ($DatabaseReady) { $previousConnection } else { 'Host=127.0.0.1;Port=1;Database=unused;Username=unused' }
    $env:ConnectionStrings__IdentityDatabase = if ($DatabaseReady) { $previousIdentityConnection } else { 'Host=127.0.0.1;Port=1;Database=unused;Username=unused' }
    $env:ConnectionStrings__WorkflowDatabase = if ($DatabaseReady) { $previousWorkflowConnection } else { 'Host=127.0.0.1;Port=1;Database=unused;Username=unused' }
    $env:ConnectionStrings__ComplianceDatabase = if ($DatabaseReady) { $previousComplianceConnection } else { 'Host=127.0.0.1;Port=1;Database=unused;Username=unused' }
    $env:ConnectionStrings__ManufacturingDatabase = if ($DatabaseReady) { $previousManufacturingConnection } else { 'Host=127.0.0.1;Port=1;Database=unused;Username=unused' }
    $env:ConnectionStrings__IntegrationDatabase = if ($DatabaseReady) { $previousIntegrationConnection } else { 'Host=127.0.0.1;Port=1;Database=unused;Username=unused' }
    $env:Authentication__Authority = 'https://issuer.example.invalid'
    $env:Authentication__Audience = 'vat-api-smoke'
    $env:Messaging__Enabled = 'false'
    foreach ($item in $catalogue) {
        $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
        $listener.Start(); $listenPort = $listener.LocalEndpoint.Port; $listener.Stop()
        $instances.Add((Start-VatServiceProcess $item $listenPort $Configuration $logs -Published:$Published))
    }
    foreach ($instance in $instances) {
        Wait-VatService $instance
        if ($DatabaseReady) {
            $ready = Invoke-RestMethod -Uri "$($instance.Url)/health/ready" -TimeoutSec 30
            if ($ready.status -ne 'Healthy') { throw "$($instance.Service.name) database readiness failed." }
        }
        $document = Invoke-RestMethod -Uri "$($instance.Url)/openapi/v1.json" -TimeoutSec 15
        $actual = @($document.paths.PSObject.Properties.Name | Where-Object { $_ -notlike '/health/*' })
        $expected = @($routeOwners | Where-Object service -EQ $instance.Service.name | Select-Object -ExpandProperty path -Unique)
        if (!$actual.Count -or @(Compare-Object ($actual | Sort-Object) ($expected | Sort-Object)).Count -ne 0) {
            throw "Route ownership mismatch in $($instance.Service.name). Inspect its OpenAPI document."
        }
        $protected = @($expected | Where-Object { $_ -notlike '*dev-token*' })[0]
        $probePath = $protected -replace '\{[^}]+\}', '00000000-0000-0000-0000-000000000001'
        try {
            $response = Invoke-WebRequest -Uri ($instance.Url + $probePath) -UseBasicParsing -TimeoutSec 10
            throw "Protected endpoint returned $($response.StatusCode) without authentication."
        } catch {
            if (!$_.Exception.Response -or [int]$_.Exception.Response.StatusCode -ne 401) { throw }
        }
        $databaseResult = if ($DatabaseReady) { ', database ready' } else { '' }
        Write-Output "$($instance.Service.name): live, owned routes only, authentication enforced$databaseResult"
    }
    $purchase = $instances | Where-Object { $_.Service.name -eq 'Purchase' }
    $sales = $instances | Where-Object { $_.Service.name -eq 'Sales' }
    $salesId = $sales.Process.Id
    Stop-VatServiceProcess $purchase
    Wait-VatService $sales
    $restarted = Start-VatServiceProcess $purchase.Service $purchase.Port $Configuration (Join-Path $logs 'restart') -Published:$Published
    $instances.Add($restarted)
    Wait-VatService $restarted
    if ($sales.Process.HasExited -or $sales.Process.Id -ne $salesId) { throw 'Restart isolation failed.' }
    Write-Output 'PASS: all 12 API processes are isolated; Purchase restarted while Sales stayed live.'
} finally {
    foreach ($instance in $instances) { Stop-VatServiceProcess $instance }
    $env:ASPNETCORE_ENVIRONMENT = $previousEnvironment
    $env:ConnectionStrings__VatDatabase = $previousConnection
    $env:ConnectionStrings__IdentityDatabase = $previousIdentityConnection
    $env:ConnectionStrings__WorkflowDatabase = $previousWorkflowConnection
    $env:ConnectionStrings__ComplianceDatabase = $previousComplianceConnection
    $env:ConnectionStrings__ManufacturingDatabase = $previousManufacturingConnection
    $env:ConnectionStrings__IntegrationDatabase = $previousIntegrationConnection
    $env:Authentication__Authority = $previousAuthority
    $env:Authentication__Audience = $previousAudience
    $env:Messaging__Enabled = $previousMessaging
}
