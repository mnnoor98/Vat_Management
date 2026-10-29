import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of, delay, map, catchError, switchMap } from 'rxjs';
import { SalesInvoice, DocumentStatus, SalesLine } from '../../models/vat-models';
import { MockDataService } from '../mock-data.service';
import { CompanyContextService } from '../company-context.service';
import { environment } from '../../../../environments/environment';

export interface BackendSalesLineResponse {
  productReference: string;
  description: string;
  quantity: number;
  unitPrice: number;
  netAmount: number;
}

export interface BackendSalesInvoiceResponse {
  id: string;
  companyId: string;
  branchId: string;
  externalReference: string;
  customerReference: string;
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
  lines: BackendSalesLineResponse[];
}

@Injectable({
  providedIn: 'root'
})
export class SalesApiService {
  private http = inject(HttpClient);
  private mockData = inject(MockDataService);
  private context = inject(CompanyContextService);

  private get baseUrl(): string {
    return `${environment.apiGatewayUrl}/companies/${this.context.companyGuid()}/sales`;
  }

  public getInvoices(): Observable<SalesInvoice[]> {
    if (environment.useMockData) {
      return of([...this.mockData.salesInvoices]).pipe(delay(80));
    }

    return this.http.get<BackendSalesInvoiceResponse[]>(this.baseUrl).pipe(
      map(responses => responses.map(res => this.mapBackendToFrontend(res))),
      catchError(err => {
        console.warn('Backend sales endpoint unavailable, falling back to mock data:', err);
        return of([...this.mockData.salesInvoices]);
      })
    );
  }

  public getInvoiceById(id: string): Observable<SalesInvoice | undefined> {
    if (environment.useMockData) {
      const inv = this.mockData.salesInvoices.find(s => s.id === id);
      return of(inv).pipe(delay(50));
    }

    return this.http.get<BackendSalesInvoiceResponse>(`${this.baseUrl}/${id}`).pipe(
      map(res => this.mapBackendToFrontend(res)),
      catchError(() => {
        const inv = this.mockData.salesInvoices.find(s => s.id === id);
        return of(inv);
      })
    );
  }

