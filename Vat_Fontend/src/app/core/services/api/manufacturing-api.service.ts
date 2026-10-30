import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of, throwError } from 'rxjs';
import { CompanyContextService } from '../company-context.service';
import { environment } from '../../../../environments/environment';
export interface ManufacturingProduct { id: string; reference: string; description: string; unitOfMeasure: string; taxCode: string; }
export interface CoefficientMaterial { product: ManufacturingProduct; quantity: number; wastagePercent: number; unitPrice: number; totalCost: number; }
export interface CoefficientSnapshot {
  finishedProduct: ManufacturingProduct; batchSize: number; materials: CoefficientMaterial[];
  directLaborCost: number; utilityCost: number; overheadCost: number; totalCost: number; unitCost: number; referencesCheckedAt: string;
}
export interface CoefficientResult {
  id: string; bomId: string; revision: number; declarationNumber: string; state: string; version: number;
  createdBy: string; approvedBy?: string; effectiveFrom: string; effectiveUntil?: string | null; snapshot: CoefficientSnapshot | null;
}
export interface CreateCoefficientPayload {
  declarationNumber: string; finishedProductId: string; batchSize: number; effectiveFrom: string; effectiveUntil?: string;
  directLaborCost: number; utilityCost: number; overheadCost: number;
  materials: { productId: string; quantity: number; wastagePercent: number; unitPrice: number }[];
}
export type CoefficientAction = 'submit' | 'approve' | 'cancel';
@Injectable({ providedIn: 'root' })
export class ManufacturingApiService {
  private http = inject(HttpClient);
  private context = inject(CompanyContextService);
  private get baseUrl(): string { return `${environment.apiGatewayUrl}/companies/${this.context.companyGuid()}/manufacturing/coefficients`; }
  getCoefficients(before?: string, take = 25): Observable<CoefficientResult[]> {
    if (environment.useMockData) return of([]);
    return this.http.get<CoefficientResult[]>(this.baseUrl, { params: { take, ...(before ? { before } : {}) } });
  }
  createCoefficient(payload: CreateCoefficientPayload, idempotencyKey?: string): Observable<CoefficientResult> {
    if (environment.useMockData) return throwError(() => new Error('Manufacturing commands require the live API.'));
    return this.http.post<CoefficientResult>(this.baseUrl, payload, idempotencyKey ? { headers: { 'Idempotency-Key': idempotencyKey } } : {});
  }
  transitionCoefficient(id: string, action: CoefficientAction, expectedVersion: number, reason?: string): Observable<CoefficientResult> {
    if (environment.useMockData) return throwError(() => new Error('Manufacturing commands require the live API.'));
    const body = { expectedVersion, ...(reason !== undefined ? { reason } : {}) };
    return action === 'approve' ? this.http.put<CoefficientResult>(`${this.baseUrl}/${id}/approve`, body)
      : this.http.post<CoefficientResult>(`${this.baseUrl}/${id}/${action}`, body);
  }
}
