import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { CompanyContextService } from '../company-context.service';
import { environment } from '../../../../environments/environment';

export interface OpeningPosition { warehouseId:string; warehouse:string; productId:string; product:string; quantity:string; }
export interface OpeningLine { warehouseId:string; productId:string; quantity:string; value:string; method?:string; }
export interface OpeningDraft { businessDate:string; basis:string; evidence:string; lines:OpeningLine[]; }
export interface OpeningReview extends OpeningDraft { id:string; companyId:string; state:string; version:number; createdBy:string; approvedBy:string|null; }
export interface LandedLine { purchaseLineId:string; amount:string; }
export interface LandedDraft { planId:string; totalAmount:string; evidence:string; lines:LandedLine[]; }
export interface LandedReview extends LandedDraft { id:string; companyId:string; state:string; version:number; createdBy:string; approvedBy:string|null; }
export interface LandedSource { planId:string; purchaseReference:string; warehouse:string; businessDate:string; lines:{purchaseLineId:string; product:string; quantity:string; netAmount:string}[]; }
export interface LedgerStatus { companyId:string; state:string; basis:string|null; openingDate:string|null; sequence:string|null; reason:string|null; stockValue:string|null; }
export interface LedgerEntry { sequence:string; warehouseId:string; productId:string; kind:string; sourceId:string; originalSourceId:string|null; businessDate:string; method:string; quantity:string; value:string; allocations:{id:string;origin:string;quantity:string;value:string}[]; }
@Injectable({providedIn:'root'})
export class StockValuationApiService {
  private http=inject(HttpClient); private context=inject(CompanyContextService);
  private get root(){return `${environment.apiGatewayUrl}/companies/${this.context.companyGuid()}/inventory`;}
  status(){return this.http.get<LedgerStatus>(`${this.root}/cost-ledger/status`);}
  journal(before?:string){return this.http.get<LedgerEntry[]>(`${this.root}/cost-ledger/entries`,{params:{take:25,...(before?{before}:{})}});}
  positions(skip:number){return this.http.get<OpeningPosition[]>(`${this.root}/cost-opening-positions`,{params:{skip}});}
  sources(before?:string){return this.http.get<LandedSource[]>(`${this.root}/landed-cost-sources`,{params:before?{before}:{}});}
  source(id:string){return this.http.get<LandedSource>(`${this.root}/landed-cost-sources/${id}`);}
  openings(before?:string){return this.http.get<OpeningReview[]>(`${this.root}/cost-adoptions`,{params:{take:25,...(before?{before}:{})}});}
  landed(before?:string){return this.http.get<LandedReview[]>(`${this.root}/landed-cost-reviews`,{params:{take:25,...(before?{before}:{})}});}
  createOpening(draft:OpeningDraft,key:string){return this.command<OpeningReview>('cost-adoptions',draft,key);}
  createLanded(draft:LandedDraft,key:string){return this.command<LandedReview>('landed-cost-reviews',draft,key);}
  approveOpening(review:OpeningReview,key:string){return this.command<OpeningReview>(`cost-adoptions/${review.id}/approve`,{expectedVersion:review.version},key);}
  approveLanded(review:LandedReview,key:string){return this.command<LandedReview>(`landed-cost-reviews/${review.id}/approve`,{expectedVersion:review.version},key);}
  private command<T>(route:string,body:unknown,key:string):Observable<T>{
    if(environment.useMockData)return throwError(()=>new Error('Valuation commands require the live API.'));
    return this.http.post<T>(`${this.root}/${route}`,body,{headers:{'Idempotency-Key':key}});
  }
}
