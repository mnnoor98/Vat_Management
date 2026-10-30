import { Component, OnInit, inject, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { InventoryApiService } from '../../../../core/services/api/inventory-api.service';
import { CompanyContextService } from '../../../../core/services/company-context.service';
import { CsvExportService } from '../../../../core/services/csv-export.service';
import { BdtCurrencyPipe } from '../../../../shared/pipes/bdt-currency.pipe';
import { BengaliNumberPipe } from '../../../../shared/pipes/bengali-number.pipe';
import { Warehouse, StockBalance } from '../../../../core/models/vat-models';

@Component({
  selector: 'app-stock-summary',
  standalone: true,
  imports: [CommonModule, FormsModule, BdtCurrencyPipe, BengaliNumberPipe],
  template: `
    <div class="vat-card">
      <div class="card-intro">
        <h2>লাইভ ইনভেন্টরি স্টক ও মূল্যায়ন (Stock Valuation)</h2>
        <p>
          পারচেজ চালানের ক্রয়মূল্যের ভিত্তিতে চলমান ভারযুক্ত গড় (Moving Weighted Average) অথবা আগে আসলে আগে খালাস (FIFO)
          পদ্ধতিতে কাঁচামাল ও তৈরি পণ্যের রিয়েল-টাইম মূল্যায়ন ও এনবিআর ৮-ডিজিট এইচএস কোড তালিকা।
        </p>
      </div>

      <!-- Live KPI Metric Cards -->
      <div class="kpi-grid">
        <div class="kpi-card">
          <div class="kpi-icon wh">🏢</div>
          <div class="kpi-content">
            <span class="kpi-label">মোট সক্রিয় গুদাম</span>
            <span class="kpi-value">{{ warehouses.length || 1 }}</span>
          </div>
        </div>

        <div class="kpi-card">
          <div class="kpi-icon qty">📦</div>
          <div class="kpi-content">
            <span class="kpi-label">মোট মজুদ আইটেম (পরিমাণ)</span>
            <span class="kpi-value">{{ totalStockQuantity | bengaliNumber }}</span>
          </div>
        </div>

        <div class="kpi-card highlight">
          <div class="kpi-icon val">💰</div>
          <div class="kpi-content">
            <span class="kpi-label">মোট ইনভেন্টরি মূল্যায়ন (BDT)</span>
            <span class="kpi-value emerald-val">{{ totalStockValuation | bdtCurrency }}</span>
          </div>
        </div>

        <div class="kpi-card">
          <div class="kpi-icon method">⚖️</div>
          <div class="kpi-content">
            <span class="kpi-label">সক্রিয় কস্টিং পদ্ধতি</span>
            <span class="kpi-badge font-mono">{{ costingMethod === 'FIFO' ? 'FIFO (First-In, First-Out)' : 'Weighted Average (ভারযুক্ত গড়)' }}</span>
          </div>
        </div>
      </div>

      <p role="alert" *ngIf="loadError" class="alert-error">{{ loadError }} <button (click)="loadStockBalances()">Retry</button></p>

      <!-- Toolbar & Filters -->
      <div class="card-toolbar">
        <div class="search-box">
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <circle cx="11" cy="11" r="8"></circle>
            <line x1="21" y1="21" x2="16.65" y2="16.65"></line>
          </svg>
          <input 
            type="text" 
            class="search-input" 
            [(ngModel)]="searchQuery" 
            placeholder="Search by Item Code, Description, or HS Code..." 
          />
        </div>

        <div class="filter-group">
          <div class="warehouse-filter">
            <label class="vat-label-inline font-bengali">গুদাম ফিল্টার:</label>
            <select *ngIf="context.isSuperAdmin()" class="vat-select select-compact" [(ngModel)]="selectedWarehouse" (change)="onWarehouseFilterChange()">
              <option value="ALL">সকল গুদাম (All Warehouses)</option>
              <option *ngFor="let w of warehouses" [value]="w.warehouseId">
                🏢 {{ w.name }} ({{ w.code }})
              </option>
            </select>
            <div *ngIf="!context.isSuperAdmin()" class="locked-wh-pill">
              🏢 {{ context.activeBranch().name }} ({{ context.activeBranch().warehouseId || 'No warehouse selected' }})
            </div>
          </div>

          <div class="method-filter">
            <label class="vat-label-inline font-bengali">মূল্যায়ন পদ্ধতি:</label>
            <select class="vat-select select-compact method-select" [(ngModel)]="costingMethod" (change)="onCostingMethodChange()">
              <option value="WEIGHTED_AVERAGE">ভারযুক্ত গড় (Weighted Average)</option>
              <option value="FIFO">আগে আসলে আগে খালাস (FIFO)</option>
            </select>
          </div>

          <button class="btn btn-secondary btn-sm" (click)="exportStockToCsv()" title="Export Stock Valuation (.csv)">
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"></path>
              <polyline points="7 10 12 15 17 10"></polyline>
              <line x1="12" y1="15" x2="12" y2="3"></line>
            </svg>
            Export CSV
          </button>
        </div>
      </div>

      <!-- Pagination Mini Toolbar -->
      <div class="pagination-bar">
        <button class="btn btn-secondary btn-xs" [disabled]="isLoading || page === 0" (click)="changePage(-1)">Previous</button>
        <span class="page-indicator">পৃষ্ঠা {{ page + 1 }} | প্রদর্শিত আইটেম: {{ filteredBalances.length }}</span>
        <button class="btn btn-secondary btn-xs" [disabled]="isLoading || !hasNext || page >= 400" (click)="changePage(1)">Next</button>
        <button class="btn btn-secondary btn-xs" *ngIf="hasMoreWarehouses" [disabled]="warehousesLoading" (click)="loadWarehouses()">Load more warehouses</button>
      </div>

      <!-- Live Stock Table -->
      <div class="vat-table-container">
        <div *ngIf="isLoading" class="loading-state">
          <div class="spinner"></div>
          <span>PostgreSQL ও ক্রয় চালান থেকে লাইভ ইনভেন্টরি কস্টিং হিসাব হচ্ছে...</span>
        </div>

        <table *ngIf="!isLoading" class="vat-table">
          <thead>
            <tr>
              <th>আইটেম কোড</th>
              <th>পণ্যের পূর্ণ বিবরণ</th>
              <th>গুদাম ও শাখা</th>
              <th>এনবিআর ৮-ডিজিট এইচএস কোড</th>
              <th>UOM</th>
              <th class="text-right">উপলব্ধ মজুদ (Stock)</th>
              <th class="text-right">ইউনিট কস্ট (BDT)</th>
              <th class="text-right">মোট মজুদ মূল্য (BDT)</th>
              <th>স্ট্যাটাস</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let b of filteredBalances">
              <td>
                <span class="product-badge font-mono">{{ b.productReference }}</span>
              </td>
              <td>
                <strong>{{ b.productDescription }}</strong>
                <small class="d-block text-dim">Tax Code: {{ b.unitOfMeasure }} | Class: STD</small>
              </td>
              <td>
                <span class="wh-tag font-mono">🏢 {{ b.warehouseCode }}</span>
                <small class="text-dim d-block">{{ b.warehouseName }}</small>
              </td>
              <td>
                <span class="hs-tag font-mono">{{ b.hsCode || '3901.20.90' }}</span>
              </td>
              <td><span class="uom-tag">{{ b.unitOfMeasure }}</span></td>
              <td class="text-right numeric font-bold" [class.text-amber]="b.availableQuantity <= 0">
                {{ b.availableQuantity | bengaliNumber }}
              </td>
              <td class="text-right numeric font-mono">
                {{ (b.standardCost ?? 0) | bdtCurrency:false }} ৳
              </td>
              <td class="text-right numeric font-bold text-emerald font-mono">
                {{ (b.totalValuation ?? 0) | bdtCurrency:false }} ৳
              </td>
              <td>
                <span *ngIf="b.availableQuantity > 0" class="stock-pill in-stock">মজুদ আছে (In Stock)</span>
                <span *ngIf="b.availableQuantity <= 0" class="stock-pill out-of-stock">শূন্য (Depleted)</span>
              </td>
            </tr>
            <tr *ngIf="filteredBalances.length === 0">
              <td colspan="9" class="text-center py-4 text-dim">
                কোনো ইনভেন্টরি মজুদ রেকর্ড পাওয়া যায়নি।
              </td>
            </tr>
          </tbody>
          <tfoot>
            <tr class="total-row">
              <th colspan="5" class="text-right font-bengali">সর্বমোট ইনভেন্টরি স্টক ও মূল্যায়ন:</th>
              <th class="text-right numeric font-bold font-mono">{{ totalStockQuantity | bengaliNumber }}</th>
              <th class="text-right text-dim font-mono">-</th>
              <th class="text-right numeric font-bold text-emerald font-mono">{{ totalStockValuation | bdtCurrency }}</th>
              <th></th>
            </tr>
          </tfoot>
        </table>
      </div>
    </div>
  `,
  styles: [`
    .card-intro { margin-bottom: 20px; }
    .card-intro h2 { font-size: 1.25rem; font-weight: 800; margin-bottom: 6px; }
    .card-intro p { font-size: 0.88rem; color: var(--text-muted); line-height: 1.5; margin: 0; }

    .kpi-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 14px; margin-bottom: 20px; }
    .kpi-card { display: flex; align-items: center; gap: 14px; background: var(--bg-card-subtle, rgba(255, 255, 255, 0.03)); border: 1px solid var(--border-card); border-radius: var(--radius-md, 8px); padding: 14px 18px; }
    .kpi-card.highlight { border-color: rgba(16, 185, 129, 0.4); background: rgba(16, 185, 129, 0.06); }
    .kpi-icon { width: 42px; height: 42px; border-radius: 10px; display: flex; align-items: center; justify-content: center; font-size: 1.3rem; background: rgba(255, 255, 255, 0.05); }
    .kpi-icon.wh { background: rgba(59, 130, 246, 0.15); }
    .kpi-icon.qty { background: rgba(245, 158, 11, 0.15); }
    .kpi-icon.val { background: rgba(16, 185, 129, 0.15); }
    .kpi-icon.method { background: rgba(168, 85, 247, 0.15); }
    .kpi-content { display: flex; flex-direction: column; gap: 4px; }
    .kpi-label { font-size: 0.76rem; color: var(--text-muted); font-weight: 600; }
    .kpi-value { font-size: 1.15rem; font-weight: 800; color: var(--text-main); }
    .emerald-val { color: var(--vat-emerald, #10b981) !important; }
    .kpi-badge { font-size: 0.74rem; font-weight: 700; color: #c084fc; background: rgba(168, 85, 247, 0.12); padding: 2px 8px; border-radius: 4px; }

    .card-toolbar { display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: 14px; margin-bottom: 14px; }
    .search-box { display: flex; align-items: center; gap: 8px; background: var(--bg-card-subtle); border: 1px solid var(--border-card); border-radius: var(--radius-md); padding: 8px 14px; flex: 1; max-width: 400px; }
    .search-input { background: transparent; border: none; color: var(--text-main); font-size: 0.85rem; outline: none; width: 100%; }

    .filter-group { display: flex; align-items: center; gap: 12px; flex-wrap: wrap; }
    .warehouse-filter, .method-filter { display: flex; align-items: center; gap: 8px; }
    .vat-label-inline { font-size: 0.8rem; color: var(--text-muted); font-weight: 600; white-space: nowrap; }
    .select-compact { width: auto; padding: 6px 12px; font-size: 0.8rem; }
    .method-select { border-color: rgba(168, 85, 247, 0.4); }
    .locked-wh-pill { font-size: 0.8rem; font-weight: 600; color: var(--vat-teal); background: rgba(20, 184, 166, 0.1); padding: 5px 12px; border-radius: var(--radius-sm); border: 1px solid rgba(20, 184, 166, 0.3); }

    .pagination-bar { display: flex; align-items: center; gap: 10px; margin-bottom: 12px; font-size: 0.78rem; color: var(--text-muted); }
    .page-indicator { font-weight: 600; }
    .btn-xs { padding: 4px 10px; font-size: 0.74rem; }

    .product-badge { background: rgba(16, 185, 129, 0.12); color: #34d399; padding: 3px 8px; border-radius: 4px; font-weight: 700; font-size: 0.82rem; }
    .wh-tag { background: rgba(59, 130, 246, 0.1); color: #60a5fa; border: 1px solid rgba(59, 130, 246, 0.2); padding: 2px 6px; border-radius: 4px; font-size: 0.75rem; }
    .hs-tag { background: rgba(148, 163, 184, 0.12); color: #94a3b8; border: 1px solid rgba(148, 163, 184, 0.2); padding: 2px 7px; border-radius: 4px; font-size: 0.78rem; font-weight: 600; }
    .uom-tag { font-size: 0.75rem; font-weight: 700; color: var(--text-dim); }
    .stock-pill { padding: 3px 10px; border-radius: 12px; font-size: 0.72rem; font-weight: 700; }
    .stock-pill.in-stock { background: rgba(16, 185, 129, 0.15); color: #34d399; }
    .stock-pill.out-of-stock { background: rgba(244, 63, 94, 0.15); color: #fb7185; }

    .total-row { background: rgba(15, 23, 42, 0.7); border-top: 2px solid var(--border-card); }
    .alert-error { background: rgba(239, 68, 68, 0.15); color: #f87171; padding: 10px 14px; border-radius: 6px; font-size: 0.85rem; }
    .loading-state { display: flex; align-items: center; justify-content: center; gap: 12px; padding: 40px; color: var(--text-muted); }
    .spinner { width: 24px; height: 24px; border: 3px solid rgba(16, 185, 129, 0.2); border-top-color: var(--vat-emerald); border-radius: 50%; animation: spin 0.8s linear infinite; }
    @keyframes spin { to { transform: rotate(360deg); } }
  `]
})
export class StockSummaryComponent implements OnInit {
  private inventoryApi = inject(InventoryApiService);
  private csvExport = inject(CsvExportService);
  public context = inject(CompanyContextService);
  private cdr = inject(ChangeDetectorRef);

  public page = 0;
  public hasNext = false;
  public loadError = '';
  public warehousesLoading = false;
  public hasMoreWarehouses = false;
  private warehouseSkip = 0;
  private requestId = 0;

  public warehouses: Warehouse[] = [];
  public stockBalances: StockBalance[] = [];
  public selectedWarehouse = 'ALL';
  public costingMethod: 'WEIGHTED_AVERAGE' | 'FIFO' = 'WEIGHTED_AVERAGE';
  public searchQuery = '';
  public isLoading = false;

  ngOnInit(): void {
    this.loadWarehouses();
    this.loadStockBalances();
  }

  public loadWarehouses(): void {
    this.warehousesLoading = true;
    this.inventoryApi.getWarehouses(this.warehouseSkip, 100).subscribe({
      next: data => {
        this.warehouses.push(...data);
        this.warehouseSkip += data.length;
        this.hasMoreWarehouses = data.length === 100 && this.warehouseSkip <= 10000;
        this.warehousesLoading = false;
        this.cdr.markForCheck();
      },
      error: e => {
        this.loadError = e?.message || 'Unable to load warehouses.';
        this.warehousesLoading = false;
        this.cdr.markForCheck();
      }
    });
  }

  public loadStockBalances(): void {
    const id = ++this.requestId;
    this.isLoading = true;
    this.loadError = '';
    this.stockBalances = [];
    this.hasNext = false;

    this.inventoryApi.getAllStockBalances(
      this.selectedWarehouse,
      this.page * 25,
      25,
      this.costingMethod
    ).subscribe({
      next: data => {
        if (id !== this.requestId) return;
        this.stockBalances = data;
        this.hasNext = data.length === 25;
        this.isLoading = false;
        this.cdr.markForCheck();
      },
      error: e => {
        if (id !== this.requestId) return;
        this.loadError = e?.message || 'Unable to load stock balances.';
        this.isLoading = false;
        this.cdr.markForCheck();
      }
    });
  }

  public onCostingMethodChange(): void {
    this.page = 0;
    this.loadStockBalances();
  }

  public changePage(direction: number): void {
    if (this.isLoading || (direction < 0 && this.page === 0) || (direction > 0 && (!this.hasNext || this.page >= 400))) return;
    this.page += direction;
    this.loadStockBalances();
  }

  public onWarehouseFilterChange(): void {
    this.page = 0;
    this.loadStockBalances();
  }

  get filteredBalances(): StockBalance[] {
    let list = this.stockBalances;
    if (!this.context.isSuperAdmin()) list = list.filter(b => b.branchId === this.context.branchGuid());
    if (this.selectedWarehouse !== 'ALL') {
      list = list.filter(b => b.warehouseId === this.selectedWarehouse || b.warehouseCode === this.selectedWarehouse);
    }
    if (!this.searchQuery) return list;
    const q = this.searchQuery.toLowerCase();
    return list.filter(b =>
      b.productReference.toLowerCase().includes(q) ||
      b.productDescription.toLowerCase().includes(q) ||
      (b.hsCode && b.hsCode.toLowerCase().includes(q)) ||
      b.warehouseCode.toLowerCase().includes(q)
    );
  }

  get totalStockQuantity(): number {
    return this.filteredBalances.reduce((sum, b) => sum + (b.availableQuantity || 0), 0);
  }

  get totalStockValuation(): number {
    return this.filteredBalances.reduce((sum, b) => sum + (b.totalValuation || 0), 0);
  }

  public exportStockToCsv(): void {
    const headers = [
      'Item Code',
      'Warehouse Code',
      'Warehouse Name',
      'Branch Location',
      'Product Description',
      'NBR HS Code',
      'UOM',
      'Available Stock',
      'Unit Cost (BDT)',
      'Total Valuation (BDT)',
      'Costing Method'
    ];

    const rows = this.filteredBalances.map(b => [
      b.productReference,
      b.warehouseCode,
      b.warehouseName,
      b.branchName,
      b.productDescription,
      b.hsCode || '3901.20.90',
      b.unitOfMeasure,
      b.availableQuantity,
      b.standardCost ?? 0,
      b.totalValuation ?? 0,
      b.costingMethod || this.costingMethod
    ]);

    const compId = this.context.activeCompany().id;
    const filename = `Inventory_Stock_Valuation_${this.costingMethod}_${compId}_${new Date().toISOString().slice(0, 10)}`;
    this.csvExport.exportToCsv(filename, headers, rows);
  }
}
