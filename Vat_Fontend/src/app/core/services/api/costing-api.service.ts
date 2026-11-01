import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { CompanyContextService } from '../company-context.service';
import { environment } from '../../../../environments/environment';

export interface CostingPolicy {
  id: string; companyId: string; productId: string | null; method: 'FIFO' | 'WEIGHTED_AVERAGE';
  effectiveFrom: string; effectiveUntil: string; state: 'Draft' | 'Approved'; version: number;
  createdBy: string; createdAt: string; approvedBy: string | null; approvedAt: string | null;
}
export interface CostingDraft { productId: string | null; method: 'FIFO' | 'WEIGHTED_AVERAGE'; effectiveFrom: string; effectiveUntil: string; }
export interface CostPreview {
  companyId: string; warehouseId: string; productId: string; state: 'Preview' | 'Unavailable'; reason: string | null;
  basis: 'PurchaseNetOnly'; supportedSourceSequence: string | null; sourceCount: number; capturedAt: string;
  calculation: null | { method: 'FIFO' | 'WEIGHTED_AVERAGE'; receivedQuantity: string; receivedValue: string;
    issuedQuantity: string; issuedValue: string; closingQuantity: string; closingValue: string;
    issues: { sourceId: string; quantity: string; value: string; allocations: { receiptId: string | null; quantity: string; value: string }[] }[] };
}
@Injectable({ providedIn: 'root' })
export class CostingApiService {
  private http = inject(HttpClient);
  private context = inject(CompanyContextService);
  private get root(): string { return `${environment.apiGatewayUrl}/companies/${this.context.companyGuid()}/inventory/costing-policies`; }
  preview(warehouseId: string, productId: string): Observable<CostPreview> {
    return this.http.get<CostPreview>(`${environment.apiGatewayUrl}/companies/${this.context.companyGuid()}/inventory/cost-preview`, { params: { warehouseId, productId } });
  }
  list(before?: string): Observable<CostingPolicy[]> {
    return this.http.get<CostingPolicy[]>(this.root, { params: { take: 25, ...(before ? { before } : {}) } });
  }
  create(draft: CostingDraft, key: string): Observable<CostingPolicy> {
    if (environment.useMockData) return throwError(() => new Error('Costing commands require the live API.'));
    return this.http.post<CostingPolicy>(this.root, draft, { headers: { 'Idempotency-Key': key } });
  }
  approve(policy: CostingPolicy, key: string): Observable<CostingPolicy> {
    if (environment.useMockData) return throwError(() => new Error('Costing commands require the live API.'));
    return this.http.post<CostingPolicy>(`${this.root}/${policy.id}/approve`, { expectedVersion: policy.version }, { headers: { 'Idempotency-Key': key } });
  }
}
