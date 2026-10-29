import { Component, OnInit, Output, EventEmitter, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ManufacturingApiService, CreateCoefficientPayload } from '../../../../core/services/api/manufacturing-api.service';
import { MasterDataApiService } from '../../../../core/services/api/master-data-api.service';
import { CompanyContextService } from '../../../../core/services/company-context.service';
import { MockDataService } from '../../../../core/services/mock-data.service';
import { ToastService } from '../../../../core/services/toast.service';
import { BdtCurrencyPipe } from '../../../../shared/pipes/bdt-currency.pipe';
import { ProductItem } from '../../../../core/models/vat-models';

export interface RawMaterialRow {
  productId: string;
  productName: string;
  hsCode: string;
  uom: string;
  qtyRequired: number;
  wastagePercent: number;
  unitPrice: number;
}

@Component({
  selector: 'app-new-declaration',
  standalone: true,
  imports: [CommonModule, FormsModule, BdtCurrencyPipe],
  template: `
    <div class="entry-card vat-card">
      <div class="entry-header">
        <div>
          <span class="entry-badge font-bengali">নতুন মূসক-৪.৩ এন্ট্রি</span>
          <h2 class="entry-title">Declare New Mushak 4.3 Input-Output Coefficient</h2>
          <p class="entry-subtitle">
            File Bill of Materials (BOM) with raw materials wastage percentage, value addition, and statutory profit margins
          </p>
        </div>
        <button class="btn btn-secondary btn-sm" (click)="cancelled.emit()">
          Cancel & View 4.3
        </button>
      </div>

      <form (ngSubmit)="submitNewDeclaration()" class="entry-form">
        <!-- Section A: Finished Good Details -->
        <h3 class="form-section-title">১. উৎপাদিত চূড়ান্ত পণ্যের বিবরণ (Finished Product Declaration)</h3>
        <div class="form-grid-3">
          <div class="form-group">
            <label class="vat-label">Declaration Ref (ঘোষণা নং) *</label>
            <input type="text" class="vat-input font-mono" [(ngModel)]="newDecModel.declarationNumber" name="decNo" required />
          </div>
          <div class="form-group">
            <label class="vat-label">Finished Good Product (চূড়ান্ত পণ্য) *</label>
            <select class="vat-select" [(ngModel)]="newDecModel.finishedProductId" (change)="onFinishedProductSelect()" name="finProd">
              <option *ngFor="let p of finishedProductsList" [value]="p.id">
                {{ p.name }} ({{ p.code }})
              </option>
            </select>
          </div>
          <div class="form-group">
            <label class="vat-label">NBR HS Code (এইচএস কোড) *</label>
            <input type="text" class="vat-input font-mono" [(ngModel)]="newDecModel.hsCode" name="finHs" required />
          </div>
        </div>

        <div class="form-grid-4 mt-3">
          <div class="form-group">
            <label class="vat-label">Batch Size (ব্যাচ সাইজ) *</label>
            <input type="number" class="vat-input font-mono" [(ngModel)]="newDecModel.batchSize" (input)="recalculatePrice()" name="batchSize" min="1" required />
          </div>
          <div class="form-group">
            <label class="vat-label">Unit of Measure (একক) *</label>
            <input type="text" class="vat-input font-mono" [(ngModel)]="newDecModel.uom" name="uom" required />
          </div>
          <div class="form-group">
            <label class="vat-label">Effective From Date *</label>
            <input type="date" class="vat-input" [(ngModel)]="newDecModel.effectiveFrom" name="effDate" required />
          </div>
          <div class="form-group">
            <label class="vat-label">
              Manufacturing Plant *
              <span *ngIf="!context.isSuperAdmin()" class="badge-branch-locked">🔒 Assigned</span>
            </label>
            <select *ngIf="context.isSuperAdmin()" class="vat-select" [(ngModel)]="selectedBranchId" name="branchId">
              <option *ngFor="let b of mockData.branches" [value]="b.id">
                🏢 {{ b.name }} ({{ b.code }})
              </option>
            </select>
            <div *ngIf="!context.isSuperAdmin()" class="vat-input locked-branch-field">
              🏢 {{ context.activeBranch().name }} ({{ context.activeBranch().code }})
            </div>
          </div>
        </div>

        <!-- Section B: Raw Materials & Wastage Repeater -->
        <div class="materials-section-header mt-4">
          <h3 class="form-section-title">২. কাঁচামাল ও অপচয় অনুপাত (Raw Materials & Wastage Breakdown)</h3>
          <button type="button" class="btn btn-secondary btn-sm" (click)="addMaterialRow()">
            + Add Raw Material Line
          </button>
        </div>

        <div class="vat-table-container mt-2">
          <table class="vat-table">
            <thead>
              <tr>
                <th style="width: 25%;">Raw Material Item</th>
                <th>HS Code</th>
                <th>UOM</th>
                <th style="width: 14%;">Qty / Batch</th>
                <th style="width: 12%;">Wastage %</th>
                <th style="width: 14%;">Unit Cost (BDT)</th>
                <th class="text-right">Total Line Cost</th>
                <th style="width: 45px;"></th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let row of materialRows; let idx = index">
                <td>
                  <select class="vat-select" [(ngModel)]="row.productId" (change)="onMaterialSelect(row)" [name]="'mat_id_' + idx">
                    <option *ngFor="let r of rawProductsList" [value]="r.id">
                      {{ r.name }} ({{ r.code }})
                    </option>
                  </select>
                </td>
                <td><input type="text" class="vat-input font-mono select-compact" [(ngModel)]="row.hsCode" [name]="'mat_hs_' + idx" /></td>
                <td><input type="text" class="vat-input select-compact" [(ngModel)]="row.uom" [name]="'mat_uom_' + idx" /></td>
                <td>
                  <input type="number" class="vat-input font-mono select-compact" [(ngModel)]="row.qtyRequired" (input)="recalculatePrice()" [name]="'mat_qty_' + idx" min="0.1" step="0.1" />
                </td>
                <td>
                  <input type="number" class="vat-input font-mono select-compact text-amber" [(ngModel)]="row.wastagePercent" (input)="recalculatePrice()" [name]="'mat_w_' + idx" min="0" max="100" step="0.5" />
                </td>
                <td>
                  <input type="number" class="vat-input font-mono select-compact" [(ngModel)]="row.unitPrice" (input)="recalculatePrice()" [name]="'mat_price_' + idx" min="0" step="0.5" />
                </td>
                <td class="text-right numeric font-bold font-mono">
                  {{ getRowTotal(row) | bdtCurrency:false }}
                </td>
                <td class="text-center">
                  <button type="button" class="btn-icon-danger" (click)="removeMaterialRow(idx)" [disabled]="materialRows.length <= 1" title="Remove Line">&times;</button>
                </td>
              </tr>
            </tbody>
            <tfoot>
              <tr class="total-row">
                <th colspan="6" class="text-right font-bengali">সর্বমোট কাঁচামাল ব্যয় (Total Raw Material Cost):</th>
                <th class="text-right numeric text-emerald font-bold">{{ computedTotalRawMaterialCost | bdtCurrency:false }}</th>
                <th></th>
              </tr>
            </tfoot>
          </table>
        </div>

        <!-- Section C: Value Addition & Profit Margin -->
        <h3 class="form-section-title mt-4">৩. মূল্য সংযোজন ও লাভ মার্জিন (Value Addition Structure)</h3>
        <div class="form-grid-4">
          <div class="form-group">
            <label class="vat-label">Direct Labor Cost (প্রত্যক্ষ শ্রম) *</label>
            <input type="number" class="vat-input font-mono" [(ngModel)]="newDecModel.directLaborCost" (input)="recalculatePrice()" name="laborCost" min="0" step="100" />
          </div>
          <div class="form-group">
            <label class="vat-label">Utility Cost (বিদ্যুৎ/জ্বালানি) *</label>
            <input type="number" class="vat-input font-mono" [(ngModel)]="newDecModel.utilityCost" (input)="recalculatePrice()" name="utilCost" min="0" step="100" />
          </div>
          <div class="form-group">
            <label class="vat-label">Factory Overheads (উপরিব্যয়) *</label>
            <input type="number" class="vat-input font-mono" [(ngModel)]="newDecModel.overheadCost" (input)="recalculatePrice()" name="overCost" min="0" step="100" />
          </div>
          <div class="form-group">
            <label class="vat-label">Producer Profit Margin % (মুনাফা) *</label>
            <input type="number" class="vat-input font-mono text-emerald" [(ngModel)]="newDecModel.marginPercent" (input)="recalculatePrice()" name="margin" min="0" max="100" step="0.5" />
          </div>
        </div>

        <!-- Section D: Live Calculated Simulation Card -->
        <div class="cost-simulation-card mt-3">
          <div class="sim-item">
            <span class="sim-label">Total Batch Manufacturing Cost:</span>
            <strong class="font-mono">{{ computedTotalBatchCost | bdtCurrency }}</strong>
          </div>
          <div class="sim-item">
            <span class="sim-label">Estimated Batch Profit ({{ newDecModel.marginPercent }}%):</span>
            <strong class="font-mono text-emerald">{{ (computedTotalBatchCost * (newDecModel.marginPercent || 15) / 100) | bdtCurrency }}</strong>
          </div>
          <div class="sim-item">
            <span class="sim-label font-bold text-emerald">Declared Ex-Factory Selling Price / Unit:</span>
            <strong class="font-mono text-xl text-emerald">{{ newDecModel.finalSellingPrice | bdtCurrency }} / {{ newDecModel.uom }}</strong>
          </div>
        </div>

        <div class="form-actions mt-4">
          <button type="submit" class="btn btn-primary" [disabled]="isSubmitting || materialRows.length === 0">
            <svg *ngIf="!isSubmitting" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z"></path>
              <polyline points="17 21 17 13 7 13 7 21"></polyline>
              <polyline points="7 3 7 8 15 8"></polyline>
            </svg>
            <span *ngIf="isSubmitting" class="spinner-sm"></span>
            {{ isSubmitting ? 'Submitting to PostgreSQL...' : 'Submit Mushak 4.3 Declaration (দাখিল করুন)' }}
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
    .entry-badge { display: inline-block; background: rgba(16, 185, 129, 0.15); color: #34d399; font-size: 0.78rem; font-weight: 800; padding: 2px 8px; border-radius: var(--radius-sm); margin-bottom: 4px; }
    .entry-title { font-size: 1.3rem; font-weight: 800; color: var(--text-main); }
    .entry-subtitle { font-size: 0.8rem; color: var(--text-muted); }
    .form-section-title { font-size: 0.95rem; font-weight: 700; color: var(--vat-teal); margin-bottom: 6px; }
    .materials-section-header { display: flex; justify-content: space-between; align-items: flex-end; }
    .form-grid-3 { display: grid; grid-template-columns: 1fr 1fr 1fr; gap: 14px; }
    .form-grid-4 { display: grid; grid-template-columns: 1fr 1fr 1fr 1fr; gap: 14px; }
    @media (max-width: 768px) {
      .form-grid-3, .form-grid-4 { grid-template-columns: 1fr; }
    }
    .form-group { display: flex; flex-direction: column; gap: 4px; }
    .form-actions { display: flex; gap: 10px; }
    .mt-2 { margin-top: 8px; }
    .mt-3 { margin-top: 12px; }
    .mt-4 { margin-top: 24px; }
    .badge-branch-locked { background: rgba(245, 158, 11, 0.15); color: #fbbf24; font-size: 0.68rem; font-weight: 700; padding: 1px 6px; border-radius: 9999px; margin-left: 6px; display: inline-flex; align-items: center; }
    .locked-branch-field { display: flex; align-items: center; background: rgba(255, 255, 255, 0.03); border-color: rgba(255, 255, 255, 0.1); color: var(--vat-teal); font-weight: 600; cursor: not-allowed; }
    .btn-icon-danger { background: rgba(239, 68, 68, 0.15); border: 1px solid rgba(239, 68, 68, 0.3); color: #f87171; border-radius: var(--radius-sm); font-size: 1rem; width: 28px; height: 28px; cursor: pointer; display: inline-flex; align-items: center; justify-content: center; }
    .cost-simulation-card { background: var(--bg-card-subtle); border: 1px solid var(--border-card); border-radius: var(--radius-md); padding: 12px 20px; display: flex; gap: 24px; flex-wrap: wrap; }
    .sim-item { display: flex; flex-direction: column; gap: 2px; }
    .sim-label { font-size: 0.75rem; color: var(--text-muted); }
    .spinner-sm { display: inline-block; width: 14px; height: 14px; border: 2px solid rgba(255,255,255,0.3); border-top-color: #fff; border-radius: 50%; animation: spin 0.8s linear infinite; margin-right: 6px; }
    @keyframes spin { to { transform: rotate(360deg); } }
  `]
})
export class NewDeclarationComponent implements OnInit {
  @Output() declarationCreated = new EventEmitter<string>();
  @Output() cancelled = new EventEmitter<void>();

