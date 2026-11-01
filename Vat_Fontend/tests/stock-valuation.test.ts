import '@angular/compiler';
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { Injector, runInInjectionContext, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom, of, throwError, Subject, Subscription } from 'rxjs';
import { CompanyContextService } from '../src/app/core/services/company-context.service';
import { StockValuationApiService, OpeningReview, LandedReview } from '../src/app/core/services/api/stock-valuation-api.service';
import { StockValuationComponent } from '../src/app/features/inventory/stock-valuation.component';
import { StockSummaryComponent } from '../src/app/features/inventory/components/stock-summary/stock-summary.component';

const opening:OpeningReview={id:'opening',companyId:'company',state:'Draft',version:1,createdBy:'maker',approvedBy:null,businessDate:'2026-10-31',basis:'NetPlusNonrecoverableTaxAndLanded',evidence:'Opening evidence',lines:[]};
const landed:LandedReview={id:'landed',companyId:'company',state:'Draft',version:1,createdBy:'maker',approvedBy:null,planId:'plan',totalAmount:'10.0000',evidence:'Charges',lines:[{purchaseLineId:'line',amount:'10.0000'}]};
function screen(api:object){return Object.assign(Object.create(StockValuationComponent.prototype),{
  api,scope:'company',context:{companyGuid:()=> 'company',currentUser:()=>({id:'checker'}),hasPermission:()=>true},subscriptions:new Subscription(),requests:new Map(),commandKeys:new Map(),reloadRequired:false,
  status:signal({companyId:'company',state:'AdoptionRequired'}),entries:signal([]),openings:signal([opening]),landedReviews:signal([landed]),sources:signal([]),positions:signal([]),
  reviewOpening:signal(opening),reviewLanded:signal(landed),reviewSource:signal({planId:'plan'}),busy:signal(false),error:signal(''),message:signal(''),positionsLoaded:signal(true),openingPage:signal(0),reviewPage:signal(0),
  opening:structuredClone(opening),landed:structuredClone(landed),reload:()=>undefined
}) as StockValuationComponent;}

