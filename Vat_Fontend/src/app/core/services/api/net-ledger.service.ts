import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, defer, EMPTY, expand, reduce, map, switchMap } from 'rxjs';
import { CompanyContextService } from '../company-context.service';
import { environment } from '../../../../environments/environment';
export interface LedgerFreshness { lastProcessedAt: string | null; latestObservedEventAt: string | null; unprocessedFinancialEvents: number; quarantinedMessages: number; isCaughtUp: boolean; }
export interface NetSummary { from: string; until: string; netAmount: number; taxAmount?: number; outputTaxAmount?: number; recoverableTaxAmount?: number; grossAmount?: number; transactionCount?: number; invoiceCount?: number; creditNoteCount?: number; priceCreditCount?: number; debitNoteCount?: number; freshness?: LedgerFreshness; }
export interface NetEntry { id: string; documentId: string; businessDate: string; kind: string; net: number; vat: number; recoverable: number; correctionId?: string | null; correctionType?: 'credit' | 'debit'; reverses?: string | null; adjusts?: string | null; }
interface PurchaseEntry { transactionId: string; purchaseId: string; businessDate: string; kind: string; netAmount: number; taxAmount: number; recoverableTaxAmount: number; reversalOfTransactionId?: string | null; }
interface SalesEntry { id: string; invoiceId: string; businessDate: string; entryType: string; netAmount: number; outputTaxAmount: number; creditNoteId?: string | null; debitNoteId?: string | null; reversesEntryId?: string | null; adjustsEntryId?: string | null; }
export interface NetLedgerReport { summary: NetSummary; entries: NetEntry[]; }
interface Page { entries: NetEntry[]; next?: string | null; }
@Injectable({ providedIn: 'root' })
export class NetLedgerService {
  private http = inject(HttpClient);
  private context = inject(CompanyContextService);
  read(kind: 'purchase' | 'sales', from: string, until: string): Observable<NetLedgerReport> {
    return defer(() => {
      const company = this.context.companyGuid();
      const base = `${environment.apiGatewayUrl}/companies/${company}`;
      const assertCompany = () => { if (company !== this.context.companyGuid()) throw new Error('Company changed during ledger loading. Reload.'); };
      const summary = () => this.http.get<NetSummary>(`${base}/${kind === 'purchase' ? 'reports/financial-summary' : 'sales/summary'}`, { params: { from, until } });
      const page = (before?: string): Observable<Page> => {
        assertCompany();
        if (kind === 'purchase') return this.http.get<PurchaseEntry[]>(`${base}/reports/financial-entries`, { params: { from, until, take: 100, ...(before ? { before } : {}) } }).pipe(map(rows => ({
          entries: rows.map(e => ({ id: e.transactionId, documentId: e.purchaseId, businessDate: e.businessDate, kind: e.kind, net: e.netAmount, vat: e.taxAmount, recoverable: e.recoverableTaxAmount, reverses: e.reversalOfTransactionId })), next: rows.length === 100 ? rows.at(-1)!.transactionId : null
        })));
        return this.http.get<{ entries: SalesEntry[]; nextBefore: string | null }>(`${base}/sales/ledger`, { params: { take: 100, ...(before ? { before } : {}) } }).pipe(map(p => ({
          entries: p.entries.map(e => ({ id: e.id, documentId: e.invoiceId, businessDate: e.businessDate, kind: e.entryType, net: e.netAmount, vat: e.outputTaxAmount, recoverable: 0, correctionId: e.creditNoteId || e.debitNoteId, correctionType: e.creditNoteId ? 'credit' as const : e.debitNoteId ? 'debit' as const : undefined, reverses: e.reversesEntryId, adjusts: e.adjustsEntryId })), next: p.nextBefore
        })));
      };
      let pages = 1; const seen = new Set<string>(); const cursors = new Set<string>();
      const scan = () => page().pipe(expand(p => {
        if (!p.next) return EMPTY;
        if (!p.entries.length || cursors.has(p.next)) throw new Error('Ledger cursor did not advance. Reload.');
        cursors.add(p.next);
        if (pages++ >= 100) throw new Error('Ledger scan limit reached. Use a server-generated report for larger datasets.');
        return page(p.next);
      }), reduce((all, p) => {
        assertCompany();
        for (const e of p.entries) { if (!e.id || seen.has(e.id)) throw new Error('Ledger repeated an entry. Reload.'); seen.add(e.id); }
        all.push(...p.entries); return all;
      }, [] as NetEntry[]), map(entries => entries.filter(e => e.businessDate >= from && e.businessDate < until)));
      return summary().pipe(switchMap(before => {
        if (before.from !== from || before.until !== until) throw new Error('Server summary returned a different date range.');
        this.requireFresh(before, kind);
        return scan().pipe(switchMap(entries => summary().pipe(map(after => {
          assertCompany(); this.requireFresh(after, kind);
          if (this.signature(before) !== this.signature(after)) throw new Error('Ledger summary changed while loading. Reload before export.');
          this.reconcile(entries, after, kind);
          return { summary: after, entries };
        }))));
      }));
    });
  }
  private units(n: number): number {
    const value = Math.round(n * 1000000);
    if (!Number.isFinite(n) || !Number.isSafeInteger(value)) throw new Error('Amount exceeds reliable client reconciliation range. Use a server-generated report.');
    return value;
  }
  private signature(s: NetSummary): string {
    return JSON.stringify([s.from, s.until, this.units(s.netAmount), this.units(s.taxAmount ?? s.outputTaxAmount ?? 0), this.units(s.recoverableTaxAmount ?? 0), s.grossAmount == null ? null : this.units(s.grossAmount), s.transactionCount, s.invoiceCount, s.creditNoteCount, s.priceCreditCount, s.debitNoteCount]);
  }
  private requireFresh(s: NetSummary, kind: string): void {
    if (kind === 'purchase' && (!s.freshness?.isCaughtUp || s.freshness.unprocessedFinancialEvents !== 0 || s.freshness.quarantinedMessages !== 0)) throw new Error(`Purchase projection is incomplete. Pending events: ${s.freshness?.unprocessedFinancialEvents ?? 'unknown'}; quarantined: ${s.freshness?.quarantinedMessages ?? 'unknown'}. Final totals/export are unavailable; resolve freshness and reload.`);
  }
  private reconcile(entries: NetEntry[], s: NetSummary, kind: string): void {
    const sum = (key: 'net' | 'vat' | 'recoverable') => entries.reduce((total, e) => { const next = total + this.units(e[key]); if (!Number.isSafeInteger(next)) throw new Error('Ledger total exceeds reliable client reconciliation range.'); return next; }, 0);
    const distinct = (rows: NetEntry[], key: 'documentId' | 'correctionId') => new Set(rows.map(e => e[key])).size;
    if (kind === 'sales' && (distinct(entries.filter(e => !e.correctionId), 'documentId') !== s.invoiceCount ||
      distinct(entries.filter(e => e.correctionType === 'credit'), 'correctionId') !== (s.creditNoteCount ?? 0) ||
      distinct(entries.filter(e => e.kind === 'PriceCredit'), 'correctionId') !== (s.priceCreditCount ?? 0) ||
      distinct(entries.filter(e => e.correctionType === 'debit'), 'correctionId') !== (s.debitNoteCount ?? 0))) throw new Error('Ledger document/correction counts do not reconcile to the server summary.');
    if (sum('net') !== this.units(s.netAmount) || sum('vat') !== this.units(s.taxAmount ?? s.outputTaxAmount ?? 0) ||
      (kind === 'purchase' && (entries.length !== s.transactionCount || sum('recoverable') !== this.units(s.recoverableTaxAmount ?? 0))) ||
      (kind === 'sales' && sum('net') + sum('vat') !== this.units(s.grossAmount ?? NaN))) throw new Error('Ledger entries do not reconcile to the server summary. Export is unavailable; reload.');
  }
}
