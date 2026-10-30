import { Component, EventEmitter, Input, OnInit, Output, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PurchaseApiService } from '../../core/services/api/purchase-api.service';
import { SalesApiService } from '../../core/services/api/sales-api.service';
import { InventoryApiService } from '../../core/services/api/inventory-api.service';
import { CompanyContextService } from '../../core/services/company-context.service';
import { PurchaseInvoice, SalesInvoice, Warehouse } from '../../core/models/vat-models';
import { FinancialPlan } from '../../core/models/financial-contracts';

@Component({
  selector: 'app-financial-posting', standalone: true, imports: [CommonModule, FormsModule],
  template: `
    <div class="modal-backdrop">
      <section class="vat-card" role="dialog" aria-modal="true" aria-label="Posting review">
        <h2>Posting review ? {{ invoice.invoiceNumber }}</h2>
        <p *ngIf="error()" role="alert">{{ error() }}</p>
        <p *ngIf="busy()">Loading?</p>
        <ng-container *ngIf="!plan()">
          <label>Warehouse for this invoice branch
            <select class="vat-select" [(ngModel)]="warehouseId" [disabled]="busy()">
              <option value="">Select warehouse</option>
              <option *ngFor="let w of warehouses()" [value]="w.warehouseId">{{ w.code }} ? {{ w.name }}</option>
            </select>
          </label>
          <button class="btn btn-secondary" *ngIf="hasMoreWarehouses()" [disabled]="busy()" (click)="loadWarehouses()">Load more warehouses</button>
          <p *ngIf="!busy() && warehouses().length === 0">No warehouses found for this branch.</p>
          <button class="btn btn-primary" [disabled]="busy() || reloadRequired() || !warehouseId || !canPrepare()" (click)="prepare()">
            {{ kind === 'purchase' ? 'Prepare tax calculation' : 'Reserve stock and calculate tax' }}
          </button>
        </ng-container>
        <ng-container *ngIf="plan() as p">
          <p>Warehouse: {{ p.warehouseId }} ? Version: {{ p.sourceVersion }} ? {{ p.status }}</p>
          <p>Prepared by: {{ p.preparedBy || p.reservedBy }}. Posting requires a different authorized user from the maker and preparer.</p>
          <div class="vat-table-container"><table class="vat-table">
            <thead><tr><th>Product</th><th>Qty</th><th>Net</th><th>VAT %</th><th>VAT</th><th>Rule / legal basis</th></tr></thead>
            <tbody><tr *ngFor="let line of p.snapshot.lines">
              <td>{{ line.description }}</td><td>{{ line.quantity }} {{ line.unitOfMeasure }}</td>
              <td>{{ line.netAmount | number:'1.2-4' }}</td><td>{{ line.ratePercent }}</td>
              <td>{{ line.taxAmount ?? line.outputTaxAmount | number:'1.2-4' }}</td>
              <td>{{ line.taxRuleId }} v{{ line.taxRuleVersion }}<br>{{ line.legalBasis }}<br>{{ line.evidenceReference }}</td>
            </tr></tbody>
          </table></div>
          <p>Net: {{ p.snapshot.netAmount | number:'1.2-4' }} ? VAT: {{ p.snapshot.taxAmount ?? p.snapshot.outputTaxAmount | number:'1.2-4' }} ? Total: {{ p.snapshot.grossAmount | number:'1.2-4' }} BDT</p>
          <button class="btn btn-primary" [disabled]="busy() || reloadRequired() || !canPost()" (click)="post()">Confirm posting</button>
        </ng-container>
        <button class="btn btn-secondary" [disabled]="busy()" (click)="closed.emit()">Close</button>
      </section>
    </div>
  `,
  styles: [`section { max-width: 1000px; width: 95vw; max-height: 90vh; overflow: auto; padding: 24px; } button { margin: 12px 8px 0 0; } [role=alert] { color: var(--danger, #dc2626); }`]
})
export class FinancialPostingComponent implements OnInit {
  @Input({ required: true }) invoice!: PurchaseInvoice | SalesInvoice;
  @Input({ required: true }) kind!: 'purchase' | 'sales';
  @Output() changed = new EventEmitter<void>();
  @Output() closed = new EventEmitter<void>();
  private purchase = inject(PurchaseApiService);
  private sales = inject(SalesApiService);
  private inventory = inject(InventoryApiService);
  private context = inject(CompanyContextService);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly plan = signal<FinancialPlan | null>(null);
  readonly reloadRequired = signal(false);
  readonly warehouses = signal<Warehouse[]>([]);
  readonly hasMoreWarehouses = signal(false);
  warehouseId = '';
  private warehouseSkip = 0;

