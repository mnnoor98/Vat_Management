import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of, delay, map, catchError } from 'rxjs';
import { Coefficient43, Coefficient43Line } from '../../models/vat-models';
import { MockDataService } from '../mock-data.service';
import { CompanyContextService } from '../company-context.service';
import { environment } from '../../../../environments/environment';

export interface BackendCoefficientLineResponse {
  id: string;
  rawMaterialId: string;
  rawMaterialName: string;
  hsCode: string;
  uom: string;
  qtyRequired: number;
  wastagePercent: number;
  wastageQty: number;
  unitPrice: number;
  totalCost: number;
}

export interface BackendCoefficientResponse {
  id: string;
  declarationNumber: string;
  finishedProductId: string;
  finishedProductName: string;
  hsCode: string;
  uom: string;
  batchSize: number;
  effectiveFrom: string;
  submissionDate: string;
  status: string;
  approvedBy?: string;
  branchId?: string;
  branchName?: string;
  totalRawMaterialCost: number;
  directLaborCost: number;
  utilityCost: number;
  overheadCost: number;
  marginPercent: number;
  finalSellingPrice: number;
  materials: BackendCoefficientLineResponse[];
}

export interface CreateCoefficientPayload {
  declarationNumber: string;
  finishedProductId: string;
  finishedProductName: string;
  hsCode: string;
  uom: string;
  batchSize: number;
  effectiveFrom: string;
  submissionDate: string;
  branchId?: string;
  branchName?: string;
  directLaborCost: number;
  utilityCost: number;
  overheadCost: number;
  marginPercent: number;
  finalSellingPrice: number;
  materials: {
    productId: string;
    quantity: number;
    wastagePercent: number;
    unitPrice: number;
  }[];
}

@Injectable({
  providedIn: 'root'
})
export class ManufacturingApiService {
  private http = inject(HttpClient);
  private mockData = inject(MockDataService);
  private context = inject(CompanyContextService);

  private get baseUrl(): string {
    return `${environment.apiGatewayUrl}/companies/${this.context.companyGuid()}/manufacturing`;
  }

  /**
   * Fetch all declared Mushak 4.3 Input-Output Coefficients
   */
  public getCoefficients(): Observable<Coefficient43[]> {
    if (environment.useMockData) {
      return of(this.getMockDeclarations()).pipe(delay(50));
    }

    return this.http.get<BackendCoefficientResponse[]>(`${this.baseUrl}/coefficients`).pipe(
      map(items => {
        if (!items || items.length === 0) {
          return this.getMockDeclarations();
        }
        return items.map(c => this.mapBackendToFrontend(c));
      }),
      catchError(err => {
        console.warn('Backend manufacturing/coefficients endpoint unavailable, falling back to mock:', err);
        return of(this.getMockDeclarations());
      })
    );
  }

  /**
   * Submit a new Mushak 4.3 Input-Output Coefficient BOM declaration
   */
  public createCoefficient(payload: CreateCoefficientPayload): Observable<any> {
    if (environment.useMockData) {
      return of({ id: 'mock-' + Date.now(), status: 'Submitted', ...payload }).pipe(delay(150));
    }

    return this.http.post<any>(`${this.baseUrl}/coefficients`, payload);
  }

  /**
   * Approve Mushak 4.3 Coefficient Declaration (TaxManager / Approver)
   */
  public approveCoefficient(id: string): Observable<any> {
    if (environment.useMockData) {
      return of({ id, status: 'Approved', approvedBy: 'USR-8803' }).pipe(delay(100));
    }

    return this.http.put<any>(`${this.baseUrl}/coefficients/${id}/approve`, {});
  }

  private mapBackendToFrontend(c: BackendCoefficientResponse): Coefficient43 {
    return {
      id: c.id,
      declarationNumber: c.declarationNumber,
      finishedProductId: c.finishedProductId,
      finishedProductName: c.finishedProductName,
      hsCode: c.hsCode || '2201.10.00',
      uom: c.uom || 'CTN',
      batchSize: Number(c.batchSize || 1000),
      effectiveFrom: c.effectiveFrom,
      submissionDate: c.submissionDate,
      status: (c.status as any) || 'Submitted',
      approvedBy: c.approvedBy,
      branchId: c.branchId || '33333333-3333-3333-3333-333333333331',
      branchName: c.branchName || 'Dhaka Central Plant & HQ',
      totalRawMaterialCost: Number(c.totalRawMaterialCost || 0),
      directLaborCost: Number(c.directLaborCost || 0),
      utilityCost: Number(c.utilityCost || 0),
      overheadCost: Number(c.overheadCost || 0),
      marginPercent: Number(c.marginPercent || 15),
      finalSellingPrice: Number(c.finalSellingPrice || 0),
      materials: (c.materials || []).map(m => ({
        id: m.id,
        rawMaterialId: m.rawMaterialId,
        rawMaterialName: m.rawMaterialName,
        hsCode: m.hsCode,
        uom: m.uom,
        qtyRequired: Number(m.qtyRequired || 0),
        wastagePercent: Number(m.wastagePercent || 0),
        wastageQty: Number(m.wastageQty || 0),
        unitPrice: Number(m.unitPrice || 0),
        totalCost: Number(m.totalCost || 0)
      }))
    };
  }

  private getMockDeclarations(): Coefficient43[] {
    return [
      {
        ...this.mockData.coefficient43List[0],
        branchId: '33333333-3333-3333-3333-333333333331',
        branchName: 'Dhaka Central Plant & HQ'
      }
    ];
  }
}
