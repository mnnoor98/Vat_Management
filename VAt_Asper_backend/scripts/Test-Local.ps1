param(
    [string]$PostgreSqlBin = 'C:/Program Files/PostgreSQL/18/bin',
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$NoBuild,
    [string]$Filter,
    [string]$Target = 'EnterpriseVat.sln',
    [string]$ResultsDirectory = 'TestResults'
)

$ErrorActionPreference = 'Stop'
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$tempRoot = Join-Path $repoRoot 'tmp'
$runRoot = Join-Path $tempRoot ('postgres-' + [Guid]::NewGuid().ToString('N'))
$dataDir = Join-Path $runRoot 'data'
$passwordFile = Join-Path $runRoot 'password.txt'
$previousConnection = $env:VAT_TEST_POSTGRES
$started = $false
$testExitCode = 1

if (!(Test-Path -LiteralPath (Join-Path $PostgreSqlBin 'initdb.exe'))) {
    throw 'PostgreSQL binaries not found. Pass -PostgreSqlBin or use VAT_TEST_POSTGRES with dotnet test.'
}

New-Item -ItemType Directory -Path $runRoot -Force | Out-Null
$password = [Guid]::NewGuid().ToString('N') + [Guid]::NewGuid().ToString('N')
[System.IO.File]::WriteAllText($passwordFile, $password)
$listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
$listener.Start()
$port = $listener.LocalEndpoint.Port
$listener.Stop()

try {
    Push-Location $repoRoot
    & (Join-Path $PostgreSqlBin 'initdb.exe') -D $dataDir -U vat_test -A scram-sha-256 --pwfile=$passwordFile --encoding=UTF8 --locale=C
    if ($LASTEXITCODE -ne 0) { throw 'Test database initialization failed.' }
    Remove-Item -LiteralPath $passwordFile
    $server = Start-Process -FilePath (Join-Path $PostgreSqlBin 'postgres.exe') -ArgumentList @(
        '-D', "`"$dataDir`"", '-h', '127.0.0.1', '-p', "$port"
    ) -WindowStyle Hidden -RedirectStandardOutput (Join-Path $runRoot 'postgres.stdout.log') -RedirectStandardError (Join-Path $runRoot 'postgres.stderr.log') -PassThru
    $started = $true
    $ready = $false
    for ($attempt = 0; $attempt -lt 60; $attempt++) {
        if ($server.HasExited) { throw 'Test database process exited. Inspect tmp/postgres-*/postgres.stderr.log.' }
        & (Join-Path $PostgreSqlBin 'pg_isready.exe') -h 127.0.0.1 -p $port -U vat_test -d postgres -q
        if ($LASTEXITCODE -eq 0) { $ready = $true; break }
        Start-Sleep -Milliseconds 500
    }
    if (!$ready) { throw 'Test database readiness timed out.' }
    $env:VAT_TEST_POSTGRES = "Host=127.0.0.1;Port=$port;Database=postgres;Username=vat_test;Password=$password;Include Error Detail=false"
    $testArguments = @('test', $Target, '--configuration', $Configuration, '--no-restore', '-m:1', '--logger', 'trx', '--results-directory', $ResultsDirectory)
    if ($NoBuild) { $testArguments += '--no-build' }
    if ($Filter) { $testArguments += @('--filter', $Filter) }
    $testErrorPreference = $ErrorActionPreference
    try {
        # Test failures write native stderr. Preserve the runner's exit code and complete
        # its TRX diagnostics rather than throwing early in Windows PowerShell 5.1.
        $ErrorActionPreference = 'Continue'
        & dotnet @testArguments
        $testExitCode = $LASTEXITCODE
    } finally { $ErrorActionPreference = $testErrorPreference }
}
finally {
    $stopped = !$started
    if ($started) {
        & (Join-Path $PostgreSqlBin 'pg_ctl.exe') -D $dataDir -m fast -w stop
        $stopped = $LASTEXITCODE -eq 0
    }
    $env:VAT_TEST_POSTGRES = $previousConnection
    Pop-Location
    # Delete only this generated directory after PostgreSQL has stopped.
    $resolvedRun = [System.IO.Path]::GetFullPath($runRoot)
    $resolvedTemp = [System.IO.Path]::GetFullPath($tempRoot).TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    if ($stopped -and $resolvedRun.StartsWith($resolvedTemp, [StringComparison]::OrdinalIgnoreCase) -and
        [System.IO.Path]::GetFileName($resolvedRun) -match '^postgres-[0-9a-f]{32}$') {
        Remove-Item -LiteralPath $resolvedRun -Recurse -Force
    }
}
exit $testExitCode
