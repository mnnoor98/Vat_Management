import '@angular/compiler';
import { FormBuilder } from '@angular/forms';
import { Router } from '@angular/router';
import { SalesApiService } from '../src/app/core/services/api/sales-api.service';
import { ToastService } from '../src/app/core/services/toast.service';
import { SalesEntryComponent } from '../src/app/features/sales/sales-entry/sales-entry.component';
import { TransferEntryComponent } from '../src/app/features/inventory/components/transfer-entry/transfer-entry.component';
import { Mushak65TransferComponent } from '../src/app/features/inventory/components/mushak-6-5/mushak-6-5.component';
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { ChangeDetectorRef, Injector, runInInjectionContext } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom, of, Subject, throwError } from 'rxjs';
import { MasterDataApiService } from '../src/app/core/services/api/master-data-api.service';
import { InventoryApiService } from '../src/app/core/services/api/inventory-api.service';
import { CompanyContextService } from '../src/app/core/services/company-context.service';
import { MockDataService } from '../src/app/core/services/mock-data.service';
import { CsvExportService } from '../src/app/core/services/csv-export.service';
import { StockSummaryComponent } from '../src/app/features/inventory/components/stock-summary/stock-summary.component';
import { MasterDataComponent } from '../src/app/features/master-data/master-data.component';

const context = { companyGuid: () => 'company', branchGuid: () => 'branch', isSuperAdmin: () => true, hasPermission: (p: string) => p === 'masterdata.manage', activeCompany: () => ({ id: 'company' }) };
function api<T>(type: new () => T, http: object): T {
  const injector = Injector.create({ providers: [ { provide: HttpClient, useValue: http },
    { provide: CompanyContextService, useValue: context }, { provide: MockDataService, useValue: { products: [], parties: [] } } ] });
  return runInInjectionContext(injector, () => new type());
}
const product = { id: 'product-id', reference: 'P-7', description: 'Product', unitOfMeasure: 'PCS', taxCode: 'CONFIGURED' };
const supplier = { id: 'supplier-id', reference: 'SUP-7', name: 'Supplier' };
const customer = { id: 'customer-id', code: 'C-7', name: 'Customer', address: 'Real address', contact: 'Real contact', taxRegistrationReference: 'Real registration', isActive: false, version: 9 };
const stock = { warehouseId: 'warehouse', warehouseCode: 'WH', warehouseName: 'Warehouse', branchId: 'branch', branchName: 'Branch', productId: 'product-id', productReference: 'P-7', productDescription: 'Product', unitOfMeasure: 'PCS', quantity: 12, reservedQuantity: 5, availableQuantity: 7 };

