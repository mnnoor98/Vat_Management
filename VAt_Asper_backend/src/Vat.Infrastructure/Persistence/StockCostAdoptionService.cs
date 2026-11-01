using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vat.Application;
using Vat.Application.Security;
using Vat.Domain;
using Vat.Domain.Costing;
using Vat.Domain.Inventory;
using Vat.Domain.Posting;
using Vat.Domain.Sales;

namespace Vat.Infrastructure.Persistence;

public sealed record CostOpeningInput(Guid WarehouseId, Guid ProductId, string Quantity, string Value, string? Method = null);
public sealed record CreateCostAdoptionRequest(DateOnly BusinessDate, string Basis, string Evidence, IReadOnlyList<CostOpeningInput> Lines);
public sealed record CostOpeningLine(Guid WarehouseId, Guid ProductId, decimal Quantity, decimal Value, string Method, Guid PolicyId);
public sealed record CostAdoptionView(Guid Id, Guid CompanyId, DateOnly BusinessDate, string Basis, string State, int Version,
    string Evidence, string CreatedBy, string? ApprovedBy, IReadOnlyList<CostOpeningInput> Lines);
public sealed record CostLedgerStatus(Guid CompanyId, string State, string? Basis, DateOnly? OpeningDate, string? Sequence,
    string? Reason, string? StockValue);

public sealed class StockCostAdoptionService(VatDbContext db, IPeriodGuard periods, IPeriodStore periodStore, IAuditWriter audit, TimeProvider clock)
{
    public async Task<CommandOutcome> Create(Actor actor, Guid company, CreateCostAdoptionRequest request, string correlation, CancellationToken ct)
    {
        Require(actor, company, Permissions.CostingManage);
        if (request.BusinessDate == default || request.Basis is not ("PurchaseNetOnly" or "NetPlusNonrecoverableTaxAndLanded") || request.Lines is null || request.Lines.Count > 10000)
            throw new DomainException("validation_failed", "Require an opening date, an explicit cost basis and at most 10000 complete opening positions.");
        Guard.Text(request.Evidence, "Opening evidence/reconciliation reference", 1000);
        await periodStore.LockCompany(actor.TenantId, company, true, ct);
        await periods.RequireOpen(actor.TenantId, company, request.BusinessDate, ct);
        await CostingStore.LockScope(db, actor.TenantId, company, false, ct);
        if (!await db.Companies.AnyAsync(x => x.TenantId == actor.TenantId && x.Id == company, ct)) throw new DomainException("not_found", "Company not found.", 404);
        await RequireInactive(actor, company, ct);
        var balances = await Physical(actor, company, ct);
        if (request.Lines.Count != balances.Count(x => x.Quantity != 0) || request.Lines.Select(x => (x.WarehouseId,x.ProductId)).Distinct().Count() != request.Lines.Count)
            throw new DomainException("cost_opening_incomplete", "Provide exactly one reviewed cost for every nonzero warehouse/product position.", 409);
        var lines = new List<CostOpeningLine>();
        foreach (var line in request.Lines)
        {
            if (!decimal.TryParse(line.Quantity, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var quantity) ||
                !decimal.TryParse(line.Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value) || quantity <= 0 || value < 0 ||
                quantity >= 1000000000000000000m || value >= 100000000000000000000m || decimal.Round(quantity,6) != quantity || decimal.Round(value,4) != value)
                throw new DomainException("validation_failed", "Opening quantity/value must be exact nonnegative decimal strings with scale 6/4.");
            if (!balances.Any(x => x.WarehouseId == line.WarehouseId && x.ProductId == line.ProductId && x.Quantity == quantity))
                throw new DomainException("cost_opening_quantity_mismatch", "Opening quantity must equal current physical on hand, including reserved stock.", 409);
            var policy = await new CostingStore(db).Resolve(actor.TenantId, company, line.ProductId, request.BusinessDate, ct)
                ?? throw new DomainException("cost_policy_unassigned", "Approve an opening-date policy for every opening product before adoption.", 409);
            lines.Add(new(line.WarehouseId, line.ProductId, quantity, value, policy.Method, policy.Id));
        }
        var adoption = new StockCostAdoption { TenantId = actor.TenantId, CompanyId = company, Id = Guid.CreateVersion7(), BusinessDate = request.BusinessDate,
            Basis = request.Basis, Evidence = request.Evidence.Trim(), Snapshot = await Snapshot(actor, company, balances, ct),
            Lines = JsonSerializer.Serialize(lines), CreatedBy = actor.Subject, CreatedAt = clock.GetUtcNow() };
        db.Add(adoption); audit.Append(actor, company, adoption.Id, "CostOpeningCreated", correlation, 1);
        return new(201, View(adoption), $"/api/v1/companies/{company}/inventory/cost-adoptions/{adoption.Id}");
    }

