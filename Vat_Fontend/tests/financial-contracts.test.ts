import '@angular/compiler';
import { PurchaseListComponent } from '../src/app/features/purchases/purchase-list/purchase-list.component';
import { SalesListComponent } from '../src/app/features/sales/sales-list/sales-list.component';
import { ToastService } from '../src/app/core/services/toast.service';
import { CsvExportService } from '../src/app/core/services/csv-export.service';
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { ChangeDetectorRef, Injector, runInInjectionContext } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom, of, throwError } from 'rxjs';
import { PurchaseApiService } from '../src/app/core/services/api/purchase-api.service';
import { SalesApiService } from '../src/app/core/services/api/sales-api.service';
import { InventoryApiService } from '../src/app/core/services/api/inventory-api.service';
import { CompanyContextService } from '../src/app/core/services/company-context.service';
import { MockDataService } from '../src/app/core/services/mock-data.service';
import { FinancialPostingComponent } from '../src/app/shared/components/financial-posting.component';
import { FinancialPlan } from '../src/app/core/models/financial-contracts';
import { PurchaseInvoice } from '../src/app/core/models/vat-models';

const context = { companyGuid: () => 'company', branchGuid: () => 'branch', resolveBranchGuid: (s: string) => s,
  activeFiscalPeriod: () => ({ periodCode: '2026-10' }), currentUser: () => ({ id: 'poster' }), hasPermission: () => true };
const source = { id: 'doc', companyId: 'company', branchId: 'branch', version: 37, status: 'ApprovedPendingPosting',
  externalReference: 'EXT-1', supplierReference: 'SUP-7', customerReference: 'CUST-7', businessDate: '2026-10-20',
  netAmount: 100, createdBy: 'maker', createdAt: '2026-10-20T10:00:00Z',
  lines: [{ productReference: 'P-7', description: 'Widget', quantity: 2, unitPrice: 50, netAmount: 100 }] };
const line = { productId: 'product', productReference: 'P-7', description: 'Widget', unitOfMeasure: 'PCS', quantity: 2,
  unitPrice: 50, netAmount: 100, ratePercent: 7.5, taxAmount: 7.5, recoverablePercent: 50,
  taxRuleId: 'published-rule', taxRuleVersion: 8, legalBasis: 'Configured basis', evidenceReference: 'Configured evidence' };
const plan: FinancialPlan = { id: 'plan', status: 'Prepared', warehouseId: 'warehouse', sourceVersion: 91, preparedBy: 'preparer',
  snapshot: { lines: [line], netAmount: 100, taxAmount: 7.5, grossAmount: 107.5 } };
function create<T>(type: new () => T, http: object): T {
  const injector = Injector.create({ providers: [
    { provide: HttpClient, useValue: http }, { provide: CompanyContextService, useValue: context },
    { provide: MockDataService, useValue: { products: [], parties: [], purchaseInvoices: [], salesInvoices: [] } }
  ] });
  return runInInjectionContext(injector, () => new type());
}

