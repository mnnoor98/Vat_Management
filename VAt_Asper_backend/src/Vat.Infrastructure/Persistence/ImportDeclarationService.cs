using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vat.Application;
using Vat.Application.Security;
using Vat.Domain;
using Vat.Domain.Posting;

namespace Vat.Infrastructure.Persistence;

public sealed record ImportChargeInput(string Kind,string Amount,string Evidence,bool Capitalizable);
public sealed record CreateImportDeclarationRequest(Guid PlanId,string DeclarationNumber,DateOnly DeclarationDate,string Currency,
    string ForeignInvoiceAmount,string ExchangeRate,string RecoverableTaxAmount,string NonrecoverableTaxAmount,string Evidence,IReadOnlyList<ImportChargeInput> Charges);
public sealed record ImportDeclarationView(Guid Id,Guid CompanyId,Guid PlanId,string DeclarationNumber,DateOnly DeclarationDate,string Currency,
    string ForeignInvoiceAmount,string ExchangeRate,string FunctionalInvoiceAmount,string RecoverableTaxAmount,string NonrecoverableTaxAmount,
    string CapitalizableChargeAmount,string Evidence,IReadOnlyList<ImportChargeInput> Charges,string State,int Version,string CreatedBy,string? ApprovedBy);

public sealed class ImportDeclarationService(VatDbContext db,IPeriodStore periodStore,IPeriodGuard periods,IAuditWriter audit,TimeProvider clock)
{
    private static readonly string[] Kinds=["Duty","Freight","Insurance","Port","Clearing","Other"];

    public async Task<CommandOutcome> Create(Actor actor,Guid company,CreateImportDeclarationRequest r,string correlation,CancellationToken ct)
    {
        Require(actor,company,Permissions.CostingManage); Guard.Text(r.DeclarationNumber,"Declaration number",100); Guard.Text(r.Evidence,"Import evidence",1000);
        if(r.Charges is null||r.Charges.Count>100||r.Charges.Any(x=>!Kinds.Contains(x.Kind,StringComparer.Ordinal)||string.IsNullOrWhiteSpace(x.Evidence)||x.Evidence.Length>500))
            throw new DomainException("validation_failed","Require at most 100 classified charges with evidence.");
        var currency=(r.Currency??"").Trim().ToUpperInvariant(); if(currency.Length!=3||currency.Any(x=>x<'A'||x>'Z'))throw new DomainException("validation_failed","Require a three-letter currency code.");
        var foreign=Amount(r.ForeignInvoiceAmount); var rate=Rate(r.ExchangeRate); var functional=Functional(foreign,rate);
        var recoverable=Amount(r.RecoverableTaxAmount); var nonrecoverable=Amount(r.NonrecoverableTaxAmount);
        var charges=r.Charges.Select(x=>x with { Amount=Amount(x.Amount).ToString("0.0000",CultureInfo.InvariantCulture),Evidence=x.Evidence.Trim() }).ToArray();
        var capitalizable=charges.Where(x=>x.Capitalizable).Sum(x=>decimal.Parse(x.Amount,CultureInfo.InvariantCulture));
        await periodStore.LockCompany(actor.TenantId,company,true,ct); await CostingStore.LockScope(db,actor.TenantId,company,false,ct);
        var plan=await Plan(actor,company,r.PlanId,ct); await periods.RequireOpen(actor.TenantId,company,plan.Snapshot.BusinessDate,ct);
        if(r.DeclarationDate>plan.Snapshot.BusinessDate)throw new DomainException("import_declaration_date_invalid","Declaration date cannot be after the receipt business date.");
        var entity=new ImportDeclaration { TenantId=actor.TenantId,CompanyId=company,Id=Guid.CreateVersion7(),PlanId=r.PlanId,DeclarationNumber=r.DeclarationNumber.Trim(),
            DeclarationDate=r.DeclarationDate,Currency=currency,ForeignInvoiceAmount=foreign,ExchangeRate=rate,FunctionalInvoiceAmount=functional,
            RecoverableTaxAmount=recoverable,NonrecoverableTaxAmount=nonrecoverable,CapitalizableChargeAmount=capitalizable,Charges=JsonSerializer.Serialize(charges),
            Evidence=r.Evidence.Trim(),CreatedBy=actor.Subject,CreatedAt=clock.GetUtcNow() };
        db.Add(entity); audit.Append(actor,company,entity.Id,"ImportDeclarationCreated",correlation,1);
        return new(201,View(entity),$"/api/v1/companies/{company}/inventory/import-declarations/{entity.Id}");
    }

