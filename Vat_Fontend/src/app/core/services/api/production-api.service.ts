import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { CompanyContextService } from '../company-context.service';
import { environment } from '../../../../environments/environment';
export interface ProductionProduct {id:string;reference:string;unitOfMeasure:string;}
export interface ProductionUom {productId:string;inputUnitOfMeasure:string;stockUnitsPerInputUnit:string;}
export interface ProductionByProduct {product:ProductionProduct;classification:'ByProduct'|'Scrap';}
export interface ProductionEvidence {uomConversions?:ProductionUom[]|null;byProducts?:ProductionByProduct[]|null;coefficientId:string;revision:number;coefficientVersion:number;warehouseId:string;warehouseName:string;businessDate:string;finishedProduct:ProductionProduct;batchQuantity:number;plannedQuantity:number;materials:{product:ProductionProduct;quantityPerBatch:number;wastagePercent:number}[];}
export interface ProductionOrderItem {id:string;reference:string;state:string;version:number;}
export interface ProductionOrder extends ProductionOrderItem {companyId:string;createdBy:string;approvedBy:string|null;evidence:ProductionEvidence;plannedQuantityExact:string;}
export interface ProductionOrderDraft {uomConversions?:ProductionUom[];byProducts?:{productId:string;classification:'ByProduct'|'Scrap'}[];reference:string;coefficientId:string;warehouseId:string;businessDate:string;plannedQuantity:string;}
export interface ProductionSession {id:string;companyId:string;orderId:string;reference:string;state:string;version:number;wipValue:string;receivedQuantity:string;plannedQuantity:string;source:ProductionEvidence;}
export interface ProductionLine {unitOfMeasure?:string|null;inputQuantity?:string|null;warehouseId:string;productId:string;quantity:string;originalIssueId?:string|null;id?:string|null;}
export type ProductionKind='Issue'|'Return'|'Conversion'|'Receipt'|'ByProduct'|'Loss'|'Close'|'Reverse';
export interface ProductionDraft {outputUnitOfMeasure?:string|null;outputProductId?:string|null;outputWarehouseId?:string|null;reversesOperationId?:string|null;expectedSessionVersion:number;kind:ProductionKind;businessDate:string;evidence:string;lines:ProductionLine[];amount:string;outputQuantity:string;labor:string;utility:string;overhead:string;}
export interface ProductionOperation {outputUnitOfMeasure?:string|null;outputInputQuantity?:string|null;outputProductId?:string|null;outputWarehouseId?:string|null;reversesOperationId?:string|null;id:string;sessionId:string;kind:ProductionKind;businessDate:string;state:string;version:number;sessionVersion:number;evidence:string;lines:ProductionLine[];amount:string;outputQuantity:string;labor:string;utility:string;overhead:string;postedValue:string;createdBy:string;approvedBy:string|null;}
@Injectable({providedIn:'root'})
export class ProductionApiService {
  private http=inject(HttpClient);private context=inject(CompanyContextService);
  private get root(){return `${environment.apiGatewayUrl}/companies/${this.context.companyGuid()}`;}
  orders(before?:string){return this.http.get<ProductionOrderItem[]>(`${this.root}/manufacturing/production-orders`,{params:{take:25,...(before?{before}:{})}});}
  order(id:string){return this.http.get<ProductionOrder>(`${this.root}/manufacturing/production-orders/${id}`);}
  createOrder(draft:ProductionOrderDraft,key:string){return this.post<ProductionOrder>('manufacturing/production-orders',draft,key);}
  orderAction(order:ProductionOrder,action:'submit'|'approve',key:string){return this.post<ProductionOrder>(`manufacturing/production-orders/${order.id}/${action}`,{expectedVersion:order.version},key);}
  sessions(before?:string){return this.http.get<ProductionSession[]>(`${this.root}/inventory/production-executions`,{params:{take:25,...(before?{before}:{})}});}
  session(id:string){return this.http.get<ProductionSession>(`${this.root}/inventory/production-executions/${id}`);}
  importOrder(orderId:string,key:string){return this.post<ProductionSession>('inventory/production-executions',{orderId},key);}
  operations(session:string,before?:string){return this.http.get<ProductionOperation[]>(`${this.root}/inventory/production-executions/${session}/operations`,{params:{take:25,...(before?{before}:{})}});}
  operation(session:string,id:string){return this.http.get<ProductionOperation>(`${this.root}/inventory/production-executions/${session}/operations/${id}`);}
  createOperation(session:string,draft:ProductionDraft,key:string){return this.post<ProductionOperation>(`inventory/production-executions/${session}/operations`,draft,key);}
  approveOperation(session:ProductionSession,op:ProductionOperation,key:string){return this.post<ProductionOperation>(`inventory/production-executions/${session.id}/operations/${op.id}/approve`,{expectedVersion:op.version,expectedSessionVersion:session.version},key);}
  cancelOperation(session:string,op:ProductionOperation,reason:string,key:string){return this.post<ProductionOperation>(`inventory/production-executions/${session}/operations/${op.id}/cancel`,{expectedVersion:op.version,reason},key);}
  private post<T>(path:string,body:unknown,key:string):Observable<T>{if(environment.useMockData)return throwError(()=>new Error('Production requires the live owner and financial APIs.'));return this.http.post<T>(`${this.root}/${path}`,body,{headers:{'Idempotency-Key':key}});}
}
