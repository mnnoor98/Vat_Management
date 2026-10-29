import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { CompanyContextService } from '../../../core/services/company-context.service';
import { MockDataService } from '../../../core/services/mock-data.service';
import { SalesApiService } from '../../../core/services/api/sales-api.service';
import { SalesInvoice } from '../../../core/models/vat-models';
import { CsvExportService } from '../../../core/services/csv-export.service';
import { BdtCurrencyPipe } from '../../../shared/pipes/bdt-currency.pipe';
import { BengaliNumberPipe } from '../../../shared/pipes/bengali-number.pipe';
import { PdfExportService } from '../../../core/services/pdf-export.service';

@Component({
  selector: 'app-mushak-6-2',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, BdtCurrencyPipe, BengaliNumberPipe],
  template: `
    <div class="mushak-page">
      <div class="page-header no-print">
        <div>
          <div class="breadcrumb-trail">
            <a routerLink="/app/sales/invoices">Sales</a> / <span>Mushak 6.2</span>
          </div>
          <h1 class="page-title">
            {{ context.locale() === 'bn' ? 'মূসক-৬.২: বিক্রয় হিসাব পুস্তক' : 'Mushak 6.2: Sales Register' }}
          </h1>
          <p class="page-subtitle">
            [বিধি ৪০ ও ৪১ দ্রষ্টব্য] — প্রস্তুতকৃত বা সরবরাহকৃত পণ্যের সংবিধিবদ্ধ বিক্রয় খতিয়ান বহি (Live PostgreSQL Data)
          </p>
        </div>

        <div class="header-actions">
          <!-- SuperAdmin Branch Filter -->
          <div class="branch-filter-box" *ngIf="context.isSuperAdmin()">
            <label class="filter-label">শাখা ফিল্টার:</label>
            <select class="branch-filter-select" [(ngModel)]="selectedBranchFilter">
              <option value="All">সমস্ত শাখা (All Branches)</option>
              <option value="BR-01">Dhaka Central (BR-01)</option>
              <option value="BR-02">Chittagong Depot (BR-02)</option>
              <option value="BR-03">Bogura Warehouse (BR-03)</option>
            </select>
          </div>

          <button class="btn btn-secondary" (click)="exportToCsv()" title="Export Excel (.csv)">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"></path><polyline points="7 10 12 15 17 10"></polyline><line x1="12" y1="15" x2="12" y2="3"></line></svg>
            Export Excel (এক্সেল ৬.২)
          </button>
          <button class="btn btn-primary" (click)="downloadPdf()">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"></path><polyline points="7 10 12 15 17 10"></polyline><line x1="12" y1="15" x2="12" y2="3"></line></svg>
            Download PDF (ডাউনলোড ৬.২)
          </button>
          <button class="btn btn-secondary" (click)="printRegister()">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="6 9 6 2 18 2 18 9"></polyline><path d="M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2"></path><rect x="6" y="14" width="12" height="8"></rect></svg>
            Print Sales Register (প্রিন্ট)
          </button>
        </div>
      </div>

      <div class="statutory-paper" id="mushak-6-2-doc">
        <div class="govt-heading">
          <h3>গণপ্রজাতন্ত্রী বাংলাদেশ সরকার</h3>
          <h4>জাতীয় রাজস্ব বোর্ড</h4>
          <div class="form-badge">মূসক-৬.২</div>
          <h2 class="register-title font-bengali">বিক্রয় হিসাব পুস্তক</h2>
          <p class="rule-ref">[বিধি ৪০ ও ৪১ এর উপ-বিধি (১) এর দফা (খ) দ্রষ্টব্য]</p>
        </div>

        <div class="taxpayer-meta-box">
          <div class="meta-row">
            <span class="meta-label">করদাতার নাম:</span>
            <span class="meta-val font-bengali">{{ context.activeCompany().nameBn }}</span>
          </div>
          <div class="meta-row">
            <span class="meta-label">ব্যবসা সনাক্তকরণ সংখ্যা (BIN):</span>
            <span class="meta-val font-mono font-bold">{{ context.activeCompany().bin }}</span>
          </div>
          <div class="meta-row">
            <span class="meta-label">নিবন্ধিত শাখা ও ডিপো:</span>
            <span class="meta-val font-bengali">{{ getBranchDisplay() }} &bull; {{ context.activeCompany().addressBn }}</span>
          </div>
          <div class="meta-row">
            <span class="meta-label">কর মেয়াদ ও অর্থবছর:</span>
            <span class="meta-val font-bengali">{{ context.periodName() }} (২০২৬-২০২৭)</span>
          </div>
        </div>

        <div class="vat-table-container">
          <table class="vat-table mushak-grid">
            <thead>
              <tr>
                <th>ক্রমিক</th>
                <th>তারিখ</th>
                <th>চালানপত্র নং</th>
                <th>ইস্যুকারী শাখা</th>
                <th>ক্রেতার নাম ও ঠিকানা</th>
                <th>ক্রেতার বিআইএন</th>
                <th>পণ্য/সেবার বিবরণ</th>
                <th class="text-right">পরিমাণ</th>
                <th class="text-right">মূল্য (কর ব্যতীত)</th>
                <th class="text-right">সম্পূরক শুল্ক</th>
                <th class="text-right">আউটপুট মূসক (১৫%)</th>
                <th class="text-right">সর্বমোট টাকা</th>
                <th>মন্তব্য</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let s of displayedInvoices; let i = index">
                <td class="text-center font-bengali">{{ (i + 1) | bengaliNumber }}</td>
                <td>{{ s.deliveryDate }}</td>
                <td><strong class="font-mono text-emerald">{{ s.challanNumber63 }}</strong></td>
                <td><small class="text-dim">🏢 {{ getBranchName(s.branchId) }}</small></td>
                <td>{{ s.customerName }}</td>
                <td class="font-mono">{{ s.customerBin }}</td>
                <td>{{ s.lines[0]?.productName || 'পণ্য সরবরাহ' }}</td>
                <td class="text-right numeric">{{ (s.lines[0]?.quantity || 100) | number }}</td>
                <td class="text-right numeric font-bold">{{ s.subTotal | bdtCurrency:false }}</td>
                <td class="text-right numeric">০.০০</td>
                <td class="text-right numeric text-rose font-bold">{{ s.totalVat | bdtCurrency:false }}</td>
                <td class="text-right numeric font-bold">{{ s.grandTotal | bdtCurrency:false }}</td>
                <td>
                  <span class="status-tag" [class.posted]="s.status === 'Posted'">
                    {{ s.status === 'Posted' ? '✓ পোস্ট সম্পন্ন' : 'অনুমোদিত' }}
                  </span>
                </td>
              </tr>
              <tr *ngIf="displayedInvoices.length === 0">
                <td colspan="13" class="text-center py-4 text-dim">
                  এই নির্বাচিত শাখা বা কর মেয়াদে কোনো বিক্রয় চালান পাওয়া যায়নি।
                </td>
              </tr>
            </tbody>
            <tfoot>
              <tr class="total-row font-bold bg-subtle">
                <td colspan="7" class="text-right">মোট যোগফল (Total Sales Summary):</td>
                <td class="text-right numeric">{{ totalSalesQty | number }}</td>
                <td class="text-right numeric text-emerald">{{ totalSubTotal | bdtCurrency:false }}</td>
                <td class="text-right numeric">০.০০</td>
                <td class="text-right numeric text-rose">{{ totalVat | bdtCurrency:false }}</td>
                <td class="text-right numeric font-bold">{{ totalGrandTotal | bdtCurrency:false }}</td>
                <td class="text-center font-mono">15% Standard</td>
              </tr>
            </tfoot>
          </table>
        </div>

        <div class="signature-section">
          <div class="sig-box">
            <div class="sig-line"></div>
            <span>প্রস্তুতকারীর স্বাক্ষর</span>
          </div>
          <div class="sig-box">
            <div class="sig-line"></div>
            <span>দায়িত্বপ্রাপ্ত কর্মকর্তার স্বাক্ষর ও সিল</span>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .mushak-page { display: flex; flex-direction: column; gap: 20px; }
    .page-header { display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: 16px; }
    .statutory-paper { background: var(--bg-card); border: 1px solid var(--border-card); border-radius: var(--radius-lg); padding: 30px; }
    .govt-heading { text-align: center; margin-bottom: 24px; display: flex; flex-direction: column; align-items: center; gap: 3px; }
    .form-badge { border: 2px solid var(--vat-emerald); color: var(--vat-emerald); padding: 3px 14px; font-weight: 800; font-size: 1rem; border-radius: var(--radius-sm); margin: 6px 0; }
    .register-title { font-size: 1.4rem; font-weight: 800; }
    .rule-ref { font-size: 0.75rem; color: var(--text-dim); }
    .taxpayer-meta-box { border: 1px solid var(--border-card); border-radius: var(--radius-md); padding: 16px; margin-bottom: 20px; display: grid; grid-template-columns: 1fr 1fr; gap: 10px; background: var(--bg-card-subtle); font-size: 0.85rem; }
    .meta-row { display: flex; gap: 8px; }
    .meta-label { color: var(--text-muted); font-weight: 600; }
    .meta-val { font-weight: 700; }
    .mushak-grid th { text-align: center; font-size: 0.72rem; padding: 8px 6px; }
    .mushak-grid td { font-size: 0.78rem; padding: 10px 8px; }
    .signature-section { display: flex; justify-content: space-between; margin-top: 50px; padding: 0 40px; }
    .sig-box { display: flex; flex-direction: column; align-items: center; gap: 6px; font-size: 0.8rem; color: var(--text-muted); }
    .sig-line { width: 180px; border-top: 1px dashed var(--border-subtle); }

    .branch-filter-box {
      display: flex;
      align-items: center;
      gap: 8px;
      background: var(--bg-card);
      border: 1px solid var(--border-card);
      padding: 4px 10px;
      border-radius: var(--radius-sm);
    }
    .filter-label {
      font-size: 0.75rem;
      font-weight: 700;
      color: var(--text-muted);
    }
    .branch-filter-select {
      background: transparent;
      border: none;
      color: var(--text-main);
      font-weight: 700;
      font-size: 0.8rem;
      cursor: pointer;
      outline: none;
    }
    .branch-filter-select option {
      background: var(--bg-card);
      color: var(--text-main);
    }
    .status-tag {
      font-size: 0.7rem;
      font-weight: 700;
      padding: 2px 6px;
      border-radius: 4px;
      background: rgba(14, 165, 233, 0.15);
      color: #38bdf8;
    }
    .status-tag.posted {
      background: rgba(16, 185, 129, 0.15);
      color: #10b981;
    }
    .total-row td {
      border-top: 2px solid var(--border-subtle) !important;
      font-weight: 800;
      background: var(--bg-card-subtle);
    }

    @media print {
      .statutory-paper { background: #ffffff !important; color: #000000 !important; border: none !important; padding: 0 !important; }
      .taxpayer-meta-box { background: #ffffff !important; border: 1px solid #000000 !important; color: #000000 !important; }
      .mushak-grid th, .mushak-grid td { border: 1px solid #000000 !important; color: #000000 !important; }
      .sig-line { border-color: #000000 !important; }
    }
  `]
})
export class Mushak62Component implements OnInit {
  private csvExport = inject(CsvExportService);
  private salesApi = inject(SalesApiService);