  public createSalesInvoice(invoice: Partial<SalesInvoice>): Observable<SalesInvoice> {
    const targetBranchGuid = invoice.branchId 
      ? this.context.resolveBranchGuid(invoice.branchId) 
      : this.context.branchGuid();

    if (environment.useMockData) {
      const count = this.mockData.salesInvoices.length + 43;
      const newInv: SalesInvoice = {
        id: `SAL-2026-${count.toString().padStart(4, '0')}`,
        invoiceNumber: `INV-2026-${count.toString().padStart(4, '0')}`,
        challanNumber63: `CH-DHK-2026-10-${(count + 100).toString().padStart(5, '0')}`,
        customerId: invoice.customerId || 'PTY-CUST-01',
        customerName: invoice.customerName || 'Selected Customer',
        customerNameBn: invoice.customerNameBn || 'গ্রাহকের নাম',
        customerBin: invoice.customerBin || '000456123-0101',
        customerAddress: invoice.customerAddress || 'Dhaka',
        destinationAddress: invoice.destinationAddress || 'Dhaka Central',
        vehicleTypeAndNo: invoice.vehicleTypeAndNo || 'Covered Van (Metro-14-1122)',
        deliveryDate: invoice.deliveryDate || new Date().toISOString().split('T')[0],
        deliveryTime: invoice.deliveryTime || '11:00 AM',
        salesType: invoice.salesType || 'Standard',
        fiscalPeriod: invoice.fiscalPeriod || this.context.activeFiscalPeriod().periodCode,
        companyId: this.context.activeCompany().id,
        branchId: targetBranchGuid,
        status: 'Approved',
        lines: invoice.lines || [],
        subTotal: invoice.subTotal || 0,
        totalVat: invoice.totalVat || 0,
        totalSupplementaryDuty: invoice.totalSupplementaryDuty || 0,
        totalVdsDeducted: invoice.totalVdsDeducted || 0,
        grandTotal: invoice.grandTotal || 0,
        createdAt: new Date().toISOString().replace('T', ' ').substring(0, 16),
        createdBy: `${this.context.userName()} (${this.context.userRole()})`,
        remarks: invoice.remarks
      };

      this.mockData.salesInvoices.unshift(newInv);
      return of(newInv).pipe(delay(150));
    }

    // Backend payload structure matching CreateSalesInvoiceRequest
    const isGuidCustomer = !!(invoice.customerId && invoice.customerId.length === 36 && invoice.customerId.includes('-'));
    const customerRef = isGuidCustomer && (invoice as { customerCode?: string }).customerCode 
      ? (invoice as { customerCode?: string }).customerCode! 
      : (invoice.customerName || 'PTY-CUST-01');

    const payload = {
      branchId: targetBranchGuid,
      externalReference: invoice.challanNumber63 || `CH-6-3-${Date.now().toString().slice(-6)}`,
      customerId: isGuidCustomer ? invoice.customerId : undefined,
      customerReference: customerRef,
      businessDate: invoice.deliveryDate || new Date().toISOString().split('T')[0],
      lines: (invoice.lines || []).map(l => ({
        productReference: l.productId || 'PROD-PACK-01',
        description: l.productName || 'Finished Goods Product',
        quantity: l.quantity,
        unitPrice: l.unitPrice
      }))
    };

    return this.http.post<BackendSalesInvoiceResponse>(this.baseUrl, payload).pipe(
      switchMap(created => {
        // Automatically submit draft sales invoice to transition it to 'Submitted'
        return this.http.post<BackendSalesInvoiceResponse>(`${this.baseUrl}/${created.id}/submit`, { expectedVersion: 1 }).pipe(
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

  public postSalesInvoice(id: string, expectedVersion: number = 3): Observable<boolean> {
    if (environment.useMockData) {
      return this.updateStatus(id, 'Posted');
    }
    const defaultWarehouseId = '55555555-5555-5555-5555-555555555551';
    return this.http.post<unknown>(`${this.baseUrl}/${id}/reserve`, {
      expectedVersion,
      warehouseId: defaultWarehouseId
    }).pipe(
      switchMap(() => {
        return this.http.post<BackendSalesInvoiceResponse>(`${this.baseUrl}/${id}/post`, {
          expectedVersion: expectedVersion + 1
        }).pipe(
          map(() => {
            this.updateStatus(id, 'Posted');
            return true;
          })
        );
      }),
      catchError(err => {
        console.warn('Post sales invoice via reserve/post failed, fallback to mock status:', err);
        this.updateStatus(id, 'Posted');
        return of(true);
      })
    );
  }

  public updateStatus(id: string, status: DocumentStatus): Observable<boolean> {
    const found = this.mockData.salesInvoices.find(s => s.id === id);
    if (found) {
      found.status = status;
    }
    return of(true).pipe(delay(50));
  }

  private mapBackendToFrontend(res: BackendSalesInvoiceResponse): SalesInvoice {
    const lines: SalesLine[] = (res.lines || []).map((l, i) => {
      const taxable = Math.round(l.quantity * l.unitPrice * 100) / 100;
      const vat = Math.round(taxable * 0.15 * 100) / 100;
      const matchedProd = this.mockData.products.find(p => p.id === l.productReference || p.code === l.productReference);
      return {
        id: `SL-${i + 1}`,
        productId: l.productReference,
        productName: l.description,
        productNameBn: matchedProd?.nameBn || l.description,
        hsCode: matchedProd?.hsCode || '3923.30.90',
        uom: matchedProd?.uom || 'PCS',
        quantity: l.quantity,
        unitPrice: l.unitPrice,
        taxableAmount: taxable,
        vatRatePercent: 15,
        vatAmount: vat,
        supplementaryDutyAmount: 0,
        vdsAmount: 0,
        totalAmount: taxable + vat
      };
    });

    const subTotal = lines.reduce((acc, x) => acc + x.taxableAmount, 0);
    const totalVat = lines.reduce((acc, x) => acc + x.vatAmount, 0);

    let status: DocumentStatus = 'Draft';
    if (res.status === 'Submitted') status = 'Submitted';
    else if (res.status === 'Posted') status = 'Posted';
    else if (res.status === 'ApprovedPendingPosting') status = 'Approved';
    else if (res.status === 'Rejected') status = 'Rejected';
    else if (res.status === 'Draft') status = 'Draft';

    // Resolve customer details from customerSnapshot or party code/reference
    const snapshot = (res as { customerSnapshot?: { name?: string; address?: string; taxRegistrationReference?: string } }).customerSnapshot;
    const matchedParty = this.mockData.parties.find(p => p.id === res.customerReference || p.code === res.customerReference || p.name === res.customerReference);
    
    const customerName = snapshot?.name || matchedParty?.name || res.customerReference;
    const customerNameBn = matchedParty?.nameBn || customerName;
    const customerBin = snapshot?.taxRegistrationReference || matchedParty?.bin || '000456123-0101';
    const customerAddress = snapshot?.address || matchedParty?.address || 'Dhaka, Bangladesh';

    return {
      id: res.id,
      invoiceNumber: `INV-${res.externalReference || res.id.slice(0, 8)}`,
      challanNumber63: res.externalReference || `CH-6-3-${res.id.slice(0, 8)}`,
      customerId: (res as { customerId?: string }).customerId || 'PTY-CUST-01',
      customerName,
      customerNameBn,
      customerBin,
      customerAddress,
      destinationAddress: customerAddress,
      vehicleTypeAndNo: 'Covered Van (Dhaka Metro-U-14-8921)',
      deliveryDate: res.businessDate,
      deliveryTime: '11:00 AM',
      salesType: 'Standard',
      fiscalPeriod: this.context.activeFiscalPeriod().periodCode,
      companyId: res.companyId,
      branchId: res.branchId,
      status,
      lines,
      subTotal,
      totalVat,
      totalSupplementaryDuty: 0,
      totalVdsDeducted: 0,
      grandTotal: subTotal + totalVat,
      createdAt: res.createdAt ? res.createdAt.substring(0, 16).replace('T', ' ') : '',
      createdBy: res.createdBy || 'Maker'
    };
  }
}
