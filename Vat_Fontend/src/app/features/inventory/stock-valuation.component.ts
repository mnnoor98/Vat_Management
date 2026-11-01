import { Component, OnDestroy, effect, inject, signal, untracked } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Subscription, Observable, EMPTY, expand, reduce, forkJoin, of } from 'rxjs';
import { CompanyContextService } from '../../core/services/company-context.service';
import { StockValuationApiService, OpeningPosition, OpeningDraft, OpeningReview, LandedDraft, LandedReview, LandedSource, LedgerStatus, LedgerEntry, ImportDraft, ImportReview } from '../../core/services/api/stock-valuation-api.service';

@Component({selector:'app-stock-valuation',standalone:true,imports:[CommonModule,FormsModule,RouterLink],template:`
  <section class="vat-card" style="padding:24px">
    <h1>Stock valuation ledger</h1>
    <p>Cost basis: net purchase + non-recoverable VAT + approved allocated landed cost, in BDT. FIFO or Moving Weighted Average follows approved company/product policies. Reserved units remain part of on-hand valuation.</p>
    <p role="alert" *ngIf="error()">{{error()}}</p><p role="status" *ngIf="message()">{{message()}}</p>
    <button class="btn btn-secondary" (click)="reload()" [disabled]="busy()">Reload ledger and reviews</button>
    <p *ngIf="status() as s">{{s.state}} / {{s.basis ?? 'Opening approval required'}} / Opening {{s.openingDate ?? 'Pending'}} / Sequence {{s.sequence ?? 'Unavailable'}} / BDT {{s.stockValue ?? 'Unavailable'}} <span *ngIf="s.reason">({{s.reason}})</span></p>
    <a routerLink="/app/inventory/costing-policies">Costing policies</a> | <a routerLink="/app/inventory/summary">Stock balances</a>
    <h2>Opening stock adoption</h2>
    <p>Review all nonzero warehouse/product balances with supporting opening cost evidence. An independent approver activates the ledger. Changed stock activity invalidates the prepared opening. Approved openings cannot be reset.</p>
    <fieldset *ngIf="context.hasPermission('costing.manage') && status()?.state === 'AdoptionRequired'" [disabled]="busy()">
      <label>Opening date <input type="date" [(ngModel)]="opening.businessDate"></label>
      <label>Opening reconciliation / evidence reference <input [(ngModel)]="opening.evidence" maxlength="1000"></label>
      <button class="btn btn-secondary" (click)="loadPositions()">Load all opening balances</button>
      <p>{{positionsLoaded() ? opening.lines.length + ' positions loaded. Enter each reviewed full opening value.' : 'Load the complete opening balances before saving. An empty company can be activated with no opening lines.'}}</p>
      <table class="vat-table"><thead><tr><th>Warehouse / product</th><th>On hand</th><th>Reviewed total BDT</th></tr></thead><tbody>
        <tr *ngFor="let line of opening.lines.slice(openingPage()*25,openingPage()*25+25)"><td>{{positionLabel(line.warehouseId,line.productId)}}</td><td>{{line.quantity}}</td><td><input inputmode="decimal" [(ngModel)]="line.value" aria-label="Reviewed opening value"></td></tr>
      </tbody></table>
      <button (click)="openingPage.set(openingPage()-1)" [disabled]="openingPage()===0">Previous positions</button>
      <button (click)="openingPage.set(openingPage()+1)" [disabled]="(openingPage()+1)*25>=opening.lines.length">Next positions</button>
      <button class="btn btn-primary" (click)="createOpening()" [disabled]="!positionsLoaded()">Save opening for independent review</button>
    </fieldset>
    <table class="vat-table"><thead><tr><th>Opening date / evidence</th><th>State / maker</th><th>Action</th></tr></thead><tbody>
      <tr *ngFor="let a of openings()"><td>{{a.businessDate}} / {{a.evidence}}</td><td>{{a.state}} / {{a.createdBy}}</td><td><button (click)="reviewOpening.set(a); reviewPage.set(0)">Review opening</button></td></tr>
    </tbody></table>
    <button (click)="openingBefore=undefined; loadOpenings()" [disabled]="busy()">Newest openings</button><button (click)="olderOpenings()" [disabled]="busy() || openings().length!==25">Older openings</button>
    <section *ngIf="reviewOpening() as a">
      <h3>Opening approval review</h3><p>{{a.businessDate}} / {{a.basis}} / {{a.evidence}} / version {{a.version}}. Positions: {{a.lines.length}}.</p>
      <table class="vat-table"><thead><tr><th>Warehouse / product</th><th>Method</th><th>Quantity</th><th>Total BDT</th></tr></thead><tbody>
        <tr *ngFor="let l of a.lines.slice(reviewPage()*25,reviewPage()*25+25)"><td>{{positionLabel(l.warehouseId,l.productId)}}</td><td>{{l.method}}</td><td>{{l.quantity}}</td><td>{{l.value}}</td></tr>
      </tbody></table><button (click)="reviewPage.set(reviewPage()-1)" [disabled]="reviewPage()===0">Previous</button><button (click)="reviewPage.set(reviewPage()+1)" [disabled]="(reviewPage()+1)*25>=a.lines.length">Next</button>
      <button class="btn btn-primary" (click)="approveOpening(a)" [disabled]="busy() || !canApprove(a)">Approve reviewed opening and activate valuation</button><button (click)="reviewOpening.set(null)">Close review</button>
    </section>
    <h2>Reviewed import declaration</h2>
    <p>Freeze the bill of entry, foreign invoice, exchange rate, tax split and classified charges before allocating landed cost. An independent approver is required.</p>
    <fieldset *ngIf="context.hasPermission('costing.manage')" [disabled]="busy()">
      <label>Prepared receipt <select [(ngModel)]="importDraft.planId"><option value="">Select receipt</option><option *ngFor="let s of sources()" [value]="s.planId">{{s.purchaseReference}} / {{s.businessDate}}</option></select></label>
      <label>Declaration number <input [(ngModel)]="importDraft.declarationNumber" maxlength="100"></label><label>Declaration date <input type="date" [(ngModel)]="importDraft.declarationDate"></label>
      <label>Currency <input [(ngModel)]="importDraft.currency" maxlength="3"></label><label>Foreign invoice <input [(ngModel)]="importDraft.foreignInvoiceAmount" inputmode="decimal"></label><label>BDT exchange rate <input [(ngModel)]="importDraft.exchangeRate" inputmode="decimal"></label>
      <label>Recoverable tax BDT <input [(ngModel)]="importDraft.recoverableTaxAmount" inputmode="decimal"></label><label>Non-recoverable tax BDT <input [(ngModel)]="importDraft.nonrecoverableTaxAmount" inputmode="decimal"></label>
      <label>Evidence <input [(ngModel)]="importDraft.evidence" maxlength="1000"></label>
      <div *ngFor="let charge of importDraft.charges"><select [(ngModel)]="charge.kind"><option *ngFor="let k of importKinds" [value]="k">{{k}}</option></select><input [(ngModel)]="charge.amount" inputmode="decimal" placeholder="BDT amount"><input [(ngModel)]="charge.evidence" maxlength="500" placeholder="Charge evidence"><label><input type="checkbox" [(ngModel)]="charge.capitalizable"> Capitalize</label></div>
      <button (click)="addImportCharge()">Add charge</button><button class="btn btn-primary" (click)="createImport()">Save import declaration</button>
    </fieldset>
    <table class="vat-table"><thead><tr><th>Declaration / currency</th><th>BDT basis / capitalizable</th><th>State / action</th></tr></thead><tbody><tr *ngFor="let d of importReviews()"><td>{{d.declarationNumber}} / {{d.currency}}</td><td>{{d.functionalInvoiceAmount}} / {{d.capitalizableChargeAmount}}</td><td>{{d.state}} / {{d.createdBy}} <button (click)="approveImport(d)" [disabled]="busy() || !canApprove(d)">Approve</button></td></tr></tbody></table>
    <h2>Landed cost before purchase receipt</h2>
    <p>Allocate the full declared BDT freight/duty/other capitalizable cost across every receipt line. VAT components already in the purchase calculation must not be entered again. Record zero charges explicitly when none apply. Amounts freeze on approval; late charges after posting require a separate reconciliation workflow.</p>
    <fieldset *ngIf="context.hasPermission('costing.manage')" [disabled]="busy()">
      <button (click)="sourceBefore=undefined; loadSources()">Load prepared receipts</button><button (click)="olderSources()" [disabled]="sources().length!==25">Older prepared receipts</button>
      <label>Receipt <select [ngModel]="landed.planId" (ngModelChange)="chooseSource($event)"><option value="">Select prepared purchase receipt</option><option *ngFor="let s of sources()" [value]="s.planId">{{s.purchaseReference}} / {{s.warehouse}} / {{s.businessDate}}</option></select></label>
      <label>Approved import declaration <select [(ngModel)]="landed.importDeclarationId"><option [ngValue]="null">Domestic / none</option><option *ngFor="let d of approvedImportsForLanded()" [value]="d.id">{{d.declarationNumber}} / BDT {{d.capitalizableChargeAmount}}</option></select></label>
      <label>Declared total landed cost (BDT) <input [(ngModel)]="landed.totalAmount" inputmode="decimal"></label>
      <label>Supporting charge / allocation evidence <input [(ngModel)]="landed.evidence" maxlength="1000"></label>
      <button (click)="declareZero()" [disabled]="!landed.planId">Declare no landed charges</button>
      <table class="vat-table"><thead><tr><th>Product</th><th>Quantity / net BDT</th><th>Allocated landed BDT</th></tr></thead><tbody>
        <tr *ngFor="let line of landed.lines"><td>{{sourceLineLabel(line.purchaseLineId)}}</td><td>{{sourceLineDetails(line.purchaseLineId)}}</td><td><input [(ngModel)]="line.amount" inputmode="decimal" aria-label="Allocated landed cost"></td></tr>
      </tbody></table><button class="btn btn-primary" (click)="createLanded()">Save allocation for independent review</button>
    </fieldset>
    <table class="vat-table"><thead><tr><th>Evidence / total BDT</th><th>State / maker</th><th>Action</th></tr></thead><tbody>
      <tr *ngFor="let a of landedReviews()"><td>{{a.evidence}} / {{a.totalAmount}}</td><td>{{a.state}} / {{a.createdBy}}</td><td><button (click)="selectLanded(a)">Review allocation</button></td></tr>
    </tbody></table><button (click)="landedBefore=undefined; loadLanded()" [disabled]="busy()">Newest allocations</button><button (click)="olderLanded()" [disabled]="busy() || landedReviews().length!==25">Older allocations</button>
    <section *ngIf="reviewLanded() as a"><h3>Landed-cost approval review</h3><p>{{reviewSource()?.purchaseReference ?? 'Source details unavailable'}} / {{reviewSource()?.warehouse}} / {{reviewSource()?.businessDate}}</p>
      <p>{{a.evidence}} / Total BDT {{a.totalAmount}} / version {{a.version}}</p>
      <p *ngFor="let l of a.lines">{{reviewLineLabel(l.purchaseLineId)}}: BDT {{l.amount}}</p>
      <button class="btn btn-primary" (click)="approveLanded(a)" [disabled]="busy() || !canApprove(a) || !reviewSource()">Approve reviewed landed allocation</button><button (click)="reviewLanded.set(null); reviewSource.set(null)">Close review</button>
    </section>
    <h2>Immutable cost journal</h2>
    <table class="vat-table"><thead><tr><th>Sequence / date</th><th>Movement / source</th><th>Method</th><th>Quantity</th><th>BDT change</th></tr></thead><tbody>
      <tr *ngFor="let e of entries()"><td>{{e.sequence}} / {{e.businessDate}}</td><td>{{e.kind}} / {{e.sourceId}}</td><td>{{e.method}}</td><td>{{e.quantity}}</td><td>{{e.value}}</td></tr>
    </tbody></table><button (click)="entryBefore=undefined; loadEntries()" [disabled]="busy()">Newest entries</button><button (click)="olderEntries()" [disabled]="busy() || entries().length!==25">Older entries</button>
  </section>`,styles:[`fieldset{padding:16px;margin:16px 0}label{display:inline-flex;flex-direction:column;margin:8px;gap:6px}input,select{padding:8px}button{margin:6px}[role=alert]{color:#b42318}`]})
