param([switch]$Published, [switch]$DatabaseReady)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Service-Processes.ps1')
if ($DatabaseReady -and !$env:ConnectionStrings__VatDatabase) { throw 'Import the financial runtime connection before testing readiness.' }
$saved = @{}
foreach ($name in @('ASPNETCORE_ENVIRONMENT', 'ConnectionStrings__VatDatabase', 'Authentication__Authority', 'Authentication__Audience', 'Messaging__Enabled', 'OutboxServer__ServiceKey', 'OutboxServer__AllowLoopbackHttp')) {
    $saved[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}
$instance = $null
$rng = [Security.Cryptography.RandomNumberGenerator]::Create()
$bytes = New-Object byte[] 32
$rng.GetBytes($bytes); $rng.Dispose()
$key = ([BitConverter]::ToString($bytes)).Replace('-', '')
$listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
$listener.Start(); $listenPort = $listener.LocalEndpoint.Port; $listener.Stop()
function Get-ProbeStatus([string]$Uri, [hashtable]$Headers, [string]$Body) {
    try { return [int](Invoke-WebRequest -Uri $Uri -Method Post -Headers $Headers -Body $Body -ContentType 'application/json' -UseBasicParsing -TimeoutSec 5).StatusCode }
    catch { if ($_.Exception.Response) { return [int]$_.Exception.Response.StatusCode }; throw }
}
try {
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    if (!$DatabaseReady) { $env:ConnectionStrings__VatDatabase = 'Host=127.0.0.1;Port=1;Database=unused;Username=unused' }
    $env:Authentication__Authority = 'https://probe.invalid'
    $env:Authentication__Audience = 'vat-api'
    $env:Messaging__Enabled = 'false'
    $env:OutboxServer__ServiceKey = $key
    $env:OutboxServer__AllowLoopbackHttp = 'true'
    $logs = Join-Path $script:ServicesRepoRoot ('artifacts/outbox-host-smoke/' + [Guid]::NewGuid().ToString('N'))
    $instance = Start-VatServiceProcess (Get-VatFinancialOutboxService) $listenPort 'Release' $logs -Published:$Published
    Wait-VatService $instance
    if ($DatabaseReady) { $null = Invoke-RestMethod -Uri ($instance.Url + '/health/ready') -TimeoutSec 30 }
    $schema = Invoke-RestMethod -Uri ($instance.Url + '/openapi/v1.json') -TimeoutSec 10
    if (@($schema.paths.PSObject.Properties.Name | Where-Object { $_ -like '/api/*' }).Count -ne 0) { throw 'Business controllers leaked into the technical host.' }
    if (@($schema.paths.PSObject.Properties.Name | Where-Object { $_ -like '/internal/v1/message-operations/*' }).Count -ne 4) { throw 'Message operator contract routes are missing.' }
    $operatorUri = $instance.Url + '/internal/v1/message-operations/companies/00000000-0000-0000-0000-000000000001/outbox'
    try {
        Invoke-WebRequest -Uri $operatorUri -Headers @{ 'X-Outbox-Service-Key' = $key } -UseBasicParsing -TimeoutSec 5 | Out-Null
        throw 'Worker credentials unexpectedly authorized operator access.'
    } catch {
        if (!$_.Exception.Response -or [int]$_.Exception.Response.StatusCode -ne 401) { throw }
    }
    $prefix = $instance.Url + '/internal/v1/outbox/financial-coordinated'
    if (@($schema.paths.PSObject.Properties.Name | Where-Object { $_ -like '/internal/v1/reporting-source/*' }).Count -ne 3) { throw 'Reporting source V1 routes are missing.' }
    $exportUri = $instance.Url + '/internal/v1/reporting-source/companies/00000000-0000-0000-0000-000000000001/exports'
    if ((Get-ProbeStatus $exportUri @{ 'X-Outbox-Service-Key' = $key } '{"reason":"Probe"}') -ne 401) { throw 'Worker credentials authorized reporting export preparation.' }
    if ((Get-ProbeStatus ($prefix + '/claim') @{} '') -ne 401) { throw 'Anonymous claim was not rejected.' }
    if ((Get-ProbeStatus ($prefix + '/claim') @{ Authorization = 'Bearer user-token' } '') -ne 401) { throw 'User bearer token was not rejected.' }
    # A deliberately invalid receipt verifies authenticated routing without claiming or changing stored events.
    $body = '{"contractVersion":2,"sourceOwner":"financial-coordinated","messageId":"00000000-0000-0000-0000-000000000000","tenantId":"00000000-0000-0000-0000-000000000000","companyId":"00000000-0000-0000-0000-000000000000","leaseToken":"00000000-0000-0000-0000-000000000000"}'
    if ((Get-ProbeStatus ($prefix + '/confirm') @{ 'X-Outbox-Service-Key' = $key } $body) -ne 400) { throw 'Service credential or invalid-contract rejection failed.' }
    Write-Output 'PASS: FinancialOutbox published host, service authentication, bounded contract routing and business-controller isolation.'
    if ($DatabaseReady) { Write-Output 'PASS: FinancialOutbox database readiness with runtime credentials.' }
} finally {
    if ($instance) { Stop-VatServiceProcess $instance }
    foreach ($name in $saved.Keys) { [Environment]::SetEnvironmentVariable($name, $saved[$name], 'Process') }
    $key = $null; [Array]::Clear($bytes, 0, $bytes.Length)
}
