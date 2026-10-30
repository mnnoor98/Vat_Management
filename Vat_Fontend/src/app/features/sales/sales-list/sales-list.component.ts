import { FinancialPostingComponent } from '../../../shared/components/financial-posting.component';
import { Component, OnInit, inject, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { SalesApiService } from '../../../core/services/api/sales-api.service';
import { CompanyContextService } from '../../../core/services/company-context.service';
import { CsvExportService } from '../../../core/services/csv-export.service';
import { ToastService } from '../../../core/services/toast.service';
import { BdtCurrencyPipe } from '../../../shared/pipes/bdt-currency.pipe';
import { StatusBadgeComponent } from '../../../shared/components/status-badge.component';
import { SalesInvoice, Branch } from '../../../core/models/vat-models';
import { MockDataService } from '../../../core/services/mock-data.service';

@Component({
  selector: 'app-sales-list',
  standalone: true,
  imports: [FinancialPostingComponent, CommonModule, RouterModule, FormsModule, BdtCurrencyPipe, StatusBadgeComponent],
  template: `
    <div class="sales-page">
      <a routerLink="/app/sales/payments" class="btn btn-secondary">Payments and customer receivables</a>
      <app-financial-posting *ngIf="postingInvoice as invoice" [invoice]="invoice" kind="sales" (changed)="loadInvoices()" (closed)="postingInvoice = null"></app-financial-posting>
      <p *ngIf="loadError" role="alert">{{ loadError }} <button (click)="loadInvoices()">Retry</button></p>
      <div class="header-actions">
        <button class="btn btn-secondary" [disabled]="loading || pageIndex === 0" (click)="changePage(-1)">Previous</button>
        <span>Page {{ pageIndex + 1 }} ? Filters and export apply to this page</span>
        <button class="btn btn-secondary" [disabled]="loading || !hasNextPage" (click)="changePage(1)">Next</button>
      </div>
      <p>Tax is pending until a server calculation is prepared. Amounts before preparation exclude tax. Posted amounts show the original posting snapshot; subsequent corrections remain separate.</p>
      <!-- Breadcrumb & Header -->
      <div class="page-header">
        <div>
          <h1 class="page-title">
            {{ context.locale() === 'bn' ? 'বিক্রয় ইনভয়েস ও মূসক ৬.৩ চালান' : 'Sales Invoices & Mushak 6.3 Challans' }}
          </h1>
          <p class="page-subtitle">
            {{ context.locale() === 'bn' 
              ? 'বিক্রয় চালানপত্র, মূসক ৬.৩ কর চালান এবং মূসক ৬.২ বিক্রয় হিসাব রেজিস্টার'
              : 'Outward supplies, Mushak 6.3 Tax Invoices, and Section 51 statutory documentation' }}
          </p>
        </div>
        <div class="header-actions">
          <button class="btn btn-secondary" (click)="exportToCsv()" title="Export Excel / CSV">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"></path><polyline points="7 10 12 15 17 10"></polyline><line x1="12" y1="15" x2="12" y2="3"></line></svg>
            {{ context.locale() === 'bn' ? 'এক্সেল এক্সপোর্ট' : 'Export Excel' }}
          </button>
          <a routerLink="/app/sales/mushak-6-2" class="btn btn-secondary">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M2 3h6a4 4 0 0 1 4 4v14a3 3 0 0 0-3-3H2z"></path><path d="M22 3h-6a4 4 0 0 0-4 4v14a3 3 0 0 1 3-3h7z"></path></svg>
            {{ context.locale() === 'bn' ? 'মূসক ৬.২ বিক্রয় বই' : 'Mushak 6.2 Register' }}
          </a>
          <a routerLink="/app/sales/new" class="btn btn-primary">
            <span>+</span> {{ context.locale() === 'bn' ? 'নতুন বিক্রয় চালান (৬.৩)' : '+ Create Sales Challan (6.3)' }}
          </a>
        </div>
      </div>

      <!-- Search Toolbar -->
      <div class="vat-card filter-card">
        <div class="filter-grid">
          <div class="filter-item search-item">
            <label class="vat-label">Search</label>
            <input
              type="text"
              class="vat-input"
              [(ngModel)]="searchQuery"
              (ngModelChange)="applyFilters()"
              placeholder="Challan No / Customer / BIN..."
            />
          </div>

          <div class="filter-item">
            <label class="vat-label">Sales Type</label>
            <select class="vat-select" [(ngModel)]="selectedType" (ngModelChange)="applyFilters()">
              <option value="All">All Types</option>
              <option value="Standard">Standard 15%</option>
              <option value="ZeroRated">Zero Rated (Export)</option>
              <option value="Exempt">Exempted</option>
            </select>
          </div>

          <div class="filter-item">
            <label class="vat-label">Status</label>
            <select class="vat-select" [(ngModel)]="selectedStatus" (ngModelChange)="applyFilters()">
              <option value="All">All Statuses</option>
              <option value="Approved">Approved / Active</option>
              <option value="Posted">Posted</option>
            </select>
          </div>

          <div class="filter-item" *ngIf="context.isSuperAdmin()">
            <label class="vat-label">{{ context.locale() === 'bn' ? 'শাখা ফিল্টার' : 'Branch Filter' }}</label>
            <select class="vat-select" [(ngModel)]="selectedBranchFilter" (ngModelChange)="applyFilters()">
              <option value="All">All Branches (সকল শাখা)</option>
              <option *ngFor="let b of branches" [value]="b.id">{{ b.name }}</option>
            </select>
          </div>
        </div>
      </div>

      <!-- Sales Invoices Table -->
      <div class="vat-card">
        <div class="vat-table-container">
          <table class="vat-table">
            <thead>
              <tr>
                <th>Mushak 6.3 Challan No</th>
                <th>Branch</th>
                <th>Customer & BIN</th>
                <th>Vehicle / Delivery</th>
                <th>Delivery Date</th>
                <th class="text-right">Taxable Value</th>
                <th class="text-right">Output VAT</th>
                <th class="text-right">Total Amount</th>
                <th>Status</th>
                <th class="text-right">Actions</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let s of filteredInvoices">
                <td>
                  <strong class="font-mono text-emerald">{{ s.challanNumber63 }}</strong>
                  <br>
                  <small class="text-dim">Inv: {{ s.invoiceNumber }}</small>
                </td>
                <td>
                  <span class="branch-pill">
                    🏢 {{ getBranchName(s.branchId) }}
                  </span>
                </td>
                <td>
                  <strong>{{ s.customerName }}</strong>
                  <br>
                  <span class="bin-tag">BIN: {{ s.customerBin }}</span>
                </td>
                <td>
                  <div class="vehicle-info">{{ s.vehicleTypeAndNo }}</div>
                  <small class="text-dim">Dest: {{ s.destinationAddress }}</small>
                </td>
                <td>{{ s.deliveryDate }} <br><small class="text-dim">{{ s.deliveryTime }}</small></td>
                <td class="text-right numeric">{{ s.subTotal | bdtCurrency:false }}</td>
                <td class="text-right numeric text-rose font-bold">{{ s.taxCalculated ? (s.totalVat | bdtCurrency:false) : "Pending" }}</td>
                <td class="text-right numeric font-bold">{{ s.taxCalculated ? (s.grandTotal | bdtCurrency:false) : "Pending tax" }}</td>
                <td><app-status-badge [status]="s.status"></app-status-badge></td>
                <td class="text-right">
                  <div class="row-actions">
                    <a [routerLink]="['/app/sales/challan-preview']" [queryParams]="{ id: s.id }" class="btn-action" title="View Mushak 6.3">View</a>
                    <button class="btn-action" *ngIf="s.status === 'Draft' && context.hasPermission('sales.submit')" (click)="submitInvoice(s.id)">Submit draft</button>
                    <button *ngIf="s.status === 'Submitted' && canApprove()" class="btn-action btn-approve" (click)="approveInvoice(s.id)" title="Approve Challan">
                      Approve
                    </button>
                    <button *ngIf="(s.status === 'Approved' || s.status === 'Reserved') && canPost()" class="btn-action btn-post" (click)="postInvoiceToLedger(s.id)" title="Post to VAT & Sales Ledger">
                      Post to Ledger
                    </button>
                    <a [routerLink]="['/app/sales/challan-preview']" [queryParams]="{ id: s.id }" class="btn-action print-action" title="Print Mushak 6.3">
                      Print 6.3
                    </a>
                  </div>
                </td>
              </tr>
              <tr *ngIf="filteredInvoices.length === 0">
                <td colspan="9" class="empty-state">No sales invoices found matching your criteria.</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .sales-page {
      display: flex;
      flex-direction: column;
      gap: 20px;
    }

    .page-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      flex-wrap: wrap;
      gap: 16px;
    }

    .page-title {
      font-size: 1.5rem;
      font-weight: 800;
    }

    .page-subtitle {
      font-size: 0.85rem;
      color: var(--text-muted);
    }

    .header-actions {
      display: flex;
      gap: 12px;
    }

    .filter-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
      gap: 16px;
    }

    @media (max-width: 768px) {
      .filter-grid {
        grid-template-columns: 1fr;
      }
    }

    .bin-tag {
      font-family: var(--font-mono);
      font-size: 0.72rem;
      color: var(--vat-teal);
    }

    .vehicle-info {
      font-size: 0.78rem;
      font-weight: 600;
      color: var(--text-main);
    }

    .branch-pill {
      font-size: 0.72rem;
      font-weight: 700;
      padding: 2px 7px;
      border-radius: var(--radius-sm);
      background: rgba(14, 165, 233, 0.12);
      border: 1px solid rgba(14, 165, 233, 0.25);
      color: #38bdf8;
      display: inline-flex;
      align-items: center;
      gap: 4px;
      white-space: nowrap;
    }

    .row-actions {
      display: flex;
      justify-content: flex-end;
    }

    .btn-action.btn-approve {
      background: rgba(16, 185, 129, 0.15);
      border: 1px solid rgba(16, 185, 129, 0.4);
      color: #34d399;
      padding: 4px 10px;
      border-radius: var(--radius-sm);
      font-size: 0.75rem;
      font-weight: 700;
      cursor: pointer;
      margin-right: 6px;
      transition: all 0.2s ease;
    }
    .btn-action.btn-approve:hover {
      background: var(--vat-emerald);
      color: #ffffff;
    }
    .btn-action.btn-post {
      background: rgba(14, 165, 233, 0.15);
      border: 1px solid rgba(14, 165, 233, 0.4);
      color: #38bdf8;
      padding: 4px 10px;
      border-radius: var(--radius-sm);
      font-size: 0.75rem;
      font-weight: 700;
      cursor: pointer;
      margin-right: 6px;
      transition: all 0.2s ease;
    }
    .btn-action.btn-post:hover {
      background: #0ea5e9;
      color: #ffffff;
      box-shadow: 0 0 10px rgba(14, 165, 233, 0.4);
    }

    .btn-action.print-action {
      background: rgba(16, 185, 129, 0.15);
      border: 1px solid rgba(16, 185, 129, 0.4);
      color: #34d399;
      padding: 4px 12px;
      border-radius: var(--radius-sm);
      font-size: 0.75rem;
      font-weight: 700;
      text-decoration: none;
      transition: all 0.2s ease;
    }
    .btn-action.print-action:hover {
      background: var(--vat-emerald);
      color: #ffffff;
    }

    .empty-state {
      text-align: center;
      padding: 30px;
      color: var(--text-muted);
    }
  `]
})
export class SalesListComponent implements OnInit {
  public postingInvoice: SalesInvoice | null = null;
  public loading = false;
  public loadError = '';
  public readonly pageSize = 25;
  public pageIndex = 0;
  public hasNextPage = false;
  private cursors: (string | undefined)[] = [undefined];
  private cdr = inject(ChangeDetectorRef);
  public invoices: SalesInvoice[] = [];
  public filteredInvoices: SalesInvoice[] = [];
  public branches: Branch[] = [];
  public searchQuery: string = '';
  public selectedType: string = 'All';
  public selectedStatus: string = 'All';
  public selectedBranchFilter: string = 'All';

