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
$pgArgs = @('-h', $Server, '-p', "$Port", '-U', $User, '-X', '-q', '-A', '-t', '-v', 'ON_ERROR_STOP=1')
function Invoke-AdminSql([string]$Database, [string]$Sql) {
    $output = $Sql | & (Join-Path $PostgreSqlBin 'psql.exe') @pgArgs -d $Database
    if ($LASTEXITCODE -ne 0) { throw "Database role configuration failed in $Database." }
    return $output
}
foreach ($role in @('vat_identity_owner', 'vat_identity_app', 'vat_financial_app')) {
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

# Only the newly owned Identity database has public access removed. Existing source
# application users and their privileges are left intact.
Invoke-AdminSql 'vat_identity' @'
BEGIN;
SELECT 1 FROM identity."__IdentityMigrationsHistory" LIMIT 1;
ALTER DATABASE vat_identity OWNER TO vat_identity_owner;
REVOKE ALL ON DATABASE vat_identity FROM PUBLIC;
GRANT CONNECT ON DATABASE vat_identity TO vat_identity_owner, vat_identity_app;
ALTER SCHEMA identity OWNER TO vat_identity_owner;
REVOKE ALL ON SCHEMA identity FROM PUBLIC;
REVOKE CREATE ON SCHEMA public FROM PUBLIC;
DO $$ DECLARE r record; BEGIN
  FOR r IN SELECT tablename FROM pg_tables WHERE schemaname = 'identity' LOOP
    EXECUTE format('ALTER TABLE identity.%I OWNER TO vat_identity_owner', r.tablename);
  END LOOP;
END $$;
GRANT USAGE ON SCHEMA identity TO vat_identity_app;
GRANT SELECT ON identity.roles, identity."__IdentityMigrationsHistory" TO vat_identity_app;
COMMIT;
'@ | Out-Null

$schemas = 'company, masterdata, inventory, purchasing, tax, sales, reporting, platform'
Invoke-AdminSql 'vat_management' @"
BEGIN;
GRANT CONNECT ON DATABASE vat_management TO vat_financial_app;
GRANT USAGE ON SCHEMA $schemas TO vat_financial_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA $schemas TO vat_financial_app;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA $schemas TO vat_financial_app;
GRANT USAGE ON SCHEMA public TO vat_financial_app;
GRANT SELECT ON public."__EFMigrationsHistory" TO vat_financial_app;
DO `$`$ BEGIN
  IF to_regclass('inventory.transfer_journal') IS NOT NULL THEN
    REVOKE UPDATE, DELETE, TRUNCATE ON inventory.transfer_journal FROM vat_financial_app;
    GRANT SELECT, INSERT ON inventory.transfer_journal TO vat_financial_app;
  END IF;
    IF to_regclass('platform.reporting_source_exports') IS NOT NULL THEN
      REVOKE UPDATE, DELETE, TRUNCATE ON platform.reporting_source_exports FROM vat_financial_app;
      GRANT SELECT, INSERT ON platform.reporting_source_exports TO vat_financial_app;
    END IF;
    IF to_regclass('platform.reporting_source_journal') IS NOT NULL THEN
      REVOKE UPDATE, DELETE, TRUNCATE ON platform.reporting_source_journal, platform.reporting_export_boundaries FROM vat_financial_app;
      GRANT SELECT, INSERT ON platform.reporting_source_journal, platform.reporting_export_boundaries TO vat_financial_app;
      REVOKE DELETE, TRUNCATE ON platform.reporting_source_heads FROM vat_financial_app;
      GRANT SELECT, INSERT, UPDATE ON platform.reporting_source_heads TO vat_financial_app;
    END IF;
END `$`$;
COMMIT;
"@ | Out-Null
$metadataPath = Join-Path $secretRoot 'databases.json'
$metadata = [ordered]@{ Server = $Server; Port = $Port; FinancialDatabase = 'vat_management'; IdentityDatabase = 'vat_identity' }
if (Test-Path -LiteralPath $metadataPath) {
    $previousMetadata = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json
    if ($previousMetadata.PSObject.Properties.Name -contains 'IntegrationDatabase') { $metadata['IntegrationDatabase'] = $previousMetadata.IntegrationDatabase }
    if ($previousMetadata.PSObject.Properties.Name -contains 'ManufacturingDatabase') { $metadata['ManufacturingDatabase'] = $previousMetadata.ManufacturingDatabase }
    if ($previousMetadata.PSObject.Properties.Name -contains 'ComplianceDatabase') { $metadata['ComplianceDatabase'] = $previousMetadata.ComplianceDatabase }
    if ($previousMetadata.PSObject.Properties.Name -contains 'WorkflowDatabase') { $metadata['WorkflowDatabase'] = $previousMetadata.WorkflowDatabase }
}
$metadata | ConvertTo-Json | Set-Content -LiteralPath $metadataPath
Write-Output 'Local runtime roles configured. Credentials are encrypted with Windows DPAPI under ignored artifacts/local-secrets.'
Write-Output 'Identity app: role metadata/history read only. Financial app: coordinated source schemas, without Identity database access or administrative privileges.'