test('product metadata never invents prices, tax rates, HS codes or stock availability', async () => {
  const [p] = await firstValueFrom(api(MasterDataApiService, { get: () => of([product]) }).getProducts());
  assert.equal(p.id, 'product-id'); assert.equal(p.code, 'P-7'); assert.equal(p.taxCode, 'CONFIGURED');
  assert.equal(p.metadataOnly, true); assert.equal(p.hsCode, ''); assert.equal(p.standardCost, 0); assert.equal(p.stockQty, 0); assert.equal(p.vatRatePercent, 0);
});
test('creating a product uses the entered reference and tax code and returns only persisted metadata', async () => {
  const service = api(MasterDataApiService, { post: (_url: string, body: unknown) => {
    assert.deepEqual(body, { reference: 'P-7', description: 'Product', unitOfMeasure: 'PCS', taxCode: 'CONFIGURED' }); return of(product);
  } });
  const p = await firstValueFrom(service.createProduct({ id: 'browser-temp', code: 'P-7', name: 'Product', uom: 'PCS', taxCode: 'CONFIGURED', hsCode: 'unsaved', standardCost: 999 } as never));
  assert.equal(p.id, 'product-id'); assert.equal(p.hsCode, ''); assert.equal(p.standardCost, 0);
});
test('a missing configured tax code prevents a product write', async () => {
  const service = api(MasterDataApiService, { post: () => assert.fail('Must not invent STD-15') });
  await assert.rejects(firstValueFrom(service.createProduct({ code: 'P', uom: 'PCS' } as never)), /tax code/);
});
test('supplier metadata preserves the entity ID and leaves unsupported business details empty', async () => {
  const [s] = await firstValueFrom(api(MasterDataApiService, { get: () => of([supplier]) }).getParties('Supplier'));
  assert.equal(s.id, 'supplier-id'); assert.equal(s.code, 'SUP-7');
  for (const key of ['bin', 'phone', 'email', 'address', 'contactPerson'] as const) assert.equal(s[key], '');
});
test('customer creation translates the UI fields to the backend contract and maps the saved version', async () => {
  const service = api(MasterDataApiService, { post: (_url: string, body: unknown) => {
    assert.deepEqual(body, { code: 'C-7', name: 'Customer', contact: 'Real contact', address: 'Real address', taxRegistrationReference: 'Real registration' }); return of(customer);
  } });
  const saved = await firstValueFrom(service.createParty({ partyType: 'Customer', code: 'C-7', name: 'Customer', contactPerson: 'Real contact', address: 'Real address', bin: 'Real registration' } as never));
  assert.equal(saved.version, 9); assert.equal(saved.isActive, false); assert.equal(saved.bin, 'Real registration'); assert.equal(saved.email, '');
});
test('customer pagination sends the backend cursor and size explicitly', async () => {
  const service = api(MasterDataApiService, { get: (_url: string, options: unknown) => {
    assert.deepEqual(options, { params: { take: 25, before: 'last-id' } }); return of([customer]);
  } });
  await firstValueFrom(service.getParties('Customer', { before: 'last-id', take: 25 }));
});
test('stock pages preserve quantities and scope without supplying fictional valuation', async () => {
  const service = api(InventoryApiService, { get: (_url: string, options: unknown) => {
    assert.deepEqual(options, { params: { skip: 50, take: 25, method: 'WEIGHTED_AVERAGE', warehouseId: 'warehouse' } }); return of([stock]);
  } });
  const [balance] = await firstValueFrom(service.getAllStockBalances('warehouse', 50, 25));
  assert.equal(balance.branchId, 'branch'); assert.equal(balance.balanceId, 'warehouse:product-id');
  assert.equal(balance.quantity, 12); assert.equal(balance.reservedQuantity, 5); assert.equal(balance.availableQuantity, 7);
  assert.equal(balance.standardCost, undefined); assert.equal(balance.totalValuation, undefined); assert.equal(balance.hsCode, undefined);
});
function screen<T>(type: new () => T, providers: { provide: unknown; useValue: unknown }[]): T {
  const injector = Injector.create({ providers: [
    { provide: CompanyContextService, useValue: context }, { provide: ChangeDetectorRef, useValue: { markForCheck: () => undefined } },
    { provide: CsvExportService, useValue: { exportToCsv: () => undefined } }, ...providers as never[]
  ] });
  return runInInjectionContext(injector, () => new type());
}
test('stock CSV marks unknown valuation unavailable and preserves genuine zero values', () => {
  const output: unknown[][] = [];
  const component = screen(StockSummaryComponent, [ { provide: InventoryApiService, useValue: {} },
    { provide: CsvExportService, useValue: { exportToCsv: (...args: unknown[]) => output.push(args) } } ]);
  component.stockBalances = [{ ...stock, balanceId: 'balance' }];
  assert.equal(component.totalStockValuation, null); component.exportStockToCsv();
  assert.equal((output[0][2] as unknown[][])[0][5], 'Unavailable');
  assert.equal((output[0][2] as unknown[][])[0][8], 'Unavailable');
  assert.equal((output[0][2] as unknown[][])[0][9], 'Unavailable');
  component.stockBalances = [{ ...stock, balanceId: 'balance', standardCost: 0, totalValuation: 0 }];
  assert.equal(component.totalStockValuation, 0); component.exportStockToCsv();
  assert.equal((output[1][2] as unknown[][])[0][8], 0); assert.equal((output[1][2] as unknown[][])[0][9], 0);
  component.stockBalances.push({ ...stock, balanceId: 'unknown', productId: 'unknown' });
  assert.equal(component.totalStockValuation, null);
});
test('an older stock response cannot overwrite the selected warehouse page', () => {
  const requests: Subject<unknown[]>[] = [];
  const component = screen(StockSummaryComponent, [{ provide: InventoryApiService, useValue: { getAllStockBalances: () => { const s = new Subject<unknown[]>(); requests.push(s); return s; } } }]);
  component.loadStockBalances(); component.selectedWarehouse = 'new'; component.onWarehouseFilterChange();
  requests[1].next([{ ...stock, warehouseId: 'new' }]); requests[0].next([{ ...stock, warehouseId: 'old' }]);
  assert.equal(component.stockBalances[0].warehouseId, 'new'); assert.equal(component.isLoading, false);
});

