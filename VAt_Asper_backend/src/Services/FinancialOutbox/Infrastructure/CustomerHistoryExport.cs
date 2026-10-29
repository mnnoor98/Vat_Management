using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vat.Application;
using Vat.Application.Security;
using Vat.Domain;
using Vat.Domain.Sales;
using Vat.Messaging.Contracts.V1;

namespace Vat.Infrastructure.Persistence;

public sealed class CustomerHistoryExportService(VatDbContext db, IPeriodStore periods, IAuditWriter audit, TimeProvider clock)
{
    public async Task<CommandOutcome> Prepare(Actor actor, Guid company, Guid customer, string reason, string correlation, CancellationToken ct)
    {
        actor.RequireCompany(company);
        if (reason is null || reason.Length > 1000) throw new DomainException("validation_failed", "Supply a reason of at most 1000 characters.");
        reason = Guard.Text(reason, nameof(reason), 1000);
        // Command executor owns the transaction. The same fence excludes financial posting,
        // allocation and correction writers throughout reconciliation and capture.
        await periods.LockCompany(actor.TenantId, company, true, ct);
        var person = await db.Set<Customer>().AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == actor.TenantId
            && x.CompanyId == company && x.Id == customer, ct) ?? throw new DomainException("not_found", "Customer not found.", 404);
        var invoiceQuery = db.SalesInvoices.AsNoTracking().Where(x => x.TenantId == actor.TenantId && x.CompanyId == company && x.CustomerId == customer);
        // Bound every materialized history family, including pending records inspected by
        // the authoritative reconciler. Fail closed instead of exporting a truncated opening.
        long count = await invoiceQuery.CountAsync(ct);
        count += await db.Set<SalesPostingEntry>().CountAsync(x => x.TenantId == actor.TenantId && x.CompanyId == company
            && invoiceQuery.Any(i => i.Id == x.InvoiceId), ct);
        count += await db.Set<SalesCreditNote>().CountAsync(x => x.TenantId == actor.TenantId && x.CompanyId == company && invoiceQuery.Any(i => i.Id == x.InvoiceId), ct);
        count += await db.Set<SalesDebitNote>().CountAsync(x => x.TenantId == actor.TenantId && x.CompanyId == company && invoiceQuery.Any(i => i.Id == x.InvoiceId), ct);
        count += await db.Set<CustomerPayment>().CountAsync(x => x.TenantId == actor.TenantId && x.CompanyId == company && x.CustomerId == customer, ct);
        count += await db.Set<PaymentAllocation>().CountAsync(x => x.TenantId == actor.TenantId && x.CompanyId == company && x.CustomerId == customer, ct);
        count += await db.Set<PaymentCorrection>().CountAsync(x => x.TenantId == actor.TenantId && x.CompanyId == company && x.CustomerId == customer, ct);
        count += await db.Set<PaymentCashAdjustment>().CountAsync(x => x.TenantId == actor.TenantId && x.CompanyId == company && x.CustomerId == customer, ct);
        count += await db.Set<PaymentAllocationRelease>().CountAsync(x => x.TenantId == actor.TenantId && x.CompanyId == company && x.CustomerId == customer, ct);
        if (count > CustomerHistoryV1.MaxRows) Limit();
        if (await invoiceQuery.AnyAsync(x => x.PostedVersion == null && (x.Status == SalesInvoiceStatus.Posted
                || x.Status == SalesInvoiceStatus.CreditPending || x.Status == SalesInvoiceStatus.Credited
                || x.Status == SalesInvoiceStatus.PartiallyCredited || x.Status == SalesInvoiceStatus.DebitPending), ct)
            || await db.Set<SalesPostingEntry>().AnyAsync(x => x.TenantId == actor.TenantId && x.CompanyId == company
                && invoiceQuery.Any(i => i.Id == x.InvoiceId && i.PostedVersion == null), ct))
            throw new DomainException("reconciliation_failed", "Posted customer history is missing its source version.", 409);
        if (!await new PaymentStore(db).Reconciles(actor.TenantId, company, ct, customer))
            throw new DomainException("payment_reconciliation_failed", "Reconcile customer cash and allocations before export.", 409);
        var invoices = new List<CustomerHistoryInvoiceV1>();
        var documents = new List<CustomerHistoryDocumentV1>();
        var postingStore = new SalesPostingStore(db);
        foreach (var id in await invoiceQuery.Where(x => x.PostedVersion != null).Select(x => x.Id).ToArrayAsync(ct))
        {
            var read = await postingStore.Read(actor.TenantId, company, id, ct);
            if (read is null || !SalesReconciler.Evaluate(read).IsReconciled)
                throw new DomainException("reconciliation_failed", "Reconcile customer invoices before export.", 409);
            if (read.Invoice.Currency != "BDT") throw new DomainException("unsupported_currency", "Customer history V1 supports BDT only.", 409);
            invoices.Add(new(id, read.Invoice.ExternalReference, read.Invoice.BusinessDate, read.Invoice.DueDate));
            foreach (var group in read.Entries.GroupBy(x => x.AdjustmentId ?? x.InvoiceId))
            {
                var credit = (read.CreditNotes ?? []).SingleOrDefault(x => x.Id == group.Key);
                var debit = (read.DebitNotes ?? []).SingleOrDefault(x => x.Id == group.Key);
                documents.Add(new(group.Key, id, credit is not null ? "CreditNote" : debit is not null ? "DebitNote" : "Invoice",
                    group.First().BusinessDate, credit?.DocumentNumber ?? debit?.DocumentNumber ?? read.Fulfillment!.DocumentNumber!,
                    group.Sum(x => x.NetAmount), group.Sum(x => x.OutputTaxAmount)));
            }
        }
        var payments = await db.Set<CustomerPayment>().AsNoTracking().Where(x => x.TenantId == actor.TenantId && x.CompanyId == company
            && x.CustomerId == customer && x.PostedAt != null).ToArrayAsync(ct);
        if (payments.Any(x => x.Currency != "BDT")) throw new DomainException("unsupported_currency", "Customer history V1 supports BDT only.", 409);
        var allocations = await db.Set<PaymentAllocation>().AsNoTracking().Where(x => x.TenantId == actor.TenantId && x.CompanyId == company && x.CustomerId == customer).ToArrayAsync(ct);
        var corrections = await new PaymentCorrectionStore(db).Read(actor.TenantId, company, customer, ct);
        var data = new CustomerHistoryDataV1(person.Code, person.Name, person.Version, "BDT",
            Order(invoices, x => x.Id), Order(documents, x => x.Id),
            Order(payments.Select(x => new CustomerHistoryPaymentV1(x.Id, x.Reference, x.PaymentDate, x.Amount)), x => x.Id),
            Order(corrections.Cash.Select(x =>
            {
                var note = corrections.Notes.Single(n => n.Id == x.CorrectionId);
                return new CustomerHistoryCorrectionV1(note.Id, x.PaymentId, note.Mode == "Refund" ? "PaymentRefund" : "ReceiptReversal",
                    x.BusinessDate, note.DocumentNumber!, x.Amount);
            }), x => x.Id),
            Order(allocations.Select(x => new CustomerHistoryAllocationV1(x.Id, x.PaymentId, x.InvoiceId, x.AllocationDate, x.Amount)), x => x.Id),
            Order(corrections.Releases.Select(x => new CustomerHistoryReleaseV1(x.Id, x.CorrectionId, x.AllocationId, x.BusinessDate, x.Amount)), x => x.Id));
        var json = JsonSerializer.Serialize(data, ReportingSourceV1.Json);
        if (CustomerHistoryV1.Count(data) > CustomerHistoryV1.MaxRows || System.Text.Encoding.UTF8.GetByteCount(json) > CustomerHistoryV1.MaxBytes) Limit();
        var manifest = new CustomerHistoryManifestV1(1, Guid.NewGuid(), actor.TenantId, company, customer, CustomerHistoryV1.Scope,
            clock.GetUtcNow(), true, CustomerHistoryV1.Count(data), CustomerHistoryV1.Fingerprint(actor.TenantId, company, customer, data));
        // The existing append-only export store owns frozen source evidence for both scopes.
        db.Add(new ReportingSourceExport { TenantId = actor.TenantId, CompanyId = company, Id = manifest.ExportId,
            ManifestJson = JsonSerializer.Serialize(manifest, ReportingSourceV1.Json), RowsJson = json });
        audit.Append(actor, company, manifest.ExportId, "CustomerHistoryExportPrepared", correlation, reason: reason);
        return new(200, manifest);
    }

    public async Task<CustomerHistoryExportV1> Read(Actor actor, Guid company, Guid customer, Guid id, CancellationToken ct)
    {
        actor.RequireCompany(company);
        var stored = await db.Set<ReportingSourceExport>().AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == actor.TenantId
            && x.CompanyId == company && x.Id == id, ct) ?? throw new DomainException("not_found", "Customer export not found.", 404);
        var manifest = JsonSerializer.Deserialize<CustomerHistoryManifestV1>(stored.ManifestJson, ReportingSourceV1.Json)!;
        if (manifest.Scope != CustomerHistoryV1.Scope || manifest.CustomerId != customer)
            throw new DomainException("not_found", "Customer export not found.", 404);
        return new(manifest, JsonSerializer.Deserialize<CustomerHistoryDataV1>(stored.RowsJson, ReportingSourceV1.Json)!);
    }
    private static T[] Order<T>(IEnumerable<T> rows, Func<T, Guid> id) => rows.OrderBy(x => id(x).ToString("D"), StringComparer.Ordinal).ToArray();
    private static void Limit() => throw new DomainException("export_limit", "Complete customer history exceeds V1 limits; a new contract is required.", 409);
}
