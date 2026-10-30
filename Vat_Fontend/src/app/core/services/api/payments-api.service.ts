import { Observable, throwError } from 'rxjs';
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { CompanyContextService } from '../company-context.service';
export interface AllocationInput { invoiceId: string; amount: number; }
export interface ReceiptDocument { id: string; companyId: string; customerId: string; reference: string; method: string; evidenceReference: string; paymentDate: string; amount: number; currency: string; status: string; version: number; requestedBy: string; approvedBy?: string; postedAt?: string; requestedAllocations: AllocationInput[]; }
export interface ReceiptAllocation { id: string; invoiceId: string; amount: number; allocationDate: string; paymentVersion: number; }
export interface ReceiptRelease { id: string; correctionId: string; allocationId: string; invoiceId: string; businessDate: string; amount: number; }
export interface Receipt { payment: ReceiptDocument; allocations: ReceiptAllocation[]; allocatedAmount: number; unallocatedAmount: number; returnedAmount: number; netCollectedAmount: number; allocationReleases: ReceiptRelease[]; lastCorrectionDate?: string; }
export interface ReceiptCorrection { id: string; paymentId: string; customerId: string; sourceVersion: number; version: number; mode: 'Refund' | 'Reversal'; status: string; correctionDate: string; amount: number; method: string; reason: string; evidenceReference: string; requestedBy: string; approvedBy?: string; documentNumber?: string; releases: { allocationId: string; invoiceId: string; amount: number }[]; }
export interface Receivables { customerId: string; currency: string; salesGrossAmount: number; collectedAmount: number; allocatedAmount: number; unallocatedAmount: number; balance: number; returnedAmount: number; netCollectedAmount: number; invoices: { invoiceId: string; reference: string; grossAmount: number; allocatedAmount: number; outstandingAmount: number }[]; }
export interface Statement { customerId: string; currency: string; from: string; until: string; capturedAt: string; openingBalance: number; totalDebit: number; totalCredit: number; closingBalance: number; lines: { documentId: string; invoiceId?: string; businessDate: string; type: string; documentNumber: string; netAmount: number; outputTaxAmount: number; balanceChange: number; runningBalance: number }[]; }
export interface Ageing { customerId: string; currency: string; asOf: string; capturedAt: string; buckets: { notDue: number; overdue1To30: number; overdue31To60: number; overdue61To90: number; overdueOver90: number; unknownDueDate: number }; outstandingDebitAmount: number; invoiceCreditAmount: number; collectedAmount: number; allocatedAmount: number; unallocatedAmount: number; balance: number; returnedAmount: number; netCollectedAmount: number; invoices: { invoiceId: string; reference: string; businessDate: string; dueDate?: string; daysOverdue?: number; bucket: string; grossAmount: number; allocatedAmount: number; outstandingAmount: number }[]; }
export interface CreateReceipt { customerId: string; reference: string; method: string; evidenceReference: string; paymentDate: string; amount: number; allocations: AllocationInput[]; }
export interface CorrectionRequest { expectedVersion: number; correctionDate: string; reason: string; evidenceReference: string; }
import { environment } from '../../../../environments/environment';
@Injectable({ providedIn: 'root' })
export class PaymentsApiService {
  private http = inject(HttpClient); private context = inject(CompanyContextService);
  private get base(): string { return `${environment.apiGatewayUrl}/companies/${this.context.companyGuid()}`; }
  private write<T>(url: string, body: unknown, options?: { headers: Record<string, string> }): Observable<T> {
    if (environment.useMockData) return throwError(() => new Error('Payment commands require the live API.'));
    return this.http.post<T>(url, body, options);
  }
  list(customerId: string, before?: string) { return this.http.get<ReceiptDocument[]>(`${this.base}/payments`, { params: { customerId, take: 25, ...(before ? { before } : {}) } }); }
  get(id: string) { return this.http.get<Receipt>(`${this.base}/payments/${id}`); }
  corrections(id: string) { return this.http.get<ReceiptCorrection[]>(`${this.base}/payments/${id}/corrections`); }
  receivables(customer: string) { return this.http.get<Receivables>(`${this.base}/customers/${customer}/receivables`); }
  create(body: CreateReceipt, key: string) { return this.write<Receipt>(`${this.base}/payments`, body, { headers: { 'Idempotency-Key': key } }); }
  post(id: string, expectedVersion: number) { return this.write<Receipt>(`${this.base}/payments/${id}/post`, { expectedVersion }); }
  cancel(id: string, expectedVersion: number, reason: string) { return this.write<Receipt>(`${this.base}/payments/${id}/cancel`, { expectedVersion, reason }); }
  allocate(id: string, expectedVersion: number, allocationDate: string, allocations: AllocationInput[]) { return this.write<Receipt>(`${this.base}/payments/${id}/allocate`, { expectedVersion, allocationDate, allocations }); }
  requestReversal(id: string, body: CorrectionRequest, key: string) { return this.write<ReceiptCorrection>(`${this.base}/payments/${id}/reversals`, body, { headers: { 'Idempotency-Key': key } }); }
  requestRefund(id: string, body: CorrectionRequest & { amount: number; method: string; releases: { allocationId: string; amount: number }[] }, key: string) { return this.write<ReceiptCorrection>(`${this.base}/payments/${id}/refunds`, body, { headers: { 'Idempotency-Key': key } }); }
  decide(id: string, note: ReceiptCorrection, action: 'approve' | 'cancel', reason?: string) { return this.write<ReceiptCorrection>(`${this.base}/payments/${id}/${note.mode === 'Reversal' ? 'reversals' : 'refunds'}/${note.id}/${action}`, { expectedVersion: note.sourceVersion, ...(action === 'cancel' ? { reason } : {}) }); }
  statement(customer: string, from: string, until: string) { return this.http.get<Statement>(`${this.base}/customers/${customer}/statement`, { params: { from, until } }); }
  ageing(customer: string, asOf: string) { return this.http.get<Ageing>(`${this.base}/customers/${customer}/ageing`, { params: { asOf } }); }
}
