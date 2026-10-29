# 0016: Owned Workflow context

Date: 2026-10-23

## Decision

Extract Workflow into independent Domain/Application/Infrastructure projects and a dedicated `vat_workflow` database. Keep API routes and metadata contracts stable. Use `WorkflowDbContext` with its own migration history. Internal relationships include TenantId and CompanyId; Company, user and source-document identities remain external UUID/subject references. No other source schema has a foreign key into Workflow at cutover inspection.

The seven tables are workflow_definitions, workflow_versions, approval_instances, approval_tasks, approval_actions, delegations and escalation_rules. Retain version, temporal, state, uniqueness and maker/checker constraints from the foundation schema. Do not add commands that would imply cross-service decisions or financial posting without versioned contracts.

The financial application no longer compiles Workflow application/persistence source or registers its reader. Only the optional compatibility API explicitly composes Workflow. Shared HTTP/kernel libraries contain no financial persistence dependencies.

## Cutover and access

Use an operator connection for initial owned migrations and verified binary transfer. Freeze legacy writers during cutover. Source repeatable-read/share locks provide a consistent snapshot; target changes are transactional, dependency-ordered and fingerprint verified. Refuse a nonempty divergent target. Retain historical source tables without adding source migration changes or dual writes.

Provision `vat_workflow_owner` for migrations and `vat_workflow_app` for task/history SELECT only. Remove public access to the owned database and revoke the financial runtime role's legacy workflow access. Local credentials use the existing Windows DPAPI profile. Each API child receives only its own runtime connection.

## Validation

Standalone integration tests run without Company/financial tables and cover API scope/auth/paging, seven-table ownership/internal foreign keys, cross-tenant and invalid-decision rejection, nonempty graph transfer, Unicode/quoted/multiline text, identical reruns and divergent-target preservation. Validate context model drift, source/target backups with restore rehearsals, runtime-role boundaries and published API readiness before considering the cutover complete.
