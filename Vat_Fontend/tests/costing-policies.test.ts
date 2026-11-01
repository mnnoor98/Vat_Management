import '@angular/compiler';
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { Injector, runInInjectionContext, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { of, firstValueFrom, throwError, Subscription } from 'rxjs';
import { CostingApiService, CostingPolicy } from '../src/app/core/services/api/costing-api.service';
import { CostingPoliciesComponent } from '../src/app/features/inventory/costing-policies.component';
import { CompanyContextService } from '../src/app/core/services/company-context.service';

const policy: CostingPolicy = { id: 'policy', companyId: 'company', productId: null, method: 'FIFO',
  effectiveFrom: '2026-01-01', effectiveUntil: '2027-01-01', state: 'Draft', version: 1,
  createdBy: 'maker', createdAt: '2026-10-31', approvedBy: null, approvedAt: null };
function screen(api: object, actor = 'checker', allowed = true) {
  return Object.assign(Object.create(CostingPoliciesComponent.prototype), {
    api, context: { companyGuid: () => 'company', currentUser: () => ({id: actor}), hasPermission: () => allowed },
    company: 'company', subscriptions: new Subscription(), cursors: [undefined], commandKey: '', commandPayload: '',
    policies: signal([policy]), products: signal([]), busy: signal(false), error: signal(''), message: signal(''),
    selected: signal(policy), page: signal(0), reloadRequired: signal(false),
    draft: { productId: null, method: 'FIFO', effectiveFrom: '2026-01-01', effectiveUntil: '2027-01-01' }
  }) as CostingPoliciesComponent;
}

test('costing API preserves company scope, cursor, explicit command key and reviewed version', async () => {
  const calls: unknown[][] = [];
  const injector = Injector.create({providers: [
    {provide: CompanyContextService, useValue: {companyGuid: () => 'company'}},
    {provide: HttpClient, useValue: {get: (...args: unknown[]) => {calls.push(args); return of([policy]);},
      post: (...args: unknown[]) => {calls.push(args); return of(policy);}}}
  ]});
  const api = runInInjectionContext(injector, () => new CostingApiService());
  await firstValueFrom(api.list('cursor')); await firstValueFrom(api.approve(policy, 'stable-key'));
  assert.deepEqual(calls[0], ['/api/v1/companies/company/inventory/costing-policies', {params: {take: 25, before: 'cursor'}}]);
  assert.deepEqual(calls[1], ['/api/v1/companies/company/inventory/costing-policies/policy/approve', {expectedVersion: 1}, {headers: {'Idempotency-Key': 'stable-key'}}]);
});

test('costing maker, read-only user and frozen policies cannot approve', () => {
  for (const c of [screen({}, 'maker'), screen({}, 'checker', false)]) { assert.equal(c.canApprove(policy), false); c.approve(); assert.equal(c.busy(), false); }
  const c = screen({}); assert.equal(c.canApprove({...policy, state: 'Approved', version: 2}), false);
  c.reloadRequired.set(true); assert.equal(c.canApprove(policy), false);
});

test('product costing approval requires loaded catalogue evidence', () => {
  const c = screen({}); const productPolicy = {...policy, productId: 'product'};
  assert.equal(c.canApprove(productPolicy), false);
  c.products.set([{id: 'product', code: 'ITEM', name: 'Material'}] as never);
  assert.equal(c.canApprove(productPolicy), true); assert.match(c.scopeLabel(productPolicy), /ITEM/);
});

test('failed costing approval never changes policy state and requires reload before another command', () => {
  let calls = 0;
  const c = screen({approve: () => {calls++; return throwError(() => new Error('Conflict'));}});
  c.approve(); c.selected.set(policy); c.approve();
  assert.equal(calls, 1); assert.equal(c.policies()[0].state, 'Draft'); assert.equal(c.policies()[0].version, 1);
  assert.equal(c.reloadRequired(), true); assert.equal(c.message(), ''); assert.match(c.error(), /Conflict/);
});

test('costing draft retries retain the command key until payload changes', () => {
  const keys: string[] = [];
  const c = screen({create: (_payload: unknown, key: string) => {keys.push(key); return throwError(() => new Error('Offline'));}});
  c.create(); c.create(); assert.equal(keys.length, 2); assert.equal(keys[0], keys[1]);
  c.draft.method = 'WEIGHTED_AVERAGE'; c.create(); assert.notEqual(keys[2], keys[0]);
  assert.equal(c.message(), '');
});

test('costing draft rejects invalid calendar dates, reversed ranges and unregistered product choices', () => {
  const c = screen({create: () => assert.fail('Invalid drafts must not reach the API')});
  for (const values of [{effectiveFrom: '2026-02-30'}, {effectiveFrom: '2028-01-01'}, {productId: 'unregistered'}]) {
    c.draft = {productId: null, method: 'FIFO', effectiveFrom: '2026-01-01', effectiveUntil: '2027-01-01', ...values};
    c.create(); assert.match(c.error(), /Select/);
  }
});

test('costing reload rejects foreign-company responses and never retains stale policies', () => {
  const c = screen({list: () => of([{...policy, companyId: 'foreign'}])}); c.load();
  assert.deepEqual(c.policies(), []); assert.equal(c.selected(), null); assert.equal(c.reloadRequired(), true); assert.match(c.error(), /scope mismatch/);
});