  private manufacturingApi = inject(ManufacturingApiService);
  private masterDataApi = inject(MasterDataApiService);
  private toast = inject(ToastService);
  public context = inject(CompanyContextService);
  public mockData = inject(MockDataService);

  public allProducts: ProductItem[] = [];
  public selectedBranchId = '';
  public isSubmitting = false;

  public newDecModel = {
    declarationNumber: 'DEC-4.3/2026/02',
    finishedProductId: '66666666-6666-6666-6666-666666666665',
    finishedProductName: '500ml Bottled Consumer Liquid Soap',
    hsCode: '3402.20.00',
    uom: 'PCS',
    batchSize: 1000,
    effectiveFrom: new Date().toISOString().slice(0, 10),
    submissionDate: new Date().toISOString().slice(0, 10),
    directLaborCost: 15000.0,
    utilityCost: 10000.0,
    overheadCost: 8000.0,
    marginPercent: 15.0,
    finalSellingPrice: 180.0
  };

  public materialRows: RawMaterialRow[] = [
    {
      productId: '66666666-6666-6666-6666-666666666661',
      productName: 'High Density Polyethylene Resin (HDPE)',
      hsCode: '3901.20.00',
      uom: 'KG',
      qtyRequired: 80,
      wastagePercent: 2.0,
      unitPrice: 165.0
    },
    {
      productId: '66666666-6666-6666-6666-666666666663',
      productName: 'Raw Material Chemical Additive',
      hsCode: '3824.99.00',
      uom: 'KG',
      qtyRequired: 500,
      wastagePercent: 1.5,
      unitPrice: 120.0
    }
  ];

