import { Component, OnDestroy, effect, inject, signal, untracked } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subscription } from 'rxjs';
import { CostingApiService, CostingDraft, CostingPolicy } from '../../core/services/api/costing-api.service';
import { CompanyContextService } from '../../core/services/company-context.service';
import { MasterDataApiService } from '../../core/services/api/master-data-api.service';
import { ProductItem } from '../../core/models/vat-models';

@Component({
  selector: 'app-costing-policies', standalone: true, imports: [CommonModule, FormsModule],
  template: `
    <section class="vat-card" style="padding:24px">
      <h1>Inventory costing policies</h1>
      <p>An approved product policy overrides the company default for its effective dates. End dates are exclusive. Approval requires an open period and a different user.</p>
      <p>Approved policies determine receipt and issue costing. Activate reconciled stock valuation through independent opening adoption and landed-cost review. Production cost posting remains a separate workflow.</p>
      <p role="alert" *ngIf="error()">{{error()}}</p><p role="status" *ngIf="message()">{{message()}}</p>
      <button class="btn btn-secondary" [disabled]="busy()" (click)="load()">Reload policies</button>
      <fieldset *ngIf="context.hasPermission('costing.manage')" [disabled]="busy()">
        <legend>Create draft</legend>
        <label>Scope <select [(ngModel)]="draft.productId"><option [ngValue]="null">Company default</option><option *ngFor="let p of products()" [ngValue]="p.id">{{p.code}} — {{p.name}}</option></select></label>
        <p *ngIf="catalogError()">{{catalogError()}} Company-default policies can still be created.</p>
        <label>Method <select [(ngModel)]="draft.method"><option value="FIFO">FIFO</option><option value="WEIGHTED_AVERAGE">Moving Weighted Average</option></select></label>
        <label>Effective from <input type="date" [(ngModel)]="draft.effectiveFrom" /></label>
        <label>Effective until (exclusive) <input type="date" [(ngModel)]="draft.effectiveUntil" /></label>
        <button class="btn btn-primary" (click)="create()">Save draft</button>
      </fieldset>
      <button class="btn btn-secondary" *ngIf="catalogError()" [disabled]="busy()" (click)="loadProducts()">Reload product catalogue</button>
      <div class="vat-table-container"><table class="vat-table">
        <thead><tr><th>Scope</th><th>Method</th><th>Effective dates</th><th>State / version</th><th>Maker / approver</th><th>Action</th></tr></thead>
        <tbody><tr *ngFor="let p of policies()"><td>{{scopeLabel(p)}}</td><td>{{p.method}}</td><td>{{p.effectiveFrom}} to {{p.effectiveUntil}}</td><td>{{p.state}} / {{p.version}}</td><td>{{p.createdBy}} / {{p.approvedBy ?? 'Pending'}}</td><td><button class="btn btn-primary" [disabled]="busy() || !canApprove(p)" (click)="selected.set(p)">Review approval</button></td></tr></tbody>
      </table></div>
      <p *ngIf="!busy() && !policies().length">No policies loaded.</p>
      <button class="btn btn-secondary" [disabled]="busy() || page() === 0" (click)="changePage(-1)">Previous</button>
      <span>Page {{page() + 1}}</span><button class="btn btn-secondary" [disabled]="busy() || policies().length !== 25 || page() >= 400" (click)="changePage(1)">Next</button>
      <section *ngIf="selected() as p" aria-label="Confirm costing policy approval">
        <h2>Confirm approval</h2><p>{{scopeLabel(p)}}: {{p.method}}, {{p.effectiveFrom}} to {{p.effectiveUntil}} (exclusive), version {{p.version}}.</p>
        <p>Approved policies cannot be edited. Existing receipt history cannot be reassigned.</p>
        <button class="btn btn-primary" [disabled]="busy() || !canApprove(p)" (click)="approve()">Confirm approval</button>
        <button class="btn btn-secondary" [disabled]="busy()" (click)="selected.set(null)">Back</button>
      </section>
    </section>`,
  styles: [`fieldset { margin:20px 0; padding:16px; } label { display:inline-flex; flex-direction:column; gap:6px; margin:8px; } button { margin:8px; } input,select { padding:8px; } [role=alert] { color:#b42318; }`]
})
export class CostingPoliciesComponent implements OnDestroy {
  private api = inject(CostingApiService);
  private master = inject(MasterDataApiService);
  readonly context = inject(CompanyContextService);
  readonly policies = signal<CostingPolicy[]>([]); readonly products = signal<ProductItem[]>([]);
  readonly busy = signal(false); readonly error = signal(''); readonly message = signal(''); readonly catalogError = signal('');
  readonly selected = signal<CostingPolicy | null>(null); readonly page = signal(0); readonly reloadRequired = signal(true);
  draft: CostingDraft = { productId: null, method: 'WEIGHTED_AVERAGE', effectiveFrom: '', effectiveUntil: '' };
  private subscriptions = new Subscription(); private cursors: (string | undefined)[] = [undefined];
  private company = ''; private commandKey = ''; private commandPayload = '';
  constructor() {
    effect(() => { const company = this.context.companyGuid(); untracked(() => {
      this.subscriptions.unsubscribe(); this.subscriptions = new Subscription(); this.company = company;
      this.busy.set(false); this.page.set(0); this.cursors = [undefined]; this.products.set([]); this.catalogError.set(''); this.message.set('');
      this.commandKey = ''; this.commandPayload = ''; this.draft = { productId: null, method: 'WEIGHTED_AVERAGE', effectiveFrom: '', effectiveUntil: '' };
      this.load();
      this.loadProducts();
    }); });
  }
  loadProducts(): void {
    const company = this.context.companyGuid(); this.products.set([]); this.catalogError.set('');
    if (!this.context.hasPermission('masterdata.read')) { this.catalogError.set('Product review requires masterdata.read permission.'); return; }
    this.subscriptions.add(this.master.getProducts().subscribe({
      next: rows => { if (company === this.context.companyGuid()) this.products.set(rows); },
      error: () => { if (company === this.context.companyGuid()) this.catalogError.set('Product catalogue unavailable.'); }
    }));
  }
  load(): void {
    if (this.busy()) return;
    this.policies.set([]); this.selected.set(null); this.reloadRequired.set(true); this.error.set('');
    if (!this.context.hasPermission('costing.read')) { this.error.set('Costing read permission is required.'); return; }
    this.busy.set(true); const company = this.context.companyGuid();
    this.subscriptions.add(this.api.list(this.cursors[this.page()]).subscribe({
      next: rows => { if (company !== this.context.companyGuid()) return;
        if (rows.some(p => p.companyId !== company)) { this.fail('Policy scope mismatch. Reload required.'); return; }
        this.policies.set(rows); this.reloadRequired.set(false); this.busy.set(false); },
      error: e => { if (company === this.context.companyGuid()) this.fail(e); }
    }));
  }
  changePage(direction: number): void {
    if (this.busy() || (direction < 0 && this.page() === 0) || (direction > 0 && (this.policies().length !== 25 || this.page() >= 400))) return;
    if (direction > 0) this.cursors[this.page() + 1] = this.policies().at(-1)?.id;
    this.page.update(p => p + (direction > 0 ? 1 : -1)); this.load();
  }
  scopeLabel(p: CostingPolicy): string {
    if (p.productId === null) return 'Company default';
    const product = this.products().find(x => x.id === p.productId);
    return product ? `${product.code} — ${product.name}` : `Product ${p.productId} (catalogue details unavailable)`;
  }
  canApprove(p: CostingPolicy): boolean {
    return !this.reloadRequired() && p.companyId === this.context.companyGuid() && p.state === 'Draft' && p.version === 1 &&
      (p.productId === null || this.products().some(product => product.id === p.productId)) &&
      p.createdBy !== this.context.currentUser().id && this.context.hasPermission('costing.approve');
  }
  create(): void {
    if (this.busy() || !this.context.hasPermission('costing.manage') || this.company !== this.context.companyGuid()) return;
    const validDate = (s: string) => /^\d{4}-\d{2}-\d{2}$/.test(s) && Number.isFinite(Date.parse(s)) && new Date(s).toISOString().slice(0,10) === s;
    if (!validDate(this.draft.effectiveFrom) || !validDate(this.draft.effectiveUntil) || this.draft.effectiveFrom >= this.draft.effectiveUntil ||
      !['FIFO', 'WEIGHTED_AVERAGE'].includes(this.draft.method) || (this.draft.productId !== null && !this.products().some(p => p.id === this.draft.productId))) {
      this.error.set('Select a registered product or company default and a valid exclusive date range.'); return;
    }
    const payload = { ...this.draft }, serialized = JSON.stringify(payload);
    if (serialized !== this.commandPayload) { this.commandPayload = serialized; this.commandKey = crypto.randomUUID(); }
    const company = this.company; this.busy.set(true); this.error.set(''); this.message.set('');
    this.subscriptions.add(this.api.create(payload, this.commandKey).subscribe({
      next: p => { if (company !== this.context.companyGuid()) return;
        if (p.companyId !== company || p.state !== 'Draft' || p.version !== 1 || p.method !== payload.method || p.productId !== payload.productId ||
          p.effectiveFrom !== payload.effectiveFrom || p.effectiveUntil !== payload.effectiveUntil) { this.fail('Policy response mismatch. Reload required.'); return; }
        this.busy.set(false); this.message.set('Draft saved. Independent approval is required.'); this.page.set(0); this.cursors = [undefined]; this.load(); },
      error: e => { if (company === this.context.companyGuid()) this.fail(e); }
    }));
  }
  approve(): void {
    const policy = this.selected(); if (!policy || this.busy() || !this.canApprove(policy)) return;
    const company = this.context.companyGuid(); this.busy.set(true); this.error.set(''); this.message.set('');
    this.subscriptions.add(this.api.approve(policy, crypto.randomUUID()).subscribe({
      next: p => { if (company !== this.context.companyGuid()) return;
        if (p.companyId !== company || p.id !== policy.id || p.state !== 'Approved' || p.version !== policy.version + 1 ||
          p.productId !== policy.productId || p.method !== policy.method || p.effectiveFrom !== policy.effectiveFrom || p.effectiveUntil !== policy.effectiveUntil) { this.fail('Policy response mismatch. Reload required.'); return; }
        this.busy.set(false); this.message.set('Policy approved for new receipt capture. Final valuation remains unavailable.'); this.load(); },
      error: e => { if (company === this.context.companyGuid()) { this.reloadRequired.set(true); this.selected.set(null); this.fail(e); } }
    }));
  }
  private fail(e: unknown): void { const failure = e as {error?: {detail?: string}; message?: string}; this.error.set(typeof e === 'string' ? e : failure?.error?.detail || failure?.message || 'Request failed. Reload before retrying.'); this.reloadRequired.set(true); this.selected.set(null); this.busy.set(false); }
  ngOnDestroy(): void { this.subscriptions.unsubscribe(); }
}
