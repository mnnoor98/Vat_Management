import { Component, OnInit, Output, EventEmitter, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ManufacturingApiService } from '../../../../core/services/api/manufacturing-api.service';
import { CompanyContextService } from '../../../../core/services/company-context.service';
import { CsvExportService } from '../../../../core/services/csv-export.service';
import { BdtCurrencyPipe } from '../../../../shared/pipes/bdt-currency.pipe';
import { Coefficient43 } from '../../../../core/models/vat-models';

@Component({
  selector: 'app-bom-list',
  standalone: true,
  imports: [CommonModule, FormsModule, BdtCurrencyPipe],
  template: `
    <div class="vat-card">
      <div class="card-toolbar">
        <div class="search-box">
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <circle cx="11" cy="11" r="8"></circle>
            <line x1="21" y1="21" x2="16.65" y2="16.65"></line>
          </svg>
          <input 
            type="text" 
            class="search-input" 
            [(ngModel)]="bomSearchQuery" 
            placeholder="Search by BOM Code, Product Name, or HS Code..." 
          />
        </div>

        <div class="filter-group">
          <label class="vat-label-inline font-bengali">স্ট্যাটাস ফিল্টার:</label>
          <select class="vat-select select-compact" [(ngModel)]="bomStatusFilter">
            <option value="ALL">সকল স্ট্যাটাস (All)</option>
            <option value="Approved">Approved (অনুমোদিত)</option>
            <option value="Submitted">Submitted (দাখিলকৃত)</option>
            <option value="Draft">Draft (খসড়া)</option>
          </select>
          <button class="btn btn-secondary btn-sm" (click)="exportBomToCsv()" title="Export BOM Registry (.csv)">
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"></path>
              <polyline points="7 10 12 15 17 10"></polyline>
              <line x1="12" y1="15" x2="12" y2="3"></line>
            </svg>
            Export BOM (.csv)
          </button>
        </div>
      </div>

      <div class="vat-table-container">
        <table class="vat-table">
          <thead>
            <tr>
              <th>Declaration Ref</th>
              <th>Manufacturing Branch</th>
              <th>Finished Good Product</th>
              <th>NBR HS Code</th>
              <th class="text-right">Batch Size</th>
              <th class="text-right">Raw Material Cost</th>
              <th class="text-right">Selling Price/Unit</th>
              <th>Effective Date</th>
              <th>Status</th>
              <th class="text-center">Action</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let d of filteredDeclarations">
              <td><strong class="font-mono text-emerald">{{ d.declarationNumber }}</strong></td>
              <td>
                <span class="branch-pill">🏢 {{ d.branchName || 'Dhaka Central Plant' }}</span>
              </td>
              <td>
                <strong>{{ d.finishedProductName }}</strong>
              </td>
              <td><span class="font-mono text-dim">{{ d.hsCode }}</span></td>
              <td class="text-right font-mono">{{ d.batchSize }} {{ d.uom }}</td>
              <td class="text-right numeric font-bold">{{ d.totalRawMaterialCost | bdtCurrency:false }}</td>
              <td class="text-right numeric text-emerald font-bold">{{ d.finalSellingPrice | bdtCurrency:false }}</td>
              <td class="text-dim">{{ d.effectiveFrom }}</td>
              <td>
                <span class="status-badge" [class.approved]="d.status === 'Approved'" [class.submitted]="d.status === 'Submitted'">
                  {{ d.status }}
                </span>
              </td>
              <td class="text-center">
                <button class="btn btn-sm btn-secondary" (click)="declarationSelected.emit(d)">
                  View 4.3 Form
                </button>
              </td>
            </tr>
            <tr *ngIf="filteredDeclarations.length === 0">
              <td colspan="10" class="text-center py-4 text-dim">
                No BOM declarations found matching your filter criteria.
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  `,
  styles: [`
    .card-toolbar { display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: 14px; margin-bottom: 16px; }
    .search-box { display: flex; align-items: center; gap: 8px; background: var(--bg-card-subtle); border: 1px solid var(--border-card); border-radius: var(--radius-md); padding: 6px 12px; flex: 1; max-width: 400px; }
    .search-input { background: transparent; border: none; color: var(--text-main); font-size: 0.85rem; outline: none; width: 100%; }
    .filter-group { display: flex; align-items: center; gap: 8px; flex-wrap: wrap; }
    .vat-label-inline { font-size: 0.8rem; color: var(--text-muted); font-weight: 600; }
    .select-compact { width: auto; padding: 6px 10px; font-size: 0.8rem; }
    .status-badge { padding: 2px 8px; border-radius: 9999px; font-size: 0.75rem; font-weight: 700; }
    .status-badge.approved { background: rgba(16, 185, 129, 0.15); color: #34d399; }
    .status-badge.submitted { background: rgba(245, 158, 11, 0.15); color: #fbbf24; }
    .branch-pill { font-size: 0.72rem; font-weight: 700; padding: 2px 7px; border-radius: var(--radius-sm); background: rgba(14, 165, 233, 0.12); border: 1px solid rgba(14, 165, 233, 0.25); color: #38bdf8; display: inline-flex; align-items: center; gap: 4px; white-space: nowrap; }
  `]
})
export class BomListComponent implements OnInit {
  @Output() declarationSelected = new EventEmitter<Coefficient43>();

