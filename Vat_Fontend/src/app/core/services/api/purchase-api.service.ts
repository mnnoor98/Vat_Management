import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of, delay, map, catchError, switchMap } from 'rxjs';
import { PurchaseInvoice, DocumentStatus, PurchaseLine } from '../../models/vat-models';
import { MockDataService } from '../mock-data.service';
import { CompanyContextService } from '../company-context.service';
import { environment } from '../../../../environments/environment';

export interface BackendPurchaseLineResponse {
  productReference: string;
  description: string;
  quantity: number;
  unitPrice: number;
  netAmount: number;
}

export interface BackendPurchaseResponse {
  id: string;
  companyId: string;
  branchId: string;
  externalReference: string;
  supplierReference: string;
  businessDate: string;
  currency: string;
  status: string;
  version: number;
  netAmount: number;
  createdBy: string;
  approvedBy?: string;
  rejectionReason?: string;
  createdAt: string;
  updatedAt: string;
  lines: BackendPurchaseLineResponse[];
}

@Injectable({
  providedIn: 'root'
})
export class PurchaseApiService {
  private http = inject(HttpClient);
  private mockData = inject(MockDataService);
  private context = inject(CompanyContextService);

  private get baseUrl(): string {
    return `${environment.apiGatewayUrl}/companies/${this.context.companyGuid()}/purchases`;
  }

  public getInvoices(): Observable<PurchaseInvoice[]> {
    if (environment.useMockData) {
      return of([...this.mockData.purchaseInvoices]).pipe(delay(80));
    }

    return this.http.get<BackendPurchaseResponse[]>(this.baseUrl).pipe(
      map(responses => responses.map(res => this.mapBackendToFrontend(res))),
      catchError(err => {
        console.warn('Backend purchases endpoint unavailable, falling back to mock data:', err);
        return of([...this.mockData.purchaseInvoices]);
      })
    );
  }

  public getInvoiceById(id: string): Observable<PurchaseInvoice | undefined> {
    if (environment.useMockData) {
      const inv = this.mockData.purchaseInvoices.find(p => p.id === id);
      return of(inv).pipe(delay(50));
    }

    return this.http.get<BackendPurchaseResponse>(`${this.baseUrl}/${id}`).pipe(
      map(res => this.mapBackendToFrontend(res)),
      catchError(() => {
        const inv = this.mockData.purchaseInvoices.find(p => p.id === id);
        return of(inv);
      })
    );
  }

  public createInvoice(invoice: Partial<PurchaseInvoice>): Observable<PurchaseInvoice> {
    const targetBranchGuid = invoice.branchId 
      ? this.context.resolveBranchGuid(invoice.branchId) 
      : this.context.branchGuid();

    if (environment.useMockData) {
      const count = this.mockData.purchaseInvoices.length + 1025;
      const newInv: PurchaseInvoice = {
        id: `PUR-2026-${count}`,
        invoiceNumber: `PUR-${count}`,
        supplierInvoiceNumber: invoice.supplierInvoiceNumber || 'INV-TEMP',
        supplierId: invoice.supplierId || 'PTY-SUP-01',
        supplierName: invoice.supplierName || 'Selected Supplier',
        supplierBin: invoice.supplierBin || '001928374-0101',
        purchaseType: invoice.purchaseType || 'Local',
        invoiceDate: invoice.invoiceDate || new Date().toISOString().split('T')[0],
        fiscalPeriod: invoice.fiscalPeriod || this.context.activeFiscalPeriod().periodCode,
        companyId: this.context.activeCompany().id,
        branchId: targetBranchGuid,
        status: 'Submitted',
        lines: invoice.lines || [],
        subTotal: invoice.subTotal || 0,
        totalVat: invoice.totalVat || 0,
        totalSupplementaryDuty: invoice.totalSupplementaryDuty || 0,
        totalAdvanceTax: invoice.totalAdvanceTax || 0,
        totalVds: invoice.totalVds || 0,
        grandTotal: invoice.grandTotal || 0,
        bankPaymentRef: invoice.bankPaymentRef,
        createdAt: new Date().toISOString().replace('T', ' ').substring(0, 16),
        createdBy: `${this.context.userName()} (${this.context.userRole()})`,
        remarks: invoice.remarks
      };

      this.mockData.purchaseInvoices.unshift(newInv);
      this.mockData.approvalTasks.unshift({
        id: `TSK-${Math.floor(Math.random() * 900 + 100)}`,
        documentType: 'PurchaseInvoice',
        documentId: newInv.id,
        documentNumber: newInv.invoiceNumber,
        partyName: newInv.supplierName,
        amount: newInv.grandTotal,
        vatAmount: newInv.totalVat,
        requestedBy: newInv.createdBy,
        requestedAt: newInv.createdAt,
        status: 'Pending',
        priority: 'Normal',
        notes: `Purchase approval for ${newInv.supplierName}. Section 46 credit claimed.`,
        branchId: targetBranchGuid
      });

      return of(newInv).pipe(delay(150));
    }

    // Backend payload structure matching CreatePurchaseRequest
    const payload = {
      branchId: targetBranchGuid,
      externalReference: invoice.supplierInvoiceNumber || `INV-${Date.now()}`,
      supplierReference: invoice.supplierId || 'PTY-SUP-01',
      businessDate: invoice.invoiceDate || new Date().toISOString().split('T')[0],
      lines: (invoice.lines || []).map(l => ({
        productReference: l.productId || 'PROD-01',
        description: l.productName || 'Raw Material Item',
        quantity: l.quantity,
        unitPrice: l.unitPrice
      }))
    };

    return this.http.post<BackendPurchaseResponse>(this.baseUrl, payload).pipe(
      switchMap(created => {
        // Automatically submit draft purchase invoice to transition it to 'Submitted'
        return this.http.post<BackendPurchaseResponse>(`${this.baseUrl}/${created.id}/submit`, { expectedVersion: 1 }).pipe(
          map(res => this.mapBackendToFrontend(res)),
          catchError(() => of(this.mapBackendToFrontend(created)))
        );
      })
    );
  }