    public async Task<CommandOutcome> Approve(Actor actor,Guid company,Guid id,ApproveCostingPolicyRequest r,string correlation,CancellationToken ct)
    {
        Require(actor,company,Permissions.CostingApprove); await periodStore.LockCompany(actor.TenantId,company,true,ct); await CostingStore.LockScope(db,actor.TenantId,company,false,ct);
        var d=await Find(actor,company,id,ct); if(d.State!="Draft"||d.Version!=r.ExpectedVersion)throw new DomainException("version_conflict","Reload the import declaration.",409);
        if(d.CreatedBy==actor.Subject)throw new DomainException("self_approval_denied","A different user must approve the import declaration.",403);
        var plan=await Plan(actor,company,d.PlanId,ct); await periods.RequireOpen(actor.TenantId,company,plan.Snapshot.BusinessDate,ct);
        if(await db.Set<ImportDeclaration>().AnyAsync(x=>x.TenantId==actor.TenantId&&x.CompanyId==company&&x.PlanId==d.PlanId&&x.State=="Approved",ct))
            throw new DomainException("import_declaration_already_approved","This receipt already has an approved import declaration.",409);
        d.State="Approved";d.Version=2;d.ApprovedBy=actor.Subject;d.ApprovedAt=clock.GetUtcNow();audit.Append(actor,company,id,"ImportDeclarationApproved",correlation,2);
        return new(200,View(d));
    }

    public async Task<ImportDeclarationView> Get(Actor actor,Guid company,Guid id,CancellationToken ct){Require(actor,company,Permissions.CostingRead);return View(await Find(actor,company,id,ct));}
    public async Task<IReadOnlyList<ImportDeclarationView>> List(Actor actor,Guid company,Guid? before,int take,CancellationToken ct)
    {
        Require(actor,company,Permissions.CostingRead);if(take is <1 or >100)throw new DomainException("validation_failed","Require take 1-100.");
        return (await db.Set<ImportDeclaration>().AsNoTracking().Where(x=>x.TenantId==actor.TenantId&&x.CompanyId==company&&(before==null||x.Id.CompareTo(before.Value)<0)).OrderByDescending(x=>x.Id).Take(take).ToArrayAsync(ct)).Select(View).ToArray();
    }
    private async Task<ImportDeclaration> Find(Actor a,Guid c,Guid id,CancellationToken ct)=>await db.Set<ImportDeclaration>().SingleOrDefaultAsync(x=>x.TenantId==a.TenantId&&x.CompanyId==c&&x.Id==id,ct)??throw new DomainException("not_found","Import declaration not found.",404);
    private async Task<PurchasePostingPlan> Plan(Actor a,Guid c,Guid id,CancellationToken ct)
    {
        var p=await db.Set<PurchasePostingPlan>().AsNoTracking().SingleOrDefaultAsync(x=>x.TenantId==a.TenantId&&x.CompanyId==c&&x.Id==id,ct)??throw new DomainException("not_found","Posting plan not found.",404);
        if(p.Status!=PostingPlanStatus.Prepared)throw new DomainException("import_source_not_prepared","Only a prepared, unposted receipt can receive an import declaration.",409);return p;
    }
    private static decimal Amount(string s)=>decimal.TryParse(s,NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var v)&&v>=0&&v<100000000000000000000m&&decimal.Round(v,4)==v?v:throw new DomainException("validation_failed","Require nonnegative decimal strings with at most four places.");
    private static decimal Rate(string s)=>decimal.TryParse(s,NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var v)&&v>0&&v<1000000000m&&decimal.Round(v,8)==v?v:throw new DomainException("validation_failed","Require a positive exchange rate with at most eight places.");
    private static decimal Functional(decimal amount,decimal rate){try{return decimal.Round(checked(amount*rate),4,MidpointRounding.AwayFromZero);}catch(OverflowException){throw new DomainException("validation_failed","Converted invoice amount exceeds the supported range.");}}
    private static ImportDeclarationView View(ImportDeclaration d)=>new(d.Id,d.CompanyId,d.PlanId,d.DeclarationNumber,d.DeclarationDate,d.Currency,F(d.ForeignInvoiceAmount),d.ExchangeRate.ToString("0.00000000",CultureInfo.InvariantCulture),F(d.FunctionalInvoiceAmount),F(d.RecoverableTaxAmount),F(d.NonrecoverableTaxAmount),F(d.CapitalizableChargeAmount),d.Evidence,JsonSerializer.Deserialize<ImportChargeInput[]>(d.Charges)!,d.State,d.Version,d.CreatedBy,d.ApprovedBy);
    private static string F(decimal value)=>value.ToString("0.0000",CultureInfo.InvariantCulture);
    private static void Require(Actor a,Guid c,string p){a.RequireCompany(c);if(!a.Permissions.Contains(p))throw new DomainException("permission_denied","Costing permission required.",403);}
}
