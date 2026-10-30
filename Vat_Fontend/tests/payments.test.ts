import '@angular/compiler';
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { Injector, runInInjectionContext } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom, of, throwError, Subject } from 'rxjs';
import { PaymentsApiService, Receipt, ReceiptCorrection } from '../src/app/core/services/api/payments-api.service';
import { PaymentsComponent } from '../src/app/features/payments/payments.component';
import { MasterDataApiService } from '../src/app/core/services/api/master-data-api.service';
import { CompanyContextService } from '../src/app/core/services/company-context.service';
import { CsvExportService } from '../src/app/core/services/csv-export.service';
function create<T>(type: new () => T, providers: { provide: unknown; useValue: unknown }[], actor = 'checker', permissions = () => true) {
  const injector = Injector.create({ providers: [{ provide: CompanyContextService, useValue: { companyGuid: () => 'company', currentUser: () => ({ id: actor }), hasPermission: permissions } }, ...providers as never[]] });
  return runInInjectionContext(injector, () => new type());
}
const document = { id: 'receipt', companyId: 'company', customerId: 'customer', reference: 'RCPT', method: 'Bank', evidenceReference: 'BANK-REF', paymentDate: '2026-10-01', amount: 100, currency: 'BDT', status: 'Posted', version: 11, requestedBy: 'maker', requestedAllocations: [] };
const receipt: Receipt = { payment: document, allocations: [{ id: 'allocation', invoiceId: 'invoice', amount: 50, allocationDate: '2026-10-01', paymentVersion: 3 }], allocationReleases: [{ id: 'release', correctionId: 'past', allocationId: 'allocation', invoiceId: 'invoice', businessDate: '2026-10-02', amount: 20 }], allocatedAmount: 30, unallocatedAmount: 70, returnedAmount: 0, netCollectedAmount: 100 };
const account = { customerId: 'customer', currency: 'BDT', salesGrossAmount: 50, collectedAmount: 100, allocatedAmount: 30, unallocatedAmount: 70, balance: -50, returnedAmount: 0, netCollectedAmount: 100, invoices: [{ invoiceId: 'invoice', reference: 'INV', grossAmount: 50, allocatedAmount: 30, outstandingAmount: 20 }] };
const note: ReceiptCorrection = { id: 'note', paymentId: 'receipt', customerId: 'customer', sourceVersion: 42, version: 1, mode: 'Refund', status: 'Pending', correctionDate: '2026-10-03', amount: 30, method: 'Bank', reason: 'Return', evidenceReference: 'REFUND-REF', requestedBy: 'requester', releases: [{ allocationId: 'allocation', invoiceId: 'invoice', amount: 30 }] };
function api(http: object) { return create(PaymentsApiService, [{ provide: HttpClient, useValue: http }]); }

