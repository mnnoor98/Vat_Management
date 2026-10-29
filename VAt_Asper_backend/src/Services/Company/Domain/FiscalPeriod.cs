namespace Vat.Domain.Company;

public enum FiscalPeriodStatus { Open, Closing, Closed }

public sealed record PeriodCloseSnapshot(Guid PeriodId, DateOnly From, DateOnly Until, DateTimeOffset CapturedAt,
    IReadOnlyList<Guid> PurchaseIds, IReadOnlyList<Guid> StockEntryIds, IReadOnlyList<Guid> TaxEntryIds,
    decimal NetAmount, decimal TaxAmount, decimal RecoverableTaxAmount, string LedgerFingerprint, SalesPeriodCheckpoint? Sales = null, PaymentPeriodCheckpoint? Payments = null,
    IReadOnlyList<Guid>? TransferEntryIds = null);

public sealed record PaymentPeriodCheckpoint(IReadOnlyList<Guid> PaymentIds, IReadOnlyList<Guid> AllocationIds, decimal CollectedAmount, decimal AllocatedAmount, IReadOnlyList<Guid>? CashAdjustmentIds = null, IReadOnlyList<Guid>? AllocationReleaseIds = null, decimal ReturnedAmount = 0, decimal ReleasedAmount = 0);

public sealed record SalesPeriodCheckpoint(IReadOnlyList<Guid> InvoiceIds, IReadOnlyList<Guid> EntryIds, decimal NetAmount, decimal OutputTaxAmount);

public sealed class FiscalPeriod
{
    private FiscalPeriod() { }
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CompanyId { get; private set; }
    public string Name { get; private set; } = "";
    public DateOnly From { get; private set; }
    public DateOnly Until { get; private set; }
    public FiscalPeriodStatus Status { get; private set; }
    public long Version { get; private set; }
    public string? CloseRequestedBy { get; private set; }
    public string? ClosedBy { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public PeriodCloseSnapshot? Snapshot { get; private set; }

    public static FiscalPeriod Create(Guid tenantId, Guid companyId, string name, DateOnly from, DateOnly until)
    {
        if (from == default || until <= from)
            throw new DomainException("validation_failed", "Period dates must define a non-empty [from, until) interval.");
        return new()
        {
            Id = Guid.CreateVersion7(), TenantId = Guard.Id(tenantId, nameof(tenantId)), CompanyId = Guard.Id(companyId, nameof(companyId)),
            Name = Guard.Text(name, nameof(name), 100), From = from, Until = until, Version = 1
        };
    }

    public void RequestClose(string actor, long expectedVersion)
    {
        Expect(expectedVersion, FiscalPeriodStatus.Open);
        CloseRequestedBy = Guard.Text(actor, nameof(actor), 200);
        Status = FiscalPeriodStatus.Closing;
        Version++;
    }

    public void CancelClose(long expectedVersion)
    {
        Expect(expectedVersion, FiscalPeriodStatus.Closing);
        CloseRequestedBy = null;
        Status = FiscalPeriodStatus.Open;
        Version++;
    }

    public void Close(string actor, long expectedVersion, PeriodCloseSnapshot snapshot, DateTimeOffset now)
    {
        Expect(expectedVersion, FiscalPeriodStatus.Closing);
        if (actor == CloseRequestedBy)
            throw new DomainException("self_approval_forbidden", "The close requester cannot approve their own request.", 403);
        if (snapshot.PeriodId != Id || snapshot.From != From || snapshot.Until != Until)
            throw new DomainException("snapshot_mismatch", "The close snapshot must belong to this period.", 409);
        ClosedBy = Guard.Text(actor, nameof(actor), 200);
        ClosedAt = now;
        Snapshot = snapshot;
        Status = FiscalPeriodStatus.Closed;
        Version++;
    }

    private void Expect(long version, FiscalPeriodStatus status)
    {
        if (Version != version) throw new DomainException("version_conflict", "The period changed. Reload it before retrying.", 409);
        if (Status != status) throw new DomainException("invalid_state", $"This operation requires an {status} period.", 409);
    }
}
