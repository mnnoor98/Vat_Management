import { Component, OnInit, Input, Output, EventEmitter, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ManufacturingApiService, CoefficientResult, CoefficientAction } from '../../../../core/services/api/manufacturing-api.service';
import { CompanyContextService } from '../../../../core/services/company-context.service';
import { CsvExportService } from '../../../../core/services/csv-export.service';
@Component({
  selector: 'app-coefficient-declaration', standalone: true, imports: [CommonModule, FormsModule],
  template: `
    <section class="vat-card">
      <h2>Manufacturing coefficient register</h2>
      <p>Company-level drafts and approvals. Values below come from saved server snapshots. This review does not certify or file Mushak 4.3.</p>
      <button class="btn btn-secondary" [disabled]="busy()" (click)="loadDeclarations()">Reload</button>
      <button class="btn btn-secondary" [disabled]="busy()" (click)="exportPage()">Export current page</button>
      <p role="alert" *ngIf="error()">{{ error() }}</p>
      <p role="status" *ngIf="message()">{{ message() }}</p>
      <p *ngIf="busy()">Loading?</p>
      <div class="vat-table-container"><table class="vat-table">
        <thead><tr><th>Reference / revision</th><th>Finished product</th><th>Effective dates</th><th>State / version</th><th>Unit cost</th><th>Review</th></tr></thead>
        <tbody><tr *ngFor="let d of declarations()"><td>{{ d.declarationNumber }} / {{ d.revision }}</td><td>{{ d.snapshot?.finishedProduct?.description || 'Snapshot unavailable' }}</td><td>{{ d.effectiveFrom }} ? {{ d.effectiveUntil || 'Open-ended' }}</td><td>{{ d.state }} / {{ d.version }}</td><td>{{ d.snapshot ? (d.snapshot.unitCost | number:'1.2-6') : 'Unavailable' }}</td><td><button class="btn btn-secondary" [disabled]="busy()" (click)="setDeclaration(d); declarationSelected.emit(d)">Review</button></td></tr>
          <tr *ngIf="!busy() && declarations().length === 0"><td>No declarations on this page.</td></tr>
        </tbody>
      </table></div>
      <button class="btn btn-secondary" [disabled]="busy() || page() === 0" (click)="changePage(-1)">Previous</button>
      <span>Page {{ page() + 1 }} ? Export applies to this page.</span>
      <button class="btn btn-secondary" [disabled]="busy() || !hasNext()" (click)="changePage(1)">Next</button>
      <div *ngIf="activeDeclaration() as d">
        <h3>{{ d.declarationNumber }} ? {{ d.state }} (version {{ d.version }})</h3>
        <p>Maker: {{ d.createdBy }} ? Approver: {{ d.approvedBy || 'Pending' }}</p>
        <ng-container *ngIf="d.snapshot as s; else missing">
          <p>Product: {{ s.finishedProduct.reference }} ? {{ s.finishedProduct.description }} ? Batch: {{ s.batchSize }} {{ s.finishedProduct.unitOfMeasure }}</p>
          <p>References checked: {{ s.referencesCheckedAt }}</p>
          <div class="vat-table-container"><table class="vat-table"><thead><tr><th>Material</th><th>Unit</th><th>Quantity</th><th>Wastage %</th><th>Declared unit price</th><th>Material cost</th></tr></thead>
            <tbody><tr *ngFor="let m of s.materials"><td>{{ m.product.reference }} ? {{ m.product.description }}</td><td>{{ m.product.unitOfMeasure }}</td><td>{{ m.quantity }}</td><td>{{ m.wastagePercent }}</td><td>{{ m.unitPrice | number:'1.2-6' }}</td><td>{{ m.totalCost | number:'1.2-6' }}</td></tr></tbody>
          </table></div>
          <p>Labor: {{ s.directLaborCost | number:'1.2-6' }} ? Utilities: {{ s.utilityCost | number:'1.2-6' }} ? Overhead: {{ s.overheadCost | number:'1.2-6' }}</p>
          <p>Total batch cost: {{ s.totalCost | number:'1.2-6' }} ? Unit cost: {{ s.unitCost | number:'1.2-6' }} BDT. Unit cost is not a selling price.</p>
        </ng-container>
        <ng-template #missing><p role="alert">Snapshot unavailable. Reload or investigate this record before continuing.</p></ng-template>
        <div *ngIf="!readOnly && !reloadRequired()">
          <button class="btn btn-secondary" *ngFor="let action of actions" [hidden]="!canAction(d, action)" [disabled]="busy()" (click)="pendingAction.set(action)">{{ action }}</button>
          <div *ngIf="pendingAction() as action">
            <p>Confirm {{ action }} for {{ d.declarationNumber }} at version {{ d.version }}.</p>
            <label *ngIf="action === 'cancel'">Reason<textarea class="vat-input" [(ngModel)]="reason" maxlength="1000"></textarea></label>
            <button class="btn btn-primary" [disabled]="busy() || (action === 'cancel' && !reason.trim())" (click)="confirmAction()">Confirm {{ action }}</button>
            <button class="btn btn-secondary" [disabled]="busy()" (click)="pendingAction.set(null)">Back</button>
          </div>
        </div>
      </div>
    </section>
  `,
  styles: [`button { margin:10px 8px 10px 0; } [role=alert] { color:#dc2626; }`]
})
export class CoefficientDeclarationComponent implements OnInit {
  @Input() readOnly = false;
  @Output() declarationSelected = new EventEmitter<CoefficientResult>();
  private api = inject(ManufacturingApiService);
  private csv = inject(CsvExportService);
  readonly context = inject(CompanyContextService);
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
  ngOnInit(): void { this.loadDeclarations(); }
  loadDeclarations(): void {
    if (this.busy()) return;
    this.busy.set(true); this.error.set(''); this.reloadRequired.set(true); this.declarations.set([]); this.activeDeclaration.set(null); this.pendingAction.set(null); this.hasNext.set(false);
    this.api.getCoefficients(this.cursors[this.page()], 25).subscribe({ next: rows => { this.declarations.set(rows); this.hasNext.set(rows.length === 25); this.reloadRequired.set(false); this.busy.set(false); },
      error: e => { this.error.set(e?.message || 'Unable to load declarations.'); this.busy.set(false); } });
  }
  changePage(direction: number): void {
    if (this.busy() || (direction < 0 && this.page() === 0) || (direction > 0 && !this.hasNext())) return;
    if (direction > 0) this.cursors[this.page() + 1] = this.declarations().at(-1)?.id;
    this.page.update(p => p + direction); this.loadDeclarations();
  }
  setDeclaration(d: CoefficientResult): void { this.activeDeclaration.set(d); this.pendingAction.set(null); this.reason = ''; }
  canAction(d: CoefficientResult, action: CoefficientAction): boolean {
    if (this.readOnly || this.reloadRequired() || !d.snapshot || !Number.isSafeInteger(d.version) || d.version < 1) return false;
    if (action === 'approve') return d.state === 'Submitted' && this.context.currentUser().id !== d.createdBy && this.context.hasPermission('manufacturing.approve');
    return this.context.hasPermission('manufacturing.request') && (action === 'submit' ? d.state === 'Draft' : ['Draft', 'Submitted'].includes(d.state));
  }
  confirmAction(): void {
    const d = this.activeDeclaration(), action = this.pendingAction();
    if (!d || !action || this.busy() || !this.canAction(d, action) || (action === 'cancel' && !this.reason.trim())) return;
    this.busy.set(true); this.error.set(''); this.message.set('');
    this.api.transitionCoefficient(d.id, action, d.version, action === 'cancel' ? this.reason.trim() : undefined).subscribe({
      next: result => { this.message.set(`${result.declarationNumber}: ${result.state} (version ${result.version}).`); this.busy.set(false); this.loadDeclarations(); },
      error: e => { this.busy.set(false); this.reloadRequired.set(true); this.error.set(`${e?.message || 'Action failed.'} Reload before another action.`); }
    });
  }
  exportPage(): void {
    this.csv.exportToCsv(`Manufacturing_Page_${this.page() + 1}`, ['Reference', 'Revision', 'State', 'Version', 'Effective from', 'Effective until', 'Product reference', 'Batch size', 'Total batch cost', 'Unit cost'],
      this.declarations().map(d => [d.declarationNumber, d.revision, d.state, d.version, d.effectiveFrom, d.effectiveUntil || '', d.snapshot?.finishedProduct.reference || 'Unavailable', d.snapshot?.batchSize ?? 'Unavailable', d.snapshot?.totalCost ?? 'Unavailable', d.snapshot?.unitCost ?? 'Unavailable']));
  }
}
