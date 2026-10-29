param(
    [string[]]$Service,
    [switch]$IncludeFinancialOutbox,
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release'
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Service-Processes.ps1')
$catalogue = @(Get-VatServices)
if ($IncludeFinancialOutbox) { $catalogue += Get-VatFinancialOutboxService }
if ($Service) {
    foreach ($name in $Service) {
        if ($name -notin $catalogue.name) { throw "Unknown service: $name" }
    }
    $catalogue = @($catalogue | Where-Object name -In $Service)
}
foreach ($item in $catalogue) {
    if ($item.name -notmatch '^[A-Za-z][A-Za-z0-9]*$') { throw 'Invalid service name.' }
    $publishRoot = [IO.Path]::GetFullPath((Join-Path $script:ServicesRepoRoot 'artifacts/publish'))
    $destination = Join-Path $publishRoot $item.name
    $stage = Join-Path $publishRoot ('.staging-' + $item.name + '-' + [Guid]::NewGuid().ToString('N'))
    $previous = Join-Path $publishRoot ('.previous-' + $item.name + '-' + [Guid]::NewGuid().ToString('N'))
    # Resolve every directory before moving a package tree. All are generated children of publishRoot.
    $prefix = $publishRoot.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    foreach ($path in @($destination, $stage, $previous)) {
        if (![IO.Path]::GetFullPath($path).StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Package path escaped artifacts/publish.'
        }
    }
    dotnet publish (Join-Path $script:ServicesRepoRoot $item.project) --configuration $Configuration `
        --no-build --no-restore --output $stage /p:UseAppHost=false -m:1 --verbosity minimal
    if ($LASTEXITCODE -ne 0) { throw "Publishing $($item.name) failed." }
    if (!(Test-Path -LiteralPath (Join-Path $stage "Vat.$($item.name).Api.dll"))) {
        throw "Published assembly is missing for $($item.name)."
    }
    if ($item.ownership -eq 'independent') {
        foreach ($forbidden in @('Vat.Hosting', 'Vat.Application', 'Vat.Domain', 'Vat.Infrastructure', 'Vat.Api')) {
            if (Test-Path -LiteralPath (Join-Path $stage ($forbidden + '.dll'))) { throw "Financial bridge dependency leaked into $($item.name): $forbidden" }
        }
    }
    $manifest = [ordered]@{
        service = $item.name
        ownership = $item.ownership
        builtAtUtc = [DateTime]::UtcNow.ToString('o')
        assemblySha256 = (Get-FileHash -LiteralPath (Join-Path $stage "Vat.$($item.name).Api.dll") -Algorithm SHA256).Hash
    }
    $manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $stage 'package.manifest.json')
    $hadPrevious = Test-Path -LiteralPath $destination
    if ($hadPrevious) { Move-Item -LiteralPath $destination -Destination $previous }
    try { Move-Item -LiteralPath $stage -Destination $destination }
    catch {
        if ($hadPrevious -and !(Test-Path -LiteralPath $destination)) { Move-Item -LiteralPath $previous -Destination $destination }
        throw
    }
    Write-Output "$($item.name) package: $destination"
}