  private csvExport = inject(CsvExportService);
  private toast = inject(ToastService);
  private mockData = inject(MockDataService);

  constructor(
    public context: CompanyContextService,
    private salesApi: SalesApiService
  ) {}

  ngOnInit(): void {
    this.branches = this.mockData.branches;
    this.loadInvoices();
  }

  loadInvoices(): void {
    this.loading = true; this.loadError = '';
    this.salesApi.getInvoices({ before: this.cursors[this.pageIndex], take: this.pageSize }).subscribe({
      next: data => {
        this.invoices = data; this.hasNextPage = data.length === this.pageSize;
        this.loading = false; this.applyFilters(); this.cdr.markForCheck();
      },
      error: e => { this.loading = false; this.loadError = e?.message || 'Unable to load invoices.'; this.invoices = []; this.filteredInvoices = []; this.hasNextPage = false; this.cdr.markForCheck(); }
    });
  }

  changePage(direction: number): void {
    if (this.loading || (direction < 0 && this.pageIndex === 0) || (direction > 0 && !this.hasNextPage)) return;
    if (direction > 0) this.cursors[this.pageIndex + 1] = this.invoices[this.invoices.length - 1].id;
    this.pageIndex += direction; this.loadInvoices();
  }

  applyFilters(): void {
    let result = [...this.invoices];

    if (!this.context.isSuperAdmin()) {
      result = result.filter(i => i.branchId === this.context.branchGuid());
    } else if (this.selectedBranchFilter !== 'All') {
      result = result.filter(i => i.branchId === this.context.resolveBranchGuid(this.selectedBranchFilter));
    }

    if (this.searchQuery.trim()) {
      const q = this.searchQuery.toLowerCase();
      result = result.filter(s => 
        s.challanNumber63.toLowerCase().includes(q) ||
        s.customerName.toLowerCase().includes(q) ||
        s.customerBin.includes(q)
      );
    }
    if (this.selectedType !== 'All') {
      result = result.filter(s => s.salesType === this.selectedType);
    }
    if (this.selectedStatus !== 'All') {
      result = result.filter(s => s.status === this.selectedStatus);
    }
    this.filteredInvoices = result;
  }

