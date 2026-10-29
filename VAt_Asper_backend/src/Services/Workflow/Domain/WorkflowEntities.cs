namespace Vat.Services.Workflow.Domain;

public sealed class WorkflowDefinition
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = "";
    public int Version { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
}

public sealed class WorkflowVersion
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = "";
    public int Version { get; set; }
    public Guid DefinitionId { get; set; }
    public int Revision { get; set; }
    public string State { get; set; } = "";
    public string Definition { get; set; } = "";
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveUntil { get; set; }
    public string? ApprovedBy { get; set; }
}

public sealed class ApprovalInstance
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = "";
    public int Version { get; set; }
    public Guid WorkflowVersionId { get; set; }
    public string SourceType { get; set; } = "";
    public Guid SourceId { get; set; }
    public int SourceVersion { get; set; }
    public string RequestedBy { get; set; } = "";
    public string State { get; set; } = "";
}

public sealed class ApprovalTask
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = "";
    public int Version { get; set; }
    public Guid InstanceId { get; set; }
    public string AssignedTo { get; set; } = "";
    public int StepNumber { get; set; }
    public string State { get; set; } = "";
    public DateTimeOffset? DueAt { get; set; }
    public DateTimeOffset? DecisionAt { get; set; }
}

public sealed class ApprovalAction
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = "";
    public int Version { get; set; }
    public Guid TaskId { get; set; }
    public string Actor { get; set; } = "";
    public string Decision { get; set; } = "";
    public string Reason { get; set; } = "";
    public DateTimeOffset OccurredAt { get; set; }
    public string IdempotencyKey { get; set; } = "";
}

public sealed class Delegation
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = "";
    public int Version { get; set; }
    public string Delegator { get; set; } = "";
    public string Delegate { get; set; } = "";
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}

public sealed class EscalationRule
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = "";
    public int Version { get; set; }
    public Guid WorkflowVersionId { get; set; }
    public int StepNumber { get; set; }
    public int AfterMinutes { get; set; }
    public string EscalateTo { get; set; } = "";
}
