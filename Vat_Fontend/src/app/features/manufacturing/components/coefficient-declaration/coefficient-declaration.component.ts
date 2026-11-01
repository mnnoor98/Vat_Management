import { Component, OnInit, Input, Output, EventEmitter, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ManufacturingApiService, CoefficientResult, CoefficientAction } from '../../../../core/services/api/manufacturing-api.service';
import { CompanyContextService } from '../../../../core/services/company-context.service';
import { CsvExportService } from '../../../../core/services/csv-export.service';
import { BdtCurrencyPipe } from '../../../../shared/pipes/bdt-currency.pipe';

@Component({
  selector: 'app-coefficient-declaration',
  standalone: true,
  imports: [CommonModule, FormsModule, BdtCurrencyPipe],
  template: `
    <section class="vat-card">
      <h2>Coefficient Declaration Register / সহগ ঘোষণা</h2>
      <p>Saved coefficient snapshots. Statutory print layouts require validation before use.</p>
      <button class="btn btn-secondary" [disabled]="busy()" (click)="loadDeclarations()">Reload</button>
      <button class="btn btn-secondary" [disabled]="busy() || reloadRequired() || !declarations().length" (click)="exportPage()">Export current page</button>
      <p role="alert" *ngIf="error()">{{ error() }}</p>
      <p role="status" *ngIf="message()">{{ message() }}</p>
      <p *ngIf="busy()">Loading...</p>
      <div class="vat-table-container"><table class="vat-table">
        <thead><tr><th>Reference / revision</th><th>Product</th><th>Effective from</th><th>Status</th><th>Unit cost</th><th>Review</th></tr></thead>
        <tbody><tr *ngFor="let d of declarations()">
          <td>{{ d.declarationNumber }} / {{ d.revision }}</td><td>{{ d.snapshot?.finishedProduct?.description ?? 'Unavailable' }}</td>
          <td>{{ d.effectiveFrom }}</td><td>{{ d.state }}</td><td>{{ d.snapshot ? (d.snapshot.unitCost | bdtCurrency:false) : 'Unavailable' }}</td>
          <td><button class="btn btn-secondary" [disabled]="busy()" (click)="setDeclaration(d); declarationSelected.emit(d)">Review</button></td>
        </tr><tr *ngIf="!busy() && !declarations().length"><td colspan="6">No declarations loaded.</td></tr></tbody>
      </table></div>
      <button class="btn btn-secondary" [disabled]="busy() || page() === 0" (click)="changePage(-1)">Previous</button>
      <span>Page {{ page() + 1 }}</span>
      <button class="btn btn-secondary" [disabled]="busy() || !hasNext()" (click)="changePage(1)">Next</button>
      <article *ngIf="activeDeclaration() as d">
        <h3>{{ d.declarationNumber }} — {{ d.state }} (version {{ d.version }})</h3>
        <p>Maker: {{ d.createdBy }} · Approver: {{ d.approvedBy ?? 'Pending' }}</p>
        <ng-container *ngIf="d.snapshot as s; else missingSnapshot">
          <p>Product: {{ s.finishedProduct.reference }} — {{ s.finishedProduct.description }} · Unit: {{ s.finishedProduct.unitOfMeasure }}</p>
          <p>Tax code: {{ s.finishedProduct.taxCode }} · Batch size: {{ s.batchSize }}</p>
          <p>Effective: {{ d.effectiveFrom }} — {{ d.effectiveUntil ?? 'Open ended' }} · References checked: {{ s.referencesCheckedAt }}</p>
          <div class="vat-table-container"><table class="vat-table">
            <thead><tr><th>Material</th><th>Quantity / unit</th><th>Wastage %</th><th>Unit price</th><th>Total cost</th></tr></thead>
            <tbody><tr *ngFor="let m of s.materials"><td>{{ m.product.reference }} — {{ m.product.description }}</td><td>{{ m.quantity }} {{ m.product.unitOfMeasure }}</td><td>{{ m.wastagePercent }}</td><td>{{ m.unitPrice | bdtCurrency:false }}</td><td>{{ m.totalCost | bdtCurrency:false }}</td></tr></tbody>
          </table></div>
          <p>Labor: {{ s.directLaborCost | bdtCurrency }} · Utilities: {{ s.utilityCost | bdtCurrency }} · Overhead: {{ s.overheadCost | bdtCurrency }}</p>
          <p>Saved batch cost: {{ s.totalCost | bdtCurrency }} · Saved unit cost: {{ s.unitCost | bdtCurrency }}</p>
        </ng-container>
        <ng-template #missingSnapshot><p>Historical snapshot unavailable. Commands are disabled.</p></ng-template>
        <div *ngIf="!readOnly"><button *ngFor="let action of actions" class="btn btn-secondary" [disabled]="busy() || !canAction(d, action)" (click)="pendingAction.set(action); reason = ''">{{ action }}</button></div>
        <div *ngIf="pendingAction() as action">
          <p>Confirm {{ action }} for {{ d.declarationNumber }} at version {{ d.version }}.</p>
          <label *ngIf="action === 'cancel'">Reason <input [(ngModel)]="reason" [disabled]="busy()" /></label>
          <button class="btn btn-primary" [disabled]="busy() || !canAction(d, action) || (action === 'cancel' && !reason.trim())" (click)="confirmAction()">Confirm</button>
          <button class="btn btn-secondary" [disabled]="busy()" (click)="pendingAction.set(null)">Back</button>
        </div>
      </article>
    </section>
  `,
  styles: [`article { margin-top:24px; } button { margin:8px; } [role=alert] { color:#b42318; }` ]
})
export class CoefficientDeclarationComponent implements OnInit {
  @Input() readOnly = false;
  @Output() declarationSelected = new EventEmitter<CoefficientResult>();

