import { Component, OnInit, Output, EventEmitter, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { InventoryApiService, TransferAction } from '../../../../core/services/api/inventory-api.service';
import { CompanyContextService } from '../../../../core/services/company-context.service';
import { Mushak65Transfer } from '../../../../core/models/vat-models';
@Component({
  selector: 'app-mushak-6-5', standalone: true, imports: [CommonModule, FormsModule],
  template: `
    <section class="vat-card">
      <h2>Stock transfer register</h2>
      <p>Draft ? Submitted ? Approved ? Posted. Stock moves only after posting. This operational review is not a certified statutory challan.</p>
      <button class="btn btn-primary" *ngIf="context.hasPermission('inventory.manage')" [disabled]="busy()" (click)="requestNewTransfer.emit()">New draft</button>
      <button class="btn btn-secondary" [disabled]="busy()" (click)="loadTransfers()">Reload</button>
      <p role="alert" *ngIf="error()">{{ error() }}</p>
      <p role="status" *ngIf="message()">{{ message() }}</p>
      <p *ngIf="busy()">Loading?</p>
      <div class="vat-table-container"><table class="vat-table">
        <thead><tr><th>Reference</th><th>Date</th><th>From ? To</th><th>State / version</th><th>Review</th></tr></thead>
        <tbody><tr *ngFor="let t of transfers()"><td>{{ t.transferNumber }}</td><td>{{ t.dispatchDate }}</td><td>{{ t.fromWarehouseName }} ? {{ t.toWarehouseName }}</td><td>{{ t.status }} / {{ t.version }}</td><td><button class="btn btn-secondary" [disabled]="busy()" (click)="select(t)">Review</button></td></tr>
          <tr *ngIf="!busy() && transfers().length === 0"><td>No transfers on this page.</td></tr>
        </tbody>
      </table></div>
      <button class="btn btn-secondary" [disabled]="busy() || page() === 0" (click)="changePage(-1)">Previous</button>
      <span>Page {{ page() + 1 }}</span>
      <button class="btn btn-secondary" [disabled]="busy() || !hasNext() || page() >= 400" (click)="changePage(1)">Next</button>
      <div *ngIf="activeTransfer() as t">
        <h3>{{ t.transferNumber }} ? {{ t.status }}</h3>
        <p>Maker: {{ t.createdBy }} ? Approver: {{ t.approvedBy || 'Pending' }}</p>
        <p>Vehicle: {{ t.vehicleNumber || 'Not supplied' }} ? Driver: {{ t.driverName || 'Not supplied' }} ? Dispatch time: {{ t.dispatchTime || 'Not supplied' }}</p>
        <p>{{ t.remarks }}</p>
        <table class="vat-table"><thead><tr><th>Product</th><th>Quantity</th><th>Declared unit price</th><th>Declared value</th></tr></thead>
          <tbody><tr *ngFor="let line of t.lines"><td>{{ line.productReference }} ? {{ line.productName }}</td><td>{{ line.quantity }} {{ line.uom }}</td><td>{{ line.unitPrice | number:'1.2-6' }}</td><td>{{ line.totalValue | number:'1.2-6' }}</td></tr></tbody>
        </table>
        <p>Declared transfer value: {{ total(t) | number:'1.2-6' }} BDT. This is not an inventory costing calculation.</p>
        <div *ngIf="!reloadRequired()">
          <button class="btn btn-secondary" *ngFor="let action of actions" [hidden]="!canAction(t, action)" [disabled]="busy()" (click)="pendingAction.set(action)">{{ action }}</button>
          <div *ngIf="pendingAction() as action">
            <p>Confirm {{ action }} for {{ t.transferNumber }} at version {{ t.version }}{{ action === 'post' ? '. This moves stock between the selected warehouses.' : '.' }}</p>
            <label *ngIf="action === 'cancel'">Cancellation reason<textarea class="vat-input" [(ngModel)]="reason" maxlength="1000"></textarea></label>
            <button class="btn btn-primary" [disabled]="busy() || (action === 'cancel' && !reason.trim())" (click)="confirmAction()">Confirm {{ action }}</button>
            <button class="btn btn-secondary" [disabled]="busy()" (click)="pendingAction.set(null)">Back</button>
          </div>
        </div>
      </div>
    </section>
  `,
  styles: [`button { margin:10px 8px 10px 0; } [role=alert] { color:#dc2626; }`]
})
export class Mushak65TransferComponent implements OnInit {
  @Output() requestNewTransfer = new EventEmitter<void>();
  private api = inject(InventoryApiService);
  readonly context = inject(CompanyContextService);
  readonly transfers = signal<Mushak65Transfer[]>([]);
  readonly activeTransfer = signal<Mushak65Transfer | null>(null);
  readonly busy = signal(false);
  readonly reloadRequired = signal(false);
  readonly error = signal('');
  readonly message = signal('');
  readonly page = signal(0);
  readonly hasNext = signal(false);
  readonly pendingAction = signal<TransferAction | null>(null);
  readonly actions: TransferAction[] = ['submit', 'approve', 'post', 'cancel'];
  reason = '';
  ngOnInit(): void { this.loadTransfers(); }
  loadTransfers(): void {
    if (this.busy()) return;
    this.busy.set(true); this.error.set(''); this.reloadRequired.set(true); this.activeTransfer.set(null); this.pendingAction.set(null); this.transfers.set([]); this.hasNext.set(false);
    this.api.getTransfers(this.page() * 25, 25).subscribe({ next: rows => { this.transfers.set(rows); this.hasNext.set(rows.length === 25); this.busy.set(false); this.reloadRequired.set(false); },
      error: e => { this.error.set(e?.message || 'Unable to load transfers.'); this.busy.set(false); } });
  }
  changePage(direction: number): void {
    if (this.busy() || (direction < 0 && this.page() === 0) || (direction > 0 && (!this.hasNext() || this.page() >= 400))) return;
    this.page.update(p => p + direction); this.loadTransfers();
  }
  select(t: Mushak65Transfer): void { this.activeTransfer.set(t); this.pendingAction.set(null); this.reason = ''; }
  total(t: Mushak65Transfer): number { return t.lines.reduce((sum, line) => sum + line.totalValue, 0); }
  canAction(t: Mushak65Transfer, action: TransferAction): boolean {
    if (!t.id || !Number.isSafeInteger(t.version) || t.version! < 1 || this.reloadRequired()) return false;
    if (action === 'approve') return t.status === 'Submitted' && this.context.currentUser().id !== t.createdBy && this.context.hasPermission('inventory.approve');
    if (action === 'post') return t.status === 'Approved' && !!t.approvedBy && this.context.hasPermission('inventory.post');
    return this.context.hasPermission('inventory.manage') && (action === 'submit' ? t.status === 'Draft' : ['Draft', 'Submitted', 'Approved'].includes(t.status || ''));
  }
  confirmAction(): void {
    const t = this.activeTransfer(), action = this.pendingAction();
    if (!t || !action || this.busy() || !this.canAction(t, action) || (action === 'cancel' && !this.reason.trim())) return;
    this.busy.set(true); this.error.set(''); this.message.set('');
    this.api.transitionTransfer(t.id!, action, t.version!, action === 'cancel' ? this.reason.trim() : undefined).subscribe({
      next: result => { this.message.set(`${result.reference}: ${result.state} (version ${result.version}).`); this.busy.set(false); this.loadTransfers(); },
      error: e => { this.busy.set(false); this.reloadRequired.set(true); this.error.set(`${e?.message || 'Action failed.'} Reload the register before another action.`); }
    });
  }
}