export class StockValuationComponent implements OnDestroy {
  readonly context=inject(CompanyContextService); private api=inject(StockValuationApiService); private subscriptions=new Subscription();
  private requests=new Map<string,Subscription>();
  readonly status=signal<LedgerStatus|null>(null); readonly entries=signal<LedgerEntry[]>([]); readonly openings=signal<OpeningReview[]>([]);
  readonly landedReviews=signal<LandedReview[]>([]); readonly importReviews=signal<ImportReview[]>([]); readonly sources=signal<LandedSource[]>([]); readonly positions=signal<OpeningPosition[]>([]);
  readonly reviewOpening=signal<OpeningReview|null>(null); readonly reviewLanded=signal<LandedReview|null>(null); readonly reviewSource=signal<LandedSource|null>(null);
  readonly busy=signal(false); readonly error=signal(''); readonly message=signal(''); readonly positionsLoaded=signal(false);
  readonly openingPage=signal(0); readonly reviewPage=signal(0); private reloadRequired=true; private scope=''; private commandKeys=new Map<string,string>();
  opening:OpeningDraft={businessDate:'',basis:'NetPlusNonrecoverableTaxAndLanded',evidence:'',lines:[]};
  landed:LandedDraft={planId:'',totalAmount:'',evidence:'',lines:[]}; openingBefore?:string; landedBefore?:string; entryBefore?:string; sourceBefore?:string;
  readonly importKinds=['Duty','Freight','Insurance','Port','Clearing','Other']; importDraft:ImportDraft={planId:'',declarationNumber:'',declarationDate:'',currency:'USD',foreignInvoiceAmount:'',exchangeRate:'',recoverableTaxAmount:'0.0000',nonrecoverableTaxAmount:'0.0000',evidence:'',charges:[]};
  constructor(){effect(()=>{const company=this.context.companyGuid();untracked(()=>this.reset(company));});}
  reset(company:string):void{
    this.subscriptions.unsubscribe();this.subscriptions=new Subscription();this.scope=company;this.commandKeys.clear();this.busy.set(false);this.message.set('');
    this.opening={businessDate:'',basis:'NetPlusNonrecoverableTaxAndLanded',evidence:'',lines:[]};this.landed={planId:'',totalAmount:'',evidence:'',lines:[]};this.importDraft={planId:'',declarationNumber:'',declarationDate:'',currency:'USD',foreignInvoiceAmount:'',exchangeRate:'',recoverableTaxAmount:'0.0000',nonrecoverableTaxAmount:'0.0000',evidence:'',charges:[]};
    this.positions.set([]);this.positionsLoaded.set(false);this.openingPage.set(0);this.reviewPage.set(0);this.sources.set([]);
    this.openingBefore=this.landedBefore=this.entryBefore=this.sourceBefore=undefined;this.reload();
  }
  reload():void{
    if(this.busy())return;this.subscriptions.unsubscribe();this.subscriptions=new Subscription();this.requests.clear();this.error.set('');this.reloadRequired=true;this.reviewOpening.set(null);this.reviewLanded.set(null);this.reviewSource.set(null);this.status.set(null);
    this.openings.set([]);this.landedReviews.set([]);this.importReviews?.set([]);this.entries.set([]);
    if(!this.context.hasPermission('costing.read')){this.error.set('Costing read permission is required.');return;}
    this.busy.set(true);
    this.read(forkJoin({status:this.api.status(),openings:this.api.openings(this.openingBefore),landed:this.api.landed(this.landedBefore),imports:this.api.imports?.()??of([]),entries:this.api.journal(this.entryBefore)}),r=>{
      if(r.status.companyId!==this.scope||r.openings.some(a=>a.companyId!==this.scope)||r.landed.some(a=>a.companyId!==this.scope))throw new Error('Ledger/review scope mismatch.');
      this.status.set(r.status);this.openings.set(r.openings);this.landedReviews.set(r.landed);this.importReviews?.set(r.imports);this.entries.set(r.entries);this.reloadRequired=false;this.busy.set(false);
    },'reload');
  }
  private read<T>(request:Observable<T>,next:(value:T)=>void,slot='page'):void{
    this.requests.get(slot)?.unsubscribe();const scope=this.scope;
    const sub=request.subscribe({next:value=>{if(scope!==this.context.companyGuid())return;try{next(value);}catch(e){this.fail(e);}},error:e=>{if(scope===this.context.companyGuid())this.fail(e);}});
    this.requests.set(slot,sub);this.subscriptions.add(sub);
  }
  loadOpenings():void{this.openings.set([]);this.reviewOpening.set(null);this.read(this.api.openings(this.openingBefore),rows=>{if(rows.some(x=>x.companyId!==this.scope))throw new Error('Opening scope mismatch.');this.openings.set(rows);},'openings');}
  loadLanded():void{this.landedReviews.set([]);this.reviewLanded.set(null);this.reviewSource.set(null);this.read(this.api.landed(this.landedBefore),rows=>{if(rows.some(x=>x.companyId!==this.scope))throw new Error('Landed review scope mismatch.');this.landedReviews.set(rows);},'landed');}
  loadEntries():void{this.entries.set([]);this.read(this.api.journal(this.entryBefore),rows=>this.entries.set(rows),'entries');}
  loadSources():void{this.sources.set([]);this.landed={planId:'',totalAmount:'',evidence:'',lines:[]};this.read(this.api.sources(this.sourceBefore),rows=>this.sources.set(rows),'sources');}
  olderOpenings():void{if(this.openings().length===25){this.openingBefore=this.openings().at(-1)!.id;this.loadOpenings();}}
  olderLanded():void{if(this.landedReviews().length===25){this.landedBefore=this.landedReviews().at(-1)!.id;this.loadLanded();}}
  olderEntries():void{if(this.entries().length===25){this.entryBefore=this.entries().at(-1)!.sequence;this.loadEntries();}}
  olderSources():void{if(this.sources().length===25){this.sourceBefore=this.sources().at(-1)!.planId;this.loadSources();}}
  loadPositions():void{
    if(this.busy())return;this.positionsLoaded.set(false);this.positions.set([]);this.opening.lines=[];this.openingPage.set(0);this.busy.set(true);
    let skip=0;
    this.read(this.api.positions(0).pipe(expand(rows=>{skip+=100;if(rows.length<100)return EMPTY;if(skip>10000)throw new Error('Opening exceeds supported page range; reviewed batch adoption is required.');return this.api.positions(skip);}),reduce((all,rows)=>[...all,...rows],[] as OpeningPosition[])),rows=>{
      if(rows.length>10000)throw new Error('Opening exceeds 10000 positions.');this.positions.set(rows);this.opening.lines=rows.map(x=>({warehouseId:x.warehouseId,productId:x.productId,quantity:x.quantity,value:''}));this.positionsLoaded.set(true);this.busy.set(false);
    },'positions');
  }
  chooseSource(id:string):void{const s=this.sources().find(x=>x.planId===id);this.landed={planId:s?.planId??'',totalAmount:'',evidence:'',lines:s?.lines.map(x=>({purchaseLineId:x.purchaseLineId,amount:''}))??[],importDeclarationId:null};}
  approvedImportsForLanded():ImportReview[]{return this.importReviews().filter(x=>x.state==='Approved'&&x.planId===this.landed.planId);}
  addImportCharge():void{this.importDraft.charges.push({kind:'Freight',amount:'',evidence:'',capitalizable:true});}
  createImport():void{
    if(this.busy()||!this.context.hasPermission('costing.manage'))return;
    const d=this.importDraft;if(!d.planId||!d.declarationNumber.trim()||!this.validDate(d.declarationDate)||!/^[A-Za-z]{3}$/.test(d.currency)||!this.amount(d.foreignInvoiceAmount)||!/^\d+(\.\d{1,8})?$/.test(d.exchangeRate)||Number(d.exchangeRate)<=0||!this.amount(d.recoverableTaxAmount)||!this.amount(d.nonrecoverableTaxAmount)||!d.evidence.trim()||d.charges.some(x=>!this.amount(x.amount)||!x.evidence.trim())){this.error.set('Complete the receipt, declaration, currency/rate, tax, evidence and charge fields.');return;}
    const draft=structuredClone(d);this.command('import',draft,key=>this.api.createImport(draft,key),r=>r.state==='Draft'&&r.planId===draft.planId&&r.declarationNumber===draft.declarationNumber.trim());
  }
  declareZero():void{this.landed.totalAmount='0.0000';this.landed.lines=this.landed.lines.map(x=>({...x,amount:'0.0000'}));}
  selectLanded(a:LandedReview):void{this.reviewLanded.set(a);this.reviewSource.set(null);this.read(this.api.source(a.planId),source=>{if(this.reviewLanded()?.id===a.id && source.planId===a.planId)this.reviewSource.set(source);},'review-source');}
  canApprove(a:OpeningReview|LandedReview|ImportReview):boolean{return !this.reloadRequired&&a.companyId===this.context.companyGuid()&&a.state==='Draft'&&a.version===1&&a.createdBy!==this.context.currentUser().id&&this.context.hasPermission('costing.approve');}
  createOpening():void{
    if(this.busy()||!this.context.hasPermission('costing.manage')||!this.positionsLoaded())return;
    if(!this.validDate(this.opening.businessDate)||!this.opening.evidence.trim()||this.opening.lines.some(x=>!this.amount(x.value))){this.error.set('Enter a valid opening date, evidence and every reviewed total value.');return;}
    const draft=structuredClone(this.opening);this.command('opening',draft,key=>this.api.createOpening(draft,key),a=>a.state==='Draft'&&this.openingMatches(a,draft));
  }
  createLanded():void{
    if(this.busy()||!this.context.hasPermission('costing.manage'))return;
    if(!this.landed.planId||!this.landed.evidence.trim()||!this.amount(this.landed.totalAmount)||!this.landed.lines.length||this.landed.lines.some(x=>!this.amount(x.amount))){this.error.set('Select a prepared receipt and enter evidence, declared total and every allocation.');return;}
    const draft=structuredClone(this.landed);this.command('landed',draft,key=>this.api.createLanded(draft,key),a=>a.state==='Draft'&&this.landedMatches(a,draft));
  }
  approveOpening(a:OpeningReview):void{if(!this.canApprove(a)||this.busy())return;this.command('approve-opening',{id:a.id,version:a.version},key=>this.api.approveOpening(a,key),r=>r.id===a.id&&r.state==='Approved'&&r.version===2&&this.openingMatches(r,a));}
  approveLanded(a:LandedReview):void{if(!this.canApprove(a)||this.busy()||this.reviewSource()?.planId!==a.planId)return;this.command('approve-landed',{id:a.id,version:a.version},key=>this.api.approveLanded(a,key),r=>r.id===a.id&&r.state==='Approved'&&r.version===2&&this.landedMatches(r,a));}
  approveImport(a:ImportReview):void{if(!this.canApprove(a)||this.busy())return;this.command('approve-import',{id:a.id,version:a.version},key=>this.api.approveImport(a,key),r=>r.id===a.id&&r.state==='Approved'&&r.version===2&&r.planId===a.planId);}
  private command<T extends {companyId:string}>(kind:string,payload:unknown,send:(key:string)=>Observable<T>,valid:(response:T)=>boolean):void{
    const scope=this.context.companyGuid();if(scope!==this.scope)return;const fingerprint=JSON.stringify([kind,payload]);let key=this.commandKeys.get(fingerprint);if(!key){key=crypto.randomUUID();this.commandKeys.set(fingerprint,key);}
    this.busy.set(true);this.error.set('');this.message.set('');this.subscriptions.add(send(key).subscribe({next:r=>{if(scope!==this.context.companyGuid())return;if(r.companyId!==scope||!valid(r)){this.fail(new Error('Command response mismatch. Reload required.'));return;}this.busy.set(false);this.message.set('Saved and verified. Reloaded current ledger and reviews.');this.reload();},error:e=>{if(scope===this.context.companyGuid())this.fail(e);}}));
  }
  positionLabel(w:string,p:string):string{const x=this.positions().find(x=>x.warehouseId===w&&x.productId===p);return x?`${x.warehouse} / ${x.product}`:`${w} / ${p}`;}
  sourceLineLabel(id:string):string{return this.sources().find(x=>x.planId===this.landed.planId)?.lines.find(x=>x.purchaseLineId===id)?.product??id;}
  sourceLineDetails(id:string):string{const l=this.sources().find(x=>x.planId===this.landed.planId)?.lines.find(x=>x.purchaseLineId===id);return l?`${l.quantity} / ${l.netAmount}`:'';}
  reviewLineLabel(id:string):string{return this.reviewSource()?.lines.find(x=>x.purchaseLineId===id)?.product??id;}
  private amount(s:string):boolean{return /^\d+(\.\d{1,4})?$/.test(s);}
  private normalized(s:string):string{const [whole,fraction='']=s.split('.');return `${whole.replace(/^0+(?=\d)/,'')}.${fraction.replace(/0+$/,'')}`;}
  private openingMatches(a:OpeningDraft,b:OpeningDraft):boolean{return a.basis===b.basis&&a.businessDate===b.businessDate&&a.evidence===b.evidence.trim()&&a.lines.length===b.lines.length&&a.lines.every(x=>{
    const y=b.lines.find(y=>y.warehouseId===x.warehouseId&&y.productId===x.productId);return y&&this.normalized(x.quantity)===this.normalized(y.quantity)&&this.normalized(x.value)===this.normalized(y.value)&&(!y.method||x.method===y.method);
  });}
  private landedMatches(a:LandedDraft,b:LandedDraft):boolean{return a.planId===b.planId&&a.evidence===b.evidence.trim()&&this.normalized(a.totalAmount)===this.normalized(b.totalAmount)&&a.lines.length===b.lines.length&&a.lines.every(x=>{
    const y=b.lines.find(y=>y.purchaseLineId===x.purchaseLineId);return y&&this.normalized(x.amount)===this.normalized(y.amount);
  });}
  private validDate(s:string):boolean{return /^\d{4}-\d{2}-\d{2}$/.test(s)&&Number.isFinite(Date.parse(s))&&new Date(s).toISOString().slice(0,10)===s;}
  private fail(e:unknown):void{const v=e as {error?:{detail?:string};message?:string};this.error.set(v?.error?.detail??v?.message??'Request failed. Reload before approval.');this.busy.set(false);this.reloadRequired=true;this.status.set(null);this.entries.set([]);this.reviewOpening.set(null);this.reviewLanded.set(null);this.reviewSource.set(null);}
  ngOnDestroy():void{this.subscriptions.unsubscribe();}
}
