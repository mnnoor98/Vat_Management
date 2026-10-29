using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vat.Workflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class WorkflowOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "workflow");

            migrationBuilder.CreateTable(
                name: "delegations",
                schema: "workflow",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    Delegator = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Delegate = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_delegations", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_delegations_rules", "(\"Delegator\" <> \"Delegate\" AND \"ExpiresAt\" > \"StartsAt\")");
                    table.CheckConstraint("ck_delegations_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                });

            migrationBuilder.CreateTable(
                name: "workflow_definitions",
                schema: "workflow",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflow_definitions", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_workflow_definitions_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                });

            migrationBuilder.CreateTable(
                name: "workflow_versions",
                schema: "workflow",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    DefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Definition = table.Column<string>(type: "jsonb", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    ApprovedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflow_versions", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_workflow_versions_rules", "\"Revision\" > 0 AND (\"EffectiveUntil\" IS NULL OR \"EffectiveUntil\" >= \"EffectiveFrom\") AND (\"ApprovedBy\" IS NULL OR \"ApprovedBy\" <> \"CreatedBy\") AND \"State\" IN ('Draft','Pending','Approved','Rejected','Cancelled','Running','Completed','Failed','Submitted','Posted')");
                    table.CheckConstraint("ck_workflow_versions_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_workflow_versions_workflow_definitions_TenantId_CompanyId_D~",
                        columns: x => new { x.TenantId, x.CompanyId, x.DefinitionId },
                        principalSchema: "workflow",
                        principalTable: "workflow_definitions",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "approval_instances",
                schema: "workflow",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    WorkflowVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceVersion = table.Column<int>(type: "integer", nullable: false),
                    RequestedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    State = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_approval_instances", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_approval_instances_rules", "\"SourceVersion\" > 0 AND \"State\" IN ('Draft','Pending','Approved','Rejected','Cancelled','Running','Completed','Failed','Submitted','Posted')");
                    table.CheckConstraint("ck_approval_instances_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_approval_instances_workflow_versions_TenantId_CompanyId_Wor~",
                        columns: x => new { x.TenantId, x.CompanyId, x.WorkflowVersionId },
                        principalSchema: "workflow",
                        principalTable: "workflow_versions",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "escalation_rules",
                schema: "workflow",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    WorkflowVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepNumber = table.Column<int>(type: "integer", nullable: false),
                    AfterMinutes = table.Column<int>(type: "integer", nullable: false),
                    EscalateTo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_escalation_rules", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_escalation_rules_rules", "(\"StepNumber\" > 0 AND \"AfterMinutes\" > 0)");
                    table.CheckConstraint("ck_escalation_rules_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_escalation_rules_workflow_versions_TenantId_CompanyId_Workf~",
                        columns: x => new { x.TenantId, x.CompanyId, x.WorkflowVersionId },
                        principalSchema: "workflow",
                        principalTable: "workflow_versions",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "approval_tasks",
                schema: "workflow",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    InstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedTo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StepNumber = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    DueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DecisionAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_approval_tasks", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_approval_tasks_rules", "(\"StepNumber\" > 0 AND \"State\" IN ('Pending','Approved','Rejected','Cancelled'))");
                    table.CheckConstraint("ck_approval_tasks_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_approval_tasks_approval_instances_TenantId_CompanyId_Instan~",
                        columns: x => new { x.TenantId, x.CompanyId, x.InstanceId },
                        principalSchema: "workflow",
                        principalTable: "approval_instances",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "approval_actions",
                schema: "workflow",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    Actor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Decision = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_approval_actions", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_approval_actions_rules", "(\"Decision\" IN ('Approved','Rejected','Cancelled'))");
                    table.CheckConstraint("ck_approval_actions_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_approval_actions_approval_tasks_TenantId_CompanyId_TaskId",
                        columns: x => new { x.TenantId, x.CompanyId, x.TaskId },
                        principalSchema: "workflow",
                        principalTable: "approval_tasks",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_approval_actions_TenantId_CompanyId_CreatedAt",
                schema: "workflow",
                table: "approval_actions",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_approval_actions_TenantId_CompanyId_TaskId",
                schema: "workflow",
                table: "approval_actions",
                columns: new[] { "TenantId", "CompanyId", "TaskId" });

            migrationBuilder.CreateIndex(
                name: "IX_approval_actions_TenantId_CompanyId_TaskId_IdempotencyKey",
                schema: "workflow",
                table: "approval_actions",
                columns: new[] { "TenantId", "CompanyId", "TaskId", "IdempotencyKey" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_approval_instances_TenantId_CompanyId_CreatedAt",
                schema: "workflow",
                table: "approval_instances",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_approval_instances_TenantId_CompanyId_SourceType_SourceId_S~",
                schema: "workflow",
                table: "approval_instances",
                columns: new[] { "TenantId", "CompanyId", "SourceType", "SourceId", "SourceVersion" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_approval_instances_TenantId_CompanyId_State_CreatedAt",
                schema: "workflow",
                table: "approval_instances",
                columns: new[] { "TenantId", "CompanyId", "State", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_approval_instances_TenantId_CompanyId_WorkflowVersionId",
                schema: "workflow",
                table: "approval_instances",
                columns: new[] { "TenantId", "CompanyId", "WorkflowVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_approval_tasks_TenantId_CompanyId_CreatedAt",
                schema: "workflow",
                table: "approval_tasks",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_approval_tasks_TenantId_CompanyId_InstanceId",
                schema: "workflow",
                table: "approval_tasks",
                columns: new[] { "TenantId", "CompanyId", "InstanceId" });

            migrationBuilder.CreateIndex(
                name: "IX_approval_tasks_TenantId_CompanyId_InstanceId_StepNumber_Ass~",
                schema: "workflow",
                table: "approval_tasks",
                columns: new[] { "TenantId", "CompanyId", "InstanceId", "StepNumber", "AssignedTo" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_approval_tasks_TenantId_CompanyId_State_CreatedAt",
                schema: "workflow",
                table: "approval_tasks",
                columns: new[] { "TenantId", "CompanyId", "State", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_delegations_TenantId_CompanyId_CreatedAt",
                schema: "workflow",
                table: "delegations",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_escalation_rules_TenantId_CompanyId_CreatedAt",
                schema: "workflow",
                table: "escalation_rules",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_escalation_rules_TenantId_CompanyId_WorkflowVersionId",
                schema: "workflow",
                table: "escalation_rules",
                columns: new[] { "TenantId", "CompanyId", "WorkflowVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_escalation_rules_TenantId_CompanyId_WorkflowVersionId_StepN~",
                schema: "workflow",
                table: "escalation_rules",
                columns: new[] { "TenantId", "CompanyId", "WorkflowVersionId", "StepNumber", "AfterMinutes" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_workflow_definitions_TenantId_CompanyId_Code",
                schema: "workflow",
                table: "workflow_definitions",
                columns: new[] { "TenantId", "CompanyId", "Code" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_workflow_definitions_TenantId_CompanyId_CreatedAt",
                schema: "workflow",
                table: "workflow_definitions",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_workflow_versions_TenantId_CompanyId_CreatedAt",
                schema: "workflow",
                table: "workflow_versions",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_workflow_versions_TenantId_CompanyId_DefinitionId",
                schema: "workflow",
                table: "workflow_versions",
                columns: new[] { "TenantId", "CompanyId", "DefinitionId" });

            migrationBuilder.CreateIndex(
                name: "IX_workflow_versions_TenantId_CompanyId_DefinitionId_Revision",
                schema: "workflow",
                table: "workflow_versions",
                columns: new[] { "TenantId", "CompanyId", "DefinitionId", "Revision" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_workflow_versions_TenantId_CompanyId_State_CreatedAt",
                schema: "workflow",
                table: "workflow_versions",
                columns: new[] { "TenantId", "CompanyId", "State", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "approval_actions",
                schema: "workflow");

            migrationBuilder.DropTable(
                name: "delegations",
                schema: "workflow");

            migrationBuilder.DropTable(
                name: "escalation_rules",
                schema: "workflow");

            migrationBuilder.DropTable(
                name: "approval_tasks",
                schema: "workflow");

            migrationBuilder.DropTable(
                name: "approval_instances",
                schema: "workflow");

            migrationBuilder.DropTable(
                name: "workflow_versions",
                schema: "workflow");

            migrationBuilder.DropTable(
                name: "workflow_definitions",
                schema: "workflow");
        }
    }
}