  ngOnInit(): void {
    this.selectedBranchId = this.context.activeBranch().id;
    this.loadProducts();
    this.recalculatePrice();
  }

  public loadProducts(): void {
    this.masterDataApi.getProducts().subscribe({
      next: (prods) => {
        this.allProducts = prods;
        if (prods.length > 0 && !this.newDecModel.finishedProductId) {
          this.newDecModel.finishedProductId = prods[0].id;
          this.onFinishedProductSelect();
        }
      },
      error: (err) => console.warn('Failed to load products:', err)
    });
  }

  get finishedProductsList(): ProductItem[] {
    if (this.allProducts.length > 0) return this.allProducts;
    return this.mockData.products;
  }

  get rawProductsList(): ProductItem[] {
    if (this.allProducts.length > 0) return this.allProducts;
    return this.mockData.products;
  }

  public onFinishedProductSelect(): void {
    const prod = this.finishedProductsList.find(p => p.id === this.newDecModel.finishedProductId);
    if (prod) {
      this.newDecModel.finishedProductName = prod.name;
      this.newDecModel.hsCode = prod.hsCode;
      this.newDecModel.uom = prod.uom || 'PCS';
    }
  }

  public onMaterialSelect(row: RawMaterialRow): void {
    const prod = this.rawProductsList.find(p => p.id === row.productId);
    if (prod) {
      row.productName = prod.name;
      row.hsCode = prod.hsCode;
      row.uom = prod.uom || 'KG';
      row.unitPrice = prod.standardCost || 140.0;
    }
    this.recalculatePrice();
  }

