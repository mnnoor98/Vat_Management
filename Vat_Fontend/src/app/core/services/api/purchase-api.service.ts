import { throwError } from 'rxjs';
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of, delay, map, catchError, switchMap, forkJoin } from 'rxjs';
import { PurchaseInvoice, DocumentStatus, PurchaseLine } from '../../models/vat-models';
import { MockDataService } from '../mock-data.service';
import { CompanyContextService } from '../company-context.service';
import { CalculationSnapshot, FinancialPlan, InvoicePage } from '../../models/financial-contracts';
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

  public getInvoices(page: InvoicePage = {}): Observable<PurchaseInvoice[]> {
    if (environment.useMockData) {
      const items = this.mockData.purchaseInvoices;
      const start = page.before ? items.findIndex(x => x.id === page.before) + 1 : 0;
      return of(items.slice(start, start + (page.take ?? 25))).pipe(delay(80));
    }

    return this.http.get<BackendPurchaseResponse[]>(this.baseUrl, { params: { take: page.take ?? 25, ...(page.before ? { before: page.before } : {}) } }).pipe(
      switchMap(responses => responses.length ? forkJoin(responses.map(res => this.hydrate(res))) : of([])),
      catchError(error => throwError(() => error))
    );
  }

  public getInvoiceById(id: string): Observable<PurchaseInvoice | undefined> {
    if (environment.useMockData) {
      const inv = this.mockData.purchaseInvoices.find(p => p.id === id);
      return of(inv).pipe(delay(50));
    }

    return this.http.get<BackendPurchaseResponse>(`${this.baseUrl}/${id}`).pipe(
      switchMap(res => this.hydrate(res)),
      catchError(error => throwError(() => error))
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
        supplierId: invoice.supplierId || '',
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
      supplierReference: invoice.supplierId,
      businessDate: invoice.invoiceDate || new Date().toISOString().split('T')[0],
      lines: (invoice.lines || []).map(l => ({
        productReference: l.productReference || l.productId,
        description: l.productName || 'Raw Material Item',
        quantity: l.quantity,
        unitPrice: l.unitPrice
      }))
    };

    return this.http.post<BackendPurchaseResponse>(this.baseUrl, payload).pipe(
      switchMap(created => {
        // Automatically submit draft purchase invoice to transition it to 'Submitted'
        return this.http.post<BackendPurchaseResponse>(`${this.baseUrl}/${created.id}/submit`, { expectedVersion: created.version }).pipe(
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
    return this.http.post<FinancialPlan>(`${this.baseUrl}/${id}/posting-plans`, { expectedVersion, warehouseId });
  }

  public getPostingPlan(id: string): Observable<FinancialPlan> {
    return this.http.get<{ latestPlan: FinancialPlan | null }>(`${this.baseUrl}/${id}/posting-status`).pipe(
      switchMap(status => status.latestPlan ? of(status.latestPlan) : throwError(() => new Error('No prepared posting plan exists.')))
    );
  }

  public postPurchase(id: string, expectedVersion: number, planId: string): Observable<boolean> {
    if (environment.useMockData) return this.updateStatus(id, 'Posted');
    return this.http.post<BackendPurchaseResponse>(`${this.baseUrl}/${id}/posting-plans/${planId}/post`, { expectedVersion }).pipe(map(() => true));
  }

  public updateStatus(id: string, status: DocumentStatus): Observable<boolean> {
    if (!environment.useMockData) return throwError(() => new Error('Reload the document and use a versioned server command.'));
    const found = this.mockData.purchaseInvoices.find(p => p.id === id);
    if (found) {
      found.status = status;
    }
    return of(true).pipe(delay(50));
  }

  private hydrate(res: BackendPurchaseResponse): Observable<PurchaseInvoice> {
    if (!['PostingPrepared', 'Posted', 'ReversalPending', 'Reversed'].includes(res.status)) return of(this.mapBackendToFrontend(res));
    return this.getPostingPlan(res.id).pipe(map(plan => {
      const invoice = this.mapBackendToFrontend(res, plan.snapshot);
      invoice.warehouseId = plan.warehouseId;
      invoice.postingPlanId = plan.id;
      return invoice;
    }));
  }

  private mapBackendToFrontend(res: BackendPurchaseResponse, snapshot?: CalculationSnapshot): PurchaseInvoice {
    const lines: PurchaseLine[] = (snapshot?.lines ?? res.lines ?? []).map((l, i) => {
      const calculated = snapshot?.lines[i];
      const taxable = calculated?.netAmount ?? l.netAmount;
      const vat = calculated?.taxAmount ?? calculated?.outputTaxAmount ?? 0;
      return {
        id: `PL-${i + 1}`,
        productId: l.productReference,
        productName: l.description,
        productNameBn: l.description,
        hsCode: '',
        uom: calculated?.unitOfMeasure ?? '',
        quantity: l.quantity,
        unitPrice: l.unitPrice,
        taxableAmount: taxable,
        vatRatePercent: calculated?.ratePercent ?? 0,
        vatAmount: vat,
        supplementaryDutyAmount: 0,
        advanceTaxAmount: 0,
        vdsAmount: 0,
        totalAmount: taxable + vat,
        isCreditEligible: (calculated?.recoverablePercent ?? 0) > 0
      };
    });

    const subTotal = lines.reduce((acc, x) => acc + x.taxableAmount, 0);
    const totalVat = lines.reduce((acc, x) => acc + x.vatAmount, 0);

    const status = (res.status === 'ApprovedPendingPosting' ? 'Approved' : res.status) as DocumentStatus;

    return {
      id: res.id,
      version: res.version,
      taxCalculated: !!snapshot,
      invoiceNumber: `PUR-${res.externalReference || res.id.slice(0, 8)}`,
      supplierInvoiceNumber: res.externalReference,
      supplierId: res.supplierReference,
      supplierName: snapshot?.supplierName ?? res.supplierReference,
      supplierBin: '',
      purchaseType: 'Local',
      invoiceDate: res.businessDate,
      fiscalPeriod: res.businessDate.slice(0, 7),
      companyId: res.companyId,
      branchId: res.branchId,
      status,
      lines,
      subTotal,
      totalVat,
      totalSupplementaryDuty: 0,
      totalAdvanceTax: 0,
      totalVds: 0,
      grandTotal: snapshot?.grossAmount ?? res.netAmount,
      createdAt: res.createdAt ? res.createdAt.substring(0, 16).replace('T', ' ') : '',
      createdBy: res.createdBy || 'Maker'
    };
  }
}
