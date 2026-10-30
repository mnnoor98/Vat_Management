import { throwError } from 'rxjs';
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of, delay, map, catchError, switchMap, forkJoin } from 'rxjs';
import { SalesInvoice, DocumentStatus, SalesLine } from '../../models/vat-models';
import { MockDataService } from '../mock-data.service';
import { CompanyContextService } from '../company-context.service';
import { CalculationSnapshot, FinancialPlan, InvoicePage } from '../../models/financial-contracts';
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

  public getInvoices(page: InvoicePage = {}): Observable<SalesInvoice[]> {
    if (environment.useMockData) {
      const items = this.mockData.salesInvoices;
      const start = page.before ? items.findIndex(x => x.id === page.before) + 1 : 0;
      return of(items.slice(start, start + (page.take ?? 25))).pipe(delay(80));
    }

    return this.http.get<BackendSalesInvoiceResponse[]>(this.baseUrl, { params: { take: page.take ?? 25, ...(page.before ? { before: page.before } : {}) } }).pipe(
      switchMap(responses => responses.length ? forkJoin(responses.map(res => this.hydrate(res))) : of([])),
      catchError(error => throwError(() => error))
    );
  }

  public getInvoiceById(id: string): Observable<SalesInvoice | undefined> {
    if (environment.useMockData) {
      const inv = this.mockData.salesInvoices.find(s => s.id === id);
      return of(inv).pipe(delay(50));
    }

    return this.http.get<BackendSalesInvoiceResponse>(`${this.baseUrl}/${id}`).pipe(
      switchMap(res => this.hydrate(res)),
      catchError(error => throwError(() => error))
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
        customerBin: invoice.customerBin || '',
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
        productReference: l.productReference || l.productId,
        description: l.productName || 'Finished Goods Product',
        quantity: l.quantity,
        unitPrice: l.unitPrice
      }))
    };

    return this.http.post<BackendSalesInvoiceResponse>(this.baseUrl, payload).pipe(
      switchMap(created => {
        // Automatically submit draft sales invoice to transition it to 'Submitted'
        return this.http.post<BackendSalesInvoiceResponse>(`${this.baseUrl}/${created.id}/submit`, { expectedVersion: created.version }).pipe(
          map(res => this.mapBackendToFrontend(res)),
          catchError(error => throwError(() => error))
        );
      })
    );
  }

  public submitTransition(id: string, expectedVersion: number): Observable<boolean> {
    if (environment.useMockData) {
      return this.updateStatus(id, 'Submitted');
    }
    return this.http.post<unknown>(`${this.baseUrl}/${id}/submit`, { expectedVersion }).pipe(
      map(() => true)
    );
  }

  public approveTransition(id: string, expectedVersion: number): Observable<boolean> {
    if (environment.useMockData) {
      return this.updateStatus(id, 'Approved');
    }
    return this.http.post<unknown>(`${this.baseUrl}/${id}/approve`, { expectedVersion }).pipe(
      map(() => true),
      catchError(error => throwError(() => error))
    );
  }

  public rejectTransition(id: string, reason: string, expectedVersion: number): Observable<boolean> {
    if (environment.useMockData) {
      return this.updateStatus(id, 'Rejected');
    }
    return this.http.post<unknown>(`${this.baseUrl}/${id}/reject`, { expectedVersion, reason }).pipe(
      map(() => true)
    );
  }

  public preparePosting(id: string, expectedVersion: number, warehouseId: string): Observable<FinancialPlan> {
    return this.http.post<FinancialPlan>(`${this.baseUrl}/${id}/reserve`, { expectedVersion, warehouseId });
  }

  public getPostingPlan(id: string): Observable<FinancialPlan> {
    return this.http.get<FinancialPlan>(`${this.baseUrl}/${id}/fulfillment`);
  }

  public postSalesInvoice(id: string, expectedVersion: number): Observable<boolean> {
    if (environment.useMockData) return this.updateStatus(id, 'Posted');
    return this.http.post<BackendSalesInvoiceResponse>(`${this.baseUrl}/${id}/post`, { expectedVersion }).pipe(map(() => true));
  }

  public updateStatus(id: string, status: DocumentStatus): Observable<boolean> {
    if (!environment.useMockData) return throwError(() => new Error('Reload the document and use a versioned server command.'));
    const found = this.mockData.salesInvoices.find(s => s.id === id);
    if (found) {
      found.status = status;
    }
    return of(true).pipe(delay(50));
  }

  private hydrate(res: BackendSalesInvoiceResponse): Observable<SalesInvoice> {
    if (!['Reserved', 'Posted', 'CreditPending', 'Credited', 'PartiallyCredited', 'DebitPending'].includes(res.status)) return of(this.mapBackendToFrontend(res));
    return this.getPostingPlan(res.id).pipe(map(plan => {
      const invoice = this.mapBackendToFrontend(res, plan.snapshot);
      invoice.warehouseId = plan.warehouseId;
      invoice.postingPlanId = plan.id;
      return invoice;
    }));
  }

  private mapBackendToFrontend(res: BackendSalesInvoiceResponse, snapshot?: CalculationSnapshot): SalesInvoice {
    const lines: SalesLine[] = (snapshot?.lines ?? res.lines ?? []).map((l, i) => {
      const calculated = snapshot?.lines[i];
      const taxable = calculated?.netAmount ?? l.netAmount;
      const vat = calculated?.taxAmount ?? calculated?.outputTaxAmount ?? 0;
      const matchedProd = this.mockData.products.find(p => p.id === l.productReference || p.code === l.productReference);
      return {
        id: `SL-${i + 1}`,
        productId: l.productReference,
        productName: l.description,
        productNameBn: matchedProd?.nameBn || l.description,
        hsCode: '',
        uom: calculated?.unitOfMeasure ?? '',
        quantity: l.quantity,
        unitPrice: l.unitPrice,
        taxableAmount: taxable,
        vatRatePercent: calculated?.ratePercent ?? 0,
        vatAmount: vat,
        supplementaryDutyAmount: 0,
        vdsAmount: 0,
        totalAmount: taxable + vat
      };
    });

    const subTotal = lines.reduce((acc, x) => acc + x.taxableAmount, 0);
    const totalVat = lines.reduce((acc, x) => acc + x.vatAmount, 0);

    const status = (res.status === 'ApprovedPendingPosting' ? 'Approved' : res.status) as DocumentStatus;

    // Resolve customer details from customerSnapshot or party code/reference
    const customerSnapshot = (res as { customerSnapshot?: { name?: string; address?: string; taxRegistrationReference?: string } }).customerSnapshot;
    const matchedParty = this.mockData.parties.find(p => p.id === res.customerReference || p.code === res.customerReference || p.name === res.customerReference);
    
    const customerName = customerSnapshot?.name || matchedParty?.name || res.customerReference;
    const customerNameBn = matchedParty?.nameBn || customerName;
    const customerBin = customerSnapshot?.taxRegistrationReference || matchedParty?.bin || '';
    const customerAddress = customerSnapshot?.address || matchedParty?.address || '';

    return {
      id: res.id,
      version: res.version,
      taxCalculated: !!snapshot,
      invoiceNumber: `INV-${res.externalReference || res.id.slice(0, 8)}`,
      challanNumber63: res.externalReference || `CH-6-3-${res.id.slice(0, 8)}`,
      customerId: (res as { customerId?: string }).customerId || res.customerReference,
      customerName,
      customerNameBn,
      customerBin,
      customerAddress,
      destinationAddress: customerAddress,
      vehicleTypeAndNo: '',
      deliveryDate: res.businessDate,
      deliveryTime: '',
      salesType: 'Standard',
      fiscalPeriod: res.businessDate.slice(0, 7),
      companyId: res.companyId,
      branchId: res.branchId,
      status,
      lines,
      subTotal,
      totalVat,
      totalSupplementaryDuty: 0,
      totalVdsDeducted: 0,
      grandTotal: snapshot?.grossAmount ?? res.netAmount,
      createdAt: res.createdAt ? res.createdAt.substring(0, 16).replace('T', ' ') : '',
      createdBy: res.createdBy || 'Maker'
    };
  }
}