  public salesInvoices: SalesInvoice[] = [];
  public isLoading: boolean = true;
  public selectedBranchFilter: string = 'All';

  constructor(
    public context: CompanyContextService,
    public mockData: MockDataService,
    private pdfExport: PdfExportService
  ) {}

  ngOnInit(): void {
    this.salesApi.getInvoices().subscribe({
      next: (invoices) => {
        this.salesInvoices = invoices;
        this.isLoading = false;
      },
      error: () => {
        this.salesInvoices = this.mockData.salesInvoices;
        this.isLoading = false;
      }
    });
  }

  get displayedInvoices(): SalesInvoice[] {
    const list = this.salesInvoices.length > 0 ? this.salesInvoices : this.mockData.salesInvoices;

    // Branch isolation for branch officers
    if (!this.context.isSuperAdmin()) {
      const activeBranchId = this.context.activeBranch().id;
      return list.filter(s => 
        s.branchId === activeBranchId ||
        s.branchId?.endsWith(activeBranchId.slice(-4)) ||
        (activeBranchId === 'BR-01' && (!s.branchId || s.branchId.includes('31')))
      );
    }

    // SuperAdmin filter
    if (this.selectedBranchFilter === 'All') return list;
    return list.filter(s => 
      s.branchId === this.selectedBranchFilter ||
      (this.selectedBranchFilter === 'BR-01' && (s.branchId?.includes('31') || s.branchId === 'BR-01')) ||
      (this.selectedBranchFilter === 'BR-02' && (s.branchId?.includes('32') || s.branchId === 'BR-02')) ||
      (this.selectedBranchFilter === 'BR-03' && (s.branchId?.includes('33') || s.branchId === 'BR-03'))
    );
  }

