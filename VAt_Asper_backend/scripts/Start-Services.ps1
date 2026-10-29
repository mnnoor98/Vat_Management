param(
    [string[]]$Service,
    [switch]$IncludeFinancialOutbox,
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [ValidateSet('Development', 'Production')][string]$Environment = 'Development',
    [switch]$Published
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Service-Processes.ps1')
if ($env:OS -eq 'Windows_NT' -and (Test-Path -LiteralPath (Join-Path $script:ServicesRepoRoot 'artifacts/local-secrets/databases.json'))) {
    . (Join-Path $PSScriptRoot 'Import-LocalConnections.ps1')
}
if (!$env:Authentication__Authority) { throw 'Set Authentication__Authority in this process before starting services.' }
$catalogue = @(Get-VatServices)
if ($IncludeFinancialOutbox) { $catalogue += Get-VatFinancialOutboxService }
if ($Service) {
    foreach ($name in $Service) {
        if ($name -notin $catalogue.name) { throw "Unknown service: $name" }
    }
    $catalogue = @($catalogue | Where-Object name -In $Service)
}
foreach ($item in $catalogue) {
    foreach ($key in @(@($item.connectionKey) + @($item.additionalConnectionKeys) | Where-Object { $_ })) {
        $setting = 'ConnectionStrings__' + $key
        if (![Environment]::GetEnvironmentVariable($setting)) { throw "Set $setting before starting $($item.name)." }
    }
}
$logs = Join-Path $script:ServicesRepoRoot ('artifacts/services/' + [Guid]::NewGuid().ToString('N'))
$previousEnvironment = $env:ASPNETCORE_ENVIRONMENT
$instances = [Collections.Generic.List[object]]::new()
try {
    $env:ASPNETCORE_ENVIRONMENT = $Environment
    foreach ($item in $catalogue) {
        $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, [int]$item.port)
        try { $listener.Start() } finally { $listener.Stop() }
        $instances.Add((Start-VatServiceProcess $item $item.port $Configuration $logs -Published:$Published))
    }
    foreach ($instance in $instances) {
        Wait-VatService $instance
        Write-Output "$($instance.Service.name): $($instance.Url)"
    }
    Write-Output "Logs: $logs"
    Write-Output 'Services are running. Press Ctrl+C to stop the processes started by this script.'
    while ($true) {
        foreach ($instance in $instances) {
            if ($instance.Process.HasExited) { throw "$($instance.Service.name) stopped unexpectedly. Inspect $logs" }
        }
        Start-Sleep -Seconds 1
    }
} finally {
    foreach ($instance in $instances) { Stop-VatServiceProcess $instance }
    $env:ASPNETCORE_ENVIRONMENT = $previousEnvironment
}
