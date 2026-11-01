using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Vat.Application;
using Vat.Application.Security;
using Vat.Domain;
using Vat.Domain.Posting;
using Vat.Domain.Sales;

namespace Vat.Infrastructure.Persistence;

public sealed record CreateExportShipmentRequest(Guid InvoiceId,string DeclarationNumber,DateOnly DeclarationDate,string DestinationCountry,
    string TransportDocument,string Currency,string ForeignProceedsAmount,string ExchangeRate,string TaxTreatment,string Evidence);
public sealed record ExportShipmentView(Guid Id,Guid CompanyId,Guid InvoiceId,string DeclarationNumber,DateOnly DeclarationDate,string DestinationCountry,
    string TransportDocument,string Currency,string ForeignProceedsAmount,string ExchangeRate,string FunctionalProceedsAmount,string DispatchCostAmount,
    string TaxTreatment,string FrozenOutputTaxAmount,string Evidence,string State,int Version,string CreatedBy,string? ApprovedBy);
public sealed record ExportSourceView(Guid InvoiceId,string DocumentNumber,DateOnly BusinessDate,string CustomerReference,string NetAmount,string OutputTaxAmount,string DispatchCostAmount);

public sealed class ExportShipmentService(VatDbContext db,IPeriodStore periodStore,IPeriodGuard periods,IAuditWriter audit,TimeProvider clock)
{
    public async Task<CommandOutcome> Create(Actor actor,Guid company,CreateExportShipmentRequest r,string correlation,CancellationToken ct)
    {
        Require(actor,company,Permissions.CostingManage);Guard.Text(r.DeclarationNumber,"Export declaration number",100);Guard.Text(r.TransportDocument,"Transport document",200);Guard.Text(r.Evidence,"Export customs/tax evidence",1000);
        var country=(r.DestinationCountry??"").Trim().ToUpperInvariant();if(country.Length!=2||country.Any(x=>x<'A'||x>'Z'))throw new DomainException("validation_failed","Require a two-letter destination country code.");
        var currency=(r.Currency??"").Trim().ToUpperInvariant();if(currency.Length!=3||currency.Any(x=>x<'A'||x>'Z'))throw new DomainException("validation_failed","Require a three-letter currency code.");
        if(r.TaxTreatment is not ("ZeroRated" or "Exempt" or "Standard"))throw new DomainException("validation_failed","Require ZeroRated, Exempt or Standard tax treatment.");
        var foreign=Amount(r.ForeignProceedsAmount);var rate=Rate(r.ExchangeRate);var functional=Functional(foreign,rate);
        await periodStore.LockCompany(actor.TenantId,company,true,ct);await CostingStore.LockScope(db,actor.TenantId,company,false,ct);
        var source=await Source(actor,company,r.InvoiceId,ct);await periods.RequireOpen(actor.TenantId,company,r.DeclarationDate,ct);
        if(r.DeclarationDate<source.Invoice.BusinessDate)throw new DomainException("export_declaration_date_invalid","Export declaration cannot predate the sales invoice.");
        if(r.TaxTreatment!="Standard"&&source.OutputTax!=0)throw new DomainException("export_tax_treatment_mismatch","Zero-rated or exempt export evidence requires a posted invoice with zero output tax.");
        var entity=new ExportShipment { TenantId=actor.TenantId,CompanyId=company,Id=Guid.CreateVersion7(),InvoiceId=r.InvoiceId,DeclarationNumber=r.DeclarationNumber.Trim(),DeclarationDate=r.DeclarationDate,
            DestinationCountry=country,TransportDocument=r.TransportDocument.Trim(),Currency=currency,ForeignProceedsAmount=foreign,ExchangeRate=rate,FunctionalProceedsAmount=functional,
            DispatchCostAmount=source.DispatchCost,TaxTreatment=r.TaxTreatment,FrozenOutputTaxAmount=source.OutputTax,Evidence=r.Evidence.Trim(),SourceFingerprint=source.Fingerprint,CreatedBy=actor.Subject,CreatedAt=clock.GetUtcNow() };
        db.Add(entity);audit.Append(actor,company,entity.Id,"ExportShipmentCreated",correlation,1);return new(201,View(entity),$"/api/v1/companies/{company}/inventory/export-shipments/{entity.Id}");
    }
    public async Task<CommandOutcome> Approve(Actor actor,Guid company,Guid id,ApproveCostingPolicyRequest r,string correlation,CancellationToken ct)
    {
        Require(actor,company,Permissions.CostingApprove);await periodStore.LockCompany(actor.TenantId,company,true,ct);await CostingStore.LockScope(db,actor.TenantId,company,false,ct);
        var e=await Find(actor,company,id,ct);if(e.State!="Draft"||e.Version!=r.ExpectedVersion)throw new DomainException("version_conflict","Reload the export shipment.",409);if(e.CreatedBy==actor.Subject)throw new DomainException("self_approval_denied","A different user must approve the export shipment.",403);
        await periods.RequireOpen(actor.TenantId,company,e.DeclarationDate,ct);var source=await Source(actor,company,e.InvoiceId,ct);
        if(source.Fingerprint!=e.SourceFingerprint||source.DispatchCost!=e.DispatchCostAmount||source.OutputTax!=e.FrozenOutputTaxAmount)throw new DomainException("export_source_changed","Posted sales/cost evidence changed; prepare a new export review.",409);
        if(await db.Set<ExportShipment>().AnyAsync(x=>x.TenantId==actor.TenantId&&x.CompanyId==company&&x.InvoiceId==e.InvoiceId&&x.State=="Approved",ct))throw new DomainException("export_shipment_already_approved","This invoice already has an approved export shipment.",409);
        e.State="Approved";e.Version=2;e.ApprovedBy=actor.Subject;e.ApprovedAt=clock.GetUtcNow();audit.Append(actor,company,id,"ExportShipmentApproved",correlation,2);return new(200,View(e));
    }
    public async Task<ExportShipmentView> Get(Actor actor,Guid company,Guid id,CancellationToken ct){Require(actor,company,Permissions.CostingRead);return View(await Find(actor,company,id,ct));}
    public async Task<IReadOnlyList<ExportShipmentView>> List(Actor actor,Guid company,Guid? before,int take,CancellationToken ct)
    {Require(actor,company,Permissions.CostingRead);if(take is <1 or >100)throw new DomainException("validation_failed","Require take 1-100.");return(await db.Set<ExportShipment>().AsNoTracking().Where(x=>x.TenantId==actor.TenantId&&x.CompanyId==company&&(before==null||x.Id.CompareTo(before.Value)<0)).OrderByDescending(x=>x.Id).Take(take).ToArrayAsync(ct)).Select(View).ToArray();}
    public async Task<IReadOnlyList<ExportSourceView>> Sources(Actor actor,Guid company,CancellationToken ct)
    {
        Require(actor,company,Permissions.CostingRead);var rows=await(from f in db.Set<SalesFulfillment>().AsNoTracking() join i in db.SalesInvoices.AsNoTracking() on new{f.TenantId,f.CompanyId,Id=f.InvoiceId} equals new{i.TenantId,i.CompanyId,i.Id}
            where f.TenantId==actor.TenantId&&f.CompanyId==company&&f.Status=="Posted"&&!db.Set<ExportShipment>().Any(x=>x.TenantId==actor.TenantId&&x.CompanyId==company&&x.InvoiceId==f.InvoiceId&&x.State=="Approved") orderby f.InvoiceId descending select new{Invoice=i,Fulfillment=f}).Take(25).ToArrayAsync(ct);
        var result=new List<ExportSourceView>();foreach(var row in rows){try{var source=await Source(actor,company,row.Invoice.Id,ct);result.Add(new(row.Invoice.Id,row.Fulfillment.DocumentNumber!,row.Invoice.BusinessDate,row.Invoice.CustomerReference,F(row.Invoice.NetAmount),F(source.OutputTax),F(source.DispatchCost)));}catch(DomainException e)when(e.Code=="export_cost_unavailable"){} }return result;
    }

