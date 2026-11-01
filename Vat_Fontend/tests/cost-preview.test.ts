import '@angular/compiler';
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { signal, Injector, runInInjectionContext } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom, of, Subject, Subscription, throwError } from 'rxjs';
import { CostPreviewComponent } from '../src/app/features/inventory/cost-preview.component';
import { CostingApiService, CostPreview } from '../src/app/core/services/api/costing-api.service';
import { CompanyContextService } from '../src/app/core/services/company-context.service';

const warehouse = '11111111-1111-4111-8111-111111111111', product = '22222222-2222-4222-8222-222222222222';
const preview: CostPreview = { companyId: 'company', warehouseId: warehouse, productId: product, state: 'Preview', reason: null,
  basis: 'PurchaseNetOnly', supportedSourceSequence: '9007199254740993', sourceCount: 3, capturedAt: '2026-10-31',
  calculation: { method: 'FIFO', receivedQuantity: '20.000000', receivedValue: '3000.0000', issuedQuantity: '15.000000',
    issuedValue: '2000.0000', closingQuantity: '5.000000', closingValue: '1000.0000', issues: [] } };
function screen(api: object, allowed = true) {
  return Object.assign(Object.create(CostPreviewComponent.prototype), { api, warehouse, product, request: new Subscription(),
    context: {companyGuid: () => 'company', hasPermission: () => allowed}, busy: signal(false), error: signal(''), result: signal(preview) }) as CostPreviewComponent;
}
test('cost preview API keeps explicit warehouse/product scope and decimal strings', async () => {
  let call: unknown[] = [];
  const injector = Injector.create({providers: [
    {provide: CompanyContextService, useValue: {companyGuid: () => 'company'}},
    {provide: HttpClient, useValue: {get: (...args: unknown[]) => {call = args; return of(preview);}}}
  ]});
  const api = runInInjectionContext(injector, () => new CostingApiService());
  const result = await firstValueFrom(api.preview(warehouse, product));
  assert.deepEqual(call, ['/api/v1/companies/company/inventory/cost-preview', {params: {warehouseId: warehouse, productId: product}}]);
  assert.equal(result.supportedSourceSequence, '9007199254740993'); assert.equal(result.calculation?.closingValue, '1000.0000');
});
test('preview rejects scope mismatches and inconsistent availability without keeping stale values', () => {
  for (const response of [{...preview, companyId: 'other'}, {...preview, warehouseId: product}, {...preview, state: 'Unavailable'}, {...preview, calculation: null}]) {
    const c = screen({preview: () => of(response)}); c.load();
    assert.equal(c.result(), null); assert.match(c.error(), /mismatch/); assert.equal(c.busy(), false);
  }
});
test('failed and forbidden previews never display previous costs', () => {
  const c = screen({preview: () => throwError(() => new Error('offline'))}); c.load();
  assert.equal(c.result(), null); assert.match(c.error(), /could not be loaded/);
  const forbidden = screen({preview: () => assert.fail('Permission required')}, false); forbidden.load();
  assert.equal(forbidden.result(), null); assert.match(forbidden.error(), /permission/);
});
test('preview cancels earlier requests and accepts unavailable evidence without made-up amounts', () => {
  const pending = new Subject<CostPreview>(); const c = screen({preview: () => pending});
  c.load(); c.load(); assert.equal(pending.observers.length, 1);
  pending.next({...preview, state: 'Unavailable', reason: 'opening_cost_adoption_required', calculation: null});
  assert.equal(c.result()?.calculation, null); assert.match(c.reason(c.result()!.reason), /opening-cost adoption/);
  c.ngOnDestroy(); assert.equal(pending.observers.length, 0);
});