test('stock API and export preserve unavailable valuation metadata without converting null costs to zero', async () => {
  const service = api(InventoryApiService, { get: () => of([{ ...stock, standardCost: null, totalValuation: null,
    hsCode: null, valuationStatus: 'Unavailable', valuationReason: 'cost_journal_unavailable' }]) });
  const rows = await firstValueFrom(service.getAllStockBalances());
  let exported: unknown[][] = [];
  const component = screen(StockSummaryComponent, [{ provide: InventoryApiService, useValue: {} },
    { provide: CsvExportService, useValue: { exportToCsv: (_name: string, _headers: unknown, values: unknown[][]) => exported = values } }]);
  component.stockBalances = rows;
  assert.equal(component.totalStockValuation, null);
  component.exportStockToCsv();
  assert.equal(exported[0][8], 'Unavailable'); assert.equal(exported[0][9], 'Unavailable');
  assert.equal(exported[0][11], 'Unavailable'); assert.equal(exported[0][12], 'cost_journal_unavailable');
  component.isLoading = true; exported = []; component.exportStockToCsv(); assert.deepEqual(exported, []);
  component.isLoading = false; component.loadError = 'Offline'; component.exportStockToCsv(); assert.deepEqual(exported, []);
});
test('failed stock reload clears stale rows and exposes retry state', () => {
  const component = screen(StockSummaryComponent, [{ provide: InventoryApiService, useValue: { getAllStockBalances: () => throwError(() => new Error('Offline')) } }]);
  component.stockBalances = [{ ...stock, balanceId: 'balance' }]; component.loadStockBalances();
  assert.equal(component.stockBalances.length, 0); assert.equal(component.loadError, 'Offline'); assert.equal(component.hasNext, false);
});
test('master product lists page locally while customers use server cursors', () => {
  const calls: unknown[] = [];
  const component = screen(MasterDataComponent, [{ provide: MasterDataApiService, useValue: {
    getProducts: () => of(Array.from({ length: 26 }, (_, i) => ({ id: `p-${i}`, code: `P-${i}`, name: 'Product' }))),
    getParties: (_type: string, page: unknown) => { calls.push(page); return of(Array.from({ length: 25 }, (_, i) => ({ id: `c-${i}`, code: `C-${i}`, name: 'Customer' }))); }
  } }]);
  component.reload(); assert.equal(component.rows().length, 25); component.changePage(1); assert.equal(component.rows()[0].code, 'P-25'); assert.equal(component.hasNext(), false);
  component.selectTab('Customers'); component.changePage(1);
  assert.deepEqual(calls[1], { before: 'c-24', take: 25 }); assert.equal(component.canCreate(), true);
});

test('sales entry keeps metadata-only products selectable and can load customers beyond the first page', () => {
  const pages: unknown[] = [];
  const component = screen(SalesEntryComponent, [
    { provide: FormBuilder, useValue: new FormBuilder() }, { provide: SalesApiService, useValue: {} },
    { provide: ToastService, useValue: {} }, { provide: Router, useValue: {} },
    { provide: MockDataService, useValue: { branches: [] } },
    { provide: CompanyContextService, useValue: { ...context, activeBranch: () => ({ id: 'branch' }) } },
    { provide: MasterDataApiService, useValue: {
      getProducts: () => of([{ id: 'product-id', code: 'P-7', name: 'Product', metadataOnly: true, salesPrice: 0 }]),
      getParties: (_type: string, page: unknown) => {
        pages.push(page); return of(pages.length === 1
          ? Array.from({ length: 25 }, (_, i) => ({ ...customer, id: `c-${i}`, isActive: i !== 0 }))
          : [{ ...customer, id: 'c-25', isActive: true }]);
      }
    } }
  ]);
  component.ngOnInit();
  assert.equal(component.products.length, 1); assert.equal(component.lineDraftForm.get('unitPrice')?.value, 0);
  assert.equal(component.customers.length, 24); assert.equal(component.hasMoreCustomers, true);
  component.loadMoreCustomers(); assert.deepEqual(pages[1], { before: 'c-24', take: 25 });
  assert.equal(component.customers.at(-1)?.id, 'c-25'); assert.equal(component.hasMoreCustomers, false);
});

