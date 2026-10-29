import { Component, OnInit, Output, EventEmitter, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { InventoryApiService } from '../../../../core/services/api/inventory-api.service';
import { CompanyContextService } from '../../../../core/services/company-context.service';
import { MockDataService } from '../../../../core/services/mock-data.service';
import { ToastService } from '../../../../core/services/toast.service';
import { BdtCurrencyPipe } from '../../../../shared/pipes/bdt-currency.pipe';
import { BengaliNumberPipe } from '../../../../shared/pipes/bengali-number.pipe';
import { Warehouse, StockBalance } from '../../../../core/models/vat-models';

@Component({
  selector: 'app-transfer-entry',
  standalone: true,
  imports: [CommonModule, FormsModule, BdtCurrencyPipe, BengaliNumberPipe],
  template: `
    <div class="entry-card vat-card">
      <div class="entry-header">
        <div>
          <div class="entry-badge font-bengali">শাখা স্থানান্তর এন্ট্রি (মূসক-৬.৫)</div>
          <h2 class="entry-title">Issue New Mushak 6.5 Central Goods Transfer Challan</h2>
          <p class="entry-subtitle">
            Dispatch goods from Central Plant to regional depots/branches with real-time stock ledger deduction & vehicle tracking
          </p>
        </div>
        <button class="btn btn-secondary btn-sm" (click)="cancelled.emit()">
          Cancel & View 6.5
        </button>
      </div>

      <form (ngSubmit)="submitTransferForm()" class="entry-form">
        <div class="form-grid-3">
          <div class="form-group">
            <label class="vat-label">Challan Reference *</label>
            <input type="text" class="vat-input font-mono" [(ngModel)]="newTransferModel.transferNumber" name="trfNo" required />
          </div>
          <div class="form-group">
            <label class="vat-label">Dispatch Date *</label>
            <input type="date" class="vat-input" [(ngModel)]="newTransferModel.dispatchDate" name="trfDate" required />
          </div>
          <div class="form-group">
            <label class="vat-label">Dispatch Time *</label>
            <input type="time" class="vat-input" [(ngModel)]="newTransferModel.dispatchTime" name="trfTime" required />
          </div>
        </div>

        <div class="form-grid-2 mt-3">
          <div class="form-group">
            <label class="vat-label">
              Dispatching Warehouse (প্রেরণকারী গুদাম) *
              <span *ngIf="!context.isSuperAdmin()" class="badge-branch-locked">🔒 Assigned</span>
            </label>
            <select *ngIf="context.isSuperAdmin()" class="vat-select" [(ngModel)]="newTransferModel.fromWarehouseId" (change)="onSourceWarehouseChange()" name="fromWh">
              <option *ngFor="let w of warehouses" [value]="w.warehouseId">
                🏢 {{ w.name }} ({{ w.code }}) - {{ w.branchName }}
              </option>
            </select>
            <div *ngIf="!context.isSuperAdmin()" class="vat-input locked-branch-field">
              🏢 {{ getWarehouseName(newTransferModel.fromWarehouseId) }}
            </div>
          </div>
          <div class="form-group">
            <label class="vat-label">Destination Warehouse (গ্রহীতা গুদাম/ডিপো) *</label>
            <select class="vat-select" [(ngModel)]="newTransferModel.toWarehouseId" name="toWh" required>
              <option *ngFor="let w of availableDestinationWarehouses" [value]="w.warehouseId">
                🏢 {{ w.name }} ({{ w.code }}) - {{ w.branchName }}
              </option>
            </select>
          </div>
        </div>

        <div class="form-grid-2 mt-3">
          <div class="form-group">
            <label class="vat-label">Vehicle Reg. No (পরিবহন যান নং) *</label>
            <input type="text" class="vat-input" [(ngModel)]="newTransferModel.vehicleNumber" name="trfVehicle" placeholder="কাভার্ড ভ্যান (ঢাকা মেট্রো-উ-১৪-২২১১)" required />
          </div>
          <div class="form-group">
            <label class="vat-label">Driver Name & Mobile *</label>
            <input type="text" class="vat-input" [(ngModel)]="newTransferModel.driverName" name="trfDriver" placeholder="মোঃ আনোয়ার হোসেন (01711-223344)" required />
          </div>
        </div>

        <h3 class="form-section-title mt-4">স্থানান্তরিত পণ্য নির্বাচন (Select Products to Transfer)</h3>
        
        <div class="source-stock-notice" *ngIf="selectedProductAvailableStock !== null">
          <span>Current Available Stock at Source:</span>
          <strong class="font-mono text-emerald">{{ selectedProductAvailableStock | bengaliNumber }} {{ selectedProductUom }}</strong>
        </div>

        <div class="form-grid-3">
          <div class="form-group">
            <label class="vat-label">Product Item *</label>
            <select class="vat-select" [(ngModel)]="newTransferModel.productId" (change)="onProductSelect()" name="trfProd">
              <option *ngFor="let p of availableSourceProducts" [value]="p.productId">
                {{ p.productReference }} - {{ p.productDescription }} (Stock: {{ p.availableQuantity }} {{ p.unitOfMeasure }})
              </option>
            </select>
          </div>
          <div class="form-group">
            <label class="vat-label">Transfer Quantity *</label>
            <input 
              type="number" 
              class="vat-input font-mono" 
              [(ngModel)]="newTransferModel.quantity" 
              name="trfQty" 
              min="1" 
              [max]="selectedProductAvailableStock || 999999" 
              required 
            />
            <small *ngIf="newTransferModel.quantity > (selectedProductAvailableStock || 0)" class="text-danger">
              Warning: Quantity exceeds current available stock ({{ selectedProductAvailableStock }})
            </small>
          </div>
          <div class="form-group">
            <label class="vat-label">Unit Standard Price (BDT) *</label>
            <input type="number" class="vat-input font-mono" [(ngModel)]="newTransferModel.unitPrice" name="trfPrice" min="0" step="0.5" required />
          </div>
        </div>

        <div class="form-group mt-3">
          <label class="vat-label">Transfer Remarks / Statutory Purpose</label>
          <input type="text" class="vat-input" [(ngModel)]="newTransferModel.remarks" name="trfRemarks" placeholder="চট্টগ্রাম ডিপো বাফার স্টক স্থানান্তর (বাণিজ্যিক বিক্রয় নহে)" />
        </div>

        <!-- Total Calculation preview -->
        <div class="transfer-preview-card mt-3">
          <div class="preview-item">
            <span class="preview-label">Total Transfer Quantity:</span>
            <strong class="font-mono">{{ newTransferModel.quantity | bengaliNumber }} {{ selectedProductUom }}</strong>
          </div>
          <div class="preview-item">
            <span class="preview-label">Estimated Transfer Valuation:</span>
            <strong class="font-mono text-emerald font-bold">{{ (newTransferModel.quantity * newTransferModel.unitPrice) | bdtCurrency }}</strong>
          </div>
        </div>

        <div class="form-actions mt-4">
          <button type="submit" class="btn btn-primary" [disabled]="isSubmitting || newTransferModel.quantity <= 0">
            <svg *ngIf="!isSubmitting" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z"></path>
              <polyline points="17 21 17 13 7 13 7 21"></polyline>
              <polyline points="7 3 7 8 15 8"></polyline>
            </svg>
            <span *ngIf="isSubmitting" class="spinner-sm"></span>
            {{ isSubmitting ? 'Posting Transfer to Ledger...' : 'Issue & Generate Mushak 6.5 (চালান ইস্যু করুন)' }}
          </button>
          <button type="button" class="btn btn-secondary" (click)="cancelled.emit()">
            Cancel
          </button>
        </div>
      </form>
    </div>
  `,
  styles: [`
    .entry-card { padding: 24px; }
    .entry-header { display: flex; align-items: flex-start; justify-content: space-between; border-bottom: 1px solid var(--border-card); padding-bottom: 16px; margin-bottom: 20px; }
    .entry-badge { font-size: 0.8rem; font-weight: 700; color: var(--vat-emerald); margin-bottom: 4px; }
    .entry-title { font-size: 1.25rem; font-weight: 800; color: var(--text-main); }
    .entry-subtitle { font-size: 0.85rem; color: var(--text-muted); }
    .form-grid-3 { display: grid; grid-template-columns: repeat(3, 1fr); gap: 16px; }
    .form-grid-2 { display: grid; grid-template-columns: repeat(2, 1fr); gap: 16px; }
    .form-group { display: flex; flex-direction: column; gap: 6px; }
    .form-section-title { font-size: 0.95rem; font-weight: 700; color: var(--text-main); margin-bottom: 10px; }
    .source-stock-notice { background: rgba(16, 185, 129, 0.1); border: 1px solid rgba(16, 185, 129, 0.25); padding: 8px 14px; border-radius: var(--radius-sm); font-size: 0.82rem; margin-bottom: 12px; display: flex; gap: 8px; align-items: center; }
    .transfer-preview-card { display: flex; gap: 24px; background: var(--bg-card-subtle); border: 1px solid var(--border-card); padding: 12px 18px; border-radius: var(--radius-sm); font-size: 0.85rem; }
    .preview-item { display: flex; gap: 8px; align-items: center; }
    .preview-label { color: var(--text-muted); }
    .form-actions { display: flex; gap: 12px; align-items: center; }
    .spinner-sm { width: 14px; height: 14px; border: 2px solid rgba(255,255,255,0.3); border-top-color: #fff; border-radius: 50%; animation: spin 0.8s linear infinite; display: inline-block; }
    @keyframes spin { to { transform: rotate(360deg); } }
    .badge-branch-locked { font-size: 0.7rem; background: rgba(245, 158, 11, 0.15); color: #fbbf24; padding: 2px 6px; border-radius: 4px; margin-left: 6px; }
    .locked-branch-field { background: var(--bg-card-subtle); color: var(--text-muted); padding: 8px 12px; border-radius: var(--radius-sm); border: 1px solid var(--border-card); font-size: 0.85rem; }
    .text-danger { color: #f43f5e; font-size: 0.75rem; margin-top: 4px; }
    @media (max-width: 768px) {
      .form-grid-3, .form-grid-2 { grid-template-columns: 1fr; }
    }
  `]
})
export class TransferEntryComponent implements OnInit {
  @Output() transferCreated = new EventEmitter<string>();
  @Output() cancelled = new EventEmitter<void>();

  private inventoryApi = inject(InventoryApiService);
  private toast = inject(ToastService);
  public context = inject(CompanyContextService);
  public mockData = inject(MockDataService);

  public warehouses: Warehouse[] = [];
  public stockBalances: StockBalance[] = [];
  public isSubmitting = false;

  public newTransferModel = {
    transferNumber: 'TRF-6.5-2026-0002',
    dispatchDate: new Date().toISOString().slice(0, 10),
    dispatchTime: '16:00',
    fromWarehouseId: '55555555-5555-5555-5555-555555555551',
    toWarehouseId: '77777777-7777-7777-7777-777777777772',
    vehicleNumber: 'কাভার্ড ভ্যান (ঢাকা মেট্রো-উ-১৪-২২১১)',
    driverName: 'মোঃ আনোয়ার হোসেন (01711-223344)',
    remarks: 'চট্টগ্রাম পোর্ট ডিপো বাফার স্টক স্থানান্তর',
    productId: '66666666-6666-6666-6666-666666666661',
    quantity: 100,
    unitPrice: 165.0
  };

  ngOnInit(): void {
    this.loadWarehouses();
    this.loadStockBalances();
  }

  public loadWarehouses(): void {
    this.inventoryApi.getWarehouses().subscribe({
      next: (data) => {
        this.warehouses = data;
        if (data.length > 0 && !this.newTransferModel.fromWarehouseId) {
          this.newTransferModel.fromWarehouseId = data[0].warehouseId;
        }
        if (data.length > 1 && !this.newTransferModel.toWarehouseId) {
          this.newTransferModel.toWarehouseId = data[1].warehouseId;
        }
      },
      error: (err) => console.warn('Failed to load warehouses:', err)
    });
  }

  public loadStockBalances(): void {
    this.inventoryApi.getAllStockBalances('ALL').subscribe({
      next: (data) => {
        this.stockBalances = data;
        this.onProductSelect();
      },
      error: (err) => console.warn('Failed to load stock balances:', err)
    });
  }

  public onSourceWarehouseChange(): void {
    if (this.newTransferModel.toWarehouseId === this.newTransferModel.fromWarehouseId) {
      const alt = this.availableDestinationWarehouses[0];
      if (alt) this.newTransferModel.toWarehouseId = alt.warehouseId;
    }
    this.onProductSelect();
  }

  public onProductSelect(): void {
    const bal = this.stockBalances.find(b => 
      b.productId === this.newTransferModel.productId && 
      b.warehouseId === this.newTransferModel.fromWarehouseId
    );
    if (bal) {
      this.newTransferModel.unitPrice = bal.standardCost || 165.0;
    } else {
      const prod = this.mockData.products.find(p => p.id === this.newTransferModel.productId || p.code === 'PROD-01');
      if (prod) {
        this.newTransferModel.unitPrice = prod.standardCost || 165.0;
      }
    }
  }

  get availableDestinationWarehouses(): Warehouse[] {
    return this.warehouses.filter(w => w.warehouseId !== this.newTransferModel.fromWarehouseId);
  }

  get availableSourceProducts(): StockBalance[] {
    const list = this.stockBalances.filter(b => b.warehouseId === this.newTransferModel.fromWarehouseId);
    if (list.length > 0) return list;
    return this.stockBalances;
  }

  get selectedProductAvailableStock(): number | null {
    const bal = this.stockBalances.find(b => 
      b.productId === this.newTransferModel.productId && 
      b.warehouseId === this.newTransferModel.fromWarehouseId
    );
    return bal ? bal.availableQuantity : null;
  }

  get selectedProductUom(): string {
    const bal = this.stockBalances.find(b => b.productId === this.newTransferModel.productId);
    return bal ? bal.unitOfMeasure : 'KG';
  }

  public getWarehouseName(warehouseId: string): string {
    const w = this.warehouses.find(x => x.warehouseId === warehouseId);
    return w ? `${w.name} (${w.code})` : 'Central Warehouse (WH-01)';
  }

  public submitTransferForm(): void {
    if (this.newTransferModel.quantity <= 0) {
      this.toast.error('স্থানান্তর পরিমাণ ১ বা তার বেশি হতে হবে।', 'Validation Error');
      return;
    }

    this.isSubmitting = true;
    const payload = {
      transferNumber: this.newTransferModel.transferNumber,
      dispatchDate: this.newTransferModel.dispatchDate,
      dispatchTime: this.newTransferModel.dispatchTime,
      fromWarehouseId: this.newTransferModel.fromWarehouseId,
      toWarehouseId: this.newTransferModel.toWarehouseId,
      vehicleNumber: this.newTransferModel.vehicleNumber,
      driverName: this.newTransferModel.driverName,
      remarks: this.newTransferModel.remarks,
      lines: [
        {
          productId: this.newTransferModel.productId,
          quantity: this.newTransferModel.quantity,
          unitPrice: this.newTransferModel.unitPrice,
          remarks: this.newTransferModel.remarks
        }
      ]
    };

    this.inventoryApi.createTransfer(payload).subscribe({
      next: () => {
        this.isSubmitting = false;
        const msg = `মূসক ৬.৫ স্থানান্তর চালান '${this.newTransferModel.transferNumber}' সফলভাবে নিবন্ধিত ও স্টক সমন্বয় করা হয়েছে!`;
        this.toast.success(msg, 'স্থানান্তর চালান সম্পন্ন (Mushak 6.5)');
        this.transferCreated.emit(this.newTransferModel.transferNumber);
      },
      error: (err) => {
        this.isSubmitting = false;
        console.error('Failed to create transfer:', err);
        const errMsg = err?.error?.title || err?.error || err.message || 'Transfer failed';
        this.toast.error(`চালান তৈরি ব্যর্থ হয়েছে: ${errMsg}`, 'Transfer Failed');
      }
    });
  }
}
