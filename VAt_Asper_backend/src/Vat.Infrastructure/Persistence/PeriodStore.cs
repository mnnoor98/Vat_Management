using Vat.Domain.Sales;
using Vat.Domain.Inventory;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Vat.Application;
using Vat.Domain.Company;
using Vat.Domain.Posting;

namespace Vat.Infrastructure.Persistence;

public sealed class PeriodStore(VatDbContext db) : IPeriodStore
{
    public async Task LockCompany(Guid tenantId, Guid companyId, bool exclusive, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Period fencing requires a transaction.");
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"financial-period:{tenantId}:{companyId}"));
        var key = BinaryPrimitives.ReadInt64BigEndian(hash);
        if (exclusive) await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})", ct);
        else await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock_shared({key})", ct);
    }

    public Task<FiscalPeriod?> Find(Guid tenantId, Guid companyId, Guid id, CancellationToken ct) => db.Set<FiscalPeriod>()
        .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.CompanyId == companyId && x.Id == id, ct);

    public Task<FiscalPeriod?> ForDate(Guid tenantId, Guid companyId, DateOnly date, CancellationToken ct) => db.Set<FiscalPeriod>()
        .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.CompanyId == companyId && x.From <= date && date < x.Until, ct);

    public Task<bool> Overlaps(FiscalPeriod period, CancellationToken ct) => db.Set<FiscalPeriod>().AnyAsync(x =>
        x.TenantId == period.TenantId && x.CompanyId == period.CompanyId && x.From < period.Until && period.From < x.Until, ct);

    public async Task<bool> HasPending(FiscalPeriod period, CancellationToken ct)
    {
        var tenant = period.TenantId; var company = period.CompanyId; var from = period.From; var until = period.Until;
        if(await db.Set<ProductionCostOperation>().AnyAsync(x=>x.TenantId==tenant&&x.CompanyId==company&&x.BusinessDate>=from&&x.BusinessDate<until&&x.State=="Draft",ct))return true;
        if (await db.Purchases.AnyAsync(x => x.TenantId == tenant && x.CompanyId == company && x.BusinessDate >= from && x.BusinessDate < until &&
            (x.Status == Vat.Domain.Purchasing.PurchaseStatus.ApprovedPendingPosting || x.Status == Vat.Domain.Purchasing.PurchaseStatus.PostingPrepared || x.Status == Vat.Domain.Purchasing.PurchaseStatus.ReversalPending), ct)) return true;
        if (await db.Set<PurchaseReversal>().AnyAsync(x => x.TenantId == tenant && x.CompanyId == company && x.CorrectionDate >= from && x.CorrectionDate < until && x.Status == ReversalStatus.Pending, ct)) return true;
        if (await db.SalesInvoices.AnyAsync(x => x.TenantId == tenant && x.CompanyId == company && x.BusinessDate >= from && x.BusinessDate < until &&
            (x.Status == SalesInvoiceStatus.ApprovedPendingPosting || x.Status == SalesInvoiceStatus.Reserved || x.Status == SalesInvoiceStatus.CreditPending || x.Status == SalesInvoiceStatus.DebitPending), ct)) return true;
        if (await db.Set<SalesCreditNote>().AnyAsync(x => x.TenantId == tenant && x.CompanyId == company && x.CorrectionDate >= from && x.CorrectionDate < until && x.Status == "Pending", ct)) return true;
        if (await db.Set<SalesDebitNote>().AnyAsync(x => x.TenantId == tenant && x.CompanyId == company && x.CorrectionDate >= from && x.CorrectionDate < until && x.Status == "Pending", ct)) return true;
        if (await db.Set<CustomerPayment>().AnyAsync(x => x.TenantId == tenant && x.CompanyId == company && x.PaymentDate >= from && x.PaymentDate < until && x.Status == "Pending", ct)) return true;
        if (await db.Set<PaymentCorrection>().AnyAsync(x => x.TenantId == tenant && x.CompanyId == company && x.CorrectionDate >= from && x.CorrectionDate < until && x.Status == "Pending", ct)) return true;
        return await db.Set<Dictionary<string, object>>("inventory.transfers").AnyAsync(x =>
            EF.Property<Guid>(x, "TenantId") == tenant && EF.Property<Guid>(x, "CompanyId") == company &&
            EF.Property<DateOnly>(x, "BusinessDate") >= from && EF.Property<DateOnly>(x, "BusinessDate") < until &&
            (EF.Property<string>(x, "State") == "Submitted" || EF.Property<string>(x, "State") == "Approved"), ct);
    }

    public async Task<IReadOnlyList<Guid>> FinancialPurchaseIds(FiscalPeriod period, CancellationToken ct)
    {
        var source = await db.Purchases.Where(x => x.TenantId == period.TenantId && x.CompanyId == period.CompanyId &&
            x.BusinessDate >= period.From && x.BusinessDate < period.Until && x.PostedVersion != null).Select(x => x.Id).ToArrayAsync(ct);
        var entries = await db.Set<TaxLedgerEntry>().Where(x => x.TenantId == period.TenantId && x.CompanyId == period.CompanyId &&
            x.BusinessDate >= period.From && x.BusinessDate < period.Until).Select(x => x.PurchaseId).Distinct().ToArrayAsync(ct);
        var stock = await db.Set<StockMovement>().Where(x => x.TenantId == period.TenantId && x.CompanyId == period.CompanyId &&
            x.BusinessDate >= period.From && x.BusinessDate < period.Until).Select(x => x.PurchaseId).Distinct().ToArrayAsync(ct);
        return source.Concat(entries).Concat(stock).Distinct().Order().ToArray();
    }

    public async Task<IReadOnlyList<Guid>> FinancialSalesIds(FiscalPeriod period, CancellationToken ct)
    {
        var source = await db.SalesInvoices.Where(x => x.TenantId == period.TenantId && x.CompanyId == period.CompanyId &&
            x.BusinessDate >= period.From && x.BusinessDate < period.Until && x.PostedVersion != null).Select(x => x.Id).ToArrayAsync(ct);
        var entries = await db.Set<SalesPostingEntry>().Where(x => x.TenantId == period.TenantId && x.CompanyId == period.CompanyId &&
            x.BusinessDate >= period.From && x.BusinessDate < period.Until).Select(x => x.InvoiceId).Distinct().ToArrayAsync(ct);
        return source.Concat(entries).Distinct().Order().ToArray();
    }

    public async Task<bool> BalancesReconcile(Guid tenantId, Guid companyId, CancellationToken ct)
    {
        var expected = new Dictionary<(Guid Warehouse, Guid Product), decimal>();
        void Add(Guid warehouse, Guid product, decimal quantity) => expected[(warehouse, product)] = expected.GetValueOrDefault((warehouse, product)) + quantity;
        var receipts = await db.Set<StockMovement>().AsNoTracking().Where(x => x.TenantId == tenantId && x.CompanyId == companyId)
            .GroupBy(x => new { x.WarehouseId, x.ProductId }).Select(g => new { g.Key.WarehouseId, g.Key.ProductId, Quantity = g.Sum(x => x.Quantity) }).ToArrayAsync(ct);
        foreach (var row in receipts) Add(row.WarehouseId, row.ProductId, row.Quantity);
        var sales = await db.Set<SalesPostingEntry>().AsNoTracking().Where(x => x.TenantId == tenantId && x.CompanyId == companyId)
            .GroupBy(x => new { x.WarehouseId, x.ProductId }).Select(g => new { g.Key.WarehouseId, g.Key.ProductId, Quantity = g.Sum(x => x.Quantity) }).ToArrayAsync(ct);
        foreach (var row in sales) Add(row.WarehouseId, row.ProductId, row.Quantity);
        var transfers = await db.Set<TransferJournalEntry>().AsNoTracking().Where(x => x.TenantId == tenantId && x.CompanyId == companyId).OrderBy(x => x.Id).ToArrayAsync(ct);
        foreach (var row in transfers) { Add(row.FromWarehouseId, row.ProductId, -row.Quantity); Add(row.ToWarehouseId, row.ProductId, row.Quantity); }
        var production=await db.Set<ProductionStockMovement>().AsNoTracking().Where(x=>x.TenantId==tenantId&&x.CompanyId==companyId).GroupBy(x=>new{x.WarehouseId,x.ProductId}).Select(g=>new{g.Key.WarehouseId,g.Key.ProductId,Quantity=g.Sum(x=>x.Quantity)}).ToArrayAsync(ct);
        foreach(var row in production)Add(row.WarehouseId,row.ProductId,row.Quantity);
        var reservations = new Dictionary<(Guid Warehouse, Guid Product), decimal>();
        var reserved = await db.Set<SalesFulfillment>().AsNoTracking().Where(x => x.TenantId == tenantId && x.CompanyId == companyId && x.Status == "Reserved").ToArrayAsync(ct);
        foreach (var plan in reserved)
            foreach (var line in plan.Snapshot.Lines) reservations[(plan.WarehouseId, line.ProductId)] = reservations.GetValueOrDefault((plan.WarehouseId, line.ProductId)) + line.Quantity;
        var balances = await db.Set<StockBalance>().AsNoTracking().Where(x => x.TenantId == tenantId && x.CompanyId == companyId).ToArrayAsync(ct);
        foreach (var row in balances)
        {
            var key = (row.WarehouseId, row.ProductId);
            if (row.Quantity != expected.GetValueOrDefault(key) || row.ReservedQuantity != reservations.GetValueOrDefault(key) ||
                row.Quantity < 0 || row.ReservedQuantity < 0 || row.ReservedQuantity > row.Quantity) return false;
            expected.Remove(key); reservations.Remove(key);
        }
        if(await db.Set<StockCostHead>().AnyAsync(x=>x.TenantId==tenantId&&x.CompanyId==companyId,ct)&&await new FinancialInventoryQueries(db).CostAvailability(new(tenantId,"period-reconciliation",new HashSet<Guid>{companyId},new HashSet<string>()),companyId,ct)!=null)return false;
        return expected.Count == 0 && reservations.Count == 0 && await new PaymentStore(db).Reconciles(tenantId, companyId, ct);
    }

    public async Task<PeriodCloseSnapshot> Snapshot(FiscalPeriod period, DateTimeOffset now, CancellationToken ct)
    {
        var tenant = period.TenantId; var company = period.CompanyId; var from = period.From; var until = period.Until;
        var stock = await db.Set<StockMovement>().AsNoTracking().Where(x => x.TenantId == tenant && x.CompanyId == company && x.BusinessDate >= from && x.BusinessDate < until).OrderBy(x => x.Id).ToArrayAsync(ct);
        var tax = await db.Set<TaxLedgerEntry>().AsNoTracking().Where(x => x.TenantId == tenant && x.CompanyId == company && x.BusinessDate >= from && x.BusinessDate < until).OrderBy(x => x.Id).ToArrayAsync(ct);
        var sales = await db.Set<SalesPostingEntry>().AsNoTracking().Where(x => x.TenantId == tenant && x.CompanyId == company && x.BusinessDate >= from && x.BusinessDate < until).OrderBy(x => x.Id).ToArrayAsync(ct);
        var payments = await db.Set<CustomerPayment>().AsNoTracking().Where(x => x.TenantId == tenant && x.CompanyId == company && x.PaymentDate >= from && x.PaymentDate < until && x.PostedAt != null).OrderBy(x => x.Id).ToArrayAsync(ct);
        var allocations = await db.Set<PaymentAllocation>().AsNoTracking().Where(x => x.TenantId == tenant && x.CompanyId == company && x.AllocationDate >= from && x.AllocationDate < until).OrderBy(x => x.Id).ToArrayAsync(ct);
        var cash = await db.Set<PaymentCashAdjustment>().AsNoTracking().Where(x => x.TenantId == tenant && x.CompanyId == company && x.BusinessDate >= from && x.BusinessDate < until).OrderBy(x => x.Id).ToArrayAsync(ct);
        var releases = await db.Set<PaymentAllocationRelease>().AsNoTracking().Where(x => x.TenantId == tenant && x.CompanyId == company && x.BusinessDate >= from && x.BusinessDate < until).OrderBy(x => x.Id).ToArrayAsync(ct);
        var transfers = await db.Set<TransferJournalEntry>().AsNoTracking().Where(x => x.TenantId == tenant && x.CompanyId == company && x.BusinessDate >= from && x.BusinessDate < until).OrderBy(x => x.Id).ToArrayAsync(ct);
        var purchaseIds = await FinancialPurchaseIds(period, ct); var salesIds = await FinancialSalesIds(period, ct);
        var productionOperations=await db.Set<ProductionCostOperation>().AsNoTracking().Where(x=>x.TenantId==tenant&&x.CompanyId==company&&x.BusinessDate>=from&&x.BusinessDate<until&&x.State=="Posted").OrderBy(x=>x.Id).ToArrayAsync(ct);
        var productionStock=await db.Set<ProductionStockMovement>().AsNoTracking().Where(x=>x.TenantId==tenant&&x.CompanyId==company&&x.BusinessDate>=from&&x.BusinessDate<until).OrderBy(x=>x.Id).ToArrayAsync(ct);
        var wip=await db.Set<ProductionCostSession>().AsNoTracking().Where(x=>x.TenantId==tenant&&x.CompanyId==company).OrderBy(x=>x.Id).Select(x=>new ProductionPeriodCheckpoint(x.Id,x.OrderId,x.Version,x.WipValue,x.ReceivedQuantity)).ToArrayAsync(ct);
        var fingerprint = Convert.ToHexString(SHA256.HashData(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new
        { period.Id, period.TenantId, period.CompanyId, period.From, period.Until, stock, tax, sales,
            payments = payments.Select(x => new { x.Id, x.CustomerId, x.Reference, x.PaymentDate, x.Amount, x.Currency }), allocations, cash, releases, transfers,productionOperations,productionStock,wip })));
        return new(period.Id, from, until, now, purchaseIds, stock.Select(x => x.Id).ToArray(), tax.Select(x => x.Id).ToArray(),
            tax.Sum(x => x.NetAmount), tax.Sum(x => x.TaxAmount), tax.Sum(x => x.RecoverableTaxAmount), fingerprint,
            new SalesPeriodCheckpoint(salesIds, sales.Select(x => x.Id).ToArray(), sales.Sum(x => x.NetAmount), sales.Sum(x => x.OutputTaxAmount)),
            new PaymentPeriodCheckpoint(payments.Select(x => x.Id).ToArray(), allocations.Select(x => x.Id).ToArray(), payments.Sum(x => x.Amount),
                allocations.Sum(x => x.Amount), cash.Select(x => x.Id).ToArray(), releases.Select(x => x.Id).ToArray(), -cash.Sum(x => x.Amount), releases.Sum(x => x.Amount)),
            transfers.Select(x => x.Id).ToArray(),productionOperations.Select(x=>x.Id).ToArray(),productionStock.Select(x=>x.Id).ToArray(),wip);
    }

    public async Task<IReadOnlyList<FiscalPeriod>> List(Guid tenantId, Guid companyId, Guid? before, int take, CancellationToken ct)
    {
        var query = db.Set<FiscalPeriod>().Where(x => x.TenantId == tenantId && x.CompanyId == companyId);
        if (before is not null)
        {
            var cursor = await Find(tenantId, companyId, before.Value, ct);
            if (cursor is not null) query = query.Where(x => x.From < cursor.From);
        }
        return await query.OrderByDescending(x => x.From).Take(take).ToArrayAsync(ct);
    }

    public void Add(FiscalPeriod period) => db.Set<FiscalPeriod>().Add(period);
}