  public addMaterialRow(): void {
    const fallback = this.rawProductsList[0] || { id: '66666666-6666-6666-6666-666666666661', name: 'Raw Material', hsCode: '3901.20.00', uom: 'KG', standardCost: 140 };
    this.materialRows.push({
      productId: fallback.id,
      productName: fallback.name,
      hsCode: fallback.hsCode,
      uom: fallback.uom,
      qtyRequired: 100,
      wastagePercent: 1.0,
      unitPrice: fallback.standardCost || 140
    });
    this.recalculatePrice();
  }

  public removeMaterialRow(index: number): void {
    if (this.materialRows.length > 1) {
      this.materialRows.splice(index, 1);
      this.recalculatePrice();
    }
  }

  public getRowTotal(row: RawMaterialRow): number {
    const wastageQty = (row.qtyRequired * row.wastagePercent) / 100;
    const totalQty = row.qtyRequired + wastageQty;
    return totalQty * row.unitPrice;
  }

  get computedTotalRawMaterialCost(): number {
    return this.materialRows.reduce((sum, r) => sum + this.getRowTotal(r), 0);
  }

  get computedTotalBatchCost(): number {
    return this.computedTotalRawMaterialCost + 
      (this.newDecModel.directLaborCost || 0) + 
      (this.newDecModel.utilityCost || 0) + 
      (this.newDecModel.overheadCost || 0);
  }

