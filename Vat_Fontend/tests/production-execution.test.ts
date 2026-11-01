import '@angular/compiler';
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { Injector,runInInjectionContext,signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { of,throwError,Subject,Subscription,firstValueFrom } from 'rxjs';
import { CompanyContextService } from '../src/app/core/services/company-context.service';
import { ProductionApiService,ProductionSession,ProductionOperation } from '../src/app/core/services/api/production-api.service';
import { ProductionExecutionComponent } from '../src/app/features/manufacturing/production-execution.component';
const head:ProductionSession={id:'session',companyId:'company',orderId:'order',reference:'PRODUCTION',state:'Open',version:7,wipValue:'12345678901234567890.1234',receivedQuantity:'4.000000',plannedQuantity:'10.000000',source:{coefficientId:'bom',revision:1,coefficientVersion:3,warehouseId:'output',warehouseName:'Output',businessDate:'2026-10-31',finishedProduct:{id:'fg',reference:'FG',unitOfMeasure:'PCS'},batchQuantity:10,plannedQuantity:10,materials:[{product:{id:'raw',reference:'RAW',unitOfMeasure:'KG'},quantityPerBatch:20,wastagePercent:5}]}};
const op:ProductionOperation={id:'operation',sessionId:'session',kind:'Receipt',businessDate:'2026-10-31',state:'Draft',version:1,sessionVersion:7,evidence:'Reviewed actual receipt',lines:[],amount:'100.0000',outputQuantity:'1.000000',labor:'0.0000',utility:'0.0000',overhead:'0.0000',postedValue:'0.0000',createdBy:'maker',approvedBy:null};

test('reversal approval requires the loaded original source and matching identity',()=>{
  const c=screen({});const reversal:ProductionOperation={...op,kind:'Reverse',reversesOperationId:'original'};
  assert.equal(c.canApproveOperation(reversal),false);
  c.selectedOriginal.set({...op,id:'other',state:'Posted'});assert.equal(c.canApproveOperation(reversal),false);
  c.selectedOriginal.set({...op,id:'original',state:'Posted'});assert.equal(c.canApproveOperation(reversal),true);
  c.selectedOriginal.set({...op,id:'original',kind:'Issue',state:'Posted'});assert.equal(c.canApproveOperation(reversal),false);
});

test('reviewing a different reversal ignores a late original-source response',()=>{
  const response=new Subject<ProductionOperation>();const c=screen({operation:()=>response});
  c.reviewOperation({...op,kind:'Reverse',reversesOperationId:'old-source'});c.reviewOperation({...op,id:'another'});
  response.next({...op,id:'old-source',state:'Posted'});assert.equal(c.selectedOriginal(),null);assert.equal(c.selectedOperation()?.id,'another');
});

test('copying a stale reversal retains the original reference without inventing money',()=>{
  const c=screen({});c.copyDraft({...op,kind:'Reverse',reversesOperationId:'original',amount:'0.0000',outputQuantity:'0.000000'});
  assert.equal(c.draft.reversesOperationId,'original');assert.equal(c.draft.expectedSessionVersion,head.version);assert.equal(c.draft.amount,'0.0000');assert.equal(c.draft.outputQuantity,'0.000000');
});

test('copying converted operations preserves input units and avoids converting stock quantity twice',()=>{
  const c=screen({});c.copyDraft({...op,outputQuantity:'4.000000',outputInputQuantity:'2.000000',outputUnitOfMeasure:'PAIR'});
  assert.equal(c.draft.outputQuantity,'2.000000');assert.equal(c.draft.outputUnitOfMeasure,'PAIR');
  c.copyDraft({...op,kind:'Issue',lines:[{warehouseId:'warehouse',productId:'raw',quantity:'50.000000',inputQuantity:'2.000000',unitOfMeasure:'BAG'}]});
  assert.equal(c.draft.lines[0].quantity,'2.000000');assert.equal(c.draft.lines[0].unitOfMeasure,'BAG');
});

test('changing the reviewed BOM clears ratios and physical outputs belonging to the previous products',()=>{
  const c=screen({});c.orderDraft={reference:'ORDER',coefficientId:'old',warehouseId:'warehouse',businessDate:'2026-10-31',plannedQuantity:'24',uomConversions:[{productId:'old-product',inputUnitOfMeasure:'BAG',stockUnitsPerInputUnit:'25'}],byProducts:[{productId:'scrap',classification:'Scrap'}]};
  c.changeBom('new');assert.equal(c.orderDraft.coefficientId,'new');assert.deepEqual(c.orderDraft.uomConversions,[]);assert.deepEqual(c.orderDraft.byProducts,[]);
});
test('copying a by-product draft preserves the frozen product, warehouse and entered unit',()=>{const c=screen({});c.copyDraft({...op,kind:'ByProduct',outputProductId:'scrap',outputWarehouseId:'scrap-store',outputQuantity:'4.000000',outputInputQuantity:'2.000000',outputUnitOfMeasure:'BAG'});assert.equal(c.draft.outputProductId,'scrap');assert.equal(c.draft.outputWarehouseId,'scrap-store');assert.equal(c.draft.outputQuantity,'2.000000');assert.equal(c.draft.outputUnitOfMeasure,'BAG');assert.equal(c.reversible({...op,kind:'ByProduct',state:'Posted'}),true);});

test('input unit choices come only from the frozen execution source',()=>{
  const c=screen({});c.head.set({...head,source:{...head.source,uomConversions:[{productId:'raw',inputUnitOfMeasure:'G',stockUnitsPerInputUnit:'0.001'},{productId:'fg',inputUnitOfMeasure:'PAIR',stockUnitsPerInputUnit:'2'}]}});
  assert.equal(c.stockUnit('raw'),'KG');assert.deepEqual(c.inputUnits('raw'),[{productId:'raw',inputUnitOfMeasure:'G',stockUnitsPerInputUnit:'0.001'}]);assert.deepEqual(c.inputUnits('foreign'),[]);
});
function screen(api:object){return Object.assign(Object.create(ProductionExecutionComponent.prototype),{api,scope:'company',context:{companyGuid:()=> 'company',currentUser:()=>({id:'checker'}),hasPermission:()=>true},subscriptions:new Subscription(),slots:new Map(),keys:new Map(),busy:signal(false),error:signal(''),message:signal(''),fresh:true,head:signal(head),sessions:signal([head]),operations:signal([op]),selectedOperation:signal(op),selectedOriginal:signal(null),selectedOrder:signal(null),orders:signal([]),warehouses:signal([]),products:signal([]),coefficients:signal([]),cancelReason:'',returnSource:'',draft:{expectedSessionVersion:7,kind:'Receipt',businessDate:op.businessDate,evidence:op.evidence,lines:[],amount:op.amount,outputQuantity:op.outputQuantity,labor:op.labor,utility:op.utility,overhead:op.overhead}})as ProductionExecutionComponent;}
test('production API uses company scope, exact strings and both reviewed versions',async()=>{
  const calls:unknown[][]=[];const injector=Injector.create({providers:[{provide:CompanyContextService,useValue:{companyGuid:()=> 'company'}},{provide:HttpClient,useValue:{post:(...args:unknown[])=>{calls.push(args);return of(op);}}}]});
  const api=runInInjectionContext(injector,()=>new ProductionApiService());await firstValueFrom(api.approveOperation(head,op,'stable-key'));
  assert.deepEqual(calls[0],['/api/v1/companies/company/inventory/production-executions/session/operations/operation/approve',{expectedVersion:1,expectedSessionVersion:7},{headers:{'Idempotency-Key':'stable-key'}}]);
});
test('production maker, stale WIP, closed execution and missing finance permission cannot post',()=>{
  const c=screen({approveOperation:()=>assert.fail('Posting must be denied')});assert.equal(c.canApproveOperation({...op,createdBy:'checker'}),false);assert.equal(c.canApproveOperation({...op,sessionVersion:6}),false);
  c.head.set({...head,state:'Closed'});assert.equal(c.canApproveOperation(op),false);c.head.set(head);Object.assign(c.context,{hasPermission:(p:string)=>p!=='costing.approve'});assert.equal(c.canApproveOperation(op),false);c.approveOperation(op);
});
test('failed production approval clears cost state and cannot turn into a local posted result',()=>{
  let calls=0;const c=screen({approveOperation:()=>{calls++;return throwError(()=>new Error('Conflict'));}});c.approveOperation(op);c.approveOperation(op);
  assert.equal(calls,1);assert.equal(op.state,'Draft');assert.equal(c.head(),null);assert.deepEqual(c.sessions(),[]);assert.equal(c.fresh,false);assert.equal(c.message(),'');assert.match(c.error(),/Conflict/);
});
test('production draft retries preserve typed decimal values and the same command key',()=>{
  const calls:{key:string;amount:string}[]=[];const c=screen({createOperation:(_session:string,draft:{amount:string},key:string)=>{calls.push({key,amount:draft.amount});return throwError(()=>new Error('Offline'));}});
  c.draft.amount='12345678901234567890.1234';c.createOperation();c.head.set(head);c.fresh=true;c.createOperation();
  assert.equal(calls[0].key,calls[1].key);assert.equal(calls[0].amount,'12345678901234567890.1234');assert.equal(c.message(),'');
});
test('production session reload rejects a foreign company and never keeps previous WIP',()=>{
  const c=screen({session:()=>of({...head,companyId:'foreign'}),operations:()=>of([op])});c.selectSession(head.id);assert.equal(c.head(),null);assert.deepEqual(c.operations(),[]);assert.equal(c.fresh,false);assert.match(c.error(),/mismatch/);
});
test('material return keeps the original issue identity and requires an actual returned quantity',()=>{
  const c=screen({});c.newDraft('Return');c.operations.set([{...op,kind:'Issue',state:'Posted',lines:[{id:'original-issue',warehouseId:'raw-store',productId:'raw',quantity:'8.000000'}]}]);c.returnSource='original-issue';c.addReturn();
  assert.deepEqual(c.draft.lines,[{warehouseId:'raw-store',productId:'raw',quantity:'',originalIssueId:'original-issue'}]);
});
test('a late production result from the previous company cannot update current scope',()=>{
  const pending=new Subject<ProductionOperation>();const c=screen({approveOperation:()=>pending});c.approveOperation(op);Object.assign(c.context,{companyGuid:()=> 'new-company'});pending.next({...op,state:'Posted',version:2});
  assert.equal(c.message(),'');assert.equal(c.operations()[0].state,'Draft');c.ngOnDestroy();assert.equal(pending.observers.length,0);
});
