import '@angular/compiler';
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { Injector, runInInjectionContext } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { of, firstValueFrom, throwError } from 'rxjs';
import { ManufacturingApiService, CoefficientResult } from '../src/app/core/services/api/manufacturing-api.service';
import { MasterDataApiService } from '../src/app/core/services/api/master-data-api.service';
import { CompanyContextService } from '../src/app/core/services/company-context.service';
import { CsvExportService } from '../src/app/core/services/csv-export.service';
import { NewDeclarationComponent } from '../src/app/features/manufacturing/components/new-declaration/new-declaration.component';
import { CoefficientDeclarationComponent } from '../src/app/features/manufacturing/components/coefficient-declaration/coefficient-declaration.component';
const context = { companyGuid: () => 'company', currentUser: () => ({ id: 'checker' }), hasPermission: () => true };
const product = { id: 'finished', reference: 'FG', description: 'Finished', unitOfMeasure: 'PCS', taxCode: 'Configured' };
const result: CoefficientResult = { id: 'declaration', bomId: 'bom', revision: 4, declarationNumber: 'DEC-7', state: 'Draft', version: 29, createdBy: 'maker', effectiveFrom: '2026-10-30',
  snapshot: { finishedProduct: product, batchSize: 10, directLaborCost: 0, utilityCost: 0, overheadCost: 0, totalCost: 0, unitCost: 0,
    referencesCheckedAt: '2026-10-30T00:00:00Z', materials: [{ product: { ...product, id: 'material', reference: 'RM' }, quantity: 1, unitPrice: 0, wastagePercent: 0, totalCost: 0 }] } };
function create<T>(type: new () => T, providers: { provide: unknown; useValue: unknown }[]): T {
  const injector = Injector.create({ providers: [{ provide: CompanyContextService, useValue: context }, ...providers as never[]] });
  return runInInjectionContext(injector, () => new type());
}