  getBranchName(branchId?: string): string {
    return this.branches.find(b => b.id === branchId)?.name || branchId || 'Unspecified';
  }

  canApprove(): boolean { return this.context.hasPermission('sales.approve'); }

  canPost(): boolean {
    return this.context.hasPermission('sales.post') || this.context.hasPermission('sales.reserve');
  }

  postInvoiceToLedger(id: string): void {
    this.postingInvoice = this.invoices.find(i => i.id === id) || null;
  }

  submitInvoice(id: string): void {
    const invoice = this.invoices.find(i => i.id === id);
    if (!invoice || !Number.isSafeInteger(invoice.version)) { this.toast.error('Reload the invoice to obtain its current version.'); return; }
    this.salesApi.submitTransition(id, invoice.version!).subscribe({
      next: () => this.loadInvoices(),
      error: e => { this.toast.error(e?.message || 'Submission failed. Reload the invoice.'); this.loadInvoices(); }
    });
  }

  approveInvoice(id: string): void {
    if (!this.canApprove()) {
      this.toast.error(
        this.context.locale() === 'bn'
          ? `অনুমোদন করার অনুমতি নেই। বর্তমান ভূমিকা: ${this.context.userRole()}`
          : `Access Denied: Your current role (${this.context.userRole()}) lacks approval privileges.`,
        'অননুমোদিত অ্যাকশন (RBAC Restriction)'
      );
      return;
    }

    const onApproved = () => {
      this.toast.success(
        this.context.locale() === 'bn'
          ? `বিক্রয় চালান ${id} সফলভাবে অনুমোদিত হয়েছে!`
          : `Sales Challan ${id} successfully approved!`,
        'অনুমোদন সম্পন্ন (Approved)'
      );
      this.loadInvoices();
    };

    const invoice = this.invoices.find(i => i.id === id);
    if (!Number.isSafeInteger(invoice?.version)) { this.toast.error('Reload the invoice to obtain its current version.'); return; }
    this.salesApi.approveTransition(id, invoice!.version!).subscribe({
      next: onApproved,
      error: e => { this.toast.error(e?.message || 'Approval failed. Reload the invoice.'); this.loadInvoices(); }
    });
  }

