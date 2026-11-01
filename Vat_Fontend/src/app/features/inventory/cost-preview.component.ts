import { Component, OnDestroy, effect, inject, signal, untracked } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { Subscription } from 'rxjs';
import { CompanyContextService } from '../../core/services/company-context.service';
import { CostingApiService, CostPreview } from '../../core/services/api/costing-api.service';

@Component({
  selector: 'app-cost-preview', standalone: true, imports: [CommonModule, RouterLink],
  template: `
    <section class="vat-card" style="padding:24px">
      <h1>Purchase-net source cost preview</h1>
      <p>This preview uses original purchase receipts and original sales issues in posting order. It excludes VAT, landed costs and production costs. It is not final inventory valuation.</p>
      <p>Warehouse: {{warehouse}} / Product: {{product}}</p>
      <button class="btn btn-secondary" [disabled]="busy()" (click)="load()">Reload preview</button>
      <p *ngIf="busy()" role="status">Loading cost evidence...</p>
      <p *ngIf="error()" role="alert">{{error()}}</p>
      <section *ngIf="result() as r">
        <p>Captured: {{r.capturedAt}}. Supported company source sequence: {{r.supportedSourceSequence ?? 'Unavailable'}}. Sources inspected (bounded): {{r.sourceCount}}.</p>
        <p *ngIf="r.state === 'Unavailable'" role="status">Preview unavailable: {{reason(r.reason)}}</p>
        <ng-container *ngIf="r.state === 'Preview' && r.calculation as c">
          <h2>{{c.method === 'FIFO' ? 'FIFO' : 'Moving Weighted Average'}}</h2>
          <table class="vat-table"><thead><tr><th>Source movement</th><th>Quantity</th><th>Purchase-net amount (BDT)</th></tr></thead>
            <tbody><tr><td>Received</td><td>{{c.receivedQuantity}}</td><td>{{c.receivedValue}}</td></tr>
              <tr><td>Issued</td><td>{{c.issuedQuantity}}</td><td>{{c.issuedValue}}</td></tr>
              <tr><td>Closing on hand (including reserved)</td><td>{{c.closingQuantity}}</td><td>{{c.closingValue}}</td></tr></tbody></table>
          <p>Showing the first {{c.issues.length > 100 ? 100 : c.issues.length}} of {{c.issues.length}} issue allocations.</p>
          <article *ngFor="let issue of c.issues.slice(0,100)">
            <h3>Issue {{issue.sourceId}}</h3><p>Quantity {{issue.quantity}} / BDT {{issue.value}}</p>
            <p *ngFor="let a of issue.allocations">{{a.receiptId ?? 'Weighted average pool'}}: {{a.quantity}} / BDT {{a.value}}</p>
          </article>
        </ng-container>
      </section>
      <a routerLink="/app/inventory/summary">Back to stock balances</a>
    </section>`
})
export class CostPreviewComponent implements OnDestroy {
  private api = inject(CostingApiService);
  readonly context = inject(CompanyContextService);
  private route = inject(ActivatedRoute);
  private params = toSignal(this.route.queryParamMap, { initialValue: this.route.snapshot.queryParamMap });
  private request = new Subscription();
  readonly busy = signal(false); readonly error = signal(''); readonly result = signal<CostPreview | null>(null);
  warehouse = ''; product = '';
  constructor() {
    effect(() => {
      this.context.companyGuid(); const params = this.params();
      untracked(() => { this.warehouse = params.get('warehouseId') ?? ''; this.product = params.get('productId') ?? ''; this.load(); });
    });
  }
  load(): void {
    this.request.unsubscribe(); this.request = new Subscription(); this.result.set(null); this.error.set(''); this.busy.set(false);
    if (!this.context.hasPermission('costing.read')) { this.error.set('Costing read permission is required.'); return; }
    const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
    const company = this.context.companyGuid(), warehouse = this.warehouse, product = this.product;
    if (!uuid.test(warehouse) || !uuid.test(product)) { this.error.set('Select a warehouse/product position from stock balances.'); return; }
    this.busy.set(true);
    this.request.add(this.api.preview(warehouse, product).subscribe({
      next: r => {
        if (company !== this.context.companyGuid() || warehouse !== this.warehouse || product !== this.product) return;
        this.busy.set(false);
        if (r.companyId !== company || r.warehouseId !== warehouse || r.productId !== product || r.basis !== 'PurchaseNetOnly' ||
            !['Preview','Unavailable'].includes(r.state) || (r.state === 'Preview' && !r.calculation) || (r.state === 'Unavailable' && r.calculation !== null)) {
          this.error.set('Cost preview response mismatch. Reload required.'); return;
        }
        this.result.set(r);
      },
      error: () => { if (company === this.context.companyGuid() && warehouse === this.warehouse && product === this.product) {
        this.busy.set(false); this.error.set('Cost preview could not be loaded. Retry when the API is available.');
      } }
    }));
  }
  reason(code: string | null): string {
    const reasons: Record<string, string> = {
      cost_source_journal_missing: 'No ordered source journal has been captured.',
      opening_cost_adoption_required: 'Existing stock history requires reviewed opening-cost adoption.',
      unsupported_cost_movements: 'This position includes returns, reversals or transfers whose costs are not yet supported.',
      cost_source_coverage_gap: 'Some stock movements have no matching cost source.',
      cost_source_limit_exceeded: 'The position exceeds the 10,000-source preview limit; no partial calculation is shown.',
      cost_sources_empty: 'There are no supported sources for this position.',
      cost_policy_unassigned: 'A source was posted without an approved costing policy.',
      cost_policy_conversion_required: 'The costing method changed; reviewed cost conversion is required.',
      cost_currency_not_supported: 'The preview currently supports BDT purchase sources.',
      cost_source_mismatch: 'Cost evidence does not match its original receipt or sales issue.',
      cost_stock_reconciliation_failed: 'Calculated closing quantity does not reconcile with on-hand stock.'
    };
    return code ? reasons[code] ?? `Calculation blocked (${code}).` : 'Cost evidence could not be verified.';
  }
  ngOnDestroy(): void { this.request.unsubscribe(); }
}