    public async Task<CommandOutcome> Approve(Actor actor, Guid company, Guid id, ApproveCostingPolicyRequest request, string correlation, CancellationToken ct)
    {
        Require(actor, company, Permissions.CostingApprove);
        await periodStore.LockCompany(actor.TenantId, company, true, ct);
        await CostingStore.LockScope(db, actor.TenantId, company, false, ct);
        var a = await Find(actor, company, id, ct);
        if (a.State != "Draft" || a.Version != request.ExpectedVersion) throw new DomainException("version_conflict", "Reload the opening draft.", 409);
        if (a.CreatedBy == actor.Subject) throw new DomainException("self_approval_denied", "A different user must approve opening stock costs.", 403);
        await periods.RequireOpen(actor.TenantId, company, a.BusinessDate, ct); await RequireInactive(actor, company, ct);
        var balances = await Physical(actor, company, ct);
        if (a.Snapshot != await Snapshot(actor, company, balances, ct))
            throw new DomainException("cost_opening_stale", "Stock activity changed after opening preparation; prepare a new reviewed draft.", 409);
        var lines = JsonSerializer.Deserialize<CostOpeningLine[]>(a.Lines)!;
        var head = new StockCostHead { TenantId = actor.TenantId, CompanyId = company, AdoptionId = a.Id, OpeningDate = a.BusinessDate, Basis = a.Basis };
        db.Add(head); var now = clock.GetUtcNow();
        foreach (var line in lines.OrderBy(x => x.WarehouseId).ThenBy(x => x.ProductId))
        {
            var policy = await new CostingStore(db).Resolve(actor.TenantId, company, line.ProductId, a.BusinessDate, ct);
            if (policy?.Id != line.PolicyId || policy.Method != line.Method) throw new DomainException("cost_opening_policy_changed", "The reviewed opening policy changed; prepare a new draft.", 409);
            var chunks = new[] { new CostChunk(Guid.NewGuid(), a.Id, line.Quantity, line.Value) };
            var state = new StockCostState { Method = line.Method }; state.Add(chunks);
            var p = new StockCostPosition { TenantId = actor.TenantId, CompanyId = company, WarehouseId = line.WarehouseId, ProductId = line.ProductId, Method = line.Method };
            StockCostLedger.Save(p, state); db.Add(p);
            db.Add(new StockCostEntry { TenantId = actor.TenantId, CompanyId = company, Sequence = ++head.Sequence, WarehouseId = p.WarehouseId, ProductId = p.ProductId,
                Kind = "Opening", SourceId = a.Id, BusinessDate = a.BusinessDate, Method = line.Method, Quantity = line.Quantity, Value = line.Value,
                Allocations = JsonSerializer.Serialize(chunks), CreatedAt = now });
        }
        a.State = "Approved"; a.Version = 2; a.ApprovedBy = actor.Subject; a.ApprovedAt = now;
        audit.Append(actor, company, a.Id, "CostOpeningApproved", correlation, 2);
        return new(200, View(a));
    }

    public async Task<CostAdoptionView> Get(Actor actor, Guid company, Guid id, CancellationToken ct)
    { Require(actor, company, Permissions.CostingRead); return View(await Find(actor, company, id, ct)); }
    public async Task<IReadOnlyList<CostAdoptionView>> List(Actor actor, Guid company, Guid? before, int take, CancellationToken ct)
    {
        Require(actor, company, Permissions.CostingRead);
        if (take is < 1 or > 100) throw new DomainException("validation_failed", "Require take 1-100.");
        var rows = await db.Set<StockCostAdoption>().AsNoTracking().Where(x => x.TenantId == actor.TenantId && x.CompanyId == company &&
            (before == null || x.Id.CompareTo(before.Value) < 0)).OrderByDescending(x => x.Id).Take(take).ToArrayAsync(ct);
        return rows.Select(View).ToArray();
    }
    private async Task<StockCostAdoption> Find(Actor actor, Guid company, Guid id, CancellationToken ct) => await db.Set<StockCostAdoption>()
        .SingleOrDefaultAsync(x => x.TenantId == actor.TenantId && x.CompanyId == company && x.Id == id, ct)
        ?? throw new DomainException("not_found", "Cost opening not found.", 404);
    private async Task RequireInactive(Actor actor, Guid company, CancellationToken ct)
    { if (await db.Set<StockCostHead>().AnyAsync(x => x.TenantId == actor.TenantId && x.CompanyId == company, ct)) throw new DomainException("cost_ledger_already_active", "The approved cost ledger cannot be reset or readopted.", 409); }
    private Task<StockBalance[]> Physical(Actor actor, Guid company, CancellationToken ct) => db.Set<StockBalance>().AsNoTracking()
        .Where(x => x.TenantId == actor.TenantId && x.CompanyId == company).OrderBy(x => x.WarehouseId).ThenBy(x => x.ProductId).ToArrayAsync(ct);
    private async Task<string> Snapshot(Actor actor, Guid company, StockBalance[] balances, CancellationToken ct)
    {
        var stocks = await db.Set<StockMovement>().LongCountAsync(x => x.TenantId == actor.TenantId && x.CompanyId == company, ct);
        var sales = await db.Set<SalesPostingEntry>().LongCountAsync(x => x.TenantId == actor.TenantId && x.CompanyId == company && x.Quantity != 0, ct);
        var transfers = await db.Set<TransferJournalEntry>().LongCountAsync(x => x.TenantId == actor.TenantId && x.CompanyId == company, ct);
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { balances = balances.Select(x => new { x.WarehouseId,x.ProductId,x.Quantity }), stocks,sales,transfers })));
    }
    private static CostAdoptionView View(StockCostAdoption a) => new(a.Id,a.CompanyId,a.BusinessDate,a.Basis,a.State,a.Version,a.Evidence,a.CreatedBy,a.ApprovedBy,
        JsonSerializer.Deserialize<CostOpeningLine[]>(a.Lines)!.Select(x => new CostOpeningInput(x.WarehouseId,x.ProductId,x.Quantity.ToString("0.000000",CultureInfo.InvariantCulture),x.Value.ToString("0.0000",CultureInfo.InvariantCulture),x.Method)).ToArray());
    private static void Require(Actor actor, Guid company, string permission)
    { actor.RequireCompany(company); if (!actor.Permissions.Contains(permission)) throw new DomainException("permission_denied", "Costing permission required.", 403); }
}
