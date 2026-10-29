param(
    [string]$PostgreSqlBin = 'C:/Program Files/PostgreSQL/18/bin',
    [string]$Server = '127.0.0.1',
    [int]$Port = 5432,
    [string]$User = 'postgres'
)
$ErrorActionPreference = 'Stop'
if ($env:OS -ne 'Windows_NT') { throw 'This local credential helper uses Windows DPAPI. Use deployment secrets on other operating systems.' }
if (!$env:PGPASSWORD) { throw 'Set PGPASSWORD for the local database administrator in this process.' }
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$secretRoot = Join-Path $repoRoot 'artifacts/local-secrets'
New-Item -ItemType Directory -Path $secretRoot -Force | Out-Null
$metadataPath = Join-Path $secretRoot 'databases.json'
if (!(Test-Path -LiteralPath $metadataPath)) { throw 'Initialize the existing Identity/financial local credential profile first.' }
$metadata = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json
if ($metadata.Server -ne $Server -or [int]$metadata.Port -ne $Port) {
    throw 'Manufacturing endpoint must match the existing local credential profile.'
}
$pgArgs = @('-h', $Server, '-p', "$Port", '-U', $User, '-X', '-q', '-A', '-t', '-v', 'ON_ERROR_STOP=1')
function Invoke-AdminSql([string]$Database, [string]$Sql) {
    $output = $Sql | & (Join-Path $PostgreSqlBin 'psql.exe') @pgArgs -d $Database
    if ($LASTEXITCODE -ne 0) { throw "Database role configuration failed in $Database." }
    return $output
}
foreach ($role in @('vat_manufacturing_owner', 'vat_manufacturing_app')) {
    $path = Join-Path $secretRoot ($role + '.credential.xml')
    $exists = Invoke-AdminSql 'postgres' "SELECT count(*) FROM pg_roles WHERE rolname = '$role';"
    if (!(Test-Path -LiteralPath $path)) {
        if ([int]$exists -ne 0) { throw "Role $role exists without this user's stored credentials. Refusing automatic password rotation." }
        $bytes = New-Object byte[] 32
        $random = [Security.Cryptography.RandomNumberGenerator]::Create()
        try { $random.GetBytes($bytes) } finally { $random.Dispose() }
        $password = ([BitConverter]::ToString($bytes)).Replace('-', '').ToLowerInvariant()
        $secure = ConvertTo-SecureString $password -AsPlainText -Force
        $credential = New-Object System.Management.Automation.PSCredential($role, $secure)
        $credential | Export-Clixml -LiteralPath $path
    } else { $credential = Import-Clixml -LiteralPath $path }
    if ([int]$exists -eq 0) {
        $password = $credential.GetNetworkCredential().Password
        if ($password -notmatch '^[a-f0-9]{64}$') { throw 'Stored generated credential has an unexpected format.' }
        Invoke-AdminSql 'postgres' "CREATE ROLE $role LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS PASSWORD '$password';" | Out-Null
    }
    $privileged = Invoke-AdminSql 'postgres' "SELECT count(*) FROM pg_roles WHERE rolname = '$role' AND (rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication OR rolbypassrls);"
    if ([int]$privileged -ne 0) { throw "Role $role has unexpected administrative privileges." }
    $password = $null
}

# Only the newly owned Manufacturing database has public access removed. Existing source
# application users and their privileges are left intact.
Invoke-AdminSql 'vat_manufacturing' @'
BEGIN;
SELECT 1 FROM manufacturing."__ManufacturingMigrationsHistory" LIMIT 1;
ALTER DATABASE vat_manufacturing OWNER TO vat_manufacturing_owner;
REVOKE ALL ON DATABASE vat_manufacturing FROM PUBLIC;
GRANT CONNECT ON DATABASE vat_manufacturing TO vat_manufacturing_owner, vat_manufacturing_app;
ALTER SCHEMA manufacturing OWNER TO vat_manufacturing_owner;
REVOKE ALL ON SCHEMA manufacturing FROM PUBLIC;
REVOKE CREATE ON SCHEMA public FROM PUBLIC;
DO $$ DECLARE r record; BEGIN
  FOR r IN SELECT tablename FROM pg_tables WHERE schemaname = 'manufacturing' LOOP
    EXECUTE format('ALTER TABLE manufacturing.%I OWNER TO vat_manufacturing_owner', r.tablename);
  END LOOP;
END $$;
GRANT USAGE ON SCHEMA manufacturing TO vat_manufacturing_app;
GRANT SELECT ON manufacturing.production_orders, manufacturing."__ManufacturingMigrationsHistory" TO vat_manufacturing_app;
GRANT SELECT, INSERT ON manufacturing.bom_headers, manufacturing.bom_lines,
  manufacturing.coefficient_documents, manufacturing.coefficient_commands, manufacturing.coefficient_audit TO vat_manufacturing_app;
GRANT SELECT, INSERT, UPDATE ON manufacturing.coefficient_versions TO vat_manufacturing_app;
REVOKE UPDATE, DELETE, TRUNCATE ON manufacturing.coefficient_documents, manufacturing.coefficient_commands, manufacturing.coefficient_audit FROM vat_manufacturing_app;
COMMIT;
'@ | Out-Null

Invoke-AdminSql 'vat_management' @'
REVOKE ALL ON SCHEMA manufacturing FROM vat_financial_app;
REVOKE ALL ON ALL TABLES IN SCHEMA manufacturing FROM vat_financial_app;
'@ | Out-Null
$metadata | Add-Member -NotePropertyName ManufacturingDatabase -NotePropertyValue 'vat_manufacturing' -Force
$metadata | ConvertTo-Json | Set-Content -LiteralPath $metadataPath
Write-Output 'Manufacturing owner and restricted workflow runtime roles configured; encrypted credentials stored locally.'
