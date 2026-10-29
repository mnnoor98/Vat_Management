$script:ServicesRepoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))

function Get-VatServices {
    $items = Get-Content -LiteralPath (Join-Path $script:ServicesRepoRoot 'services.json') -Raw | ConvertFrom-Json
    foreach ($item in $items) { Write-Output $item }
}

# Technical source adapter, separate from the twelve business API catalogue entries.
function Get-VatFinancialOutboxService {
    [PSCustomObject]@{
        name = 'FinancialOutbox'; port = 5113; ownership = 'coordinated-source-adapter'
        project = 'src/Services/FinancialOutbox/Vat.FinancialOutbox.Api/Vat.FinancialOutbox.Api.csproj'
        connectionKey = 'VatDatabase'
    }
}

function Start-VatServiceProcess([object]$Service, [int]$ListenPort, [string]$Configuration, [string]$LogDirectory, [switch]$Published) {
    $projectDirectory = Join-Path $script:ServicesRepoRoot (Split-Path $Service.project)
    $assembly = Join-Path $projectDirectory "bin/$Configuration/net10.0/Vat.$($Service.name).Api.dll"
    if ($Published) {
        $projectDirectory = Join-Path $script:ServicesRepoRoot "artifacts/publish/$($Service.name)"
        $assembly = Join-Path $projectDirectory "Vat.$($Service.name).Api.dll"
    }
    if (!(Test-Path -LiteralPath $assembly)) { throw "Build $($Service.name) in $Configuration first." }
    New-Item -ItemType Directory -Path $LogDirectory -Force | Out-Null
    $platformOptions = @{}
    if ($env:OS -eq 'Windows_NT') { $platformOptions['WindowStyle'] = 'Hidden' }
    $saved = @{}
    $ownConnections = @(@($Service.connectionKey) + @($Service.additionalConnectionKeys) | Where-Object { $_ } | ForEach-Object { 'ConnectionStrings__' + $_ })
    foreach ($name in @([Environment]::GetEnvironmentVariables('Process').Keys)) {
        if (($name.StartsWith('ConnectionStrings__', [StringComparison]::OrdinalIgnoreCase) -and $name -notin $ownConnections) -or
            ($name.StartsWith('OutboxServer__', [StringComparison]::OrdinalIgnoreCase) -and $Service.name -ne 'FinancialOutbox') -or
            ($name.StartsWith('OutboxSource__', [StringComparison]::OrdinalIgnoreCase) -and $Service.name -ne 'Integration') -or
            ($name.StartsWith('InboxServer__', [StringComparison]::OrdinalIgnoreCase) -and $Service.name -ne 'Reporting') -or
            ($name.StartsWith('InboxTarget__', [StringComparison]::OrdinalIgnoreCase) -and $Service.name -ne 'Integration') -or
            $name -in @('PGPASSWORD', 'PGPASSFILE', 'VAT_DATABASE_CONNECTION', 'VAT_IDENTITY_DATABASE_CONNECTION', 'VAT_WORKFLOW_DATABASE_CONNECTION', 'VAT_COMPLIANCE_DATABASE_CONNECTION', 'VAT_MANUFACTURING_DATABASE_CONNECTION', 'VAT_INTEGRATION_DATABASE_CONNECTION')) {
            $saved[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
            [Environment]::SetEnvironmentVariable($name, $null, 'Process')
        }
    }
    try {
        $process = Start-Process -FilePath 'dotnet' -ArgumentList @("`"$assembly`"", '--urls', "http://127.0.0.1:$ListenPort") `
            -WorkingDirectory $projectDirectory @platformOptions -PassThru `
            -RedirectStandardOutput (Join-Path $LogDirectory "$($Service.name).stdout.log") `
            -RedirectStandardError (Join-Path $LogDirectory "$($Service.name).stderr.log")
    } finally {
        foreach ($name in $saved.Keys) { [Environment]::SetEnvironmentVariable($name, $saved[$name], 'Process') }
    }
    [PSCustomObject]@{ Service = $Service; Process = $process; Port = $ListenPort; Url = "http://127.0.0.1:$ListenPort" }
}

function Wait-VatService([object]$Instance) {
    $deadline = [DateTime]::UtcNow.AddSeconds(45)
    while ([DateTime]::UtcNow -lt $deadline) {
        if ($Instance.Process.HasExited) { throw "$($Instance.Service.name) exited. Inspect its service logs." }
        try {
            $live = Invoke-RestMethod -Uri "$($Instance.Url)/health/live" -TimeoutSec 2
            if ($live.service -eq $Instance.Service.name -and $live.status -eq 'Healthy') { return }
        } catch { }
        Start-Sleep -Milliseconds 200
    }
    throw "$($Instance.Service.name) did not become live within 45 seconds."
}

function Stop-VatServiceProcess([object]$Instance) {
    # Stop only the child Process object started by this script, never an unrelated port owner.
    if (!$Instance.Process.HasExited) {
        Stop-Process -InputObject $Instance.Process -ErrorAction SilentlyContinue
        $Instance.Process.WaitForExit(10000) | Out-Null
    }
}
