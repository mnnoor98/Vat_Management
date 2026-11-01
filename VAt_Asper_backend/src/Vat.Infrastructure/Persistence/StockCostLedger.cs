using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vat.Domain;
using Vat.Domain.Costing;
using Vat.Domain.Posting;
using Vat.Domain.Sales;

namespace Vat.Infrastructure.Persistence;

// One ledger session per source command, fenced before any physical stock write.
internal sealed class StockCostLedger(VatDbContext db, StockCostHead head, DateTimeOffset now,
    Dictionary<(Guid Warehouse, Guid Product), StockCostPosition> positions)
{
    public static async Task<StockCostLedger?> Begin(VatDbContext db, Guid tenant, Guid company, DateTimeOffset now, CancellationToken ct)
    {
        await CostingStore.LockScope(db, tenant, company, false, ct);
        var head = await db.Set<StockCostHead>().SingleOrDefaultAsync(x => x.TenantId == tenant && x.CompanyId == company, ct);
        if (head is null) return null; // Explicit adoption is required; never invent legacy costs.
        var rows = await db.Set<StockCostPosition>().Where(x => x.TenantId == tenant && x.CompanyId == company).ToArrayAsync(ct);
        var physical = await db.Set<StockBalance>().AsNoTracking().Where(x => x.TenantId == tenant && x.CompanyId == company).ToArrayAsync(ct);
        var map = rows.ToDictionary(x => (x.WarehouseId, x.ProductId));
        var physicalMap = physical.ToDictionary(x => (x.WarehouseId,x.ProductId));
        if (physical.Any(x => x.Quantity != (map.GetValueOrDefault((x.WarehouseId, x.ProductId))?.Quantity ?? 0)) ||
            rows.Any(x => x.Quantity != (physicalMap.GetValueOrDefault((x.WarehouseId,x.ProductId))?.Quantity ?? 0)))
            throw new DomainException("cost_stock_reconciliation_failed", "Physical stock and the active cost ledger disagree; reconcile before posting.", 409);
        return new(db, head, now, map);
    }

    public async Task Receipt(PurchaseCostReceipt receipt, CancellationToken ct)
    {
        if (receipt.Currency != "BDT") throw new DomainException("cost_currency_not_supported", "Active valuation requires reviewed BDT source costs.", 409);
        var p = await Position(receipt.WarehouseId, receipt.ProductId, receipt.BusinessDate, ct);
        decimal landed = 0;
        if (head.Basis == "NetPlusNonrecoverableTaxAndLanded")
        {
            var review = await db.Set<StockLandedReview>().AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == head.TenantId &&
                x.CompanyId == head.CompanyId && x.PlanId == receipt.PlanId && x.State == "Approved", ct)
                ?? throw new DomainException("landed_cost_review_required", "Approve a complete landed-cost allocation (including explicit zero charges) before receipt posting.", 409);
            landed = JsonSerializer.Deserialize<LandedCostLine[]>(review.Lines)!.Single(x => x.PurchaseLineId == receipt.PurchaseLineId).Amount;
        }
        var value = receipt.NetAmount + (head.Basis != "PurchaseNetOnly" ? receipt.TaxAmount - receipt.RecoverableTaxAmount + landed : 0);
        var chunks = new[] { new CostChunk(Guid.NewGuid(), receipt.SourceMovementId, receipt.Quantity, value) };
        var state = State(p); state.Add(chunks); Save(p, state);
        Append(p, "Receipt", receipt.SourceMovementId, null, receipt.BusinessDate, receipt.Quantity, value, chunks);
    }

    public async Task Issue(SalesPostingEntry source, CancellationToken ct)
    {
        var p = await Position(source.WarehouseId, source.ProductId, source.BusinessDate, ct);
        var state = State(p); var chunks = state.Take(-source.Quantity); Save(p, state);
        Append(p, "Issue", source.Id, null, source.BusinessDate, source.Quantity, -chunks.Sum(x => x.Value), chunks);
    }

    public async Task Return(SalesPostingEntry source, CancellationToken ct)
    {
        if (source.Quantity == 0) return; // Price-only sales adjustments do not move inventory cost.
        var original = await db.Set<StockCostEntry>().AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == head.TenantId &&
            x.CompanyId == head.CompanyId && x.Kind == "Issue" && x.SourceId == source.ReversesEntryId, ct)
            ?? throw new DomainException("cost_original_issue_missing", "This return predates cost adoption; reviewed original issue cost evidence is required.", 409);
        var remaining = JsonSerializer.Deserialize<List<CostChunk>>(original.Allocations)!;
        var earlier = await db.Set<StockCostEntry>().AsNoTracking().Where(x => x.TenantId == head.TenantId && x.CompanyId == head.CompanyId &&
            x.Kind == "Return" && x.OriginalSourceId == original.SourceId).OrderBy(x => x.Sequence).ToArrayAsync(ct);
        foreach (var prior in earlier.Concat(db.Set<StockCostEntry>().Local.Where(x => x.Kind == "Return" && x.OriginalSourceId == original.SourceId && db.Entry(x).State == EntityState.Added)))
            _ = StockCostState.Consume(remaining, prior.Quantity);
        var chunks = StockCostState.Consume(remaining, source.Quantity);
        var p = await Position(source.WarehouseId, source.ProductId, source.BusinessDate, ct);
        var state = State(p); state.Add(chunks); Save(p, state);
        Append(p, "Return", source.Id, original.SourceId, source.BusinessDate, source.Quantity, chunks.Sum(x => x.Value), chunks);
    }

    public async Task Reverse(StockMovement source, CancellationToken ct)
    {
        var original = await db.Set<StockCostEntry>().AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == head.TenantId && x.CompanyId == head.CompanyId &&
            x.Kind == "Receipt" && x.SourceId == source.ReversesEntryId, ct)
            ?? throw new DomainException("cost_original_receipt_missing", "This reversal predates cost adoption; original receipt cost evidence is required.", 409);
        var p = await Position(source.WarehouseId, source.ProductId, source.BusinessDate, ct);
        var state = State(p); var chunks = state.ReverseReceipt(original.SourceId, original.Quantity, original.Value); Save(p, state);
        Append(p, "Reversal", source.Id, original.SourceId, source.BusinessDate, -original.Quantity, -original.Value, chunks);
    }

    public async Task Transfer(Vat.Domain.Inventory.TransferJournalEntry source, CancellationToken ct)
    {
        var from = await Position(source.FromWarehouseId, source.ProductId, source.BusinessDate, ct);
        var to = await Position(source.ToWarehouseId, source.ProductId, source.BusinessDate, ct);
        if (from.Method != to.Method) throw new DomainException("cost_transfer_method_mismatch", "Warehouse cost methods must agree before transfer.", 409);
        var fromState = State(from); var chunks = fromState.Take(source.Quantity); Save(from, fromState);
        var toState = State(to); toState.Add(chunks); Save(to, toState);
        var value = chunks.Sum(x => x.Value);
        Append(from, "TransferOut", source.Id, null, source.BusinessDate, -source.Quantity, -value, chunks);
        Append(to, "TransferIn", source.Id, null, source.BusinessDate, source.Quantity, value, chunks);
    }

    public async Task<decimal> Production(ProductionStockMovement source,CancellationToken ct)
    {
        var p=await Position(source.WarehouseId,source.ProductId,source.BusinessDate,ct);var state=State(p);IReadOnlyList<CostChunk> chunks;
        if(source.Kind=="ReceiptReversal")chunks=state.ReverseReceipt(source.OriginalIssueId!.Value,-source.Quantity,-source.Value);
        else if(source.Kind=="ReturnReversal"){
            var original=await db.Set<StockCostEntry>().AsNoTracking().SingleOrDefaultAsync(x=>x.TenantId==head.TenantId&&x.CompanyId==head.CompanyId&&x.Kind=="ProductionReturn"&&x.SourceId==source.OriginalIssueId,ct)
                ??throw new DomainException("production_cost_return_missing","Original production return cost allocation is missing.",409);
            chunks=state.ReverseReturn(JsonSerializer.Deserialize<List<CostChunk>>(original.Allocations)!);
        }
        else if(source.Kind=="Issue")chunks=state.Take(-source.Quantity);
        else if(source.Kind=="Return"){
            var original=await db.Set<StockCostEntry>().AsNoTracking().SingleOrDefaultAsync(x=>x.TenantId==head.TenantId&&x.CompanyId==head.CompanyId&&x.Kind=="ProductionIssue"&&x.SourceId==source.OriginalIssueId,ct)
                ??throw new DomainException("production_cost_issue_missing","Original production cost allocation is missing.",409);
            var remaining=JsonSerializer.Deserialize<List<CostChunk>>(original.Allocations)!;
            var prior=await db.Set<StockCostEntry>().AsNoTracking().Where(x=>x.TenantId==head.TenantId&&x.CompanyId==head.CompanyId&&x.Kind=="ProductionReturn"&&x.OriginalSourceId==original.SourceId).OrderBy(x=>x.Sequence).ToArrayAsync(ct);
            foreach(var r in prior.Concat(db.Set<StockCostEntry>().Local.Where(x=>x.Kind=="ProductionReturn"&&x.OriginalSourceId==original.SourceId&&db.Entry(x).State==EntityState.Added)))_=StockCostState.Consume(remaining,r.Quantity);
            chunks=StockCostState.Consume(remaining,source.Quantity);state.Add(chunks);
        }else{chunks=[new(Guid.NewGuid(),source.Id,source.Quantity,source.Value)];state.Add(chunks);}
        Save(p,state);var value=chunks.Sum(x=>x.Value)*(source.Quantity<0?-1:1);
        Append(p,"Production"+source.Kind,source.Id,source.OriginalIssueId,source.BusinessDate,source.Quantity,value,chunks);return value;
    }

    private async Task<StockCostPosition> Position(Guid warehouse, Guid product, DateOnly date, CancellationToken ct)
    {
        if (date < head.OpeningDate) throw new DomainException("cost_before_adoption", "Posting before the approved cost opening date is not permitted.", 409);
        var policy = await new CostingStore(db).Resolve(head.TenantId, head.CompanyId, product, date, ct)
            ?? throw new DomainException("cost_policy_unassigned", "An approved product/company costing policy is required for active valuation.", 409);
        if (!positions.TryGetValue((warehouse, product), out var p))
        {
            p = new() { TenantId = head.TenantId, CompanyId = head.CompanyId, WarehouseId = warehouse, ProductId = product, Method = policy.Method };
            db.Add(p); positions.Add((warehouse, product), p);
        }
        if (p.Method != policy.Method)
        {
            if (p.Quantity != 0) throw new DomainException("cost_policy_conversion_required", "Existing stock requires a reviewed cost-method conversion before posting.", 409);
            p.Method = policy.Method;
        }
        return p;
    }

    internal static StockCostState State(StockCostPosition p)
    {
        var state = new StockCostState { Method = p.Method, Quantity = p.Quantity, Value = p.Value, Layers = JsonSerializer.Deserialize<List<CostChunk>>(p.Layers)! };
        state.Check(); return state;
    }
    internal static void Save(StockCostPosition p, StockCostState state)
    { p.Quantity = state.Quantity; p.Value = state.Value; p.Layers = JsonSerializer.Serialize(state.Layers); }
    private void Append(StockCostPosition p, string kind, Guid source, Guid? original, DateOnly date, decimal quantity, decimal value, IReadOnlyList<CostChunk> chunks)
    {
        db.Add(new StockCostEntry { TenantId = head.TenantId, CompanyId = head.CompanyId, Sequence = ++head.Sequence,
            WarehouseId = p.WarehouseId, ProductId = p.ProductId, Kind = kind, SourceId = source, OriginalSourceId = original,
            BusinessDate = date, Method = p.Method, Quantity = quantity, Value = value, Allocations = JsonSerializer.Serialize(chunks), CreatedAt = now });
    }
}
