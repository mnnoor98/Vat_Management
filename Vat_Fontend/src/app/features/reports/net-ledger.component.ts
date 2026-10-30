import { Component, OnDestroy, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subscription } from 'rxjs';
import { NetLedgerService, NetLedgerReport } from '../../core/services/api/net-ledger.service';
import { CompanyContextService } from '../../core/services/company-context.service';
import { CsvExportService } from '../../core/services/csv-export.service';
@Component({ selector: 'app-net-ledger', standalone: true, imports: [CommonModule, FormsModule], template: `
  <section class="vat-card"><h1>Correction-inclusive net ledger</h1>
    <p>Purchase postings/reversals and sales postings/credit/debit notes are separate ledgers. Payments and inventory valuation are excluded. Dates use From inclusive / Until exclusive. Separate live reads are reconciled; this is not a versioned point-in-time or certified statutory report.</p>
    <label>Ledger<select class="vat-select" [(ngModel)]="kind" [disabled]="loading()"><option value="purchase">Purchase</option><option value="sales">Sales</option></select></label>
    <label>From<input class="vat-input" type="date" [(ngModel)]="from" [disabled]="loading()"></label>
    <label>Until<input class="vat-input" type="date" [(ngModel)]="until" [disabled]="loading()"></label>
    <button class="btn btn-primary" [disabled]="loading()" (click)="load()">Load and reconcile</button>
    <button class="btn btn-secondary" [disabled]="!ready()" (click)="exportCsv()">Export reconciled entries</button>
    <p *ngIf="loading()">Loading summary and all ledger pages?</p><p role="alert" *ngIf="error()">{{ error() }}</p>
    <ng-container *ngIf="ready()"><ng-container *ngIf="report() as r">
      <p>Reconciled entries: {{ r.entries.length }} ? Net: {{ r.summary.netAmount | number:'1.2-6' }} ? VAT: {{ r.summary.taxAmount ?? r.summary.outputTaxAmount | number:'1.2-6' }} BDT</p>
      <p *ngIf="kind === 'purchase'">Recoverable VAT: {{ r.summary.recoverableTaxAmount | number:'1.2-6' }} ? Projection caught up. Last processed: {{ r.summary.freshness?.lastProcessedAt || 'No checkpoint timestamp' }} ? Latest observed: {{ r.summary.freshness?.latestObservedEventAt || 'None' }}</p>
      <p *ngIf="kind === 'sales'">Gross: {{ r.summary.grossAmount | number:'1.2-6' }} ? Original invoices: {{ r.summary.invoiceCount }} ? Credit notes: {{ r.summary.creditNoteCount }} (price credits: {{ r.summary.priceCreditCount }}) ? Debit notes: {{ r.summary.debitNoteCount }}. Direct ledger summary; no broker freshness dependency.</p>
      <div class="vat-table-container"><table class="vat-table"><thead><tr><th>Date / kind</th><th>Entry / document</th><th>Correction links</th><th>Net</th><th>VAT</th></tr></thead><tbody>
        <tr *ngFor="let e of r.entries"><td>{{ e.businessDate }} / {{ e.kind }}</td><td>{{ e.id }}<br>{{ e.documentId }}</td><td>{{ e.correctionId }}<br>{{ e.reverses || e.adjusts }}</td><td>{{ e.net | number:'1.2-6' }}</td><td>{{ e.vat | number:'1.2-6' }}</td></tr>
        <tr *ngIf="r.entries.length === 0"><td>No ledger entries in this range.</td></tr>
      </tbody></table></div>
    </ng-container></ng-container>
  </section>`, styles: [`label { display:inline-block; margin:12px; } button { margin:10px 8px; } [role=alert] { color:#dc2626; }`] })
export class NetLedgerComponent implements OnDestroy {
  private api = inject(NetLedgerService);
  private context = inject(CompanyContextService);
  private csv = inject(CsvExportService);
  readonly loading = signal(false); readonly error = signal(''); readonly report = signal<NetLedgerReport | null>(null);
  kind: 'purchase' | 'sales' = 'purchase';
  from = new Date().toISOString().slice(0, 7) + '-01';
  until = new Date(Date.UTC(Number(this.from.slice(0, 4)), Number(this.from.slice(5, 7)), 1)).toISOString().slice(0, 10);
  private key = ''; private request?: Subscription;
  private currentKey(): string { return JSON.stringify([this.context.companyGuid(), this.kind, this.from, this.until]); }
  ready(): boolean { return !this.loading() && !!this.report() && this.key === this.currentKey(); }
  load(): void {
    this.request?.unsubscribe(); this.loading.set(false); this.report.set(null); this.error.set('');
    const valid = (s: string) => /^\d{4}-\d{2}-\d{2}$/.test(s) && !Number.isNaN(Date.parse(s)) && new Date(s).toISOString().slice(0, 10) === s;
    if (!valid(this.from) || !valid(this.until) || this.from >= this.until) { this.error.set('Choose a valid nonempty date range.'); return; }
    this.loading.set(true); this.key = this.currentKey();
    this.request = this.api.read(this.kind, this.from, this.until).subscribe({ next: r => { this.report.set(r); this.loading.set(false); }, error: e => { this.loading.set(false); this.error.set(e?.message || 'Unable to reconcile ledger.'); } });
  }
  exportCsv(): void {
    if (!this.ready()) return;
    this.csv.exportToCsv(`${this.kind}_Net_Ledger_${this.context.companyGuid()}_${this.from}_${this.until}`, ['Entry ID', 'Document ID', 'Business date', 'Kind', 'Correction ID', 'Reverses entry', 'Adjusts entry', 'Net', 'VAT', 'Recoverable purchase VAT'], this.report()!.entries.map(e => [e.id, e.documentId, e.businessDate, e.kind, e.correctionId || '', e.reverses || '', e.adjusts || '', e.net, e.vat, this.kind === 'purchase' ? e.recoverable : 'Not applicable']));
  }
  ngOnDestroy(): void { this.request?.unsubscribe(); }
}