for (const [name, service] of [['purchase', PurchaseApiService], ['sales', SalesApiService]] as const) {
  test(`${name} pagination sends the exact cursor and preserves the returned version without inventing VAT`, async () => {
    const api = create(service, { get: (_url: string, options: unknown) => {
      assert.deepEqual(options, { params: { take: 25, before: 'cursor-id' } }); return of([source]);
    } });
    const [invoice] = await firstValueFrom(api.getInvoices({ before: 'cursor-id', take: 25 }));
    assert.equal(invoice.version, 37); assert.equal(invoice.taxCalculated, false); assert.equal(invoice.totalVat, 0);
    assert.equal(invoice.status, 'Approved');
  });
  test(`${name} creation submits the returned draft version and sends the registered product reference`, async () => {
    const bodies: Record<string, unknown>[] = [];
    const api = create(service, { post: (_url: string, body: Record<string, unknown>) => {
      bodies.push(body); return of({ ...source, version: bodies.length === 1 ? 41 : 42, status: bodies.length === 1 ? 'Draft' : 'Submitted' });
    } });
    const draft = { supplierId: 'SUP-7', customerId: 'CUST-7', branchId: 'branch', lines: [
      { productId: 'guid-product', productReference: 'P-7', productName: 'Widget', quantity: 2, unitPrice: 50 }
    ] };
    const invoice = await firstValueFrom(name === 'purchase'
      ? (api as PurchaseApiService).createInvoice(draft as never) : (api as SalesApiService).createSalesInvoice(draft as never));
    assert.equal((bodies[0]['lines'] as { productReference: string }[])[0].productReference, 'P-7');
    assert.deepEqual(bodies[1], { expectedVersion: 41 }); assert.equal(invoice.version, 42);
  });
  test(`${name} approval propagates a conflict once without guessing another version`, async () => {
    let requests = 0;
    const conflict = { status: 409 };
    const api = create(service, { post: (_url: string, body: unknown) => {
      requests++; assert.deepEqual(body, { expectedVersion: 37 }); return throwError(() => conflict);
    } });
    await assert.rejects(firstValueFrom(api.approveTransition('doc', 37)), e => e === conflict);
    assert.equal(requests, 1);
  });
}

test('purchase snapshots use the published 7.5 percent calculation and retain prepared state', async () => {
  const api = create(PurchaseApiService, { get: (url: string) => of(url.endsWith('/posting-status')
    ? { latestPlan: plan } : { ...source, status: 'PostingPrepared' }) });
  const invoice = (await firstValueFrom(api.getInvoiceById('doc')))!;
  assert.equal(invoice.status, 'PostingPrepared'); assert.equal(invoice.version, 37);
  assert.equal(invoice.totalVat, 7.5); assert.equal(invoice.grandTotal, 107.5);
  assert.equal(invoice.lines[0].vatRatePercent, 7.5); assert.equal(invoice.lines[0].uom, 'PCS');
  assert.equal(invoice.warehouseId, 'warehouse'); assert.equal(invoice.taxCalculated, true);
});

test('sales zero-rate snapshot remains zero and reserved state survives reload', async () => {
  const salesPlan = { ...plan, status: 'Reserved', snapshot: { lines: [{ ...line, taxAmount: undefined, outputTaxAmount: 0, ratePercent: 0 }],
    netAmount: 100, outputTaxAmount: 0, grossAmount: 100 } };
  const api = create(SalesApiService, { get: (url: string) => of(url.endsWith('/fulfillment')
    ? salesPlan : { ...source, status: 'Reserved' }) });
  const invoice = (await firstValueFrom(api.getInvoiceById('doc')))!;
  assert.equal(invoice.status, 'Reserved'); assert.equal(invoice.totalVat, 0); assert.equal(invoice.taxCalculated, true);
  assert.equal(invoice.lines[0].vatRatePercent, 0); assert.equal(invoice.grandTotal, 100);
});

test('a missing posting snapshot fails visibly rather than fabricating posted totals', async () => {
  const api = create(PurchaseApiService, { get: (url: string) => of(url.endsWith('/posting-status')
    ? { latestPlan: null } : { ...source, status: 'Posted' }) });
  await assert.rejects(firstValueFrom(api.getInvoiceById('doc')), /No prepared posting plan/);
});

test('warehouse pages use the financial API id contract', async () => {
  const api = create(InventoryApiService, { get: (_url: string, options: unknown) => {
    assert.deepEqual(options, { params: { skip: 100, take: 100 } });
    return of([{ id: 'warehouse', code: 'WH', name: 'Actual warehouse', branchId: 'branch', branchName: 'Actual branch' }]);
  } });
  const [warehouse] = await firstValueFrom(api.getWarehouses(100, 100));
  assert.equal(warehouse.warehouseId, 'warehouse');
});

