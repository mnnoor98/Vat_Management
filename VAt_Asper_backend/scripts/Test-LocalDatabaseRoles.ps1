param([string]$PostgreSqlBin = 'C:/Program Files/PostgreSQL/18/bin')
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Import-LocalConnections.ps1') -Force
function Assert-RoleSql([string]$Connection, [string]$Sql, [string]$OverrideDatabase = '') {
    $settings = [Data.Common.DbConnectionStringBuilder]::new()
    $settings.set_ConnectionString($Connection)
    $database = if ($OverrideDatabase) { $OverrideDatabase } else { $settings['Database'] }
    $pgServer = [string]$settings['Host']
    $pgPort = [string]$settings['Port']
    $pgUser = [string]$settings['Username']
    $previousPassword = $env:PGPASSWORD
    try {
        $env:PGPASSWORD = [string]$settings['Password']
        $Sql | & (Join-Path $PostgreSqlBin 'psql.exe') -h $pgServer -p $pgPort `
            -U $pgUser -d $database -X -q -v ON_ERROR_STOP=1
        if ($LASTEXITCODE -ne 0) { throw 'Runtime database permission checks failed.' }
    } finally { $env:PGPASSWORD = $previousPassword }
}
Assert-RoleSql $env:ConnectionStrings__IdentityDatabase @'
DO $$ BEGIN
 IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = current_user AND (rolsuper OR rolcreatedb OR rolcreaterole OR rolbypassrls)) THEN
   RAISE EXCEPTION 'Identity runtime role has administrative privileges';
 END IF;
 IF NOT has_table_privilege(current_user, 'identity.roles', 'SELECT')
 OR NOT has_table_privilege(current_user, 'identity."__IdentityMigrationsHistory"', 'SELECT') THEN
   RAISE EXCEPTION 'Identity runtime role cannot read required data';
 END IF;
 IF has_table_privilege(current_user, 'identity.roles', 'INSERT')
 OR has_table_privilege(current_user, 'identity.roles', 'UPDATE')
 OR has_table_privilege(current_user, 'identity.users', 'SELECT') THEN
   RAISE EXCEPTION 'Identity runtime role has unnecessary write/private-data privileges';
 END IF;
END $$;
SELECT count(*) AS readable_roles FROM identity.roles;
'@
Assert-RoleSql $env:ConnectionStrings__IdentityDatabase @'
DO $$ BEGIN
 IF has_schema_privilege(current_user, 'company', 'USAGE')
 OR has_table_privilege(current_user, (SELECT c.oid FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
      WHERE n.nspname = 'company' AND c.relname = 'companies'), 'SELECT') THEN
   RAISE EXCEPTION 'Identity runtime role can access financial company data';
 END IF;
END $$;
'@ 'vat_management'
Assert-RoleSql $env:ConnectionStrings__VatDatabase @'
DO $$ BEGIN
 IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = current_user AND (rolsuper OR rolcreatedb OR rolcreaterole OR rolbypassrls)) THEN
   RAISE EXCEPTION 'Financial runtime role has administrative privileges';
 END IF;
 IF NOT has_table_privilege(current_user, 'company.companies', 'SELECT')
 OR NOT has_table_privilege(current_user, 'platform.outbox_messages', 'INSERT') THEN
   RAISE EXCEPTION 'Financial runtime role is missing operational privileges';
 END IF;
 IF has_schema_privilege(current_user, 'identity', 'USAGE')
 OR has_database_privilege(current_user, 'vat_identity', 'CONNECT') THEN
   RAISE EXCEPTION 'Financial runtime role can access owned Identity data';
 END IF;
END $$;
'@
Write-Output 'PASS: runtime roles have required access, no administrative privileges, and cross-owner data access is denied.'

Assert-RoleSql $env:ConnectionStrings__VatDatabase @'
DO $$ BEGIN
 IF to_regclass('inventory.transfer_journal') IS NOT NULL AND (
   NOT has_table_privilege(current_user,'inventory.transfer_journal','SELECT')
   OR NOT has_table_privilege(current_user,'inventory.transfer_journal','INSERT')
   OR has_table_privilege(current_user,'inventory.transfer_journal','UPDATE')
   OR has_table_privilege(current_user,'inventory.transfer_journal','DELETE')) THEN
   RAISE EXCEPTION 'Transfer journal must be runtime append-only';
 END IF;
 IF to_regclass('platform.reporting_source_exports') IS NOT NULL AND (
   NOT has_table_privilege(current_user,'platform.reporting_source_exports','SELECT')
   OR NOT has_table_privilege(current_user,'platform.reporting_source_exports','INSERT')
   OR has_table_privilege(current_user,'platform.reporting_source_exports','UPDATE')
   OR has_table_privilege(current_user,'platform.reporting_source_exports','DELETE')
   OR has_table_privilege(current_user,'platform.reporting_source_exports','TRUNCATE')) THEN
   RAISE EXCEPTION 'Reporting source snapshots must be runtime append-only';
 END IF;
 IF to_regclass('platform.reporting_source_journal') IS NOT NULL THEN
   IF NOT has_table_privilege(current_user,'platform.reporting_source_journal','SELECT')
      OR NOT has_table_privilege(current_user,'platform.reporting_source_journal','INSERT')
      OR has_table_privilege(current_user,'platform.reporting_source_journal','UPDATE')
      OR has_table_privilege(current_user,'platform.reporting_source_journal','DELETE')
      OR has_table_privilege(current_user,'platform.reporting_source_journal','TRUNCATE')
      OR NOT has_table_privilege(current_user,'platform.reporting_export_boundaries','SELECT')
      OR NOT has_table_privilege(current_user,'platform.reporting_export_boundaries','INSERT')
      OR has_table_privilege(current_user,'platform.reporting_export_boundaries','UPDATE')
      OR has_table_privilege(current_user,'platform.reporting_export_boundaries','DELETE')
      OR has_table_privilege(current_user,'platform.reporting_export_boundaries','TRUNCATE')
      OR NOT has_table_privilege(current_user,'platform.reporting_source_heads','SELECT')
      OR NOT has_table_privilege(current_user,'platform.reporting_source_heads','INSERT')
      OR NOT has_table_privilege(current_user,'platform.reporting_source_heads','UPDATE')
      OR has_table_privilege(current_user,'platform.reporting_source_heads','DELETE')
      OR has_table_privilege(current_user,'platform.reporting_source_heads','TRUNCATE') THEN
     RAISE EXCEPTION 'Reporting source sequence runtime permissions are invalid';
   END IF;
 END IF;
END $$;
'@

Assert-RoleSql $env:ConnectionStrings__WorkflowDatabase @'
DO $$ BEGIN
 IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname=current_user AND (rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication OR rolbypassrls)) THEN RAISE EXCEPTION 'Workflow app has administrative privileges'; END IF;
 IF NOT has_table_privilege(current_user,'workflow.approval_tasks','SELECT') OR NOT has_table_privilege(current_user,'workflow."__WorkflowMigrationsHistory"','SELECT') THEN RAISE EXCEPTION 'Workflow app lacks required reads'; END IF;
 IF has_table_privilege(current_user,'workflow.approval_tasks','INSERT') OR has_table_privilege(current_user,'workflow.approval_tasks','UPDATE') OR has_table_privilege(current_user,'workflow.approval_actions','SELECT') OR has_database_privilege(current_user,'vat_identity','CONNECT') THEN RAISE EXCEPTION 'Workflow app has unnecessary privileges'; END IF;
END $$;
SELECT count(*) AS readable_tasks FROM workflow.approval_tasks;
'@
Assert-RoleSql $env:ConnectionStrings__VatDatabase @'
DO $$ BEGIN
 IF has_database_privilege(current_user,'vat_workflow','CONNECT') OR has_schema_privilege(current_user,'workflow','USAGE') THEN RAISE EXCEPTION 'Financial app can access Workflow data'; END IF;
END $$;
'@
Assert-RoleSql $env:ConnectionStrings__WorkflowDatabase @'
DO $$ BEGIN
 IF has_schema_privilege(current_user,'company','USAGE') OR has_schema_privilege(current_user,'workflow','USAGE') THEN RAISE EXCEPTION 'Workflow app can access legacy financial data'; END IF;
END $$;
'@ 'vat_management'
Write-Output 'PASS: Workflow runtime metadata reads and cross-owner isolation verified.'

Assert-RoleSql $env:ConnectionStrings__ComplianceDatabase @'
DO $$ BEGIN
 IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname=current_user AND (rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication OR rolbypassrls)) THEN RAISE EXCEPTION 'Compliance app has administrative privileges'; END IF;
 IF NOT has_table_privilege(current_user,'compliance.vat_returns','SELECT') OR NOT has_table_privilege(current_user,'compliance."__ComplianceMigrationsHistory"','SELECT') THEN RAISE EXCEPTION 'Compliance app lacks required reads'; END IF;
 IF has_table_privilege(current_user,'compliance.vat_returns','INSERT') OR has_table_privilege(current_user,'compliance.vat_returns','UPDATE') OR has_table_privilege(current_user,'compliance.filing_evidence','SELECT') OR has_database_privilege(current_user,'vat_identity','CONNECT') OR has_database_privilege(current_user,'vat_workflow','CONNECT') THEN RAISE EXCEPTION 'Compliance app has unnecessary privileges'; END IF;
END $$;
SELECT count(*) AS readable_returns FROM compliance.vat_returns;
'@
Assert-RoleSql $env:ConnectionStrings__VatDatabase @'
DO $$ BEGIN
 IF has_database_privilege(current_user,'vat_compliance','CONNECT') OR has_schema_privilege(current_user,'compliance','USAGE') THEN RAISE EXCEPTION 'Financial app can access Compliance data'; END IF;
END $$;
'@
Assert-RoleSql $env:ConnectionStrings__ComplianceDatabase @'
DO $$ BEGIN
 IF has_schema_privilege(current_user,'company','USAGE') OR has_schema_privilege(current_user,'compliance','USAGE') THEN RAISE EXCEPTION 'Compliance app can access legacy financial data'; END IF;
END $$;
'@ 'vat_management'
Write-Output 'PASS: Compliance runtime metadata reads and cross-owner isolation verified.'

Assert-RoleSql $env:ConnectionStrings__ManufacturingDatabase @'
DO $$ BEGIN
 IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname=current_user AND (rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication OR rolbypassrls)) THEN RAISE EXCEPTION 'Manufacturing app has administrative privileges'; END IF;
 IF NOT has_table_privilege(current_user,'manufacturing.production_orders','SELECT') OR NOT has_table_privilege(current_user,'manufacturing."__ManufacturingMigrationsHistory"','SELECT') THEN RAISE EXCEPTION 'Manufacturing app lacks required reads'; END IF;
 IF has_table_privilege(current_user,'manufacturing.production_orders','INSERT') OR has_table_privilege(current_user,'manufacturing.production_orders','UPDATE') OR has_table_privilege(current_user,'manufacturing.material_issues','SELECT') OR has_database_privilege(current_user,'vat_identity','CONNECT') OR has_database_privilege(current_user,'vat_workflow','CONNECT') OR has_database_privilege(current_user,'vat_compliance','CONNECT') THEN RAISE EXCEPTION 'Manufacturing app has unnecessary privileges'; END IF;
END $$;
SELECT count(*) AS readable_orders FROM manufacturing.production_orders;
DO $$ BEGIN
 IF NOT has_table_privilege(current_user,'manufacturing.coefficient_versions','INSERT')
 OR NOT has_table_privilege(current_user,'manufacturing.coefficient_versions','UPDATE')
 OR NOT has_table_privilege(current_user,'manufacturing.coefficient_audit','INSERT')
 OR NOT has_table_privilege(current_user,'manufacturing.coefficient_documents','SELECT') THEN
   RAISE EXCEPTION 'Manufacturing app lacks workflow access';
 END IF;
 IF has_table_privilege(current_user,'manufacturing.coefficient_documents','UPDATE')
 OR has_table_privilege(current_user,'manufacturing.coefficient_documents','DELETE')
 OR has_table_privilege(current_user,'manufacturing.coefficient_audit','UPDATE')
 OR has_table_privilege(current_user,'manufacturing.coefficient_audit','DELETE')
 OR has_table_privilege(current_user,'manufacturing.coefficient_commands','UPDATE')
 OR has_table_privilege(current_user,'manufacturing.coefficient_commands','DELETE') THEN
   RAISE EXCEPTION 'Manufacturing immutable history is writable';
 END IF;
END $$;
'@
Assert-RoleSql $env:ConnectionStrings__VatDatabase @'
DO $$ BEGIN
 IF has_database_privilege(current_user,'vat_manufacturing','CONNECT') OR has_schema_privilege(current_user,'manufacturing','USAGE') THEN RAISE EXCEPTION 'Financial app can access Manufacturing data'; END IF;
END $$;
'@
Assert-RoleSql $env:ConnectionStrings__ManufacturingDatabase @'
DO $$ BEGIN
 IF has_schema_privilege(current_user,'company','USAGE') OR has_schema_privilege(current_user,'manufacturing','USAGE') THEN RAISE EXCEPTION 'Manufacturing app can access legacy financial data'; END IF;
END $$;
'@ 'vat_management'
Write-Output 'PASS: Manufacturing runtime metadata reads and cross-owner isolation verified.'

Assert-RoleSql $env:ConnectionStrings__IntegrationDatabase @'
DO $$ BEGIN
 IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname=current_user AND (rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication OR rolbypassrls)) THEN RAISE EXCEPTION 'Integration app has administrative privileges'; END IF;
 IF NOT has_table_privilege(current_user,'integration.integration_connections','SELECT') OR NOT has_table_privilege(current_user,'integration."__IntegrationMigrationsHistory"','SELECT') THEN RAISE EXCEPTION 'Integration app lacks required reads'; END IF;
 IF has_table_privilege(current_user,'integration.integration_connections','INSERT') OR has_table_privilege(current_user,'integration.integration_connections','UPDATE') OR has_table_privilege(current_user,'integration.inbound_batches','SELECT') OR has_database_privilege(current_user,'vat_identity','CONNECT') OR has_database_privilege(current_user,'vat_workflow','CONNECT') OR has_database_privilege(current_user,'vat_compliance','CONNECT') OR has_database_privilege(current_user,'vat_manufacturing','CONNECT') THEN RAISE EXCEPTION 'Integration app has unnecessary privileges'; END IF;
END $$;
SELECT count(*) AS readable_connections FROM integration.integration_connections;
'@
Assert-RoleSql $env:ConnectionStrings__VatDatabase @'
DO $$ BEGIN
 IF has_database_privilege(current_user,'vat_integration','CONNECT') OR has_schema_privilege(current_user,'integration','USAGE') THEN RAISE EXCEPTION 'Financial app can access Integration data'; END IF;
END $$;
'@
Assert-RoleSql $env:ConnectionStrings__IntegrationDatabase @'
DO $$ BEGIN
 IF has_schema_privilege(current_user,'company','USAGE') OR has_schema_privilege(current_user,'integration','USAGE') THEN RAISE EXCEPTION 'Integration app can access legacy financial data'; END IF;
END $$;
'@ 'vat_management'
Write-Output 'PASS: Integration runtime metadata reads and cross-owner isolation verified.'