test('manufacturing list preserves actual snapshot, version, revision and zero costs with cursor pagination', async () => {
  const api = create(ManufacturingApiService, [{ provide: HttpClient, useValue: { get: (_url: string, options: unknown) => {
    assert.deepEqual(options, { params: { before: 'cursor', take: 25 } }); return of([result]);
  } } }]);
  const [d] = await firstValueFrom(api.getCoefficients('cursor', 25));
  assert.equal(d, result); assert.equal(d.version, 29); assert.equal(d.snapshot?.unitCost, 0); assert.equal(d.snapshot?.materials[0].wastagePercent, 0);
  assert.equal((d as unknown as { finalSellingPrice?: number }).finalSellingPrice, undefined);
});
test('manufacturing approval uses PUT with the reviewed version; submission/cancellation use POST', async () => {
  const calls: unknown[][] = [];
  const api = create(ManufacturingApiService, [{ provide: HttpClient, useValue: {
    put: (...args: unknown[]) => { calls.push(['PUT', ...args]); return of(result); }, post: (...args: unknown[]) => { calls.push(['POST', ...args]); return of(result); }
  } }]);
  await firstValueFrom(api.transitionCoefficient('declaration', 'approve', 29));
  await firstValueFrom(api.transitionCoefficient('declaration', 'submit', 31));
  await firstValueFrom(api.transitionCoefficient('declaration', 'cancel', 37, 'Duplicate'));
  assert.equal(calls[0][0], 'PUT'); assert.match(calls[0][1] as string, /declaration\/approve$/); assert.deepEqual(calls[0][2], { expectedVersion: 29 });
  assert.equal(calls[1][0], 'POST'); assert.deepEqual(calls[1][2], { expectedVersion: 31 });
  assert.deepEqual(calls[2][2], { expectedVersion: 37, reason: 'Duplicate' });
});
test('manufacturing create forwards the stable draft command key', async () => {
  const api = create(ManufacturingApiService, [{ provide: HttpClient, useValue: { post: (_url: string, _payload: unknown, options: unknown) => {
    assert.deepEqual(options, { headers: { 'Idempotency-Key': 'command-key' } }); return of(result);
  } } }]);
  assert.equal(await firstValueFrom(api.createCoefficient({} as never, 'command-key')), result);
});
function entry(service: object, master: object = { getProducts: () => of([]) }) {
  return create(NewDeclarationComponent, [{ provide: ManufacturingApiService, useValue: service }, { provide: MasterDataApiService, useValue: master }]);
}
function ready(service: object) {
  const component = entry(service);
  component.products.set([{ id: 'finished' }, { id: 'material' }, { id: 'material-2' }] as never);
  Object.assign(component.draft, { declarationNumber: 'DEC-7', finishedProductId: 'finished', batchSize: 10 });
  component.draft.materials = [{ productId: 'material', quantity: 1, wastagePercent: 0, unitPrice: 0 }]; return component;
}
test('manufacturing entry has no preselected sample product, reference or inferred material price', () => {
  const component = entry({});
  assert.equal(component.draft.finishedProductId, ''); assert.equal(component.draft.declarationNumber, ''); assert.equal(component.draft.materials[0].productId, '');
  assert.equal(component.draft.materials[0].unitPrice, 0); component.addMaterialRow(); assert.equal(component.draft.materials[1].productId, '');
});
test('failed product loading leaves no mock fallback and exposes an error', () => {
  const component = entry({}, { getProducts: () => throwError(() => new Error('References offline')) }); component.loadProducts();
  assert.equal(component.products().length, 0); assert.equal(component.loading(), false); assert.equal(component.error(), 'References offline');
});
test('manufacturing form sends only supported fields and emits the server draft reference without submitting it', () => {
  let saved = ''; const component = ready({ createCoefficient: (payload: Record<string, unknown>) => {
    assert.deepEqual(Object.keys(payload).sort(), ['declarationNumber', 'finishedProductId', 'batchSize', 'effectiveFrom', 'effectiveUntil', 'directLaborCost', 'utilityCost', 'overheadCost', 'materials'].sort());
    assert.equal(payload['effectiveUntil'], '2026-12-31'); return of({ ...result, declarationNumber: 'SERVER-REF' });
  }, transitionCoefficient: () => assert.fail('Creation must not auto-submit') });
  component.effectiveUntil = '2026-12-31'; component.declarationCreated.subscribe(r => saved = r); component.submitNewDeclaration();
  assert.equal(saved, 'SERVER-REF'); assert.equal(component.saved(), true);
});
test('manufacturing form refuses duplicate/finished materials, negative costs, invalid dates and excess precision', () => {
  const component = ready({ createCoefficient: () => assert.fail('Invalid payload must stay local') });
  const invalid = [
    () => { component.draft.materials.push({ ...component.draft.materials[0] }); },
    () => { component.draft.materials[0].productId = 'finished'; },
    () => { component.draft.overheadCost = -1; },
    () => { component.effectiveUntil = '2026-01-01'; },
    () => { component.draft.materials[0].quantity = 1.1234567; }
  ];
  for (const mutate of invalid) {
    component.draft.materials = [{ productId: 'material', quantity: 1, wastagePercent: 0, unitPrice: 0 }]; component.draft.overheadCost = 0; component.effectiveUntil = ''; mutate();
    component.submitNewDeclaration(); assert.match(component.error(), /Check/); assert.equal(component.busy(), false);
  }
});
test('manufacturing draft retries retain the same command key until payload changes', () => {
  const keys: string[] = []; const component = ready({ createCoefficient: (_payload: unknown, key: string) => { keys.push(key); return throwError(() => new Error('Offline')); } });
  component.submitNewDeclaration(); component.submitNewDeclaration(); assert.equal(keys[0], keys[1]);
  component.draft.utilityCost = 1; component.submitNewDeclaration(); assert.notEqual(keys[2], keys[0]);
});
function register(service: object, actor = 'checker', csv: object = {}) {
  return create(CoefficientDeclarationComponent, [{ provide: ManufacturingApiService, useValue: service }, { provide: CsvExportService, useValue: csv },
    { provide: CompanyContextService, useValue: { ...context, currentUser: () => ({ id: actor }) } }]);
}
test('manufacturing register blocks maker approval, missing snapshot and terminal-state commands', () => {
  const component = register({}, 'maker'); assert.equal(component.canAction({ ...result, state: 'Submitted' }, 'approve'), false);
  assert.equal(component.canAction({ ...result, snapshot: null }, 'submit'), false);
  for (const state of ['Approved', 'Cancelled']) for (const action of component.actions) assert.equal(component.canAction({ ...result, state }, action), false);
  component.readOnly = true; assert.equal(component.canAction(result, 'submit'), false);
});
test('manufacturing failed approval uses exact version once without local status mutation', () => {
  let calls = 0; const component = register({ transitionCoefficient: (id: string, action: string, version: number) => {
    calls++; assert.equal(id, 'declaration'); assert.equal(action, 'approve'); assert.equal(version, 29); return throwError(() => new Error('Conflict'));
  } });
  component.setDeclaration({ ...result, state: 'Submitted' }); component.pendingAction.set('approve'); component.confirmAction(); component.confirmAction();
  assert.equal(calls, 1); assert.equal(component.activeDeclaration()?.state, 'Submitted'); assert.equal(component.reloadRequired(), true); assert.match(component.error(), /Reload/);
});
test('manufacturing cancellation requires a reason and reloads after a server-confirmed transition', () => {
  let calls = 0, reloads = 0; const component = register({ transitionCoefficient: (_id: string, action: string, version: number, reason: string) => {
    calls++; assert.equal(action, 'cancel'); assert.equal(version, 29); assert.equal(reason, 'Duplicate'); return of({ ...result, state: 'Cancelled', version: 30 });
  }, getCoefficients: () => { reloads++; return of([]); } });
  component.setDeclaration(result); component.pendingAction.set('cancel'); component.reason = ' '; component.confirmAction(); assert.equal(calls, 0);
  component.reason = ' Duplicate '; component.confirmAction(); assert.equal(calls, 1); assert.equal(reloads, 1); assert.match(component.message(), /Cancelled/);
});
test('manufacturing cursor navigation uses the last server ID and restores previous pages', () => {
  const pages: unknown[] = []; const component = register({ getCoefficients: (before?: string, take?: number) => {
    pages.push({ before, take }); return of(pages.length === 2 ? [] : Array.from({ length: 25 }, (_, i) => ({ ...result, id: `d-${i}` })));
  } });
  component.loadDeclarations(); component.changePage(1); component.changePage(1); assert.equal(pages.length, 2); assert.deepEqual(pages[1], { before: 'd-24', take: 25 });
  component.changePage(-1); assert.deepEqual(pages[2], { before: undefined, take: 25 });
});
test('manufacturing export preserves zero unit cost and marks absent historical snapshots unavailable', () => {
  let rows: unknown[][] = []; const component = register({}, 'checker', { exportToCsv: (_name: string, _headers: unknown, values: unknown[][]) => rows = values });
  component.declarations.set([result, { ...result, snapshot: null }]); component.exportPage();
  assert.equal(rows[0][9], 0); assert.equal(rows[1][9], 'Unavailable');
});
