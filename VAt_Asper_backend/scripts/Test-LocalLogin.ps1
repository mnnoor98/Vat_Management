param([string]$Configuration = 'Release', [int]$Port = 5188)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Net.Http
$backendRoot = Split-Path $PSScriptRoot
$assembly = Join-Path $backendRoot "src/Vat.Api/bin/$Configuration/net10.0/Vat.Api.dll"
if (!(Test-Path -LiteralPath $assembly)) { throw 'Build Vat.Api before running this check.' }
$localDirectory = Join-Path $backendRoot '.local'
New-Item -ItemType Directory -Path $localDirectory -Force | Out-Null
$savedEnvironment = @{}
$environmentKeys = @('Authentication__Authority', 'Authentication__Audience', 'ASPNETCORE_ENVIRONMENT', 'Kestrel__Endpoints__Http__Url')
$environmentKeys += @('Vat', 'Identity', 'Manufacturing', 'Compliance', 'Workflow', 'Integration') | ForEach-Object { "ConnectionStrings__$($_)Database" }
foreach ($key in $environmentKeys) { $savedEnvironment[$key] = [Environment]::GetEnvironmentVariable($key, 'Process') }
$env:Authentication__Authority = 'https://localhost:5080'
$env:Authentication__Audience = 'vat-api'
foreach ($owner in @('Vat', 'Identity', 'Manufacturing', 'Compliance', 'Workflow', 'Integration')) {
    [Environment]::SetEnvironmentVariable("ConnectionStrings__$($owner)Database", 'Host=127.0.0.1;Port=1;Database=login_probe;Username=probe;Password=unused', 'Process')
}
$client = New-Object System.Net.Http.HttpClient
$client.Timeout = [TimeSpan]::FromSeconds(5)
$base = "http://127.0.0.1:$Port"
$env:Kestrel__Endpoints__Http__Url = $base
function Send-Login([string]$Username, [string]$Password) {
    $json = @{ username = $Username; password = $Password } | ConvertTo-Json -Compress
    $content = New-Object System.Net.Http.StringContent($json, [Text.Encoding]::UTF8, 'application/json')
    try { return $client.PostAsync("$base/api/v1/auth/login", $content).GetAwaiter().GetResult() }
    finally { $content.Dispose() }
}
try {
    foreach ($mode in @('Development', 'Production')) {
        $env:ASPNETCORE_ENVIRONMENT = $mode
        $probeProcess = Start-Process dotnet -ArgumentList @($assembly, '--urls', $base) -WorkingDirectory (Join-Path $backendRoot 'src/Vat.Api') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $localDirectory "login-$mode.stdout.log") -RedirectStandardError (Join-Path $localDirectory "login-$mode.stderr.log")
        try {
            $ready = $false
            for ($attempt = 0; $attempt -lt 60; $attempt++) {
                if ($probeProcess.HasExited) { throw "Probe host exited in $mode. See .local logs." }
                try {
                    $health = $client.GetAsync("$base/health/live").GetAwaiter().GetResult()
                    $ready = $health.IsSuccessStatusCode
                    $health.Dispose()
                    if ($ready) { break }
                } catch { }
                Start-Sleep -Milliseconds 250
            }
            if (!$ready) { throw 'Probe host did not become live.' }
            if ($mode -eq 'Production') {
                $response = Send-Login 'admin' 'Admin@123456'
                if ([int]$response.StatusCode -ne 404) { throw 'Local password login must be unavailable in Production.' }
                $response.Dispose()
                Write-Host 'PASS: Production rejects local password login.'
                continue
            }
            foreach ($account in @(@('admin', 'Admin@123456'), @('maker', 'Maker@123456'), @('checker', 'Checker@123456'), @('auditor', 'Auditor@123456'))) {
                $response = Send-Login $account[0] $account[1]
                if ([int]$response.StatusCode -ne 200) { throw "Login failed for $($account[0]): $($response.StatusCode)" }
                $session = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json
                $response.Dispose()
                $client.DefaultRequestHeaders.Authorization = New-Object System.Net.Http.Headers.AuthenticationHeaderValue('Bearer', $session.accessToken)
                $profileResponse = $client.GetAsync("$base/api/v1/identity/me").GetAwaiter().GetResult()
                if ([int]$profileResponse.StatusCode -ne 200) { throw "Token validation failed for $($account[0])." }
                $profile = $profileResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json
                $profileResponse.Dispose()
                if ($profile.subject -ne $session.user.id -or @($profile.permissions).Count -eq 0) { throw 'Verified profile does not match the issued identity.' }
                $client.DefaultRequestHeaders.Authorization = $null
                $wrong = Send-Login $account[0] 'wrong-password'
                if ([int]$wrong.StatusCode -ne 401) { throw 'Wrong password was accepted.' }
                $wrong.Dispose()
                Write-Host "PASS: $($account[0]) login, signed-token profile and wrong-password rejection."
            }
            $client.DefaultRequestHeaders.Authorization = New-Object System.Net.Http.Headers.AuthenticationHeaderValue('Bearer', 'demo_token_admin')
            $fake = $client.GetAsync("$base/api/v1/identity/me").GetAwaiter().GetResult()
            if ([int]$fake.StatusCode -ne 401) { throw 'Fake token was accepted.' }
            $fake.Dispose()
            $client.DefaultRequestHeaders.Authorization = $null
            Write-Host 'PASS: Fake demo token is rejected.'
        } finally {
            if (!$probeProcess.HasExited) { $probeProcess.Kill(); $probeProcess.WaitForExit() }
            $probeProcess.Dispose()
        }
    }
} finally {
    $client.Dispose()
    foreach ($key in $environmentKeys) { [Environment]::SetEnvironmentVariable($key, $savedEnvironment[$key], 'Process') }
}
