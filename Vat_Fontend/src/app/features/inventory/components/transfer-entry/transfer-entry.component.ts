import { Component, OnInit, Output, EventEmitter, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { InventoryApiService } from '../../../../core/services/api/inventory-api.service';
import { CompanyContextService } from '../../../../core/services/company-context.service';
import { Warehouse, StockBalance } from '../../../../core/models/vat-models';
@Component({
  selector: 'app-transfer-entry', standalone: true, imports: [CommonModule, FormsModule],
  template: `
    <form class="vat-card" (ngSubmit)="submitTransferForm()" #form="ngForm">
      <h2>Create stock transfer draft</h2>
      <p>Creating a draft does not move stock. Submit, approval and posting are separate actions in the transfer register.</p>
      <p role="alert" *ngIf="error()">{{ error() }}</p>
      <label>Reference (optional; leave blank for server numbering)<input class="vat-input" name="reference" [(ngModel)]="draft.transferNumber" maxlength="100"></label>
      <label>Business date<input class="vat-input" type="date" name="date" [(ngModel)]="draft.dispatchDate" required></label>
      <label>Dispatch time (optional)<input class="vat-input" type="time" name="time" [(ngModel)]="draft.dispatchTime"></label>
      <label>Source warehouse<select class="vat-select" name="source" [(ngModel)]="draft.fromWarehouseId" (ngModelChange)="onSourceWarehouseChange()" required><option value="">Select source</option><option *ngFor="let w of warehouses()" [value]="w.warehouseId">{{ w.code }} ? {{ w.name }}</option></select></label>
      <label>Destination warehouse<select class="vat-select" name="destination" [(ngModel)]="draft.toWarehouseId" required><option value="">Select destination</option><option *ngFor="let w of warehouses()" [value]="w.warehouseId" [disabled]="w.warehouseId === draft.fromWarehouseId">{{ w.code }} ? {{ w.name }}</option></select></label>
      <button type="button" class="btn btn-secondary" [disabled]="warehousesLoading()" *ngIf="moreWarehouses() || warehouseError()" (click)="loadWarehouses()">{{ warehouseError() ? 'Retry warehouses' : 'Load more warehouses' }}</button>
      <p role="alert" *ngIf="warehouseError()">{{ warehouseError() }}</p>
      <label>Product from source warehouse<select class="vat-select" name="product" [(ngModel)]="draft.productId" required><option value="">Select product</option><option *ngFor="let b of balances()" [value]="b.productId">{{ b.productReference }} ? {{ b.productDescription }} (available {{ b.availableQuantity }} {{ b.unitOfMeasure }})</option></select></label>
      <button type="button" class="btn btn-secondary" *ngIf="moreBalances() || stockError()" [disabled]="stockLoading() || !draft.fromWarehouseId" (click)="loadStockBalances()">{{ stockError() ? 'Retry stock' : 'Load more source products' }}</button>
      <p role="alert" *ngIf="stockError()">{{ stockError() }}</p>
      <label>Quantity<input class="vat-input" type="number" name="quantity" [(ngModel)]="draft.quantity" min="0.000001" step="0.000001" required></label>
      <label>Declared unit price (not inventory cost)<input class="vat-input" type="number" name="price" [(ngModel)]="draft.unitPrice" min="0" step="0.000001" required></label>
      <label>Vehicle (optional)<input class="vat-input" name="vehicle" [(ngModel)]="draft.vehicleNumber" maxlength="100"></label>
      <label>Driver (optional)<input class="vat-input" name="driver" [(ngModel)]="draft.driverName" maxlength="200"></label>
      <label>Remarks (optional)<textarea class="vat-input" name="remarks" [(ngModel)]="draft.remarks" maxlength="1000"></textarea></label>
      <button type="submit" class="btn btn-primary" [disabled]="busy() || saved() || form.invalid || !context.hasPermission('inventory.manage')">Save draft</button>
      <button type="button" class="btn btn-secondary" [disabled]="busy()" (click)="cancelled.emit()">Back to register</button>
    </form>
  `,
  styles: [`label { display:block; margin:12px 0; max-width:650px; } button { margin:12px 8px 0 0; } [role=alert] { color:#dc2626; }`]
})
export class TransferEntryComponent implements OnInit {
  @Output() transferCreated = new EventEmitter<string>();
  @Output() cancelled = new EventEmitter<void>();
  private api = inject(InventoryApiService);
  readonly context = inject(CompanyContextService);
  readonly warehouses = signal<Warehouse[]>([]);
  readonly balances = signal<StockBalance[]>([]);
  readonly busy = signal(false);
  readonly saved = signal(false);
  readonly error = signal('');
  readonly warehouseError = signal('');
  readonly stockError = signal('');
  readonly warehousesLoading = signal(false);
  readonly stockLoading = signal(false);
  readonly moreWarehouses = signal(false);
  readonly moreBalances = signal(false);
  private warehouseSkip = 0;
  private stockSkip = 0;
  private stockRequest = 0;
  private lastCommand?: { body: string; key: string };
  draft = { transferNumber: '', dispatchDate: new Date().toISOString().slice(0, 10), dispatchTime: '',
    fromWarehouseId: '', toWarehouseId: '', vehicleNumber: '', driverName: '', remarks: '', productId: '', quantity: 1, unitPrice: 0 };
  ngOnInit(): void { this.loadWarehouses(); }
  loadWarehouses(): void {
    if (this.warehousesLoading()) return;
    this.warehousesLoading.set(true); this.warehouseError.set('');
    this.api.getWarehouses(this.warehouseSkip, 100).subscribe({ next: items => {
      this.warehouses.update(w => [...w, ...items]); this.warehouseSkip += items.length;
      this.moreWarehouses.set(items.length === 100 && this.warehouseSkip <= 10000); this.warehousesLoading.set(false);
    }, error: e => { this.warehouseError.set(e?.message || 'Unable to load warehouses.'); this.warehousesLoading.set(false); } });
  }
  onSourceWarehouseChange(): void {
    this.stockRequest++; this.stockLoading.set(false); this.stockSkip = 0; this.balances.set([]); this.moreBalances.set(false);
    this.draft.productId = ''; this.draft.unitPrice = 0;
    if (this.draft.toWarehouseId === this.draft.fromWarehouseId) this.draft.toWarehouseId = '';
    if (this.draft.fromWarehouseId) this.loadStockBalances();
  }
  loadStockBalances(): void {
    if (this.stockLoading() || !this.draft.fromWarehouseId) return;
    const request = ++this.stockRequest;
    this.stockLoading.set(true); this.stockError.set('');
    this.api.getAllStockBalances(this.draft.fromWarehouseId, this.stockSkip, 100).subscribe({ next: items => {
      if (request !== this.stockRequest) return;
      this.balances.update(current => [...current, ...items.filter(b => b.warehouseId === this.draft.fromWarehouseId)]);
      this.stockSkip += items.length; this.moreBalances.set(items.length === 100 && this.stockSkip <= 10000); this.stockLoading.set(false);
    }, error: e => { if (request !== this.stockRequest) return; this.stockError.set(e?.message || 'Unable to load source stock.'); this.stockLoading.set(false); } });
  }
  submitTransferForm(): void {
    if (this.busy() || this.saved() || !this.context.hasPermission('inventory.manage')) return;
    const d = this.draft;
    const source = this.warehouses().some(w => w.warehouseId === d.fromWarehouseId);
    const destination = this.warehouses().some(w => w.warehouseId === d.toWarehouseId);
    const balance = this.balances().find(b => b.productId === d.productId && b.warehouseId === d.fromWarehouseId);
    if (!source || !destination || d.fromWarehouseId === d.toWarehouseId || !balance || !d.dispatchDate || !Number.isFinite(d.quantity) || d.quantity <= 0 || !Number.isFinite(d.unitPrice) || d.unitPrice < 0) {
      this.error.set('Select two different warehouses, a source product, a date, positive quantity and nonnegative price.'); return;
    }
    if (d.quantity > balance.availableQuantity) { this.error.set('Quantity exceeds the displayed source availability. Reload source stock before retrying.'); return; }
    this.busy.set(true); this.error.set('');
    const payload = { transferNumber: d.transferNumber, dispatchDate: d.dispatchDate, dispatchTime: d.dispatchTime,
      fromWarehouseId: d.fromWarehouseId, toWarehouseId: d.toWarehouseId, vehicleNumber: d.vehicleNumber, driverName: d.driverName, remarks: d.remarks,
      lines: [{ productId: d.productId, quantity: d.quantity, unitPrice: d.unitPrice, remarks: d.remarks }] };
    const body = JSON.stringify(payload);
    if (this.lastCommand?.body !== body) this.lastCommand = { body, key: crypto.randomUUID() };
    this.api.createTransfer(payload, this.lastCommand!.key).subscribe({
      next: result => { this.busy.set(false); this.saved.set(true); this.transferCreated.emit(result.reference); },
      error: e => { this.busy.set(false); this.error.set(e?.message || 'Draft creation failed.'); }
    });
  }
}