  private manufacturingApi = inject(ManufacturingApiService);
  private csvExport = inject(CsvExportService);
  public context = inject(CompanyContextService);

  public declarations: Coefficient43[] = [];
  public bomSearchQuery = '';
  public bomStatusFilter = 'ALL';

  ngOnInit(): void {
    this.loadDeclarations();
  }

  public loadDeclarations(): void {
    this.manufacturingApi.getCoefficients().subscribe({
      next: (list) => this.declarations = list,
      error: (err) => console.warn('Failed to load declarations:', err)
    });
  }

  get filteredDeclarations(): Coefficient43[] {
    let list = this.declarations;
    if (!this.context.isSuperAdmin()) {
      const curBranchGuid = this.context.branchGuid();
      const curBranchId = this.context.activeBranch().id;
      list = list.filter(d => 
        !d.branchId || 
        d.branchId === curBranchGuid || 
        d.branchId === curBranchId ||
        (curBranchId === 'BR-01' && d.branchId.includes('31')) ||
        (curBranchId === 'BR-02' && d.branchId.includes('32')) ||
        (curBranchId === 'BR-03' && d.branchId.includes('33'))
      );
    }
    if (this.bomStatusFilter !== 'ALL') {
      list = list.filter(d => d.status === this.bomStatusFilter);
    }
    if (!this.bomSearchQuery) return list;
    const q = this.bomSearchQuery.toLowerCase();
    return list.filter(d =>
      d.declarationNumber.toLowerCase().includes(q) ||
      d.finishedProductName.toLowerCase().includes(q) ||
      d.hsCode.toLowerCase().includes(q)
    );
  }

  public exportBomToCsv(): void {
    const headers = [
      'Declaration Number',
      'Manufacturing Plant',
      'Finished Product',
      'NBR HS Code',
      'Batch Size',
      'UOM',
      'Total Raw Material Cost (BDT)',
      'Direct Labor Cost (BDT)',
      'Utility Cost (BDT)',
      'Factory Overheads (BDT)',
      'Producer Margin (%)',
      'Declared Unit Selling Price (BDT)',
      'Status'
    ];

    const rows = this.filteredDeclarations.map(d => [
      d.declarationNumber,
      d.branchName || 'Dhaka Central Plant & HQ',
      d.finishedProductName,
      d.hsCode,
      d.batchSize,
      d.uom,
      d.totalRawMaterialCost,
      d.directLaborCost,
      d.utilityCost,
      d.overheadCost,
      d.marginPercent,
      d.finalSellingPrice,
      d.status
    ]);

    const compId = this.context.activeCompany().id;
    const filename = `Mushak_4_3_BOM_Registry_${compId}_${new Date().toISOString().slice(0, 10)}`;
    this.csvExport.exportToCsv(filename, headers, rows);
  }
}
