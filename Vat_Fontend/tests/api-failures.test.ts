import './tax-rules.test';
import './costing-policies.test';
import './cost-preview.test';
import './stock-valuation.test';
import './production-execution.test';
import './auth-login.test';
import '@angular/compiler';
import './financial-contracts.test';
import './master-inventory.test';
import './manufacturing-contracts.test';
import './register-report.test';
import './net-ledger.test';
import './payments.test';
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { Injector, runInInjectionContext } from '@angular/core';
import { HttpClient, HttpRequest, HttpResponse } from '@angular/common/http';
import { firstValueFrom, throwError, of } from 'rxjs';
import { PurchaseApiService } from '../src/app/core/services/api/purchase-api.service';
import { SalesApiService } from '../src/app/core/services/api/sales-api.service';
import { ComplianceApiService } from '../src/app/core/services/api/compliance-api.service';
import { WorkflowApiService } from '../src/app/core/services/api/workflow-api.service';
import { MasterDataApiService } from '../src/app/core/services/api/master-data-api.service';
import { ManufacturingApiService } from '../src/app/core/services/api/manufacturing-api.service';
import { CompanyContextService } from '../src/app/core/services/company-context.service';
import { MockDataService } from '../src/app/core/services/mock-data.service';
import { NavigationService } from '../src/app/core/services/navigation.service';
import { AuthService } from '../src/app/core/services/auth.service';
import { authInterceptor } from '../src/app/core/interceptors/auth.interceptor';

function services(http?: object) {
  const failure = new Error('Server rejected the operation');
  const data = { purchaseInvoices: [{ id: 'purchase', status: 'Approved' }],
    salesInvoices: [{ id: 'sale', status: 'Approved' }],
    currentReturn91: { status: 'Draft' }, approvalTasks: [{ id: 'task', status: 'Pending' }],
    products: [], parties: [], coefficient43List: [] };
  const injector = Injector.create({ providers: [
    { provide: HttpClient, useValue: http || { get: () => throwError(() => failure), post: () => throwError(() => failure) } },
    { provide: MockDataService, useValue: data },
    { provide: CompanyContextService, useValue: { companyGuid: () => 'company' } },
    { provide: NavigationService, useValue: { updateBadgeCount: () => assert.fail('No failed request may update the badge') } }
  ] });
  return { data, failure, create: <T>(type: new () => T): T => runInInjectionContext(injector, () => new type()) };
}

test('a rejected purchase post never returns success or changes the local document', async () => {
  const s = services();
  await assert.rejects(firstValueFrom(s.create(PurchaseApiService).postPurchase('purchase', 17, 'plan')), s.failure);
  assert.equal(s.data.purchaseInvoices[0].status, 'Approved');
});

test('a rejected sales post never returns success or changes the local document', async () => {
  const s = services();
  await assert.rejects(firstValueFrom(s.create(SalesApiService).postSalesInvoice('sale', 29)), s.failure);
  assert.equal(s.data.salesInvoices[0].status, 'Approved');
});

test('a rejected return submission remains a rejected operation', async () => {
  const s = services();
  await assert.rejects(firstValueFrom(s.create(ComplianceApiService).submitReturn91({} as never)), s.failure);
  assert.equal(s.data.currentReturn91.status, 'Draft');
});

for (const [name, service] of [['purchase', PurchaseApiService], ['sales', SalesApiService]] as const) {
  test(`a failed ${name} list never falls back to demo documents`, async () => {
    const s = services();
    await assert.rejects(firstValueFrom(s.create(service).getInvoices()), s.failure);
  });
  test(`live ${name} status cannot be changed by a browser-only update`, async () => {
    const s = services();
    await assert.rejects(firstValueFrom(s.create(service).updateStatus('any', 'Posted')));
  });
}

test('retired workflow decisions never mutate local tasks or counters', async () => {
  const s = services();
  await assert.rejects(firstValueFrom(s.create(WorkflowApiService).approveTask('task')));
  assert.equal(s.data.approvalTasks[0].status, 'Pending');
});

test('a failed product creation never stores a local substitute', async () => {
  const s = services();
  await assert.rejects(firstValueFrom(s.create(MasterDataApiService).createProduct({ code: 'product', name: 'Product', uom: 'PCS', taxCode: 'CONFIGURED' } as never)), s.failure);
  assert.equal(s.data.products.length, 0);
});

test('an empty coefficient response stays empty', async () => {
  const s = services({ get: () => of([]) });
  assert.deepEqual(await firstValueFrom(s.create(ManufacturingApiService).getCoefficients()), []);
});

test('authentication attaches a verified session token only to the application API', async () => {
  Object.defineProperty(globalThis, 'location', { configurable: true, value: new URL('https://vat.example') });
  const injector = Injector.create({ providers: [{ provide: AuthService, useValue: { accessToken: () => 'verified-session' } }] });
  for (const [url, expected] of [['/api/v1/companies', 'Bearer verified-session'],
    ['https://unrelated.example/api/v1/companies', null], ['/auth-config.json', null]] as const) {
    let header: string | null = null;
    const result = runInInjectionContext(injector, () => authInterceptor(new HttpRequest('GET', url), request => {
      header = request.headers.get('Authorization'); return of(new HttpResponse({ status: 200 }));
    }));
    await firstValueFrom(result);
    assert.equal(header, expected);
  }
});

test('an absent session never receives a fallback demo token', async () => {
  const injector = Injector.create({ providers: [{ provide: AuthService, useValue: { accessToken: () => null } }] });
  const result = runInInjectionContext(injector, () => authInterceptor(new HttpRequest('GET', '/api/v1/companies'), request => {
    assert.equal(request.headers.has('Authorization'), false); return of(new HttpResponse({ status: 200 }));
  }));
  await firstValueFrom(result);
});