function posting(kind: 'purchase' | 'sales', overrides: object = {}, actorContext = context) {
  const calls: unknown[][] = [];
  const api = { preparePosting: (...args: unknown[]) => { calls.push(['prepare', ...args]); return of({ ...plan, status: kind === 'purchase' ? 'Prepared' : 'Reserved' }); },
    getPostingPlan: () => of({ ...plan, status: kind === 'purchase' ? 'Prepared' : 'Reserved' }), postPurchase: (...args: unknown[]) => { calls.push(['post', ...args]); return of(true); },
    postSalesInvoice: (...args: unknown[]) => { calls.push(['post', ...args]); return of(true); }, ...overrides };
  const injector = Injector.create({ providers: [
    { provide: PurchaseApiService, useValue: api }, { provide: SalesApiService, useValue: api },
    { provide: CompanyContextService, useValue: actorContext },
    { provide: InventoryApiService, useValue: { getWarehouses: () => of([
      { warehouseId: 'warehouse', branchId: 'branch' }, { warehouseId: 'foreign', branchId: 'other' }
    ]) } }
  ] });
  const component = runInInjectionContext(injector, () => new FinancialPostingComponent());
  component.kind = kind;
  component.invoice = { ...source, status: 'Approved', invoiceNumber: 'EXT-1' } as unknown as PurchaseInvoice;
  return { component, calls };
}
for (const kind of ['purchase', 'sales'] as const) {
  test(`${kind} preparation restricts warehouse selection and posting uses the returned plan version`, () => {
    const { component, calls } = posting(kind);
    component.ngOnInit();
    assert.deepEqual(component.warehouses().map(w => w.warehouseId), ['warehouse']);
    component.warehouseId = 'foreign'; component.prepare(); assert.equal(calls.length, 0);
    component.warehouseId = 'warehouse'; component.prepare();
    assert.deepEqual(calls[0], ['prepare', 'doc', 37, 'warehouse']);
    assert.equal(calls.length, 1, 'preparation must not automatically authorize posting');
    component.post();
    assert.deepEqual(calls[1], kind === 'purchase' ? ['post', 'doc', 91, 'plan'] : ['post', 'doc', 91]);
  });
}

test('a reserved document reloads its plan without reserving twice', () => {
  const { component, calls } = posting('sales'); component.invoice.status = 'Reserved';
  component.ngOnInit(); assert.equal(component.plan()?.sourceVersion, 91); assert.equal(calls.length, 0);
});

test('the current preparer cannot authorize their own plan', () => {
  const { component, calls } = posting('purchase');
  component.plan.set({ ...plan, preparedBy: 'poster' });
  assert.equal(component.canPost(), false); component.post(); assert.equal(calls.length, 0);
});

test('a failed post preserves the review and requires reload rather than another automatic attempt', () => {
  let requests = 0;
  const { component } = posting('purchase', { postPurchase: () => { requests++; return throwError(() => ({ status: 409 })); } });
  component.plan.set(plan); component.post(); component.post();
  assert.equal(requests, 1); assert.equal(component.plan(), plan); assert.equal(component.reloadRequired(), true);
  assert.match(component.error(), /Close and reload/); assert.equal(component.busy(), false);
});

test('a completed plan cannot be posted again from a stale review', () => {
  const { component, calls } = posting('purchase');
  component.plan.set({ ...plan, status: 'Posted' }); component.post();
  assert.equal(component.canPost(), false); assert.equal(calls.length, 0);
});