  ngOnInit(): void {
    if (this.invoice.status === 'PostingPrepared' || this.invoice.status === 'Reserved') {
      this.busy.set(true);
      const request = this.kind === 'purchase' ? this.purchase.getPostingPlan(this.invoice.id) : this.sales.getPostingPlan(this.invoice.id);
      request.subscribe({ next: p => { this.plan.set(p); this.busy.set(false); }, error: e => this.fail(e) });
    } else this.loadWarehouses();
  }
  canPrepare(): boolean { return this.context.hasPermission(this.kind === 'purchase' ? 'purchase.prepare' : 'sales.reserve'); }
  canPost(): boolean {
    const actor = this.context.currentUser().id;
    const plan = this.plan();
    return !!plan && plan.status === (this.kind === 'purchase' ? 'Prepared' : 'Reserved')
      && !!actor && actor !== this.invoice.createdBy && actor !== (plan?.preparedBy || plan?.reservedBy)
      && this.context.hasPermission(this.kind === 'purchase' ? 'purchase.post' : 'sales.post');
  }
  loadWarehouses(): void {
    this.busy.set(true); this.error.set('');
    this.inventory.getWarehouses(this.warehouseSkip, 100).subscribe({
      next: items => {
        this.warehouses.update(current => [...current, ...items.filter(w => w.branchId === this.invoice.branchId)]);
        this.warehouseSkip += items.length;
        this.hasMoreWarehouses.set(items.length === 100 && this.warehouseSkip <= 10000);
        this.busy.set(false);
      }, error: e => this.fail(e)
    });
  }
  prepare(): void {
    if (this.reloadRequired() || this.busy() || !this.canPrepare() || !this.warehouses().some(w => w.warehouseId === this.warehouseId)) return;
    if (!Number.isSafeInteger(this.invoice.version)) { this.error.set('Reload the invoice to obtain its current version.'); return; }
    this.busy.set(true); this.error.set('');
    const service = this.kind === 'purchase' ? this.purchase : this.sales;
    service.preparePosting(this.invoice.id, this.invoice.version!, this.warehouseId).subscribe({
      next: p => { this.plan.set(p); this.busy.set(false); this.changed.emit(); }, error: e => { this.reloadRequired.set(true); this.fail(e); }
    });
  }
  post(): void {
    const p = this.plan();
    if (!p || this.reloadRequired() || this.busy() || !this.canPost()) return;
    this.busy.set(true); this.error.set('');
    const request = this.kind === 'purchase'
      ? this.purchase.postPurchase(this.invoice.id, p.sourceVersion, p.id!)
      : this.sales.postSalesInvoice(this.invoice.id, p.sourceVersion);
    request.subscribe({ next: () => { this.busy.set(false); this.changed.emit(); this.closed.emit(); }, error: e => { this.reloadRequired.set(true); this.fail(e); } });
  }
  private fail(error: unknown): void {
    const e = error as { status?: number; message?: string };
    this.error.set(e.status === 409 ? 'The document changed or posting is unavailable. Close and reload before trying again.' : (e.message || 'Request failed. Close and reload the invoice before retrying.'));
    this.busy.set(false);
  }
}
