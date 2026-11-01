import { Component, inject, signal, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TaxRulesApiService, TaxRule } from '../../core/services/api/tax-rules-api.service';
import { CompanyContextService } from '../../core/services/company-context.service';
@Component({
  selector: 'app-tax-rules', standalone: true, imports: [CommonModule],
  template: `
    <section class="vat-card" style="padding:24px">
      <h1>Tax Rules</h1>
      <p>Review the tax code, treatment, dates, rates and evidence before publishing. The end date is exclusive.</p>
      <p role="alert" *ngIf="error()">{{ error() }}</p>
      <p role="status" *ngIf="message()">{{ message() }}</p>
      <button class="btn btn-secondary" [disabled]="busy()" (click)="load()">Reload rules</button>
      <p *ngIf="busy()">Loading...</p>
      <p *ngIf="!busy() && !rules().length">No rules available for this company.</p>
      <div class="vat-table-container"><table class="vat-table" *ngIf="rules().length">
        <thead><tr><th>Tax code / Treatment</th><th>VAT</th><th>Recoverable</th><th>Effective dates</th><th>Basis / Reference</th><th>Status</th><th>Action</th></tr></thead>
        <tbody><tr *ngFor="let rule of rules()">
          <td>{{rule.taxCode}}<br>{{rule.treatment}}</td><td>{{rule.ratePercent}}%</td><td>{{rule.recoverablePercent}}%</td>
          <td>{{rule.effectiveFrom}} to {{rule.effectiveUntil}} (exclusive)</td>
          <td>{{rule.legalBasis}}<br>{{rule.evidenceReference}}</td><td>{{rule.publishedAt ? 'Published' : 'Draft'}}</td>
          <td><button class="btn btn-primary" *ngIf="canPublish(rule)" [disabled]="busy()" (click)="selected.set(rule)">Review & Publish</button>
          <span *ngIf="!rule.publishedAt && !canPublish(rule)">Requires a different user with tax.rule.publish permission.</span></td>
        </tr></tbody>
      </table></div>
      <button class="btn btn-secondary" [disabled]="busy() || skip === 0" (click)="page(-1)">Previous</button>
      <button class="btn btn-secondary" [disabled]="busy() || rules().length < 25 || skip >= 10000" (click)="page(1)">Next</button>
      <section *ngIf="selected() as rule" class="vat-card" style="padding:20px;margin-top:20px" aria-label="Confirm tax rule publication">
        <h2>Confirm publication: {{rule.taxCode}}</h2>
        <p>{{rule.treatment}}: VAT {{rule.ratePercent}}%, Recoverable {{rule.recoverablePercent}}%.</p>
        <p>{{rule.effectiveFrom}} to {{rule.effectiveUntil}} (end exclusive).</p>
        <p>{{rule.legalBasis}} - {{rule.evidenceReference}}</p>
        <p>Publishing makes this rule available to financial calculations.</p>
        <button class="btn btn-primary" [disabled]="busy() || !canPublish(rule)" (click)="publish(rule)">Confirm Publish</button>
        <button class="btn btn-secondary" [disabled]="busy()" (click)="selected.set(null)">Cancel</button>
      </section>
    </section>`
})
export class TaxRulesComponent {
  private api = inject(TaxRulesApiService);
  private context = inject(CompanyContextService);
  readonly rules = signal<TaxRule[]>([]);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly message = signal('');
  readonly selected = signal<TaxRule | null>(null);
  skip = 0;
  readonly reloadRequired = signal(false);
  constructor() { effect(() => { if (this.context.companyGuid()) { this.skip = 0; this.load(); } else { this.rules.set([]); this.selected.set(null); this.busy.set(false); } }); }
  canPublish(rule: TaxRule) { return !this.reloadRequired() && !rule.publishedAt && !!this.context.currentUser().id && rule.createdBy !== this.context.currentUser().id && this.context.hasPermission('tax.rule.publish'); }
  load() {
    this.busy.set(true); this.error.set(''); this.message.set(''); this.selected.set(null); this.reloadRequired.set(false);
    const company = this.context.companyGuid();
    this.api.list(this.skip).subscribe({ next: rules => { if (company !== this.context.companyGuid()) return; this.rules.set(rules); this.busy.set(false); }, error: e => { if (company !== this.context.companyGuid()) return; this.rules.set([]); this.fail(e); } });
  }
  page(direction: number) { this.skip = Math.max(0, Math.min(10000, this.skip + direction * 25)); this.load(); }
  publish(rule: TaxRule) {
    if (this.busy() || !this.canPublish(rule) || this.selected()?.id !== rule.id) return;
    this.busy.set(true); this.error.set(''); this.message.set('');
    const company = this.context.companyGuid();
    this.api.publish(rule).subscribe({ next: updated => { if (company !== this.context.companyGuid()) return; this.rules.update(rows => rows.map(row => row.id === updated.id ? updated : row)); this.selected.set(null); this.busy.set(false); this.message.set('Rule published. Reload the purchase invoice and prepare the tax calculation again.'); }, error: e => { if (company !== this.context.companyGuid()) return; this.selected.set(null); this.reloadRequired.set(true); this.fail(e); } });
  }
  private fail(e: { error?: { detail?: string }; message?: string }) { this.error.set(e.error?.detail || e.message || 'Request failed. Reload before retrying.'); this.busy.set(false); }
}
