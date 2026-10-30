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
      <p>Live quantities are shown below. Cost, valuation and HS codes are unavailable from this stock feed.</p>
      <p role="alert" *ngIf="loadError">{{ loadError }} <button (click)="loadStockBalances()">Retry</button></p>
      <div class="card-toolbar">
        <button class="btn btn-secondary" [disabled]="isLoading || page === 0" (click)="changePage(-1)">Previous</button>
        <span>Page {{ page + 1 }} ? Search and export apply to this page.</span>
        <button class="btn btn-secondary" [disabled]="isLoading || !hasNext || page >= 400" (click)="changePage(1)">Next</button>
        <button class="btn btn-secondary" *ngIf="hasMoreWarehouses" [disabled]="warehousesLoading" (click)="loadWarehouses()">Load more warehouses</button>
      </div>
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

        <div class="summary-kpis">
          <span class="kpi-pill">Items on this page: <strong>{{ filteredBalances.length }}</strong></span>
          <span class="kpi-pill val">Valuation: <strong>Unavailable</strong></span>
          <button class="btn btn-secondary btn-sm" (click)="exportStockToCsv()" title="Export Stock Valuation (.csv)">
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"></path>
              <polyline points="7 10 12 15 17 10"></polyline>
              <line x1="12" y1="15" x2="12" y2="3"></line>
            </svg>
            Export Stock (.csv)
          </button>
        </div>
      </div>

      <div class="vat-table-container">
        <div *ngIf="isLoading" class="loading-state">
          <div class="spinner"></div>
          <span>Loading live stock balances from PostgreSQL...</span>
        </div>

        <table *ngIf="!isLoading" class="vat-table">
          <thead>
            <tr>
              <th>Item Code</th>
              <th>Product Description</th>
              <th>Warehouse / Location</th>
              <th>HS Code</th>
              <th>UOM</th>
              <th class="text-right">Available Stock</th>
              <th class="text-right">Standard Cost</th>
              <th class="text-right">Total Valuation (BDT)</th>
              <th>Status</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let b of filteredBalances">
              <td><strong class="font-mono text-emerald">{{ b.productReference }}</strong></td>
              <td>
                <strong>{{ b.productDescription }}</strong>
              </td>
              <td>
                <span class="wh-tag font-mono">🏢 {{ b.warehouseCode }}</span>
                <small class="text-dim d-block">{{ b.warehouseName }}</small>
              </td>
              <td><span class="hs-tag font-mono">{{ b.hsCode || 'Unavailable' }}</span></td>
              <td><span class="uom-tag">{{ b.unitOfMeasure }}</span></td>
              <td class="text-right numeric font-bold" [class.text-amber]="b.availableQuantity <= 0">
                {{ b.availableQuantity | bengaliNumber }}
              </td>
              <td class="text-right numeric">{{ b.standardCost == null ? "Unavailable" : (b.standardCost | bdtCurrency:false) }}</td>
              <td class="text-right numeric font-bold text-emerald">
                {{ b.totalValuation == null ? "Unavailable" : (b.totalValuation | bdtCurrency:false) }}
              </td>
              <td>
                <span *ngIf="b.availableQuantity > 0" class="stock-pill">In Stock</span>
                <span *ngIf="b.availableQuantity <= 0" class="stock-pill out-of-stock">Depleted</span>
              </td>
            </tr>
            <tr *ngIf="filteredBalances.length === 0">
              <td colspan="9" class="text-center py-4 text-dim">
                No stock balances found matching the filter criteria.
              </td>
            </tr>
          </tbody>
          <tfoot>
            <tr class="total-row">
              <th colspan="7" class="text-right font-bengali">সর্বমোট ইনভেন্টরি স্টক মূল্যায়ন:</th>
              <th class="text-right numeric font-bold text-emerald">{{ totalStockValuation == null ? "Unavailable" : (totalStockValuation | bdtCurrency:false) }}</th>
              <th></th>
            </tr>
          </tfoot>
        </table>
      </div>
    </div>
  `,
  styles: [`
    .card-toolbar { display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: 14px; margin-bottom: 16px; }
    .search-box { display: flex; align-items: center; gap: 8px; background: var(--bg-card-subtle); border: 1px solid var(--border-card); border-radius: var(--radius-md); padding: 8px 14px; flex: 1; max-width: 450px; }
    .search-input { background: transparent; border: none; color: var(--text-main); font-size: 0.85rem; outline: none; width: 100%; }
    .warehouse-filter { display: flex; align-items: center; gap: 8px; }
    .vat-label-inline { font-size: 0.8rem; color: var(--text-muted); font-weight: 600; }
    .select-compact { width: auto; padding: 6px 12px; font-size: 0.8rem; }
    .summary-kpis { display: flex; align-items: center; gap: 10px; flex-wrap: wrap; }
    .kpi-pill { background: var(--bg-card-subtle); border: 1px solid var(--border-card); padding: 6px 12px; border-radius: var(--radius-sm); font-size: 0.8rem; color: var(--text-muted); }
    .kpi-pill.val strong { color: var(--vat-emerald); }
    .locked-wh-pill { font-size: 0.8rem; font-weight: 600; color: var(--vat-teal); background: rgba(20, 184, 166, 0.1); padding: 5px 12px; border-radius: var(--radius-sm); border: 1px solid rgba(20, 184, 166, 0.3); }
    .loading-state { display: flex; align-items: center; justify-content: center; gap: 12px; padding: 40px; color: var(--text-muted); }
    .spinner { width: 24px; height: 24px; border: 3px solid rgba(16, 185, 129, 0.2); border-top-color: var(--vat-emerald); border-radius: 50%; animation: spin 0.8s linear infinite; }
    @keyframes spin { to { transform: rotate(360deg); } }
    .wh-tag { background: rgba(59, 130, 246, 0.1); color: #60a5fa; border: 1px solid rgba(59, 130, 246, 0.2); padding: 2px 6px; border-radius: 4px; font-size: 0.75rem; }
    .hs-tag { background: rgba(148, 163, 184, 0.1); color: var(--text-muted); padding: 2px 6px; border-radius: 4px; font-size: 0.75rem; }
    .uom-tag { font-size: 0.75rem; font-weight: 700; color: var(--text-dim); }
    .stock-pill { background: rgba(16, 185, 129, 0.15); color: #34d399; padding: 3px 8px; border-radius: 12px; font-size: 0.7rem; font-weight: 700; }
    .stock-pill.out-of-stock { background: rgba(244, 63, 94, 0.15); color: #fb7185; }
    .total-row { background: rgba(15, 23, 42, 0.6); }
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
  public searchQuery = '';
  public isLoading = false;

  ngOnInit(): void {
    this.loadWarehouses();
    this.loadStockBalances();
  }

  public loadWarehouses(): void {
    this.warehousesLoading = true;
    this.inventoryApi.getWarehouses(this.warehouseSkip, 100).subscribe({
      next: data => { this.warehouses.push(...data); this.warehouseSkip += data.length; this.hasMoreWarehouses = data.length === 100 && this.warehouseSkip <= 10000; this.warehousesLoading = false; this.cdr.markForCheck(); },
      error: e => { this.loadError = e?.message || 'Unable to load warehouses.'; this.warehousesLoading = false; this.cdr.markForCheck(); }
    });
  }
  public loadStockBalances(): void {
    const id = ++this.requestId;
    this.isLoading = true; this.loadError = ''; this.stockBalances = []; this.hasNext = false;
    this.inventoryApi.getAllStockBalances(this.selectedWarehouse, this.page * 25, 25).subscribe({
      next: data => { if (id !== this.requestId) return; this.stockBalances = data; this.hasNext = data.length === 25; this.isLoading = false; this.cdr.markForCheck(); },
      error: e => { if (id !== this.requestId) return; this.loadError = e?.message || 'Unable to load stock balances.'; this.isLoading = false; this.cdr.markForCheck(); }
    });
  }
  public changePage(direction: number): void {
    if (this.isLoading || (direction < 0 && this.page === 0) || (direction > 0 && (!this.hasNext || this.page >= 400))) return;
    this.page += direction; this.loadStockBalances();
  }
  public onWarehouseFilterChange(): void { this.page = 0; this.loadStockBalances(); }

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

  get totalStockValuation(): number | null {
    if (!this.filteredBalances.length || this.filteredBalances.some(b => b.totalValuation == null)) return null;
    return this.filteredBalances.reduce((sum, b) => sum + b.totalValuation!, 0);
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
      'Total Valuation (BDT)'
    ];

    const rows = this.filteredBalances.map(b => [
      b.productReference,
      b.warehouseCode,
      b.warehouseName,
      b.branchName,
      b.productDescription,
      b.hsCode || 'Unavailable',
      b.unitOfMeasure,
      b.availableQuantity,
      b.standardCost ?? 'Unavailable',
      b.totalValuation ?? 'Unavailable'
    ]);

    const compId = this.context.activeCompany().id;
    const filename = `Inventory_Stock_Valuation_${compId}_${new Date().toISOString().slice(0, 10)}`;
    this.csvExport.exportToCsv(filename, headers, rows);
  }
}