for (const kind of ['purchase', 'sales'] as const) {
  test(`${kind} screen never retries approval with a guessed version or reports success after conflict`, () => {
    let requests = 0; let reloads = 0; let errors = 0;
    const api = { approveTransition: (id: string, version: number) => {
      requests++; assert.equal(id, 'doc'); assert.equal(version, 37); return throwError(() => ({ status: 409 }));
    } };
    const injector = Injector.create({ providers: [
      { provide: MockDataService, useValue: { branches: [] } },
      { provide: ChangeDetectorRef, useValue: { markForCheck: () => undefined } },
      { provide: CsvExportService, useValue: {} },
      { provide: ToastService, useValue: { error: () => errors++, success: () => assert.fail('No success after rejection') } }
    ] });
    const screen = runInInjectionContext(injector, () => kind === 'purchase'
      ? new PurchaseListComponent({ ...context, locale: () => 'en' } as never, api as never)
      : new SalesListComponent({ ...context, locale: () => 'en' } as never, api as never));
    screen.invoices = [{ ...source, status: 'Submitted' }] as never;
    screen.loadInvoices = () => { reloads++; };
    screen.approveInvoice('doc');
    assert.equal(requests, 1); assert.equal(reloads, 1); assert.equal(errors, 1);
    assert.equal(screen.invoices[0].status, 'Submitted');
  });
  test(`${kind} screen next/previous uses the last server document cursor and disables duplicate page loads`, () => {
    const pages: unknown[] = [];
    const firstPage = Array.from({ length: 25 }, (_, i) => ({ ...source, id: `doc-${i}`, status: 'Draft' }));
    const api = { getInvoices: (page: unknown) => { pages.push(page); return of(pages.length === 2 ? [] : firstPage); } };
    const injector = Injector.create({ providers: [
      { provide: MockDataService, useValue: { branches: [] } },
      { provide: ChangeDetectorRef, useValue: { markForCheck: () => undefined } },
      { provide: CsvExportService, useValue: {} }, { provide: ToastService, useValue: {} }
    ] });
    const screen = runInInjectionContext(injector, () => kind === 'purchase'
      ? new PurchaseListComponent(context as never, api as never) : new SalesListComponent(context as never, api as never));
    screen.applyFilters = () => undefined;
    screen.loadInvoices(); screen.changePage(1); screen.changePage(1);
    assert.equal(pages.length, 2); assert.deepEqual(pages[1], { before: 'doc-24', take: 25 });
    screen.changePage(-1); assert.deepEqual(pages[2], { before: undefined, take: 25 }); assert.equal(screen.pageIndex, 0);
    screen.loading = true; screen.changePage(1); assert.equal(pages.length, 3);
  });
}

 test('posting preparation shows the missing published tax rule instead of a stale-document message', () => {
  const { component } = posting('purchase', { preparePosting: () => throwError(() => ({ status: 409,
    error: { code: 'tax_rule_unavailable', detail: 'No published rule covers product ITEM-1 on the business date.' } })) });
  component.ngOnInit(); component.warehouseId = 'warehouse'; component.prepare();
  assert.match(component.error(), /No published rule covers product ITEM-1/);
  assert.equal(component.reloadRequired(), true);
  assert.equal(component.plan(), null);
  assert.equal(component.busy(), false);
});

test('posting preparation selects the only matching warehouse and sends a request without manual reselection', () => {
  const { component, calls } = posting('purchase'); component.ngOnInit();
  assert.equal(component.warehouseId, 'warehouse'); assert.equal(component.validWarehouse(), true);
  component.prepare(); assert.deepEqual(calls[0], ['prepare', 'doc', 37, 'warehouse']);
});
test('posting preparation explains an invalid warehouse instead of silently doing nothing', () => {
  const { component, calls } = posting('purchase'); component.ngOnInit(); component.warehouseId = 'foreign'; component.prepare();
  assert.equal(calls.length, 0); assert.match(component.error(), /warehouse belonging to this invoice branch/);
});
test('posting reload refreshes the version and clears the failed-request lock before preparation', () => {
  const { component, calls } = posting('purchase', { getInvoiceById: () => of({ ...source, version: 44, status: 'Approved' }) });
  component.reloadRequired.set(true); component.reloadInvoice();
  assert.equal(component.reloadRequired(), false); assert.equal(component.invoice.version, 44);
  component.prepare(); assert.deepEqual(calls[0], ['prepare', 'doc', 44, 'warehouse']);
});

test('posting preparation explains missing permission without sending an unauthorized request', () => {
  const { component, calls } = posting('purchase', {}, { ...context, hasPermission: () => false });
  component.ngOnInit(); component.prepare();
  assert.equal(calls.length, 0); assert.match(component.error(), /preparation permission/);
});