test('payment list sends verified customer and cursor parameters', async () => {
  const service = api({ get: (url: string, options: unknown) => { assert.match(url, /companies\/company\/payments$/); assert.deepEqual(options, { params: { customerId: 'customer', before: 'cursor', take: 25 } }); return of([document]); } });
  const rows = await firstValueFrom(service.list('customer', 'cursor')); assert.equal(rows[0].version, 11);
});
test('receipt creation preserves explicit idempotency and does not auto-post', async () => {
  let calls = 0; const service = api({ post: (url: string, body: unknown, options: unknown) => { calls++; assert.match(url, /payments$/); assert.deepEqual(options, { headers: { 'Idempotency-Key': 'stable' } }); assert.equal((body as { allocations: unknown[] }).allocations.length, 0); return of(receipt); } });
  await firstValueFrom(service.create({ customerId: 'customer', reference: 'RCPT', method: 'Bank', evidenceReference: 'REF', paymentDate: '2026-10-01', amount: 100, allocations: [] }, 'stable')); assert.equal(calls, 1);
});
test('receipt posting and allocation send actual loaded versions and dates', async () => {
  const calls: unknown[][] = []; const service = api({ post: (...args: unknown[]) => { calls.push(args); return of(receipt); } });
  await firstValueFrom(service.post('receipt', 11)); await firstValueFrom(service.allocate('receipt', 19, '2026-10-02', [{ invoiceId: 'invoice', amount: 20 }]));
  assert.match(calls[0][0] as string, /receipt\/post$/); assert.deepEqual(calls[0][1], { expectedVersion: 11 });
  assert.deepEqual(calls[1][1], { expectedVersion: 19, allocationDate: '2026-10-02', allocations: [{ invoiceId: 'invoice', amount: 20 }] });
});
test('correction decisions use pending sourceVersion rather than correction version', async () => {
  const calls: unknown[][] = []; const service = api({ post: (...args: unknown[]) => { calls.push(args); return of(note); } });
  await firstValueFrom(service.decide('receipt', note, 'approve')); await firstValueFrom(service.decide('receipt', { ...note, mode: 'Reversal' }, 'cancel', 'Duplicate'));
  assert.match(calls[0][0] as string, /refunds\/note\/approve$/); assert.deepEqual(calls[0][1], { expectedVersion: 42 });
  assert.match(calls[1][0] as string, /reversals\/note\/cancel$/); assert.deepEqual(calls[1][1], { expectedVersion: 42, reason: 'Duplicate' });
});
test('refund requests preserve selected original allocation releases and command key', async () => {
  const body = { expectedVersion: 11, correctionDate: '2026-10-03', reason: 'Return', evidenceReference: 'REF', amount: 30, method: 'Bank', releases: [{ allocationId: 'allocation', amount: 30 }] };
  const service = api({ post: (url: string, sent: unknown, options: unknown) => { assert.match(url, /receipt\/refunds$/); assert.deepEqual(sent, body); assert.deepEqual(options, { headers: { 'Idempotency-Key': 'refund-key' } }); return of(note); } });
  await firstValueFrom(service.requestRefund('receipt', body, 'refund-key'));
});
test('customer reports send half-open statement range and independent ageing date', async () => {
  const calls: unknown[][] = []; const service = api({ get: (...args: unknown[]) => { calls.push(args); return of({}); } });
  await firstValueFrom(service.statement('customer', '2026-10-01', '2026-11-01')); await firstValueFrom(service.ageing('customer', '2026-10-31'));
  assert.deepEqual(calls[0][1], { params: { from: '2026-10-01', until: '2026-11-01' } }); assert.deepEqual(calls[1][1], { params: { asOf: '2026-10-31' } });
});
function screen(overrides: object = {}, actor = 'checker', permissions = () => true, csv: object = {}) {
  const service = { list: () => of([]), receivables: () => of(account), get: () => of(receipt), corrections: () => of([]), ...overrides };
  const component = create(PaymentsComponent, [{ provide: PaymentsApiService, useValue: service }, { provide: MasterDataApiService, useValue: { getParties: () => of([{ id: 'customer', code: 'C', name: 'Customer', isActive: true }, { id: 'other', code: 'O', name: 'Other', isActive: true }]) } }, { provide: CsvExportService, useValue: csv }], actor, permissions);
  component.ngOnInit(); component.customerId = 'customer'; component.selectCustomer(); component.review('receipt'); return component;
}
test('maker cannot post their receipt; financial permissions are exact', () => {
  const maker = screen({ get: () => of({ ...receipt, payment: { ...document, status: 'Pending' } }) }, 'maker'); assert.equal(maker.allowed('post'), false);
  const checker = screen({ get: () => of({ ...receipt, payment: { ...document, status: 'Pending' } }) }); assert.equal(checker.allowed('post'), true);
  const noGrant = screen({}, 'checker', () => false); assert.equal(noGrant.allowed('allocate'), false); assert.equal(noGrant.allowed('create'), false);
});
test('allocation blocks excess invoice amount and sends only the selected invoice', () => {
  let calls = 0; const component = screen({ allocate: (_id: string, version: number, date: string, lines: unknown) => { calls++; assert.equal(version, 11); assert.equal(date, '2026-10-03'); assert.deepEqual(lines, [{ invoiceId: 'invoice', amount: 20 }]); return of(receipt); } });
  component.invoiceId = 'invoice'; component.allocationDate = '2026-10-03'; component.allocationAmount = 21; component.stage('allocate'); component.confirm(); assert.equal(calls, 0);
  component.allocationAmount = 20; component.confirm(); assert.equal(calls, 1);
});
test('refund cannot release more than the remaining original allocation', () => {
  let calls = 0; const component = screen({ requestRefund: (_id: string, body: { releases: unknown }, _key: string) => { calls++; assert.deepEqual(body.releases, [{ allocationId: 'allocation', amount: 30 }]); return of(note); } });
  component.reason = 'Refund'; component.correctionEvidence = 'REF'; component.correctionDate = '2026-10-03'; component.refundAmount = 30;
  component.releaseAmounts = { allocation: 31 }; component.stage('refund'); component.confirm(); assert.equal(calls, 0); assert.equal(component.released('allocation'), 20);
  component.releaseAmounts.allocation = 30; component.confirm(); assert.equal(calls, 1);
});
test('full receipt reversal is a request using the receipt version and never auto-approves', () => {
  let calls = 0; const component = screen({ requestReversal: (_id: string, body: { expectedVersion: number }) => { calls++; assert.equal(body.expectedVersion, 11); return of({ ...note, mode: 'Reversal' }); }, decide: () => assert.fail('Request is not approval') });
  component.reason = 'Duplicate'; component.correctionEvidence = 'REF'; component.correctionDate = '2026-10-03'; component.stage('reverse'); component.confirm(); assert.equal(calls, 1);
});
test('correction approval blocks both receipt maker and correction requester', () => {
  const overrides = { get: () => of({ ...receipt, payment: { ...document, status: 'CorrectionPending', version: 42 } }), corrections: () => of([note]) };
  assert.equal(screen(overrides, 'maker').allowed('approveCorrection', note), false);
  assert.equal(screen(overrides, 'requester').allowed('approveCorrection', note), false);
  assert.equal(screen(overrides).allowed('approveCorrection', note), true);
  assert.equal(screen(overrides).allowed('approveCorrection', { ...note, sourceVersion: 1 }), false);
});
test('a failed financial command retains state and requires reload, without guessed retries', () => {
  let calls = 0; const component = screen({ allocate: () => { calls++; return throwError(() => new Error('Posting disabled')); } });
  component.invoiceId = 'invoice'; component.allocationAmount = 10; component.stage('allocate'); component.confirm(); component.stage('allocate'); component.confirm();
  assert.equal(calls, 1); assert.equal(component.reloadRequired(), true); assert.equal(component.receipt()?.payment.version, 11); assert.equal(component.receipt()?.payment.status, 'Posted');
});
test('pending receipt retries keep their idempotency key and validate monetary precision', () => {
  const keys: string[] = []; const component = screen({ create: (_body: unknown, key: string) => { keys.push(key); return throwError(() => new Error('Offline')); } });
  Object.assign(component.draft, { reference: 'NEW', evidenceReference: 'EVIDENCE', amount: 10.12345 }); component.stage('create'); component.confirm(); assert.equal(keys.length, 0);
  component.draft.amount = 10; component.confirm(); component.stage('create'); component.confirm(); assert.equal(keys.length, 2); assert.equal(keys[0], keys[1]);
  component.draft.amount = 11; component.stage('create'); component.confirm(); assert.notEqual(keys[2], keys[0]);
});
test('customer changes clear pending financial selections and disable the prior receipt', () => {
  const component = screen(); component.draft.amount = 100; component.refundAmount = 20; component.releaseAmounts = { allocation: 20 }; component.stage('refund');
  component.customerId = 'other'; component.selectCustomer(); assert.equal(component.receipt(), null); assert.equal(component.pending(), null); assert.equal(component.draft.amount, 0); assert.equal(component.refundAmount, 0); assert.deepEqual(component.releaseAmounts, {});
});
test('receipt review refuses a mismatched customer or requested document', () => {
  const component = screen({ get: () => of({ ...receipt, payment: { ...document, id: 'wrong' } }) });
  assert.equal(component.detailReady(), false); assert.equal(component.allowed('allocate'), false); assert.match(component.error(), /scope mismatch/);
});
const statement = { customerId: 'customer', currency: 'BDT', from: '2026-10-01', until: '2026-11-01', capturedAt: '2026-11-02T10:00:00Z', openingBalance: 50, totalDebit: 0, totalCredit: 20, closingBalance: 30, lines: [{ documentId: 'receipt', invoiceId: null, businessDate: '2026-10-10', type: 'Payment', documentNumber: 'RCPT', netAmount: 0, outputTaxAmount: 0, balanceChange: -20, runningBalance: 30 }] };
const ageing = { customerId: 'customer', currency: 'BDT', asOf: '2026-10-31', capturedAt: '2026-11-02T10:01:00Z', buckets: { notDue: 0, overdue1To30: 0, overdue31To60: 0, overdue61To90: 0, overdueOver90: 0, unknownDueDate: 30 }, outstandingDebitAmount: 30, invoiceCreditAmount: 0, collectedAmount: 20, allocatedAmount: 20, unallocatedAmount: 0, balance: 30, returnedAmount: 0, netCollectedAmount: 20, invoices: [{ invoiceId: 'invoice', reference: 'INV', businessDate: '2026-10-01', dueDate: null, daysOverdue: null, bucket: 'UnknownDueDate', grossAmount: 50, allocatedAmount: 20, outstandingAmount: 30 }] };
test('statement and ageing preserve signed balances, server capture time and unknown due dates in export', () => {
  const exports: unknown[][] = []; const component = screen({ statement: () => of(statement), ageing: () => of(ageing) }, 'checker', () => true, { exportToCsv: (...args: unknown[]) => exports.push(args) });
  component.from = statement.from; component.until = statement.until; component.asOf = ageing.asOf; component.loadReports();
  assert.equal(component.reportsReady(), true); assert.equal(component.statement()?.closingBalance, 30); assert.equal(component.ageing()?.buckets.unknownDueDate, 30);
  component.exportStatement(); component.exportAgeing(); assert.equal((exports[0][2] as unknown[][])[0][10], -20); assert.equal((exports[1][2] as unknown[][])[0][5], 'Unknown'); assert.equal((exports[1][2] as unknown[][])[0][6], 'Unknown');
  component.asOf = '2026-10-30'; component.exportStatement(); assert.equal(exports.length, 2);
});
test('oversized statement range and report scope mismatch block exports', () => {
  let calls = 0; const component = screen({ statement: () => { calls++; return of({ ...statement, customerId: 'other' }); }, ageing: () => of(ageing) });
  component.from = '2024-01-01'; component.until = '2026-11-01'; component.loadReports(); assert.equal(calls, 0);
  component.from = statement.from; component.until = statement.until; component.asOf = ageing.asOf; component.loadReports(); assert.equal(component.reportsReady(), false); assert.match(component.reportError(), /scope mismatch/);
});
test('leaving the customer view unsubscribes pending report reads', () => {
  const pending = new Subject<unknown>(); const component = screen({ statement: () => pending, ageing: () => of(ageing) });
  component.loadReports(); component.ngOnDestroy(); pending.next(statement); pending.complete(); assert.equal(component.statement(), null);
});

