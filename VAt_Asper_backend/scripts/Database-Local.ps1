param(
    [ValidateSet('Status', 'Backup', 'Migrate', 'Verify')][string]$Action = 'Status',
    [string]$PostgreSqlBin = 'C:/Program Files/PostgreSQL/18/bin',
    [string]$Database = 'vat_management',
    [string]$Server = '127.0.0.1',
    [int]$Port = 5432,
    [string]$User = 'postgres',
    [switch]$VerifyRestore
)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ($Database -in @('vat_identity', 'vat_workflow', 'vat_compliance', 'vat_manufacturing', 'vat_integration') -and $Action -in @('Migrate', 'Verify')) {
    throw 'Owned databases have separate migration chains. Use Update-IdentityDatabase.ps1, Update-WorkflowDatabase.ps1, Update-ComplianceDatabase.ps1, Update-ManufacturingDatabase.ps1 or Update-IntegrationDatabase.ps1.'
}
if (!$env:PGPASSWORD) { throw 'Set PGPASSWORD in this process. Never put a password in source control.' }
$pgArgs = @('-h', $Server, '-p', "$Port", '-U', $User)
function Invoke-Pg([string]$Tool, [string[]]$Arguments) {
    & (Join-Path $PostgreSqlBin ($Tool + '.exe')) @pgArgs @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Tool failed (exit $LASTEXITCODE)." }
}
function Invoke-PgSql([string]$Name, [string]$Sql) {
    # stdin preserves quoted identifiers on Windows PowerShell 5.1.
    $Sql | & (Join-Path $PostgreSqlBin 'psql.exe') @pgArgs -d $Name -X -v ON_ERROR_STOP=1
    if ($LASTEXITCODE -ne 0) { throw "SQL verification failed (exit $LASTEXITCODE)." }
}
function Inspect-Database([string]$Name) {
    Invoke-PgSql $Name @'
SELECT format('SELECT %L AS history, "MigrationId" FROM %I.%I ORDER BY "MigrationId"', table_schema || '.' || table_name, table_schema, table_name)
FROM information_schema.tables WHERE table_name IN ('__EFMigrationsHistory','__IdentityMigrationsHistory','__WorkflowMigrationsHistory','__ComplianceMigrationsHistory','__ManufacturingMigrationsHistory','__IntegrationMigrationsHistory')
\gexec
SELECT table_schema, count(*) AS tables FROM information_schema.tables
WHERE table_type = 'BASE TABLE' AND table_schema NOT IN ('pg_catalog','information_schema')
GROUP BY table_schema ORDER BY table_schema;
SELECT connamespace::regnamespace AS schema, conname FROM pg_constraint
WHERE NOT convalidated AND connamespace NOT IN ('pg_catalog'::regnamespace,'information_schema'::regnamespace);
'@
}
switch ($Action) {
    'Status' { Inspect-Database $Database }
    'Backup' {
        $directory = Join-Path $repoRoot 'artifacts/backups'
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
        $backup = Join-Path $directory ('vat-' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ') + '-' + [Guid]::NewGuid().ToString('N') + '.dump')
        Invoke-Pg 'pg_dump' @('-d', $Database, '--format=custom', '--file', $backup)
        (Get-FileHash -LiteralPath $backup -Algorithm SHA256).Hash | Set-Content -LiteralPath ($backup + '.sha256')
        Write-Output "Backup: $backup"
        if ($VerifyRestore) {
            $rehearsal = 'vat_restore_' + [Guid]::NewGuid().ToString('N')
            $created = $false
            try {
                Invoke-Pg 'createdb' @($rehearsal)
                $created = $true
                Invoke-Pg 'pg_restore' @('-d', $rehearsal, '--exit-on-error', '--no-owner', '--no-privileges', $backup)
                Inspect-Database $rehearsal
                Write-Output 'Backup restore rehearsal passed.'
            } finally {
                if ($created -and $rehearsal -match '^vat_restore_[0-9a-f]{32}$') {
                    Invoke-Pg 'dropdb' @($rehearsal)
                }
            }
        }
    }
    'Migrate' {
        $previous = $env:ConnectionStrings__VatDatabase
        $connection = [System.Data.Common.DbConnectionStringBuilder]::new()
        $connection['Host'] = $Server; $connection['Port'] = $Port
        $connection['Database'] = $Database; $connection['Username'] = $User
        $connection['Password'] = $env:PGPASSWORD
        try {
            $env:ConnectionStrings__VatDatabase = $connection.ConnectionString
            Push-Location $repoRoot
            try {
                dotnet ef database update --project src/Vat.Infrastructure --startup-project src/Vat.Api --context VatDbContext --configuration Release --no-build
                if ($LASTEXITCODE -ne 0) { throw 'EF database update failed.' }
            } finally { Pop-Location }
        } finally { $env:ConnectionStrings__VatDatabase = $previous }
    }
    'Verify' {
        Inspect-Database $Database
        $migrations = Get-ChildItem -LiteralPath (Join-Path $repoRoot 'src/Vat.Infrastructure/Persistence/Migrations') -File |
            Where-Object { $_.Name -match '^\d{14}_[A-Za-z0-9_]+\.cs$' } |
            ForEach-Object { $_.BaseName }
        if (!$migrations) { throw 'No source migrations found.' }
        # Migration names are constrained by the filename pattern above, not SQL input.
        $expected = ($migrations | ForEach-Object { "('$_')" }) -join ','
        Invoke-PgSql $Database @"
DO `$`$
BEGIN
 IF EXISTS (SELECT column1 FROM (VALUES $expected) AS expected
            EXCEPT SELECT "MigrationId" FROM "__EFMigrationsHistory") THEN
   RAISE EXCEPTION 'Database has pending source migrations';
 END IF;
 IF EXISTS (SELECT "MigrationId" FROM "__EFMigrationsHistory"
            EXCEPT SELECT column1 FROM (VALUES $expected) AS expected) THEN
   RAISE EXCEPTION 'Database contains migrations unknown to this checkout';
 END IF;
END `$`$;
"@
        Invoke-PgSql $Database @'
DO $$
BEGIN
 IF EXISTS (SELECT 1 FROM pg_constraint WHERE NOT convalidated AND connamespace NOT IN ('pg_catalog'::regnamespace,'information_schema'::regnamespace)) THEN
   RAISE EXCEPTION 'Unvalidated constraints found';
 END IF;
 IF EXISTS (SELECT 1 FROM pg_index i JOIN pg_class c ON c.oid = i.indrelid JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE NOT i.indisvalid AND n.nspname NOT IN ('pg_catalog','information_schema')) THEN
   RAISE EXCEPTION 'Invalid indexes found';
 END IF;
END $$;
'@
    }
}