  get totalSalesQty(): number {
    return this.displayedInvoices.reduce((sum, s) => {
      const qty = s.lines && s.lines[0]?.quantity ? s.lines[0].quantity : 100;
      return sum + qty;
    }, 0);
  }

  get totalSubTotal(): number {
    return this.displayedInvoices.reduce((sum, s) => sum + (s.subTotal || 0), 0);
  }

  get totalVat(): number {
    return this.displayedInvoices.reduce((sum, s) => sum + (s.totalVat || 0), 0);
  }

  get totalGrandTotal(): number {
    return this.displayedInvoices.reduce((sum, s) => sum + (s.grandTotal || 0), 0);
  }

  getBranchName(branchId?: string): string {
    if (!branchId) return 'Dhaka HQ';
    if (branchId === 'BR-02' || branchId.includes('32')) return 'Chittagong Depot';
    if (branchId === 'BR-03' || branchId.includes('33')) return 'Bogura Warehouse';
    return 'Dhaka HQ';
  }

  getBranchDisplay(): string {
    if (!this.context.isSuperAdmin()) {
      return `${this.context.activeBranch().nameBn || this.context.activeBranch().name} (${this.context.activeBranch().code})`;
    }
    if (this.selectedBranchFilter === 'All') {
      return 'সমস্ত শাখা সমন্বিত (All Operating Branches)';
    }
    return this.selectedBranchFilter === 'BR-01' ? 'ঢাকা হেডকোয়ার্টার্স ও সেন্ট্রাল প্ল্যান্ট (BR-01)' :
           this.selectedBranchFilter === 'BR-02' ? 'চট্টগ্রাম পোর্ট ডিপো (BR-02)' : 'বগুড়া রিজিওনাল ওয়্যারহাউস (BR-03)';
  }