const transfer = { id: 'transfer-id', transferNumber: 'TRF-SERVER', state: 'Submitted', version: 28, dispatchDate: '2026-10-30', dispatchTime: '',
  fromWarehouseId: 'source', fromWarehouseCode: 'SRC', fromWarehouseName: 'Source', fromBranchName: 'Branch',
  toWarehouseId: 'destination', toWarehouseCode: 'DST', toWarehouseName: 'Destination', toBranchName: 'Other branch',
  vehicleNumber: '', driverName: '', remarks: '', createdBy: 'maker', approvedBy: '', createdAt: '2026-10-30T00:00:00Z',
  lines: [{ id: 'line', productId: 'product-id', productCode: 'P-7', productName: 'Product', uom: 'PCS', quantity: 2, unitPrice: 0, totalValue: 0 }] };
test('transfer history preserves authoritative state, version and zero values without invented transport data', async () => {
  const service = api(InventoryApiService, { get: (_url: string, options: unknown) => {
    assert.deepEqual(options, { params: { skip: 50, take: 25 } }); return of([transfer]);
  } });
  const [t] = await firstValueFrom(service.getTransfers(50, 25));
  assert.equal(t.status, 'Submitted'); assert.equal(t.version, 28); assert.equal(t.vehicleNumber, ''); assert.equal(t.driverName, '');
  assert.equal(t.lines[0].totalValue, 0); assert.equal(t.lines[0].hsCode, undefined); assert.equal(t.lines[0].uom, 'PCS');
});
test('transfer commands send the loaded version and cancellation reason without retry', async () => {
  let calls = 0;
  const service = api(InventoryApiService, { post: (url: string, body: unknown) => {
    calls++; assert.match(url, /transfer-id\/cancel$/); assert.deepEqual(body, { expectedVersion: 28, reason: 'Duplicate' });
    return throwError(() => new Error('Conflict'));
  } });
  await assert.rejects(firstValueFrom(service.transitionTransfer('transfer-id', 'cancel', 28, 'Duplicate')), /Conflict/); assert.equal(calls, 1);
});
test('draft retry passes its explicit idempotency key through to the HTTP client', async () => {
  const service = api(InventoryApiService, { post: (_url: string, _body: unknown, options: unknown) => {
    assert.deepEqual(options, { headers: { 'Idempotency-Key': 'stable-key' } }); return of({ id: 'transfer-id', reference: 'TRF-SERVER', state: 'Draft', version: 13 });
  } });
  const result = await firstValueFrom(service.createTransfer({} as never, 'stable-key'));
  assert.equal(result.reference, 'TRF-SERVER'); assert.equal(result.version, 13); assert.equal(result.state, 'Draft');
});
test('transfer entry begins with no sample warehouses, product, driver or inferred unit price', () => {
  const component = screen(TransferEntryComponent, [{ provide: InventoryApiService, useValue: {} }]);
  assert.equal(component.draft.fromWarehouseId, ''); assert.equal(component.draft.toWarehouseId, ''); assert.equal(component.draft.productId, '');
  assert.equal(component.draft.unitPrice, 0); assert.equal(component.draft.vehicleNumber, ''); assert.equal(component.draft.driverName, '');
});
function readyEntry(service: object) {
  const component = screen(TransferEntryComponent, [{ provide: InventoryApiService, useValue: service }, { provide: CompanyContextService, useValue: { ...context, hasPermission: () => true } }]);
  component.warehouses.set([{ warehouseId: 'source' }, { warehouseId: 'destination' }] as never);
  component.balances.set([{ ...stock, warehouseId: 'source' }]);
  Object.assign(component.draft, { fromWarehouseId: 'source', toWarehouseId: 'destination', productId: 'product-id', quantity: 2 });
  return component;
}
test('draft creation emits the server reference and never auto-submits or claims stock posting', () => {
  const component = readyEntry({ createTransfer: (payload: Record<string, unknown>) => {
    assert.equal(payload['productId'], undefined); assert.equal(payload['quantity'], undefined);
    assert.equal((payload['lines'] as { unitPrice: number }[])[0].unitPrice, 0);
    return of({ id: 'transfer-id', reference: 'TRF-SERVER', state: 'Draft', version: 1 });
  }, transitionTransfer: () => assert.fail('A draft must not automatically post') });
  let reference = ''; component.transferCreated.subscribe(r => reference = r); component.submitTransferForm();
  assert.equal(reference, 'TRF-SERVER'); assert.equal(component.saved(), true); assert.equal(component.busy(), false);
});
test('draft entry blocks invalid source availability and same-warehouse transfers', () => {
  const component = readyEntry({ createTransfer: () => assert.fail('Invalid draft must not be sent') });
  component.draft.quantity = 8; component.submitTransferForm(); assert.match(component.error(), /availability/);
  component.draft.quantity = 1; component.draft.toWarehouseId = 'source'; component.submitTransferForm(); assert.match(component.error(), /different warehouses/);
});
test('retrying an unchanged failed draft keeps its idempotency key', () => {
  const keys: string[] = [];
  const component = readyEntry({ createTransfer: (_body: unknown, key: string) => {
    keys.push(key); return throwError(() => new Error('Network response lost'));
  } });
  component.submitTransferForm(); component.submitTransferForm(); assert.equal(keys.length, 2); assert.equal(keys[0], keys[1]);
  component.draft.remarks = 'Changed'; component.submitTransferForm(); assert.notEqual(keys[2], keys[0]);
});
test('changing source stock ignores older responses and never offers another warehouse product', () => {
  const requests: Subject<unknown[]>[] = [];
  const component = readyEntry({ getAllStockBalances: () => { const request = new Subject<unknown[]>(); requests.push(request); return request; } });
  component.onSourceWarehouseChange(); component.draft.fromWarehouseId = 'new'; component.onSourceWarehouseChange();
  requests[1].next([{ ...stock, warehouseId: 'new' }, { ...stock, warehouseId: 'foreign' }]); requests[0].next([{ ...stock, warehouseId: 'source' }]);
  assert.deepEqual(component.balances().map(b => b.warehouseId), ['new']); assert.equal(component.draft.productId, '');
});
function register(service: object, actor = 'checker') {
  const component = screen(Mushak65TransferComponent, [{ provide: InventoryApiService, useValue: service },
    { provide: CompanyContextService, useValue: { ...context, hasPermission: () => true, currentUser: () => ({ id: actor }) } }]);
  component.activeTransfer.set({ ...transfer, status: transfer.state } as never); return component;
}
test('transfer register blocks maker approval and all actions for terminal states', () => {
  const component = register({}, 'maker'); const t = component.activeTransfer()!;
  assert.equal(component.canAction(t, 'approve'), false);
  for (const state of ['Posted', 'Cancelled']) for (const action of component.actions) assert.equal(component.canAction({ ...t, status: state }, action), false);
});
test('transfer confirmation uses the reviewed version once and requires reload after failure', () => {
  let calls = 0;
  const component = register({ transitionTransfer: (id: string, action: string, version: number) => {
    calls++; assert.equal(id, 'transfer-id'); assert.equal(action, 'approve'); assert.equal(version, 28); return throwError(() => new Error('Conflict'));
  } });
  component.pendingAction.set('approve'); component.confirmAction(); component.confirmAction();
  assert.equal(calls, 1); assert.equal(component.reloadRequired(), true); assert.match(component.error(), /Reload/);
  assert.equal(component.activeTransfer()?.status, 'Submitted');
});
test('cancellation requires a reason and successful transitions reload authoritative history', () => {
  let calls = 0; let reloads = 0;
  const component = register({ transitionTransfer: (_id: string, action: string, version: number, reason: string) => {
    calls++; assert.equal(action, 'cancel'); assert.equal(version, 28); assert.equal(reason, 'Duplicate'); return of({ reference: 'TRF-SERVER', state: 'Cancelled', version: 29 });
  }, getTransfers: () => { reloads++; return of([]); } });
  component.pendingAction.set('cancel'); component.reason = ' '; component.confirmAction(); assert.equal(calls, 0);
  component.reason = ' Duplicate '; component.confirmAction(); assert.equal(calls, 1); assert.equal(reloads, 1); assert.match(component.message(), /Cancelled/);
});
