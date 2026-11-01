import '@angular/compiler';
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { signal, Injector, runInInjectionContext } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import { TaxRulesApiService, TaxRule } from '../src/app/core/services/api/tax-rules-api.service';
import { TaxRulesComponent } from '../src/app/features/vat-compliance/tax-rules.component';
import { CompanyContextService } from '../src/app/core/services/company-context.service';
const rule = { id: 'rule', taxCode: '2916.20.00', treatment: 'Purchase', ratePercent: 10,
  recoverablePercent: 80, createdBy: 'maker', publishedAt: null, version: 7 } as TaxRule;
function screen(allowed = true, actor = 'reviewer', fail = false) {
  let requests = 0;
  const component = Object.assign(Object.create(TaxRulesComponent.prototype), {
    api: { publish: () => { requests++; return fail ? throwError(() => ({ error: { detail: 'Rule changed.' } })) : of({ ...rule, publishedAt: '2026-10-31', version: 8 }); } },
    context: { companyGuid: () => 'company', currentUser: () => ({ id: actor }), hasPermission: () => allowed },
    busy: signal(false), reloadRequired: signal(false), rules: signal([rule]), selected: signal(rule), error: signal(''), message: signal('')
  }) as TaxRulesComponent;
  return { component, count: () => requests };
}
test('tax rules publish is forbidden for the maker, read-only users and published rules', () => {
  for (const s of [screen(true, 'maker'), screen(false)]) { s.component.publish(rule); assert.equal(s.count(), 0); }
  assert.equal(screen().component.canPublish({ ...rule, publishedAt: '2026-10-31' }), false);
});
test('tax rules publish updates status only after the server confirms', () => {
  const s = screen(); s.component.publish(rule);
  assert.equal(s.count(), 1); assert.equal(s.component.rules()[0].version, 8);
  assert.ok(s.component.rules()[0].publishedAt); assert.equal(s.component.selected(), null);
});
test('tax rules publish failure preserves the draft and blocks retry until reload', () => {
  const s = screen(true, 'reviewer', true); s.component.publish(rule); s.component.publish(rule);
  assert.equal(s.count(), 1); assert.equal(s.component.rules()[0].publishedAt, null);
  assert.equal(s.component.reloadRequired(), true); assert.match(s.component.error(), /Rule changed/);
});
test('tax rules API scopes listing and publishes the exact reviewed version', () => {
  const calls: unknown[] = [];
  const injector = Injector.create({ providers: [
    { provide: CompanyContextService, useValue: { companyGuid: () => 'company' } },
    { provide: HttpClient, useValue: { get: (url: string, options: unknown) => { calls.push([url, options]); return of([]); },
      post: (url: string, body: unknown) => { calls.push([url, body]); return of(rule); } } }
  ] });
  const api = runInInjectionContext(injector, () => new TaxRulesApiService());
  api.list(25).subscribe(); api.publish(rule).subscribe();
  assert.deepEqual(calls, [['/api/v1/companies/company/tax/rules', { params: { skip: 25, take: 25 } }],
    ['/api/v1/companies/company/tax/rules/rule/publish', { expectedVersion: 7 }]]);
});
