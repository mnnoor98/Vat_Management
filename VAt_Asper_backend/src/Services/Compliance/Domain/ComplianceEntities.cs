namespace Vat.Services.Compliance.Domain;

public sealed class FormTemplate
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

public sealed class FormVersion
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = "";
    public int Version { get; set; }
    public Guid TemplateId { get; set; }
    public int Revision { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveUntil { get; set; }
    public string Mapping { get; set; } = "";
    public string? ApprovedBy { get; set; }
}

public sealed class ReturnPeriod
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = "";
    public int Version { get; set; }
    public Guid FiscalPeriodId { get; set; }
    public string Code { get; set; } = "";
    public DateOnly From { get; set; }
    public DateOnly Until { get; set; }
}

public sealed class VatReturn
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = "";
    public int Version { get; set; }
    public Guid ReturnPeriodId { get; set; }
    public Guid FormVersionId { get; set; }
    public int Revision { get; set; }
    public string State { get; set; } = "";
    public DateTimeOffset? LedgerWatermark { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public string? ApprovedBy { get; set; }
    public Guid? CorrectsId { get; set; }
}

public sealed class ReturnLine
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = "";
    public int Version { get; set; }
    public Guid ReturnId { get; set; }
    public string LineCode { get; set; } = "";
    public decimal Amount { get; set; }
    public string SourceSnapshot { get; set; } = "";
}

public sealed class ReconciliationRun
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = "";
    public int Version { get; set; }
    public Guid ReturnId { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string State { get; set; } = "";
    public string Differences { get; set; } = "";
}

public sealed class FilingEvidence
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = "";
    public int Version { get; set; }
    public Guid ReturnId { get; set; }
    public string ObjectKey { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public string FilingReference { get; set; } = "";
    public DateTimeOffset FiledAt { get; set; }
}

public sealed class PeriodCloseRequest
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = "";
    public int Version { get; set; }
    public Guid FiscalPeriodId { get; set; }
    public Guid? ReturnId { get; set; }
    public string State { get; set; } = "";
    public string RequestedBy { get; set; } = "";
    public string? ApprovedBy { get; set; }
    public DateTimeOffset? DecisionAt { get; set; }
}
