import { throwError } from 'rxjs';
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of, delay, catchError } from 'rxjs';
import { Mushak91Return, Coefficient43, VdsCertificate, VatLedgerResponse, AdjustmentsResponse } from '../../models/vat-models';
import { MockDataService } from '../mock-data.service';
import { CompanyContextService } from '../company-context.service';
import { environment } from '../../../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class ComplianceApiService {
  private http = inject(HttpClient);
  private mockData = inject(MockDataService);
  private context = inject(CompanyContextService);

  private get baseUrl(): string {
    return `${environment.apiGatewayUrl}/companies/${this.context.companyGuid()}/compliance`;
  }

  public getReturn91(fiscalPeriod: string): Observable<Mushak91Return> {
    if (environment.useMockData) {
      return of({ ...this.mockData.currentReturn91 }).pipe(delay(90));
    }
    return this.http.get<Mushak91Return>(`${this.baseUrl}/returns/9-1?period=${fiscalPeriod}`).pipe(
      catchError(error => throwError(() => error))
    );
  }

  public submitReturn91(returnDoc: Mushak91Return): Observable<any> {
    if (environment.useMockData) {
      this.mockData.currentReturn91.status = 'Submitted';
      return of({ success: true, status: 'Submitted' }).pipe(delay(200));
    }
    return this.http.post<any>(`${this.baseUrl}/returns/9-1/submit`, returnDoc).pipe(
      catchError(error => throwError(() => error))
    );
  }

  public getCoefficient43List(): Observable<Coefficient43[]> {
    if (environment.useMockData) {
      return of([...this.mockData.coefficient43List]).pipe(delay(60));
    }
    return this.http.get<Coefficient43[]>(`${this.baseUrl}/coefficients/4-3`).pipe(
      catchError(error => throwError(() => error))
    );
  }

  public createCoefficient43(item: Partial<Coefficient43>): Observable<Coefficient43> {
    if (environment.useMockData) {
      const newItem: Coefficient43 = {
        id: `DEC-43-2026-00${this.mockData.coefficient43List.length + 5}`,
        declarationNumber: `DEC-4.3/2026/0${this.mockData.coefficient43List.length + 5}`,
        finishedProductId: item.finishedProductId || 'PROD-03',
        finishedProductName: item.finishedProductName || 'Finished Good',
        hsCode: item.hsCode || '2201.10.00',
        uom: item.uom || 'CTN',
        batchSize: item.batchSize || 1000,
        effectiveFrom: item.effectiveFrom || new Date().toISOString().split('T')[0],
        submissionDate: new Date().toISOString().split('T')[0],
        status: 'Submitted',
        totalRawMaterialCost: item.totalRawMaterialCost || 0,
        directLaborCost: item.directLaborCost || 0,
        utilityCost: item.utilityCost || 0,
        overheadCost: item.overheadCost || 0,
        marginPercent: item.marginPercent || 15,
        finalSellingPrice: item.finalSellingPrice || 0,
        materials: item.materials || []
      };
      this.mockData.coefficient43List.unshift(newItem);
      return of(newItem).pipe(delay(150));
    }
    return this.http.post<Coefficient43>(`${this.baseUrl}/coefficients/4-3`, item);
  }

  public getVdsCertificates(period?: string): Observable<VdsCertificate[]> {
    const url = period ? `${this.baseUrl}/vds-certificates?period=${period}` : `${this.baseUrl}/vds-certificates`;
    return this.http.get<VdsCertificate[]>(url).pipe(
      catchError(error => throwError(() => error))
    );
  }

  public issueVdsCertificate(item: Partial<VdsCertificate>): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/vds-certificates`, item);
  }

  public recordTreasuryDeposit(id: string, challan: { treasuryChallanNo: string; treasuryChallanDate: string; bankBranch: string }): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/vds-certificates/${id}/deposit`, challan);
  }

  public getVatLedger(period?: string): Observable<VatLedgerResponse> {
    const url = period ? `${this.baseUrl}/vat-ledger?period=${period}` : `${this.baseUrl}/vat-ledger`;
    return this.http.get<VatLedgerResponse>(url);
  }

  public recordLedgerDeposit(deposit: { depositAmount: number; treasuryChallanNo: string; depositDate: string; bankBranch: string; economicCode?: string }): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/vat-ledger/deposit`, deposit);
  }

  public getAdjustments(period?: string): Observable<AdjustmentsResponse> {
    const url = period ? `${this.baseUrl}/adjustments?period=${period}` : `${this.baseUrl}/adjustments`;
    return this.http.get<AdjustmentsResponse>(url);
  }

  public issueCreditNote(note: { originalDocumentNumber: string; customerName: string; reason: string; netAmount: number; vatAmount: number; correctionDate: string }): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/credit-notes`, note);
  }

  public issueDebitNote(note: { originalDocumentNumber: string; customerName: string; reason: string; netAmount: number; vatAmount: number; correctionDate: string }): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/debit-notes`, note);
  }
}
