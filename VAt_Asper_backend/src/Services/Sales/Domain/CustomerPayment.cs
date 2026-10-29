namespace Vat.Domain.Sales;

public sealed record PaymentAllocationInput(Guid InvoiceId, decimal Amount);
public sealed class CustomerPayment
{
    private CustomerPayment() { }
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid CustomerId { get; private set; }
    public string Reference { get; private set; } = "";
    public string Method { get; private set; } = "";
    public string EvidenceReference { get; private set; } = "";
    public DateOnly PaymentDate { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = "BDT";
    public string Status { get; private set; } = "Pending";
    public long Version { get; private set; } = 1;
    public string RequestedBy { get; private set; } = "";
    public string? ApprovedBy { get; private set; }
    public string? CancellationReason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? PostedAt { get; private set; }
    public IReadOnlyList<PaymentAllocationInput> RequestedAllocations { get; private set; } = [];
    public static CustomerPayment Create(Guid tenant, Guid company, Guid customer, string reference, string method,
        string evidence, DateOnly date, decimal amount, IReadOnlyList<PaymentAllocationInput>? allocations, string actor, DateTimeOffset now)
    {
        Money(amount);
        if (date == default || method is not ("Cash" or "Bank")) throw new DomainException("validation_failed", "Supply a payment date and method Cash or Bank.");
        var lines = Lines(allocations ?? [], true);
        if (lines.Sum(x => x.Amount) > amount) throw new DomainException("payment_allocation_exceeded", "Allocations exceed payment amount.", 409);
        return new() { Id = Guid.CreateVersion7(), TenantId = Guard.Id(tenant, nameof(tenant)), CompanyId = Guard.Id(company, nameof(company)),
            CustomerId = Guard.Id(customer, nameof(customer)), Reference = Guard.Text(reference, nameof(reference), 100),
            Method = method, EvidenceReference = Guard.Text(evidence, nameof(evidence), 1000), PaymentDate = date, Amount = amount,
            RequestedAllocations = Array.AsReadOnly(lines.ToArray()), RequestedBy = Guard.Text(actor, nameof(actor), 200), CreatedAt = now };
    }
    public void Approve(long expected, string actor, DateTimeOffset now)
    {
        Expect(expected, "Pending");
        if (actor == RequestedBy) throw new DomainException("self_approval_forbidden", "Payment requester cannot approve their receipt.", 403);
        ApprovedBy = Guard.Text(actor, nameof(actor), 200); Status = "Posted"; PostedAt = now; Version++;
    }
    public void Cancel(long expected, string reason)
    {
        Expect(expected, "Pending"); CancellationReason = Guard.Text(reason, nameof(reason), 1000); Status = "Cancelled"; Version++;
    }
    public void Allocate(long expected) { Expect(expected, "Posted"); Version++; }
    public void RequestCorrection(long expected) { Expect(expected, "Posted"); Status = "CorrectionPending"; Version++; }
    public void DecideCorrection(long expected, string? terminalStatus)
    {
        Expect(expected, "CorrectionPending"); Status = terminalStatus ?? "Posted"; Version++;
    }
    private void Expect(long expected, string status)
    {
        if (Version != expected) throw new DomainException("version_conflict", "Reload the payment before retrying.", 409);
        if (Status != status) throw new DomainException("invalid_state", $"Payment must be {status}.", 409);
    }
    public static void Money(decimal amount)
    {
        if (amount <= 0 || amount > 1_000_000_000_000_000m || decimal.Round(amount, 4) != amount)
            throw new DomainException("validation_failed", "Amount must be positive, at most 1000000000000000 and have at most four decimal places.");
    }
    public static IReadOnlyList<PaymentAllocationInput> Lines(IReadOnlyList<PaymentAllocationInput>? lines, bool allowEmpty = false)
    {
        if (lines is null || lines.Count > 200 || (!allowEmpty && lines.Count == 0) || lines.Any(x => x is null || x.InvoiceId == Guid.Empty) || lines.Select(x => x.InvoiceId).Distinct().Count() != lines.Count)
            throw new DomainException("validation_failed", "Select distinct invoice IDs, at most 200.");
        foreach (var line in lines) Money(line.Amount);
        return lines;
    }
}

public sealed class PaymentAllocation
{
    private PaymentAllocation() { }
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid PaymentId { get; private set; }
    public Guid InvoiceId { get; private set; }
    public long PaymentVersion { get; private set; }
    public DateOnly AllocationDate { get; private set; }
    public decimal Amount { get; private set; }
    public string AllocatedBy { get; private set; } = "";
    public DateTimeOffset CreatedAt { get; private set; }
    public static PaymentAllocation Create(CustomerPayment payment, PaymentAllocationInput line, DateOnly date, string actor, DateTimeOffset now)
    {
        CustomerPayment.Money(line.Amount);
        if (payment.Status != "Posted" || date < payment.PaymentDate) throw new DomainException("invalid_state", "Allocation requires a posted receipt and a date on/after collection.", 409);
        return new() { Id = Guid.CreateVersion7(), TenantId = payment.TenantId, CompanyId = payment.CompanyId, CustomerId = payment.CustomerId,
            PaymentId = payment.Id, InvoiceId = line.InvoiceId, PaymentVersion = payment.Version, AllocationDate = date, Amount = line.Amount,
            AllocatedBy = Guard.Text(actor, nameof(actor), 200), CreatedAt = now };
    }
}
