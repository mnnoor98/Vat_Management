using Vat.Application.Security;
using Vat.Domain.Tax;

namespace Vat.Services.VatEngine;

public static class VatEnginePermissions
{
    public const string TaxRuleManage = "tax.rule.manage";
    public const string TaxRulePublish = "tax.rule.publish";
    public const string TaxRead = "tax.read";
}

public sealed record CommandOutcome(int StatusCode, object Body, string? Location = null);
public sealed record StoredResponse(int StatusCode, string Json, string? Location, bool Replayed);

public sealed record CreateTaxRuleRequest(string TaxCode, DateOnly EffectiveFrom, DateOnly EffectiveUntil,
    decimal RatePercent, decimal RecoverablePercent, string LegalBasis, string EvidenceReference, string Treatment = "Purchase");

public sealed record TransitionRequest(long ExpectedVersion);

public sealed record TaxRuleResponse(Guid Id, string TaxCode, DateOnly EffectiveFrom, DateOnly EffectiveUntil,
    decimal RatePercent, decimal RecoverablePercent, string LegalBasis, string EvidenceReference,
    string CreatedBy, string? PublishedBy, DateTimeOffset? PublishedAt, long Version, string Treatment = "Purchase")
{
    public static TaxRuleResponse From(TaxRule x) => new(x.Id, x.TaxCode, x.EffectiveFrom, x.EffectiveUntil,
        x.RatePercent, x.RecoverablePercent, x.LegalBasis, x.EvidenceReference, x.CreatedBy, x.PublishedBy, x.PublishedAt, x.Version, x.Treatment);
}

public sealed record TaxLedgerResponse(Guid Id, Guid PurchaseId, Guid PurchaseLineId, Guid PlanId,
    Guid TaxRuleId, DateOnly BusinessDate, decimal NetAmount, decimal TaxAmount, decimal RecoverableTaxAmount,
    Guid? ReversesEntryId, Guid? ReversalId);

public sealed record TaxCalculationItemRequest(Guid ProductId, decimal Quantity, decimal UnitPrice, string TaxCode);
public sealed record TaxCalculationItemResult(Guid ProductId, decimal NetAmount, decimal VatRate, decimal VatAmount, decimal SdRate, decimal SdAmount, decimal TotalAmount);
public sealed record TaxCalculationRequest(DateOnly Date, IReadOnlyList<TaxCalculationItemRequest> Items);
public sealed record TaxCalculationResult(decimal TotalNetAmount, decimal TotalVatAmount, decimal TotalSdAmount, decimal GrandTotal, IReadOnlyList<TaxCalculationItemResult> Items);

public interface ICommandExecutor
{
    Task<StoredResponse> Execute(Actor actor, Guid companyId, string operation, string key,
        object request, Func<Task<CommandOutcome>> action, CancellationToken ct);
}

public interface IAuditWriter
{
    void Append(Actor actor, Guid companyId, Guid resourceId, string action, string correlationId,
        long? version = null, string? reason = null);
}

public interface IEventWriter
{
    void Append<T>(Guid tenantId, Guid companyId, Guid aggregateId, long version,
        string eventType, string correlationId, T payload);
}

public interface ITaxRuleStore
{
    Task<TaxRuleResponse> Create(Actor actor, Guid companyId, CreateTaxRuleRequest request, string correlationId, CancellationToken ct);
    Task<TaxRuleResponse> Publish(Actor actor, Guid companyId, Guid id, TransitionRequest request, string correlationId, CancellationToken ct);
    Task<TaxRuleResponse?> Get(Actor actor, Guid companyId, Guid id, CancellationToken ct);
    Task<TaxRule?> EffectiveRule(Guid tenantId, Guid companyId, string taxCode, string treatment, DateOnly date, CancellationToken ct);
}

public interface ITaxLedgerStore
{
    Task<IReadOnlyList<TaxLedgerResponse>> List(Actor actor, Guid companyId, Guid? purchaseId, Guid? before, int take, CancellationToken ct);
}

