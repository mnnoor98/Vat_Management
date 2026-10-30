import '@angular/compiler';
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { Injector, runInInjectionContext } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom, of, throwError } from 'rxjs';
import { NetLedgerService } from '../src/app/core/services/api/net-ledger.service';
import { NetLedgerComponent } from '../src/app/features/reports/net-ledger.component';
import { CompanyContextService } from '../src/app/core/services/company-context.service';
import { CsvExportService } from '../src/app/core/services/csv-export.service';
const from = '2026-10-01', until = '2026-11-01';
const freshness = { isCaughtUp: true, unprocessedFinancialEvents: 0, quarantinedMessages: 0, lastProcessedAt: null, latestObservedEventAt: null };
const summary = { from, until, netAmount: 80, taxAmount: 8, recoverableTaxAmount: 4, transactionCount: 2, freshness };
const original = { transactionId: 'original', purchaseId: 'purchase', businessDate: '2026-10-15', kind: 'Purchase', netAmount: 100, taxAmount: 10, recoverableTaxAmount: 5 };
const reversal = { ...original, transactionId: 'reversal', kind: 'Reversal', netAmount: -20, taxAmount: -2, recoverableTaxAmount: -1, reversalOfTransactionId: 'original' };
function create<T>(type: new () => T, providers: { provide: unknown; useValue: unknown }[], company = () => 'company'): T {
  const injector = Injector.create({ providers: [{ provide: CompanyContextService, useValue: { companyGuid: company } }, ...providers as never[]] });
  return runInInjectionContext(injector, () => new type());
}
function service(get: (url: string, options?: unknown) => unknown, company?: () => string) {
  return create(NetLedgerService, [{ provide: HttpClient, useValue: { get, post: () => assert.fail('Report loading must be read-only') } }], company);
}
test('purchase net ledger reconciles signed reversals, recoverable tax and both summary reads', async () => {
  let summaries = 0;
  const api = service((url, options) => {
    if (url.endsWith('/financial-summary')) { summaries++; return of(summary); }
    assert.deepEqual(options, { params: { from, until, take: 100 } }); return of([original, reversal]);
  });
  const report = await firstValueFrom(api.read('purchase', from, until));
  assert.equal(summaries, 2); assert.equal(report.summary.netAmount, 80); assert.equal(report.entries[1].net, -20); assert.equal(report.entries[1].reverses, 'original'); assert.equal(report.entries[1].recoverable, -1);
});
test('purchase lag or quarantine blocks final totals before entry scanning', async () => {
  const api = service(url => { assert.match(url, /financial-summary$/); return of({ ...summary, freshness: { ...freshness, unprocessedFinancialEvents: 3, quarantinedMessages: 1 } }); });
  await assert.rejects(firstValueFrom(api.read('purchase', from, until)), /Pending events: 3; quarantined: 1/);
});
test('entry-summary mismatch cannot produce a successful report', async () => {
  const api = service(url => of(url.endsWith('/financial-summary') ? summary : [original]));
  await assert.rejects(firstValueFrom(api.read('purchase', from, until)), /reconcile/);
});
test('a changed before/after summary blocks export instead of using mixed reads', async () => {
  let n = 0; const api = service(url => of(url.endsWith('/financial-summary') ? { ...summary, netAmount: ++n === 1 ? 80 : 81 } : [original, reversal]));
  await assert.rejects(firstValueFrom(api.read('purchase', from, until)), /changed while loading/);
});
test('summary date mismatch is rejected', async () => {
  const api = service(() => of({ ...summary, until: '2026-12-01' }));
  await assert.rejects(firstValueFrom(api.read('purchase', from, until)), /different date range/);
});
test('sales ledger includes signed price credits and debits and filters by correction business date', async () => {
  const totals = { from, until, netAmount: 85, outputTaxAmount: 8.5, grossAmount: 93.5, invoiceCount: 1, creditNoteCount: 1, priceCreditCount: 1, debitNoteCount: 1 };
  const row = { id: 'sale', invoiceId: 'invoice', businessDate: '2026-10-15', entryType: 'Goods', netAmount: 100, outputTaxAmount: 10 };
  let pages = 0;
  const api = service((url, options) => {
    if (url.endsWith('/summary')) return of(totals);
    pages++; if (pages === 1) return of({ entries: [row], nextBefore: 'sale' });
    assert.deepEqual(options, { params: { take: 100, before: 'sale' } });
    return of({ entries: [{ ...row, id: 'credit', entryType: 'PriceCredit', creditNoteId: 'credit-note', adjustsEntryId: 'sale', netAmount: -20, outputTaxAmount: -2 }, { ...row, id: 'debit', entryType: 'PriceDebit', debitNoteId: 'debit-note', netAmount: 5, outputTaxAmount: .5 }, { ...row, id: 'outside', businessDate: until }], nextBefore: null });
  });
  const report = await firstValueFrom(api.read('sales', from, until));
  assert.equal(report.entries.length, 3); assert.equal(report.entries[1].correctionId, 'credit-note'); assert.equal(report.entries[1].adjusts, 'sale'); assert.equal(report.entries[2].correctionId, 'debit-note');
});
test('ledger duplicate entries fail the whole scan', async () => {
  const api = service(url => of(url.endsWith('/financial-summary') ? summary : [original, original]));
  await assert.rejects(firstValueFrom(api.read('purchase', from, until)), /repeated/);
});
test('company changes during ledger loading cancel its result', async () => {
  let company = 'company'; const api = service(url => { if (url.endsWith('/financial-summary')) return of(summary); company = 'other'; return of([original, reversal]); }, () => company);
  await assert.rejects(firstValueFrom(api.read('purchase', from, until)), /Company changed/);
});
test('unsupported numeric range fails reconciliation rather than rounding silently', async () => {
  const huge = { ...summary, netAmount: 1e15 }; const api = service(url => of(url.endsWith('/financial-summary') ? huge : [original, reversal]));
  await assert.rejects(firstValueFrom(api.read('purchase', from, until)), /reliable client/);
});
function screen(read: () => unknown, csv: object = {}, company?: () => string) {
  const component = create(NetLedgerComponent, [{ provide: NetLedgerService, useValue: { read } }, { provide: CsvExportService, useValue: csv }], company);
  component.from = from; component.until = until; return component;
}
test('net ledger screen clears stale data on failure and blocks export', () => {
  const component = screen(() => throwError(() => new Error('Not caught up')), { exportToCsv: () => assert.fail('No incomplete export') }); component.load(); component.exportCsv();
  assert.equal(component.ready(), false); assert.equal(component.report(), null); assert.equal(component.error(), 'Not caught up');
});
test('net ledger export preserves signed corrections and becomes unavailable when filters change', () => {
  let exports = 0; const component = screen(() => of({ summary, entries: [{ id: 'reversal', documentId: 'purchase', businessDate: '2026-10-15', kind: 'Reversal', net: -20, vat: -2, recoverable: -1, reverses: 'original' }] }), {
    exportToCsv: (_name: string, _headers: unknown, rows: unknown[][]) => { exports++; assert.equal(rows[0][7], -20); assert.equal(rows[0][5], 'original'); }
  });
  component.load(); component.exportCsv(); assert.equal(exports, 1); component.kind = 'sales'; component.exportCsv(); assert.equal(exports, 1); assert.equal(component.ready(), false);
});
test('invalid ranges make no ledger request', () => {
  const component = screen(() => assert.fail('Invalid range')); component.until = '2026-02-30'; component.load(); assert.equal(component.ready(), false); assert.match(component.error(), /valid/);
});

test('missing zero-value sales corrections fail document-count reconciliation', async () => {
  const api = service(url => of(url.endsWith('/summary') ? { from, until, netAmount: 0, outputTaxAmount: 0, grossAmount: 0, invoiceCount: 0, creditNoteCount: 1, priceCreditCount: 1, debitNoteCount: 0 } : { entries: [], nextBefore: null }));
  await assert.rejects(firstValueFrom(api.read('sales', from, until)), /counts do not reconcile/);
});