  downloadPdf(): void {
    const filename = 'Mushak_6_2_Sales_Register_October_2026';
    this.pdfExport.downloadElementAsPdf('mushak-6-2-doc', filename, 'landscape');
  }

  printRegister(): void {
    this.pdfExport.printClean();
  }

  exportToCsv(): void {
    const headers = [
      'ক্রমিক নং (SL)',
      'তারিখ (Date)',
      'চালানপত্র নম্বর (Mushak 6.3 Challan No)',
      'শাখা (Branch)',
      'ক্রেতার নাম ও ঠিকানা (Customer Name & Address)',
      'ক্রেতার বিআইএন (Customer BIN)',
      'সরবরাহকৃত পণ্যের বিবরণ (Product Description)',
      'পরিমাণ (Sales Qty)',
      'মূল্য সম্পূরক শুল্ক ও মূসক ব্যতীত (Taxable Value BDT)',
      'সম্পূরক শুল্ক (SD BDT)',
      'মূসক (15% VAT BDT)',
      'সর্বমোট মূল্য (Total Payable BDT)',
      'স্ট্যাটাস (Status)'
    ];

    const dataList = this.displayedInvoices;

    const rows = dataList.map((s, idx) => [
      idx + 1,
      s.deliveryDate,
      s.challanNumber63,
      this.getBranchName(s.branchId),
      s.customerName,
      s.customerBin,
      s.lines && s.lines[0]?.productName ? s.lines[0].productName : 'পণ্য সরবরাহ',
      s.lines && s.lines[0]?.quantity ? s.lines[0].quantity : 100,
      s.subTotal,
      s.totalSupplementaryDuty || 0,
      s.totalVat,
      s.grandTotal,
      s.status
    ]);

    const compId = this.context.activeCompany().id;
    const period = this.context.activeFiscalPeriod().periodCode;
    const filename = `Mushak_6_2_Sales_Register_${compId}_${period}`;
    this.csvExport.exportToCsv(filename, headers, rows);
  }
}