test('summary exports preserve balances even when statement and ageing have no invoice lines', () => {
  const exports: unknown[][] = []; const cashCredit = { ...ageing, invoices: [], outstandingDebitAmount: 0, netCollectedAmount: 100, unallocatedAmount: 100, balance: -100 };
  const component = screen({ statement: () => of({ ...statement, lines: [], openingBalance: -100, totalDebit: 0, totalCredit: 0, closingBalance: -100 }), ageing: () => of(cashCredit) }, 'checker', () => true, { exportToCsv: (...args: unknown[]) => exports.push(args) });
  component.from = statement.from; component.until = statement.until; component.asOf = ageing.asOf; component.loadReports();
  component.exportSummary('statement'); component.exportSummary('ageing');
  assert.equal((exports[0][2] as unknown[][])[0][8], -100); assert.equal((exports[1][2] as unknown[][])[0][16], 100); assert.equal((exports[1][2] as unknown[][])[0][17], -100);
});
test('report export is disabled while a financial command is in progress', () => {
  const component = screen({ statement: () => of(statement), ageing: () => of(ageing) }, 'checker', () => true, { exportToCsv: () => assert.fail('No export during command') });
  component.from = statement.from; component.until = statement.until; component.asOf = ageing.asOf; component.loadReports(); component.busy.set(true);
  component.exportSummary('ageing'); component.exportStatement(); assert.equal(component.reportsReady(), false);
});