  exportToCsv(): void {
    const headers = [
      'Mushak 6.3 Challan No',
      'Invoice No',
      'Customer Name',
      'Customer BIN',
      'Sales Type',
      'Delivery Date',
      'Delivery Time',
      'Vehicle No',
      'Destination',
      'Taxable Subtotal (BDT)',
      'Output VAT (BDT)',
      'Supplementary Duty (BDT)',
      'VDS Deducted (BDT)',
      'Grand Total (BDT)',
      'Status'
    ];

    const rows = this.filteredInvoices.map(s => [
      s.challanNumber63,
      s.invoiceNumber,
      s.customerName,
      s.customerBin,
      s.salesType,
      s.deliveryDate,
      s.deliveryTime,
      s.vehicleTypeAndNo || '',
      s.destinationAddress,
      s.subTotal,
      s.taxCalculated ? s.totalVat : 'Pending',
      s.totalSupplementaryDuty || 0,
      s.totalVdsDeducted || 0,
      s.taxCalculated ? s.grandTotal : 'Pending tax',
      s.status
    ]);

    const compId = this.context.activeCompany().id;
    const filename = `Sales_Challans_6_3_${compId}_${new Date().toISOString().slice(0, 10)}`;
    this.csvExport.exportToCsv(filename, headers, rows);
  }
}
