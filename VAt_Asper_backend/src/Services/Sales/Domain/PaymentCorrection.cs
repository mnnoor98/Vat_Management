namespace Vat.Domain.Sales;

public sealed record PaymentReleaseInput(Guid AllocationId, decimal Amount);
public sealed record FrozenPaymentRelease(Guid AllocationId, Guid InvoiceId, decimal Amount);
public sealed class PaymentCorrection
{
    private PaymentCorrection() { }
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid PaymentId { get; private set; }
    public long SourceVersion { get; private set; }
    public long Version { get; private set; } = 1;
    public string Mode { get; private set; } = "";
    public string Status { get; private set; } = "Pending";
    public DateOnly CorrectionDate { get; private set; }
    public decimal Amount { get; private set; }
    public string Method { get; private set; } = "";
    public string Reason { get; private set; } = "";
    public string EvidenceReference { get; private set; } = "";
    public string RequestedBy { get; private set; } = "";
    public string? ApprovedBy { get; private set; }
    public string? DocumentNumber { get; private set; }
    public string? CancellationReason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? PostedAt { get; private set; }
    public IReadOnlyList<FrozenPaymentRelease> Releases { get; private set; } = [];
    public static PaymentCorrection Create(CustomerPayment payment, string mode, DateOnly date, decimal amount,
        string method, string reason, string evidence, string actor, IReadOnlyList<FrozenPaymentRelease> releases, DateTimeOffset now)
    {
        CustomerPayment.Money(amount);
        if (payment.Status != "CorrectionPending" || mode is not ("Reversal" or "Refund") || method is not ("Cash" or "Bank") ||
            date < payment.PaymentDate || releases.Count > 2000 || releases.Sum(x => x.Amount) > amount)
            throw new DomainException("validation_failed", "Correction state, date, method or release budget is invalid.");
        return new() { Id = Guid.CreateVersion7(), TenantId = payment.TenantId, CompanyId = payment.CompanyId, CustomerId = payment.CustomerId,
            PaymentId = payment.Id, SourceVersion = payment.Version, Mode = mode, CorrectionDate = date, Amount = amount, Method = method,
            Reason = Guard.Text(reason, nameof(reason), 1000), EvidenceReference = Guard.Text(evidence, nameof(evidence), 1000),
            RequestedBy = Guard.Text(actor, nameof(actor), 200), Releases = Array.AsReadOnly(releases.OrderBy(x => x.AllocationId).ToArray()), CreatedAt = now };
    }
    public void Approve(string actor, string receiptMaker, DateTimeOffset now)
    {
        Pending();
        if (actor == RequestedBy || actor == receiptMaker) throw new DomainException("self_approval_forbidden", "Correction requester and receipt maker cannot approve.", 403);
        ApprovedBy = Guard.Text(actor, nameof(actor), 200); Status = "Posted"; Version++; PostedAt = now;
        DocumentNumber = $"{(Mode == "Reversal" ? "RV" : "RF")}-{Id:N}";
    }
    public void Cancel(string reason) { Pending(); CancellationReason = Guard.Text(reason, nameof(reason), 1000); Status = "Cancelled"; Version++; }
    private void Pending() { if (Status != "Pending") throw new DomainException("invalid_state", "Only pending corrections can be decided.", 409); }
}
public sealed class PaymentCashAdjustment
{
    private PaymentCashAdjustment() { }
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid PaymentId { get; private set; }
    public Guid CorrectionId { get; private set; }
    public DateOnly BusinessDate { get; private set; }
    public decimal Amount { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public static PaymentCashAdjustment Create(PaymentCorrection n, DateTimeOffset now)
    {
        if (n.Status != "Posted") throw new DomainException("invalid_state", "Posted correction required.", 409);
        return new() { Id = Guid.CreateVersion7(), TenantId = n.TenantId, CompanyId = n.CompanyId, CustomerId = n.CustomerId,
            PaymentId = n.PaymentId, CorrectionId = n.Id, BusinessDate = n.CorrectionDate, Amount = -n.Amount, CreatedAt = now };
    }
}
public sealed class PaymentAllocationRelease
{
    private PaymentAllocationRelease() { }
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid PaymentId { get; private set; }
    public Guid CorrectionId { get; private set; }
    public Guid AllocationId { get; private set; }
    public Guid InvoiceId { get; private set; }
    public DateOnly BusinessDate { get; private set; }
    public decimal Amount { get; private set; }
    public static PaymentAllocationRelease Create(PaymentCorrection n, FrozenPaymentRelease line)
    {
        if (n.Status != "Posted" || !n.Releases.Contains(line)) throw new DomainException("invalid_state", "Frozen posted release required.", 409);
        return new() { Id = Guid.CreateVersion7(), TenantId = n.TenantId, CompanyId = n.CompanyId, CustomerId = n.CustomerId,
            PaymentId = n.PaymentId, CorrectionId = n.Id, AllocationId = line.AllocationId, InvoiceId = line.InvoiceId, BusinessDate = n.CorrectionDate, Amount = line.Amount };
    }
}
public static class PaymentCorrectionRules
{
    public static IReadOnlyList<FrozenPaymentRelease> Freeze(CustomerPayment p, IReadOnlyList<PaymentAllocation> originals,
        IReadOnlyList<PaymentCorrection> previous, IReadOnlyList<PaymentAllocationRelease> released, string mode, decimal amount,
        DateOnly date, IReadOnlyList<PaymentReleaseInput>? selected)
    {
        var cash = p.Amount - previous.Where(x => x.Status == "Posted").Sum(x => x.Amount);
        var lastDate = previous.Where(x => x.Status == "Posted").Select(x => x.CorrectionDate)
            .Concat(originals.Select(x => x.AllocationDate)).Append(p.PaymentDate).Max();
        if (date < lastDate) throw new DomainException("validation_failed", "Correction date cannot precede collection, allocations or earlier posted corrections.");
        var inputs = mode == "Reversal" ? originals.Select(x => new PaymentReleaseInput(x.Id, x.Amount - released.Where(r => r.AllocationId == x.Id).Sum(r => r.Amount))).Where(x => x.Amount > 0).ToArray() : selected ?? [];
        if (mode is not ("Reversal" or "Refund") || inputs.Count > 2000 || inputs.Any(x => x is null) || inputs.Select(x => x.AllocationId).Distinct().Count() != inputs.Count)
            throw new DomainException("validation_failed", "Distinct original allocations are required (maximum 2000 releases).");
        CustomerPayment.Money(amount);
        if (amount > cash || (mode == "Reversal" && amount != cash)) throw new DomainException("correction_amount_exceeded", "Correction exceeds remaining receipt cash.", 409);
        var result = new List<FrozenPaymentRelease>();
        foreach (var input in inputs)
        {
            CustomerPayment.Money(input.Amount);
            var original = originals.SingleOrDefault(x => x.Id == input.AllocationId) ?? throw new DomainException("not_found", "Allocation not found on this receipt.", 404);
            if (input.Amount > original.Amount - released.Where(x => x.AllocationId == original.Id).Sum(x => x.Amount))
                throw new DomainException("release_amount_exceeded", "Release exceeds the remaining original allocation.", 409);
            result.Add(new(original.Id, original.InvoiceId, input.Amount));
        }
        var total = result.Sum(x => x.Amount);
        var allocated = originals.Sum(x => x.Amount) - released.Sum(x => x.Amount);
        if (total > amount || amount - total > cash - allocated)
            throw new DomainException("refund_funding_exceeded", "Use receipt unallocated cash or explicitly release eligible invoice credit.", 409);
        return result.OrderBy(x => x.AllocationId).ToArray();
    }
    public static bool Reconciles(CustomerPayment p, IReadOnlyList<PaymentAllocation> allocations,
        IReadOnlyList<PaymentCorrection> notes, IReadOnlyList<PaymentCashAdjustment> cash, IReadOnlyList<PaymentAllocationRelease> releases)
    {
        if (p.PostedAt is null) return allocations.Count == 0 && notes.Count == 0 && cash.Count == 0 && releases.Count == 0 &&
            p.Status is "Pending" or "Cancelled" && p.Version == (p.Status == "Pending" ? 1 : 2);
        if (p.ApprovedBy is null || p.ApprovedBy == p.RequestedBy) return false;
        var initial = allocations.Where(x => x.PaymentVersion == 2).OrderBy(x => x.InvoiceId);
        if (!initial.Select(x => new PaymentAllocationInput(x.InvoiceId,x.Amount)).SequenceEqual(p.RequestedAllocations.OrderBy(x => x.InvoiceId)) ||
            initial.Any(x => x.AllocationDate != p.PaymentDate || x.AllocatedBy != p.ApprovedBy)) return false;
        var batches = allocations.Where(x => x.PaymentVersion > 2).GroupBy(x => x.PaymentVersion).ToArray();
        var actions = batches.Select(x => (Version: x.Key, Note: (PaymentCorrection?)null))
            .Concat(notes.Select(x => (Version: x.SourceVersion, Note: (PaymentCorrection?)x))).OrderBy(x => x.Version).ToArray();
        long version = 2;
        var priorNotes = new List<PaymentCorrection>();
        var priorReleases = new List<PaymentAllocationRelease>();
        bool pending = false; string expectedStatus = "Posted";
        foreach (var action in actions)
        {
            if (pending || expectedStatus is "Reversed" or "Refunded" || action.Version != version + 1) return false;
            if (action.Note is not { } n)
            {
                var batch = batches.Single(x => x.Key == action.Version);
                if (batch.Select(x => x.AllocationDate).Distinct().Count() != 1 || batch.Select(x => x.AllocatedBy).Distinct().Count() != 1) return false;
                version++; continue;
            }
            if (n.Status is not ("Pending" or "Posted" or "Cancelled") || n.Version != (n.Status == "Pending" ? 1 : 2)) return false;
            IReadOnlyList<FrozenPaymentRelease> expected;
            try { expected = Freeze(p, allocations.Where(x => x.PaymentVersion < n.SourceVersion).ToArray(), priorNotes, priorReleases,
                n.Mode, n.Amount, n.CorrectionDate, n.Releases.Select(x => new PaymentReleaseInput(x.AllocationId,x.Amount)).ToArray()); }
            catch (DomainException) { return false; }
            if (!expected.SequenceEqual(n.Releases.OrderBy(x => x.AllocationId))) return false;
            var c = cash.Where(x => x.CorrectionId == n.Id).ToArray(); var r = releases.Where(x => x.CorrectionId == n.Id).ToArray();
            if (n.Status == "Posted")
            {
                if (n.ApprovedBy is null || n.ApprovedBy == n.RequestedBy || n.ApprovedBy == p.RequestedBy || n.PostedAt is null ||
                    n.DocumentNumber != $"{(n.Mode == "Reversal" ? "RV" : "RF")}-{n.Id:N}" || c.Length != 1 || c[0].Amount != -n.Amount || c[0].BusinessDate != n.CorrectionDate ||
                    r.Length != n.Releases.Count || r.Any(x => x.BusinessDate != n.CorrectionDate) ||
                    !r.OrderBy(x => x.AllocationId).Select(x => new FrozenPaymentRelease(x.AllocationId,x.InvoiceId,x.Amount)).SequenceEqual(n.Releases.OrderBy(x => x.AllocationId))) return false;
                priorNotes.Add(n); priorReleases.AddRange(r);
                expectedStatus = n.Mode == "Reversal" ? "Reversed" : priorNotes.Sum(x => x.Amount) == p.Amount ? "Refunded" : "Posted";
            }
            else if (c.Length != 0 || r.Length != 0) return false;
            pending = n.Status == "Pending"; version = n.SourceVersion + (pending ? 0 : 1);
        }
        return p.Version == version && p.Status == (pending ? "CorrectionPending" : expectedStatus) &&
            allocations.Sum(x => x.Amount) - releases.Sum(x => x.Amount) >= 0 &&
            allocations.Sum(x => x.Amount) - releases.Sum(x => x.Amount) <= p.Amount + cash.Sum(x => x.Amount) &&
            cash.All(x => notes.Any(n => n.Id == x.CorrectionId && n.Status == "Posted")) && releases.All(x => notes.Any(n => n.Id == x.CorrectionId && n.Status == "Posted"));
    }
}
