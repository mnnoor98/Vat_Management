import { Component, Input, OnInit, OnDestroy, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subscription } from 'rxjs';
import { RegisterReportService, ReportInvoice } from '../../core/services/api/register-report.service';
import { CompanyContextService } from '../../core/services/company-context.service';
import { CsvExportService } from '../../core/services/csv-export.service';
@Component({ selector: 'app-invoice-register-report', standalone: true, imports: [CommonModule, FormsModule],
  template: `
    <section class="vat-card">
      <h1>{{ kind === 'purchase' ? 'Purchase' : 'Sales' }} original posting register</h1>
      <p>All invoice pages must load before totals or export are available. This live scan is not a point-in-time database snapshot. Values are original postings; later reversals, credit/debit notes and payments are separate. This review is not a certified statutory register.</p>
      <label>From (inclusive)<input class="vat-input" type="date" [(ngModel)]="from"></label>
      <label>Until (exclusive)<input class="vat-input" type="date" [(ngModel)]="until"></label>
      <button class="btn btn-secondary" [disabled]="loading()" (click)="load()">Reload all pages</button>
      <button class="btn btn-primary" [disabled]="!ready()" (click)="exportToCsv()">Export filtered original postings</button>
      <p *ngIf="loading()">Loading invoice pages and posting snapshots?</p>
      <p role="alert" *ngIf="error()">{{ error() }}</p>
      <p *ngIf="complete() && !sameCompany()" role="alert">Company changed. Reload this report before review or export.</p>
      <p *ngIf="!validRange()" role="alert">Choose valid dates with From earlier than Until.</p>
      <ng-container *ngIf="ready()">
        <p>All pages loaded: {{ invoices().length }} source documents. Filtered original posted documents: {{ displayed().length }}.</p>
        <p>Filtered net: {{ totals().net | number:'1.2-4' }} ? VAT: {{ totals().vat | number:'1.2-4' }} ? Gross: {{ totals().gross | number:'1.2-4' }} BDT</p>
        <div class="vat-table-container"><table class="vat-table">
          <thead><tr><th>Reference / date</th><th>Party</th><th>Branch ID</th><th>State</th><th>Net</th><th>Original VAT</th><th>Original gross</th></tr></thead>
          <tbody><tr *ngFor="let i of displayed()"><td>{{ i.invoiceNumber }}<br>{{ date(i) }}</td><td>{{ party(i) }}</td><td>{{ i.branchId }}</td><td>{{ i.status }}</td><td>{{ i.subTotal | number:'1.2-4' }}</td><td>{{ i.totalVat | number:'1.2-4' }}</td><td>{{ i.grandTotal | number:'1.2-4' }}</td></tr>
            <tr *ngIf="displayed().length === 0"><td>No original postings in this range.</td></tr>
          </tbody>
        </table></div>
      </ng-container>
    </section>
  `,
  styles: [`label { display:inline-block; margin:12px 12px 12px 0; } button { margin:10px 8px 10px 0; } [role=alert] { color:#dc2626; }`]
})
export class InvoiceRegisterReportComponent implements OnInit, OnDestroy {
  @Input({ required: true }) kind!: 'purchase' | 'sales';
  private reports = inject(RegisterReportService);
  private context = inject(CompanyContextService);
  private csv = inject(CsvExportService);
  readonly loading = signal(false);
  readonly complete = signal(false);
  readonly error = signal('');
  readonly invoices = signal<ReportInvoice[]>([]);
  private request?: Subscription;
  private company = '';
  from = new Date().toISOString().slice(0, 7) + '-01';
  until = new Date(Date.UTC(Number(this.from.slice(0, 4)), Number(this.from.slice(5, 7)), 1)).toISOString().slice(0, 10);
  ngOnInit(): void { this.load(); }
  ngOnDestroy(): void { this.request?.unsubscribe(); }
  sameCompany(): boolean { return this.company === this.context.companyGuid(); }
  validRange(): boolean {
    const valid = (s: string) => /^\d{4}-\d{2}-\d{2}$/.test(s) && !Number.isNaN(Date.parse(s)) && new Date(s).toISOString().slice(0, 10) === s;
    return valid(this.from) && valid(this.until) && this.from < this.until;
  }
  ready(): boolean { return this.complete() && !this.loading() && this.sameCompany() && this.validRange(); }
  load(): void {
    this.request?.unsubscribe(); this.loading.set(true); this.complete.set(false); this.error.set(''); this.invoices.set([]); this.company = this.context.companyGuid();
    this.request = this.reports.scan(this.kind).subscribe({ next: rows => {
      const posted = ['Posted', 'Reversed', 'ReversalPending', 'CreditPending', 'Credited', 'PartiallyCredited', 'DebitPending'];
      if (rows.some(i => posted.includes(i.status) && !i.taxCalculated)) { this.error.set('A posted document is missing its tax snapshot. Report export is unavailable.'); this.loading.set(false); return; }
      this.invoices.set(rows); this.complete.set(true); this.loading.set(false);
    }, error: e => { this.error.set(e?.message || 'Unable to load the complete report.'); this.loading.set(false); } });
  }
  date(i: ReportInvoice): string { return 'invoiceDate' in i ? i.invoiceDate : i.deliveryDate; }
  party(i: ReportInvoice): string { return 'supplierName' in i ? i.supplierName : i.customerName; }
  displayed(): ReportInvoice[] {
    if (!this.ready()) return [];
    return this.invoices().filter(i => i.taxCalculated && !['Draft', 'Submitted', 'Approved', 'PostingPrepared', 'Reserved', 'Rejected', 'Cancelled'].includes(i.status) && this.date(i) >= this.from && this.date(i) < this.until)
      .sort((a, b) => this.date(a).localeCompare(this.date(b)) || a.id.localeCompare(b.id));
  }
  totals(): { net: number; vat: number; gross: number } { return this.displayed().reduce((t, i) => ({ net: t.net + i.subTotal, vat: t.vat + i.totalVat, gross: t.gross + i.grandTotal }), { net: 0, vat: 0, gross: 0 }); }
  exportToCsv(): void {
    if (!this.ready()) return;
    const rows = this.displayed().flatMap(i => i.lines.map(l => [i.id, i.invoiceNumber, this.date(i), this.party(i), i.branchId, i.status, l.productId, l.productName, l.uom, l.quantity, l.unitPrice, l.taxableAmount, l.vatRatePercent, l.vatAmount, l.totalAmount]));
    this.csv.exportToCsv(`${this.kind}_Original_Postings_${this.company}_${this.from}_${this.until}`, ['Document ID', 'Reference', 'Business date', 'Party', 'Branch ID', 'Current state', 'Product reference', 'Description', 'Unit', 'Quantity', 'Unit price', 'Original net', 'Original VAT rate', 'Original VAT', 'Original gross'], rows);
  }
}
