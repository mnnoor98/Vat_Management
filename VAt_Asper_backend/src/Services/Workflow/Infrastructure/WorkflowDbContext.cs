using Microsoft.EntityFrameworkCore;
using Vat.Services.Workflow.Domain;
namespace Vat.Services.Workflow.Infrastructure;

public sealed class WorkflowDbContext(DbContextOptions<WorkflowDbContext> options) : DbContext(options)
{
    public DbSet<ApprovalTask> Tasks => Set<ApprovalTask>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<WorkflowDefinition>(b =>
        {
            b.ToTable("workflow_definitions", "workflow", t =>
            {
                t.HasCheckConstraint("ck_workflow_definitions_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
            });
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.CreatedBy).HasMaxLength(200).IsRequired();
            b.Property(x => x.Version).HasDefaultValue(1).IsConcurrencyToken();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.CreatedAt });
            b.Property(x => x.CreatedBy).IsRequired();
            b.Property(x => x.Code).IsRequired();
            b.Property(x => x.Name).IsRequired();
            b.Property(x => x.Code).HasMaxLength(100);
            b.Property(x => x.Name).HasMaxLength(200);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.Code }).IsUnique().AreNullsDistinct(false);
        });
        model.Entity<WorkflowVersion>(b =>
        {
            b.ToTable("workflow_versions", "workflow", t =>
            {
                t.HasCheckConstraint("ck_workflow_versions_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                t.HasCheckConstraint("ck_workflow_versions_rules", "\"Revision\" > 0 AND (\"EffectiveUntil\" IS NULL OR \"EffectiveUntil\" >= \"EffectiveFrom\") AND (\"ApprovedBy\" IS NULL OR \"ApprovedBy\" <> \"CreatedBy\") AND \"State\" IN ('Draft','Pending','Approved','Rejected','Cancelled','Running','Completed','Failed','Submitted','Posted')");
            });
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.CreatedBy).HasMaxLength(200).IsRequired();
            b.Property(x => x.Version).HasDefaultValue(1).IsConcurrencyToken();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.CreatedAt });
            b.Property(x => x.CreatedBy).IsRequired();
            b.Property(x => x.State).IsRequired();
            b.Property(x => x.Definition).IsRequired();
            b.Property(x => x.State).HasMaxLength(40);
            b.Property(x => x.Definition).HasColumnType("jsonb");
            b.Property(x => x.ApprovedBy).HasMaxLength(200);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.DefinitionId, x.Revision }).IsUnique().AreNullsDistinct(false);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.State, x.CreatedAt });
            b.HasOne<WorkflowDefinition>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.DefinitionId }).HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.DefinitionId });
        });
        model.Entity<ApprovalInstance>(b =>
        {
            b.ToTable("approval_instances", "workflow", t =>
            {
                t.HasCheckConstraint("ck_approval_instances_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                t.HasCheckConstraint("ck_approval_instances_rules", "\"SourceVersion\" > 0 AND \"State\" IN ('Draft','Pending','Approved','Rejected','Cancelled','Running','Completed','Failed','Submitted','Posted')");
            });
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.CreatedBy).HasMaxLength(200).IsRequired();
            b.Property(x => x.Version).HasDefaultValue(1).IsConcurrencyToken();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.CreatedAt });
            b.Property(x => x.CreatedBy).IsRequired();
            b.Property(x => x.SourceType).IsRequired();
            b.Property(x => x.RequestedBy).IsRequired();
            b.Property(x => x.State).IsRequired();
            b.Property(x => x.SourceType).HasMaxLength(80);
            b.Property(x => x.RequestedBy).HasMaxLength(200);
            b.Property(x => x.State).HasMaxLength(40);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.SourceType, x.SourceId, x.SourceVersion }).IsUnique().AreNullsDistinct(false);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.State, x.CreatedAt });
            b.HasOne<WorkflowVersion>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.WorkflowVersionId }).HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.WorkflowVersionId });
        });
        model.Entity<ApprovalTask>(b =>
        {
            b.ToTable("approval_tasks", "workflow", t =>
            {
                t.HasCheckConstraint("ck_approval_tasks_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                t.HasCheckConstraint("ck_approval_tasks_rules", "(\"StepNumber\" > 0 AND \"State\" IN ('Pending','Approved','Rejected','Cancelled'))");
            });
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.CreatedBy).HasMaxLength(200).IsRequired();
            b.Property(x => x.Version).HasDefaultValue(1).IsConcurrencyToken();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.CreatedAt });
            b.Property(x => x.CreatedBy).IsRequired();
            b.Property(x => x.AssignedTo).IsRequired();
            b.Property(x => x.State).IsRequired();
            b.Property(x => x.AssignedTo).HasMaxLength(200);
            b.Property(x => x.State).HasMaxLength(40);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.InstanceId, x.StepNumber, x.AssignedTo }).IsUnique().AreNullsDistinct(false);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.State, x.CreatedAt });
            b.HasOne<ApprovalInstance>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.InstanceId }).HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.InstanceId });
        });
        model.Entity<ApprovalAction>(b =>
        {
            b.ToTable("approval_actions", "workflow", t =>
            {
                t.HasCheckConstraint("ck_approval_actions_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                t.HasCheckConstraint("ck_approval_actions_rules", "(\"Decision\" IN ('Approved','Rejected','Cancelled'))");
            });
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.CreatedBy).HasMaxLength(200).IsRequired();
            b.Property(x => x.Version).HasDefaultValue(1).IsConcurrencyToken();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.CreatedAt });
            b.Property(x => x.CreatedBy).IsRequired();
            b.Property(x => x.Actor).IsRequired();
            b.Property(x => x.Decision).IsRequired();
            b.Property(x => x.Reason).IsRequired();
            b.Property(x => x.IdempotencyKey).IsRequired();
            b.Property(x => x.Actor).HasMaxLength(200);
            b.Property(x => x.Decision).HasMaxLength(40);
            b.Property(x => x.Reason).HasMaxLength(1000);
            b.Property(x => x.IdempotencyKey).HasMaxLength(100);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.TaskId, x.IdempotencyKey }).IsUnique().AreNullsDistinct(false);
            b.HasOne<ApprovalTask>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.TaskId }).HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.TaskId });
        });
        model.Entity<Delegation>(b =>
        {
            b.ToTable("delegations", "workflow", t =>
            {
                t.HasCheckConstraint("ck_delegations_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                t.HasCheckConstraint("ck_delegations_rules", "(\"Delegator\" <> \"Delegate\" AND \"ExpiresAt\" > \"StartsAt\")");
            });
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.CreatedBy).HasMaxLength(200).IsRequired();
            b.Property(x => x.Version).HasDefaultValue(1).IsConcurrencyToken();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.CreatedAt });
            b.Property(x => x.CreatedBy).IsRequired();
            b.Property(x => x.Delegator).IsRequired();
            b.Property(x => x.Delegate).IsRequired();
            b.Property(x => x.Delegator).HasMaxLength(200);
            b.Property(x => x.Delegate).HasMaxLength(200);
        });
        model.Entity<EscalationRule>(b =>
        {
            b.ToTable("escalation_rules", "workflow", t =>
            {
                t.HasCheckConstraint("ck_escalation_rules_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                t.HasCheckConstraint("ck_escalation_rules_rules", "(\"StepNumber\" > 0 AND \"AfterMinutes\" > 0)");
            });
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.CreatedBy).HasMaxLength(200).IsRequired();
            b.Property(x => x.Version).HasDefaultValue(1).IsConcurrencyToken();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.CreatedAt });
            b.Property(x => x.CreatedBy).IsRequired();
            b.Property(x => x.EscalateTo).IsRequired();
            b.Property(x => x.EscalateTo).HasMaxLength(200);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.WorkflowVersionId, x.StepNumber, x.AfterMinutes }).IsUnique().AreNullsDistinct(false);
            b.HasOne<WorkflowVersion>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.WorkflowVersionId }).HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.WorkflowVersionId });
        });
    }
}
