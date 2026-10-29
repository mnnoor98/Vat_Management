param([switch]$Published, [switch]$DatabaseReady)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Service-Processes.ps1')
if ($DatabaseReady -and !$env:ConnectionStrings__VatDatabase) { throw 'Import the financial runtime connection before testing readiness.' }
$saved = @{}
foreach ($name in @('ASPNETCORE_ENVIRONMENT', 'ConnectionStrings__VatDatabase', 'Authentication__Authority', 'Authentication__Audience', 'Messaging__Enabled', 'InboxServer__Enabled', 'InboxServer__ServiceKey', 'InboxServer__AllowLoopbackHttp')) {
    $saved[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}
$instance = $null
$rng = [Security.Cryptography.RandomNumberGenerator]::Create()
$bytes = New-Object byte[] 32
$rng.GetBytes($bytes); $rng.Dispose()
$key = ([BitConverter]::ToString($bytes)).Replace('-', '')
$listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
$listener.Start(); $listenPort = $listener.LocalEndpoint.Port; $listener.Stop()
function Get-InboxProbeStatus([string]$Uri, [hashtable]$Headers, [string]$Body) {
    try { return [int](Invoke-WebRequest -Uri $Uri -Method Post -Headers $Headers -Body $Body -ContentType 'application/json' -UseBasicParsing -TimeoutSec 5).StatusCode }
    catch { if ($_.Exception.Response) { return [int]$_.Exception.Response.StatusCode }; throw }
}
try {
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    if (!$DatabaseReady) { $env:ConnectionStrings__VatDatabase = 'Host=127.0.0.1;Port=1;Database=unused;Username=unused' }
    $env:Authentication__Authority = 'https://probe.invalid'
    $env:Authentication__Audience = 'vat-api'
    $env:Messaging__Enabled = 'false'
    $env:InboxServer__Enabled = 'true'
    $env:InboxServer__ServiceKey = $key
    $env:InboxServer__AllowLoopbackHttp = 'true'
    $logs = Join-Path $script:ServicesRepoRoot ('artifacts/inbox-host-smoke/' + [Guid]::NewGuid().ToString('N'))
    $service = Get-VatServices | Where-Object name -EQ 'Reporting'
    $instance = Start-VatServiceProcess $service $listenPort 'Release' $logs -Published:$Published
    Wait-VatService $instance
    if ($DatabaseReady) { $null = Invoke-RestMethod -Uri ($instance.Url + '/health/ready') -TimeoutSec 30 }
    $schema = Invoke-RestMethod -Uri ($instance.Url + '/openapi/v1.json') -TimeoutSec 10
    $path = '/internal/v1/inbox/reporting-v1/deliver'
    if ($path -notin $schema.paths.PSObject.Properties.Name) { throw 'Enabled inbox delivery route is missing.' }
    $uri = $instance.Url + $path
    if ((Get-InboxProbeStatus $uri @{} '{}') -ne 401) { throw 'Anonymous delivery was not rejected.' }
    if ((Get-InboxProbeStatus $uri @{ 'X-Outbox-Service-Key' = $key } '{}') -ne 401) { throw 'Outbox capability was accepted by inbox.' }
    # An invalid protocol verifies routing without persisting an inbox message or quarantine.
    $body = '{"contractVersion":2,"consumerOwner":"reporting-v1","messageId":null,"eventType":null,"contentType":null,"originalBodyLength":0,"bodySha256":"","bodyPrefix":"","prefixSha256":""}'
    if ((Get-InboxProbeStatus $uri @{ 'X-Inbox-Service-Key' = $key } $body) -ne 400) { throw 'Service credential or invalid-contract rejection failed.' }
    Write-Output 'PASS: Reporting inbox published host, dedicated service authentication and invalid-contract rejection without data writes.'
    if ($DatabaseReady) { Write-Output 'PASS: Reporting inbox database readiness with runtime credentials.' }
} finally {
    if ($instance) { Stop-VatServiceProcess $instance }
    foreach ($name in $saved.Keys) { [Environment]::SetEnvironmentVariable($name, $saved[$name], 'Process') }
    $key = $null; [Array]::Clear($bytes, 0, $bytes.Length)
}