  public recalculatePrice(): void {
    const totalBatchCost = this.computedTotalBatchCost;
    const margin = this.newDecModel.marginPercent || 15;
    const profit = (totalBatchCost * margin) / 100;
    const batchSize = this.newDecModel.batchSize > 0 ? this.newDecModel.batchSize : 1000;
    this.newDecModel.finalSellingPrice = Math.round(((totalBatchCost + profit) / batchSize) * 100) / 100;
  }

  public submitNewDeclaration(): void {
    if (this.materialRows.length === 0) {
      this.toast.error('কমপক্ষে একটি কাঁচামাল লাইন থাকা আবশ্যক।', 'Validation Error');
      return;
    }

    this.isSubmitting = true;
    const branchGuid = this.context.resolveBranchGuid(this.selectedBranchId || this.context.activeBranch().id);
    const branchObj = this.mockData.branches.find(b => b.id === this.selectedBranchId || b.id === this.context.activeBranch().id);

    const payload: CreateCoefficientPayload = {
      declarationNumber: this.newDecModel.declarationNumber,
      finishedProductId: this.newDecModel.finishedProductId,
      finishedProductName: this.newDecModel.finishedProductName,
      hsCode: this.newDecModel.hsCode,
      uom: this.newDecModel.uom,
      batchSize: this.newDecModel.batchSize,
      effectiveFrom: this.newDecModel.effectiveFrom,
      submissionDate: this.newDecModel.submissionDate,
      branchId: branchGuid,
      branchName: branchObj ? branchObj.name : this.context.activeBranch().name,
      directLaborCost: this.newDecModel.directLaborCost,
      utilityCost: this.newDecModel.utilityCost,
      overheadCost: this.newDecModel.overheadCost,
      marginPercent: this.newDecModel.marginPercent,
      finalSellingPrice: this.newDecModel.finalSellingPrice,
      materials: this.materialRows.map(r => ({
        productId: r.productId,
        quantity: r.qtyRequired,
        wastagePercent: r.wastagePercent,
        unitPrice: r.unitPrice
      }))
    };

    this.manufacturingApi.createCoefficient(payload).subscribe({
      next: () => {
        this.isSubmitting = false;
        const msg = `উপকরণ-উৎপাদ সহগ ঘোষণা '${this.newDecModel.declarationNumber}' সফলভাবে দাখিল ও পোস্টগ্রেস ডাটাবেজে সংরক্ষিত হয়েছে!`;
        this.toast.success(msg, 'সহগ ঘোষণা দাখিল সম্পন্ন (Mushak 4.3)');
        this.declarationCreated.emit(this.newDecModel.declarationNumber);
      },
      error: (err) => {
        this.isSubmitting = false;
        console.error('Failed to submit Mushak 4.3:', err);
        const errMsg = err?.error?.title || err?.message || 'Declaration submission failed';
        this.toast.error(`দাখিল ব্যর্থ হয়েছে: ${errMsg}`, 'Submission Failed');
      }
    });
  }
}