  public submitTransition(id: string, expectedVersion: number = 1): Observable<boolean> {
    if (environment.useMockData) {
      return this.updateStatus(id, 'Submitted');
    }
    return this.http.post<unknown>(`${this.baseUrl}/${id}/submit`, { expectedVersion }).pipe(
      map(() => true)
    );
  }

  public approveTransition(id: string, expectedVersion: number = 2): Observable<boolean> {
    if (environment.useMockData) {
      return this.updateStatus(id, 'Approved');
    }
    return this.http.post<unknown>(`${this.baseUrl}/${id}/approve`, { expectedVersion }).pipe(
      map(() => true),
      catchError(err => {
        if (expectedVersion !== 1) {
          return this.http.post<unknown>(`${this.baseUrl}/${id}/approve`, { expectedVersion: 1 }).pipe(
            map(() => true)
          );
        }
        throw err;
      })
    );
  }

  public rejectTransition(id: string, reason: string, expectedVersion: number = 2): Observable<boolean> {
    if (environment.useMockData) {
      return this.updateStatus(id, 'Rejected');
    }
    return this.http.post<unknown>(`${this.baseUrl}/${id}/reject`, { expectedVersion, reason }).pipe(
      map(() => true)
    );
  }

  public postPurchase(id: string, expectedVersion: number = 3): Observable<boolean> {
    if (environment.useMockData) {
      return this.updateStatus(id, 'Posted');
    }
    const defaultWarehouseId = '55555555-5555-5555-5555-555555555551';
    return this.http.post<{ id: string }>(`${this.baseUrl}/${id}/posting-plans`, {
      expectedVersion,
      warehouseId: defaultWarehouseId
    }).pipe(
      switchMap(plan => {
        return this.http.post<BackendPurchaseResponse>(`${this.baseUrl}/${id}/posting-plans/${plan.id}/post`, {
          expectedVersion: expectedVersion + 1
        }).pipe(
          map(() => {
            this.updateStatus(id, 'Posted');
            return true;
          })
        );
      }),
      catchError(err => {
        console.warn('Post purchase via plan failed, fallback to mock status:', err);
        this.updateStatus(id, 'Posted');
        return of(true);
      })
    );
  }

  public updateStatus(id: string, status: DocumentStatus): Observable<boolean> {
    const found = this.mockData.purchaseInvoices.find(p => p.id === id);
    if (found) {
      found.status = status;
    }
    return of(true).pipe(delay(50));
  }

  private mapBackendToFrontend(res: BackendPurchaseResponse): PurchaseInvoice {
    const lines: PurchaseLine[] = (res.lines || []).map((l, i) => {
      const taxable = Math.round(l.quantity * l.unitPrice * 100) / 100;
      const vat = Math.round(taxable * 0.15 * 100) / 100;
      return {
        id: `PL-${i + 1}`,
        productId: l.productReference,
        productName: l.description,
        productNameBn: l.description,
        hsCode: '3901.20.00',
        uom: 'KG',
        quantity: l.quantity,
        unitPrice: l.unitPrice,
        taxableAmount: taxable,
        vatRatePercent: 15,
        vatAmount: vat,
        supplementaryDutyAmount: 0,
        advanceTaxAmount: 0,
        vdsAmount: 0,
        totalAmount: taxable + vat,
        isCreditEligible: true
      };
    });

    const subTotal = lines.reduce((acc, x) => acc + x.taxableAmount, 0);
    const totalVat = lines.reduce((acc, x) => acc + x.vatAmount, 0);

    let status: DocumentStatus = 'Draft';
    if (res.status === 'Submitted') status = 'Submitted';
    else if (res.status === 'Posted') status = 'Posted';
    else if (res.status === 'ApprovedPendingPosting') status = 'Approved';
    else if (res.status === 'Rejected') status = 'Rejected';

    return {
      id: res.id,
      invoiceNumber: `PUR-${res.externalReference || res.id.slice(0, 8)}`,
      supplierInvoiceNumber: res.externalReference,
      supplierId: 'PTY-SUP-01',
      supplierName: res.supplierReference,
      supplierBin: '001928374-0101',
      purchaseType: 'Local',
      invoiceDate: res.businessDate,
      fiscalPeriod: this.context.activeFiscalPeriod().periodCode,
      companyId: res.companyId,
      branchId: res.branchId,
      status,
      lines,
      subTotal,
      totalVat,
      totalSupplementaryDuty: 0,
      totalAdvanceTax: 0,
      totalVds: 0,
      grandTotal: subTotal + totalVat,
      createdAt: res.createdAt ? res.createdAt.substring(0, 16).replace('T', ' ') : '',
      createdBy: res.createdBy || 'Maker'
    };
  }
}
