import '@angular/compiler';
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { Injector, runInInjectionContext } from '@angular/core';
import { firstValueFrom, of, throwError, Subject } from 'rxjs';
import { PurchaseApiService } from '../src/app/core/services/api/purchase-api.service';
import { SalesApiService } from '../src/app/core/services/api/sales-api.service';
import { RegisterReportService } from '../src/app/core/services/api/register-report.service';
import { CompanyContextService } from '../src/app/core/services/company-context.service';
import { CsvExportService } from '../src/app/core/services/csv-export.service';
import { InvoiceRegisterReportComponent } from '../src/app/shared/components/invoice-register-report.component';
const line = { productId: 'P', productName: 'Product', uom: 'PCS', quantity: 2, unitPrice: 50, taxableAmount: 100, vatRatePercent: 0, vatAmount: 0, totalAmount: 100 };
const doc = { id: 'doc', companyId: 'company', branchId: 'branch', invoiceNumber: 'REF', invoiceDate: '2026-10-15', supplierName: 'Supplier', status: 'Posted', taxCalculated: true, subTotal: 100, totalVat: 0, grandTotal: 100, lines: [line] };
function create<T>(type: new () => T, providers: { provide: unknown; useValue: unknown }[], company = () => 'company'): T {
  const injector = Injector.create({ providers: [{ provide: CompanyContextService, useValue: { companyGuid: company } }, ...providers as never[]] });
  return runInInjectionContext(injector, () => new type());
}
function scanner(getInvoices: (page: { before?: string; take: number }) => unknown, company?: () => string) {
  return create(RegisterReportService, [{ provide: PurchaseApiService, useValue: { getInvoices } }, { provide: SalesApiService, useValue: { getInvoices } }], company);
}
for (const kind of ['purchase', 'sales'] as const) {
  test(`${kind} complete report scans every page and uses the last server ID`, async () => {
    const calls: unknown[] = [];
    const service = scanner(page => { calls.push(page); return of(calls.length === 1 ? Array.from({ length: 25 }, (_, i) => ({ ...doc, id: `doc-${i}` })) : [{ ...doc, id: 'last-doc' }]); });
    const rows = await firstValueFrom(service.scan(kind)); assert.equal(rows.length, 26); assert.deepEqual(calls[1], { before: 'doc-24', take: 25 });
  });
}
test('a failed later report page never emits an incomplete result', async () => {
  let calls = 0; const service = scanner(() => ++calls === 1 ? of(Array.from({ length: 25 }, (_, i) => ({ ...doc, id: `doc-${i}` }))) : throwError(() => new Error('Page offline')));
  await assert.rejects(firstValueFrom(service.scan('purchase')), /Page offline/);
});
test('repeated document IDs stop report scanning rather than double-count', async () => {
  const service = scanner(() => of(Array.from({ length: 25 }, (_, i) => ({ ...doc, id: `doc-${i}` }))));
  await assert.rejects(firstValueFrom(service.scan('purchase')), /repeated/);
});
test('report scan limit fails closed instead of exporting a capped total', async () => {
  let page = 0; const service = scanner(() => { const n = page++; return of(Array.from({ length: 25 }, (_, i) => ({ ...doc, id: `${n}-${i}` }))); });
  await assert.rejects(firstValueFrom(service.scan('purchase')), /limit/); assert.equal(page, 100);
});
test('company changes during report scan reject even an empty later page', async () => {
  let company = 'company', calls = 0;
  const service = scanner(() => { if (++calls === 1) return of(Array.from({ length: 25 }, (_, i) => ({ ...doc, id: `doc-${i}` }))); company = 'other'; return of([]); }, () => company);
  await assert.rejects(firstValueFrom(service.scan('purchase')), /company/i);
});
test('report scan rejects a company-mismatched document', async () => {
  const service = scanner(() => of([{ ...doc, companyId: 'other' }]));
  await assert.rejects(firstValueFrom(service.scan('purchase')), /company/);
});
function report(scan: () => unknown, csv: object = {}, company?: () => string) {
  const component = create(InvoiceRegisterReportComponent, [{ provide: RegisterReportService, useValue: { scan } }, { provide: CsvExportService, useValue: csv }], company);
  component.kind = 'purchase'; component.from = '2026-10-01'; component.until = '2026-11-01'; return component;
}
test('register totals use the selected half-open dates and original posted snapshots only', () => {
  const component = report(() => of([doc, { ...doc, id: 'draft', status: 'Draft' }, { ...doc, id: 'prepared', status: 'PostingPrepared' }, { ...doc, id: 'reversed', status: 'Reversed' }, { ...doc, id: 'outside', invoiceDate: '2026-11-01' }]));
  component.load(); assert.equal(component.displayed().length, 2); assert.deepEqual(component.totals(), { net: 200, vat: 0, gross: 200 });
});
test('register exports every actual line and preserves zero VAT without fake quantities/opening balances', () => {
  let rows: unknown[][] = []; const component = report(() => of([{ ...doc, lines: [line, { ...line, productId: 'P2', quantity: 3, uom: 'KG' }] }]), { exportToCsv: (_name: string, _headers: unknown, values: unknown[][]) => rows = values });
  component.load(); component.exportToCsv(); assert.equal(rows.length, 2); assert.equal(rows[0][9], 2); assert.equal(rows[1][9], 3); assert.equal(rows[1][8], 'KG'); assert.equal(rows[0][13], 0);
});
test('a missing posted snapshot keeps totals and export unavailable', () => {
  const component = report(() => of([{ ...doc, taxCalculated: false }]), { exportToCsv: () => assert.fail('No incomplete export') });
  component.load(); component.exportToCsv(); assert.equal(component.ready(), false); assert.match(component.error(), /snapshot/);
});
test('failed report loading clears prior rows and blocks stale exports', () => {
  const component = report(() => throwError(() => new Error('Offline')), { exportToCsv: () => assert.fail('No failed export') });
  component.invoices.set([doc] as never); component.load(); component.exportToCsv(); assert.equal(component.invoices().length, 0); assert.equal(component.complete(), false);
});
test('invalid date range or company change disables export of a loaded report', () => {
  let company = 'company'; const component = report(() => of([doc]), { exportToCsv: () => assert.fail('No stale export') }, () => company);
  component.load(); component.until = '2026-02-30'; assert.equal(component.ready(), false); component.exportToCsv();
  component.until = '2026-11-01'; company = 'other'; assert.equal(component.ready(), false); component.exportToCsv();
});
test('leaving the report unsubscribes its pending page scan', () => {
  const pending = new Subject<unknown[]>(); const component = report(() => pending); component.load(); component.ngOnDestroy();
  pending.next([doc]); assert.equal(component.complete(), false); assert.equal(component.invoices().length, 0);
});