test('valuation API preserves scoped decimal strings, sequence cursors and reviewed command versions',async()=>{
  const calls:unknown[][]=[];const injector=Injector.create({providers:[{provide:CompanyContextService,useValue:{companyGuid:()=> 'company'}},
    {provide:HttpClient,useValue:{get:(...args:unknown[])=>{calls.push(args);return of([]);},post:(...args:unknown[])=>{calls.push(args);return of(landed);}}}]});
  const api=runInInjectionContext(injector,()=>new StockValuationApiService());
  await firstValueFrom(api.journal('9007199254740993'));await firstValueFrom(api.createLanded({...landed,totalAmount:'12345678901234567890.1234'},'stable-key'));await firstValueFrom(api.approveLanded(landed,'approve-key'));
  assert.deepEqual(calls[0],['/api/v1/companies/company/inventory/cost-ledger/entries',{params:{take:25,before:'9007199254740993'}}]);
  assert.equal((calls[1][1] as {totalAmount:string}).totalAmount,'12345678901234567890.1234');assert.deepEqual(calls[2][1],{expectedVersion:1});
  assert.deepEqual(calls[2][2],{headers:{'Idempotency-Key':'approve-key'}});
});
test('opening preparation loads every page and preserves exact quantities without pricing guesses',()=>{
  const offsets:number[]=[];const c=screen({positions:(skip:number)=>{offsets.push(skip);return of(Array.from({length:skip===0?100:1},(_,i)=>({warehouseId:'w',productId:`p${skip+i}`,warehouse:'Main',product:'Material',quantity:'123456789012.000001'})));}});
  c.loadPositions();assert.deepEqual(offsets,[0,100]);assert.equal(c.opening.lines.length,101);assert.equal(c.opening.lines[100].quantity,'123456789012.000001');assert.equal(c.opening.lines[100].value,'');assert.equal(c.positionsLoaded(),true);
});
test('failed complete-opening loads discard incomplete lines and prevent submission',()=>{
  const c=screen({positions:()=>throwError(()=>new Error('Unavailable')),createOpening:()=>assert.fail('Incomplete opening must not post')});
  c.opening.lines=[{warehouseId:'w',productId:'p',quantity:'1.000000',value:'5.0000'}];c.loadPositions();c.createOpening();
  assert.deepEqual(c.opening.lines,[]);assert.equal(c.positionsLoaded(),false);assert.equal(c.busy(),false);assert.match(c.error(),/Unavailable/);
});
test('failed opening retries retain their key and exact reviewed decimal value',()=>{
  const sent:{key:string;value:string}[]=[];const c=screen({createOpening:(draft:OpeningReview,key:string)=>{sent.push({key,value:draft.lines[0].value});return throwError(()=>new Error('Offline'));}});
  c.opening.lines=[{warehouseId:'w',productId:'p',quantity:'1.000000',value:'12345678901234567890.1234'}];c.createOpening();c.createOpening();
  assert.equal(sent[0].key,sent[1].key);assert.equal(sent[0].value,'12345678901234567890.1234');assert.equal(c.message(),'');
  c.opening.lines[0].value='12345678901234567890.1235';c.createOpening();assert.notEqual(sent[1].key,sent[2].key);
});
test('financial approvals require an independent actor, current scope, loaded source and fresh review',()=>{
  const c=screen({approveOpening:()=>assert.fail('Denied approval')});
  assert.equal(c.canApprove({...opening,createdBy:'checker'}),false);assert.equal(c.canApprove({...opening,companyId:'foreign'}),false);
  assert.equal(c.canApprove({...opening,state:'Approved',version:2}),false);
  c.reviewSource.set(null);c.approveLanded(landed);assert.equal(c.busy(),false);
});
test('failed or mismatched approval clears stale ledger values and requires reload',()=>{
  for(const api of [{approveOpening:()=>throwError(()=>new Error('Conflict'))},{approveOpening:()=>of({...opening,state:'Approved',version:2,basis:'PurchaseNetOnly'})}]){
    const c=screen(api);c.approveOpening(opening);assert.equal(c.canApprove(opening),false);assert.equal(c.reviewOpening(),null);assert.equal(c.status(),null);assert.deepEqual(c.entries(),[]);assert.equal(c.message(),'');
  }
});
test('late results from a previous company cannot enable current valuation',()=>{
  const pending=new Subject<OpeningReview[]>();const c=screen({openings:()=>pending});c.loadOpenings();
  Object.assign(c.context,{companyGuid:()=> 'new-company'});pending.next([opening]);assert.deepEqual(c.openings(),[]);c.ngOnDestroy();assert.equal(pending.observers.length,0);
});
test('stock page totals add exact cost and quantity strings beyond JavaScript number precision',()=>{
  const c=Object.assign(Object.create(StockSummaryComponent.prototype),{context:{isSuperAdmin:()=>true},selectedWarehouse:'ALL',searchQuery:'',stockBalances:[
    {warehouseId:'w',totalValuationExact:'12345678901234567890.1234',availableQuantityExact:'123456789012.000001'},
    {warehouseId:'w',totalValuationExact:'0.0001',availableQuantityExact:'0.000001'}
  ]}) as StockSummaryComponent;
  assert.equal(c.totalStockValuationExact,'12345678901234567890.1235');assert.equal(c.totalAvailableQuantityExact,'123456789012.000002');
  c.stockBalances[0].totalValuationExact=null;assert.equal(c.totalStockValuationExact,null);
});
test('coherent review reload rejects foreign or failed data before enabling approval',()=>{
  for(const openingResult of [of([{...opening,companyId:'foreign'}]),throwError(()=>new Error('Read failed'))]){
    const c=screen({status:()=>of({companyId:'company',state:'Reconciled'}),openings:()=>openingResult,landed:()=>of([landed]),journal:()=>of([])});
    StockValuationComponent.prototype.reload.call(c);assert.equal(c.status(),null);assert.equal(c.canApprove(opening),false);assert.deepEqual(c.entries(),[]);assert.equal(c.busy(),false);
  }
});