    private async Task<(SalesInvoice Invoice,decimal OutputTax,decimal DispatchCost,string Fingerprint)> Source(Actor a,Guid c,Guid invoiceId,CancellationToken ct)
    {
        var invoice=await db.SalesInvoices.AsNoTracking().SingleOrDefaultAsync(x=>x.TenantId==a.TenantId&&x.CompanyId==c&&x.Id==invoiceId,ct)??throw new DomainException("not_found","Sales invoice not found.",404);
        var fulfillment=await db.Set<SalesFulfillment>().AsNoTracking().SingleOrDefaultAsync(x=>x.TenantId==a.TenantId&&x.CompanyId==c&&x.InvoiceId==invoiceId,ct);
        if(fulfillment?.Status!="Posted"||invoice.Status is not (SalesInvoiceStatus.Posted or SalesInvoiceStatus.PartiallyCredited or SalesInvoiceStatus.Credited))throw new DomainException("export_source_not_posted","Export evidence requires a posted sales dispatch.",409);
        var sales=await db.Set<SalesPostingEntry>().AsNoTracking().Where(x=>x.TenantId==a.TenantId&&x.CompanyId==c&&x.InvoiceId==invoiceId&&x.CreditNoteId==null&&x.DebitNoteId==null).OrderBy(x=>x.Id).ToArrayAsync(ct);
        if(sales.Length==0)throw new DomainException("export_source_not_posted","Posted sales entries are missing.",409);
        var ids=sales.Select(x=>x.Id).ToArray();var costs=await db.Set<StockCostEntry>().AsNoTracking().Where(x=>x.TenantId==a.TenantId&&x.CompanyId==c&&x.Kind=="Issue"&&ids.Contains(x.SourceId)).OrderBy(x=>x.SourceId).ToArrayAsync(ct);
        if(costs.Length!=sales.Length||costs.Select(x=>x.SourceId).Distinct().Count()!=sales.Length)throw new DomainException("export_cost_unavailable","Every export line requires an immutable stock-ledger issue cost.",409);
        var raw=string.Join("|",sales.Select(x=>$"{x.Id:N}:{x.Quantity}:{x.NetAmount}:{x.OutputTaxAmount}").Concat(costs.Select(x=>$"{x.SourceId:N}:{x.Sequence}:{x.Quantity}:{x.Value}")));
        return(invoice,sales.Sum(x=>x.OutputTaxAmount),-costs.Sum(x=>x.Value),Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant());
    }
    private async Task<ExportShipment> Find(Actor a,Guid c,Guid id,CancellationToken ct)=>await db.Set<ExportShipment>().SingleOrDefaultAsync(x=>x.TenantId==a.TenantId&&x.CompanyId==c&&x.Id==id,ct)??throw new DomainException("not_found","Export shipment not found.",404);
    private static decimal Amount(string s)=>decimal.TryParse(s,NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var v)&&v>=0&&v<100000000000000000000m&&decimal.Round(v,4)==v?v:throw new DomainException("validation_failed","Require a nonnegative amount with at most four places.");
    private static decimal Rate(string s)=>decimal.TryParse(s,NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var v)&&v>0&&v<1000000000m&&decimal.Round(v,8)==v?v:throw new DomainException("validation_failed","Require a positive exchange rate with at most eight places.");
    private static decimal Functional(decimal amount,decimal rate){try{return decimal.Round(checked(amount*rate),4,MidpointRounding.AwayFromZero);}catch(OverflowException){throw new DomainException("validation_failed","Converted export proceeds exceed the supported range.");}}
    private static string F(decimal x)=>x.ToString("0.0000",CultureInfo.InvariantCulture);
    private static ExportShipmentView View(ExportShipment e)=>new(e.Id,e.CompanyId,e.InvoiceId,e.DeclarationNumber,e.DeclarationDate,e.DestinationCountry,e.TransportDocument,e.Currency,F(e.ForeignProceedsAmount),e.ExchangeRate.ToString("0.00000000",CultureInfo.InvariantCulture),F(e.FunctionalProceedsAmount),F(e.DispatchCostAmount),e.TaxTreatment,F(e.FrozenOutputTaxAmount),e.Evidence,e.State,e.Version,e.CreatedBy,e.ApprovedBy);
    private static void Require(Actor a,Guid c,string p){a.RequireCompany(c);if(!a.Permissions.Contains(p))throw new DomainException("permission_denied","Costing permission required.",403);}
}
