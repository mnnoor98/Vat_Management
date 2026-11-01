using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vat.Application;
using Vat.Application.Security;
using Vat.Domain;
using Vat.Domain.Posting;

namespace Vat.Infrastructure.Persistence;

public sealed record LandedCostInput(Guid PurchaseLineId, string Amount);
public sealed record LandedCostLine(Guid PurchaseLineId, decimal Amount);
public sealed record CreateLandedCostRequest(Guid PlanId, string TotalAmount, string Evidence, IReadOnlyList<LandedCostInput> Lines);
public sealed record LandedCostView(Guid Id, Guid CompanyId, Guid PlanId, string TotalAmount, string State, int Version, string Evidence,
    string CreatedBy, string? ApprovedBy, IReadOnlyList<LandedCostInput> Lines);

public sealed class LandedCostService(VatDbContext db, IPeriodStore periodStore, IPeriodGuard periods, IAuditWriter audit, TimeProvider clock)
{
    public async Task<CommandOutcome> Create(Actor actor, Guid company, CreateLandedCostRequest r, string correlation, CancellationToken ct)
    {
        Require(actor, company, Permissions.CostingManage); Guard.Text(r.Evidence,"Landed charge evidence/zero-charge declaration",1000);
        await periodStore.LockCompany(actor.TenantId,company,true,ct); await CostingStore.LockScope(db,actor.TenantId,company,false,ct);
        var plan = await Plan(actor,company,r.PlanId,ct); await periods.RequireOpen(actor.TenantId,company,plan.Snapshot.BusinessDate,ct);
        if (r.Lines is null || r.Lines.Count != plan.Snapshot.Lines.Count || r.Lines.Select(x => x.PurchaseLineId).Distinct().Count() != r.Lines.Count ||
            r.Lines.Any(x => !plan.Snapshot.Lines.Any(p => p.PurchaseLineId == x.PurchaseLineId)))
            throw new DomainException("landed_allocation_incomplete", "Allocate the complete declared BDT landed cost across every original posting-plan line, including zero allocations.");
        var lines = r.Lines.Select(x => new LandedCostLine(x.PurchaseLineId, Amount(x.Amount))).ToArray(); var total = Amount(r.TotalAmount);
        if (lines.Sum(x => x.Amount) != total) throw new DomainException("landed_allocation_mismatch", "Allocated costs must equal the declared total exactly.");
        var review = new StockLandedReview { TenantId=actor.TenantId,CompanyId=company,Id=Guid.CreateVersion7(),PlanId=r.PlanId,TotalAmount=total,
            Lines=JsonSerializer.Serialize(lines),Evidence=r.Evidence.Trim(),CreatedBy=actor.Subject,CreatedAt=clock.GetUtcNow() };
        db.Add(review); audit.Append(actor,company,review.Id,"LandedCostCreated",correlation,1);
        return new(201,View(review),$"/api/v1/companies/{company}/inventory/landed-cost-reviews/{review.Id}");
    }
    public async Task<CommandOutcome> Approve(Actor actor, Guid company, Guid id, ApproveCostingPolicyRequest r, string correlation, CancellationToken ct)
    {
        Require(actor,company,Permissions.CostingApprove); await periodStore.LockCompany(actor.TenantId,company,true,ct);
        await CostingStore.LockScope(db,actor.TenantId,company,false,ct); var a = await Find(actor,company,id,ct);
        if (a.State!="Draft" || a.Version!=r.ExpectedVersion) throw new DomainException("version_conflict","Reload the landed-cost review.",409);
        if (a.CreatedBy==actor.Subject) throw new DomainException("self_approval_denied","A different user must approve landed-cost allocation.",403);
        var plan = await Plan(actor,company,a.PlanId,ct); await periods.RequireOpen(actor.TenantId,company,plan.Snapshot.BusinessDate,ct);
        if (await db.Set<StockLandedReview>().AnyAsync(x => x.TenantId==actor.TenantId && x.CompanyId==company && x.PlanId==a.PlanId && x.State=="Approved",ct))
            throw new DomainException("landed_cost_already_approved","This posting plan already has an approved allocation.",409);
        a.State="Approved"; a.Version=2; a.ApprovedBy=actor.Subject; a.ApprovedAt=clock.GetUtcNow();
        audit.Append(actor,company,id,"LandedCostApproved",correlation,2); return new(200,View(a));
    }
    public async Task<LandedCostView> Get(Actor actor,Guid company,Guid id,CancellationToken ct)
    { Require(actor,company,Permissions.CostingRead); return View(await Find(actor,company,id,ct)); }
    public async Task<IReadOnlyList<LandedCostView>> List(Actor actor,Guid company,Guid? before,int take,CancellationToken ct)
    {
        Require(actor,company,Permissions.CostingRead); if(take is <1 or >100) throw new DomainException("validation_failed","Require take 1-100.");
        return (await db.Set<StockLandedReview>().AsNoTracking().Where(x=>x.TenantId==actor.TenantId && x.CompanyId==company && (before==null || x.Id.CompareTo(before.Value)<0))
            .OrderByDescending(x=>x.Id).Take(take).ToArrayAsync(ct)).Select(View).ToArray();
    }
    private async Task<StockLandedReview> Find(Actor a,Guid c,Guid id,CancellationToken ct) => await db.Set<StockLandedReview>().SingleOrDefaultAsync(x=>x.TenantId==a.TenantId && x.CompanyId==c && x.Id==id,ct)
        ?? throw new DomainException("not_found","Landed-cost review not found.",404);
    private async Task<PurchasePostingPlan> Plan(Actor a,Guid c,Guid id,CancellationToken ct)
    {
        var p = await db.Set<PurchasePostingPlan>().AsNoTracking().SingleOrDefaultAsync(x=>x.TenantId==a.TenantId && x.CompanyId==c && x.Id==id,ct)
            ?? throw new DomainException("not_found","Posting plan not found.",404);
        if(p.Status!=PostingPlanStatus.Prepared) throw new DomainException("landed_source_not_prepared","Only a prepared, unposted receipt can receive landed cost approval.",409);
        return p;
    }
    private static decimal Amount(string s) => decimal.TryParse(s,NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var v) && v>=0 && v<100000000000000000000m && decimal.Round(v,4)==v
        ? v : throw new DomainException("validation_failed","Require nonnegative BDT decimal strings with at most four places.");
    private static LandedCostView View(StockLandedReview a)=>new(a.Id,a.CompanyId,a.PlanId,a.TotalAmount.ToString("0.0000",CultureInfo.InvariantCulture),a.State,a.Version,a.Evidence,a.CreatedBy,a.ApprovedBy,
        JsonSerializer.Deserialize<LandedCostLine[]>(a.Lines)!.Select(x=>new LandedCostInput(x.PurchaseLineId,x.Amount.ToString("0.0000",CultureInfo.InvariantCulture))).ToArray());
    private static void Require(Actor a,Guid c,string p) { a.RequireCompany(c); if(!a.Permissions.Contains(p)) throw new DomainException("permission_denied","Costing permission required.",403); }
}