  private api = inject(ManufacturingApiService);
  private csv = inject(CsvExportService);
  public context = inject(CompanyContextService);

  readonly declarations = signal<CoefficientResult[]>([]);
  readonly activeDeclaration = signal<CoefficientResult | null>(null);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly message = signal('');
  readonly reloadRequired = signal(false);
  readonly page = signal(0);
  readonly hasNext = signal(false);
  readonly pendingAction = signal<CoefficientAction | null>(null);
  readonly actions: CoefficientAction[] = ['submit', 'approve', 'cancel'];

  private cursors: (string | undefined)[] = [undefined];
  reason = '';

  ngOnInit(): void {
    this.loadDeclarations();
  }

  loadDeclarations(): void {
    if (this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    this.reloadRequired.set(true);
    this.declarations.set([]);
    this.activeDeclaration.set(null);
    this.pendingAction.set(null);
    this.hasNext.set(false);

    this.api.getCoefficients(this.cursors[this.page()], 25).subscribe({
      next: rows => {
        this.declarations.set(rows);
        this.activeDeclaration.set(rows[0] ?? null);
        this.hasNext.set(rows.length === 25);
        this.reloadRequired.set(false);
        this.busy.set(false);
      },
      error: () => {
        this.error.set('Unable to load declarations. Reload to retry.');
        this.busy.set(false);
      }
    });
  }

  changePage(direction: number): void {
    if (this.busy() || (direction > 0 && (!this.hasNext() || this.page() >= 400)) || (direction < 0 && this.page() === 0)) return;
    if (direction > 0) this.cursors[this.page() + 1] = this.declarations().at(-1)?.id;
    this.page.update(p => p + (direction > 0 ? 1 : -1));
    this.loadDeclarations();
  }

  setDeclaration(d: CoefficientResult): void {
    this.activeDeclaration.set(d);
    this.pendingAction.set(null);
    this.reason = '';
  }

  canAction(d: CoefficientResult, action: CoefficientAction): boolean {
    if (this.readOnly || this.reloadRequired() || !d.snapshot || !Number.isSafeInteger(d.version) || d.version < 1) return false;
    if (action === 'approve') return d.state === 'Submitted' && this.context.currentUser().id !== d.createdBy && this.context.hasPermission('manufacturing.approve');
    return this.context.hasPermission('manufacturing.request') && (action === 'submit' ? d.state === 'Draft' : ['Draft', 'Submitted'].includes(d.state));
  }

  confirmAction(): void {
    const d = this.activeDeclaration(), action = this.pendingAction();
    if (!d || !action || this.busy() || !this.canAction(d, action) || (action === 'cancel' && !this.reason.trim())) return;
    this.busy.set(true);
    this.error.set('');
    this.message.set('');
    this.api.transitionCoefficient(d.id, action, d.version, action === 'cancel' ? this.reason.trim() : undefined).subscribe({
      next: result => {
        this.message.set(`${result.declarationNumber}: ${result.state} (version ${result.version}).`);
        this.busy.set(false);
        this.loadDeclarations();
      },
      error: () => {
        this.error.set('Decision could not be confirmed. Reload before another command.');
        this.reloadRequired.set(true);
        this.busy.set(false);
        this.pendingAction.set(null);
      }
    });
  }

  exportPage(): void {
    if (this.busy() || this.reloadRequired()) return;
    const headers = ['Declaration No', 'Revision', 'Finished Product', 'Tax Code', 'UOM', 'Batch Size', 'Effective From', 'Status', 'Batch Cost', 'Unit Cost'];
    const rows = this.declarations().map(d => [d.declarationNumber, d.revision,
      d.snapshot?.finishedProduct.description ?? 'Unavailable', d.snapshot?.finishedProduct.taxCode ?? 'Unavailable',
      d.snapshot?.finishedProduct.unitOfMeasure ?? 'Unavailable', d.snapshot?.batchSize ?? 'Unavailable',
      d.effectiveFrom, d.state, d.snapshot?.totalCost ?? 'Unavailable', d.snapshot?.unitCost ?? 'Unavailable']);
    this.csv.exportToCsv(`Coefficient_Declarations_Page_${this.page() + 1}`, headers, rows);
  }
}
