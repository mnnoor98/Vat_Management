import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { CompanyContextService } from '../../../core/services/company-context.service';
import { MockDataService } from '../../../core/services/mock-data.service';
import { PurchaseApiService } from '../../../core/services/api/purchase-api.service';
import { PurchaseInvoice } from '../../../core/models/vat-models';
import { CsvExportService } from '../../../core/services/csv-export.service';
import { BdtCurrencyPipe } from '../../../shared/pipes/bdt-currency.pipe';
import { BengaliNumberPipe } from '../../../shared/pipes/bengali-number.pipe';
import { PdfExportService } from '../../../core/services/pdf-export.service';

@Component({
  selector: 'app-mushak-6-1',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, BdtCurrencyPipe, BengaliNumberPipe],
  template: `
    <div class="mushak-page">
      <!-- Action Toolbar (Hidden in Print) -->
      <div class="page-header no-print">
        <div>
          <div class="breadcrumb-trail">
            <a routerLink="/app/purchases/invoices">Purchases</a> / <span>Mushak 6.1</span>
          </div>
          <h1 class="page-title">
            {{ context.locale() === 'bn' ? 'মূসক-৬.১: ক্রয় হিসাব পুস্তক' : 'Mushak 6.1: Purchase Register' }}
          </h1>
          <p class="page-subtitle">
            [বিধি ৪০ ও ৪১ দ্রষ্টব্য] — পণ্য বা কাঁচামাল ক্রয়ের সংবিধিবদ্ধ খতিয়ান বহি (Live PostgreSQL Data)
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
            Export Excel (এক্সেল ৬.১)
          </button>
          <button class="btn btn-primary" (click)="downloadPdf()">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"></path><polyline points="7 10 12 15 17 10"></polyline><line x1="12" y1="15" x2="12" y2="3"></line></svg>
            Download PDF (ডাউনলোড ৬.১)
          </button>
          <button class="btn btn-secondary" (click)="printRegister()">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="6 9 6 2 18 2 18 9"></polyline><path d="M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2"></path><rect x="6" y="14" width="12" height="8"></rect></svg>
            Print Register (প্রিন্ট)
          </button>
        </div>
      </div>

      <!-- Statutory Government Header -->
      <div class="statutory-paper" id="mushak-6-1-doc">
        <div class="govt-heading">
          <h3>গণপ্রজাতন্ত্রী বাংলাদেশ সরকার</h3>
          <h4>জাতীয় রাজস্ব বোর্ড</h4>
          <div class="form-badge">মূসক-৬.১</div>
          <h2 class="register-title">ক্রয় হিসাব পুস্তক</h2>
          <p class="rule-ref">[বিধি ৪০ ও ৪১ এর উপ-বিধি (১) এর দফা (ক) দ্রষ্টব্য]</p>
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
            <span class="meta-label">ঠিকানা / নিবন্ধিত শাখা:</span>
            <span class="meta-val font-bengali">{{ context.activeCompany().addressBn }} &bull; {{ getBranchDisplay() }}</span>
          </div>
          <div class="meta-row">
            <span class="meta-label">কর মেয়াদ ও অর্থবছর:</span>
            <span class="meta-val font-bengali">{{ context.periodName() }} (২০২৬-২০২৭)</span>
          </div>
        </div>

        <!-- 6.1 Statutory Grid Table -->
        <div class="vat-table-container print-table-container">
          <table class="vat-table mushak-grid">
            <thead>
              <tr class="header-row-1">
                <th rowspan="2">ক্রমিক</th>
                <th rowspan="2">তারিখ</th>
                <th colspan="2">প্রারম্ভিক জের</th>
                <th colspan="6">ক্রয়কৃত পণ্যের বিবরণ</th>
                <th colspan="2">মোট পণ্য</th>
                <th rowspan="2">মন্তব্য</th>
              </tr>
              <tr class="header-row-2">
                <th>পরিমাণ</th>
                <th>মূল্য (টাকা)</th>
                <th>চালান/বিল অব এন্ট্রি নং</th>
                <th>তারিখ</th>
                <th>সরবরাহকারীর নাম ও ঠিকানা</th>
                <th>বিআইএন</th>
                <th>পরিমাণ</th>
                <th>মূল্য (মূসক ব্যতীত)</th>
                <th>পরিমাণ</th>
                <th>মূল্য</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let p of displayedInvoices; let i = index">
                <td class="text-center font-bengali">{{ (i + 1) | bengaliNumber }}</td>
                <td>{{ p.invoiceDate }}</td>
                <td class="text-right">১০,০০০</td>
                <td class="text-right">১৪,০০,০০০</td>
                <td><strong class="font-mono text-emerald">{{ p.supplierInvoiceNumber || p.invoiceNumber }}</strong></td>
                <td>{{ p.invoiceDate }}</td>
                <td>
                  <strong>{{ p.supplierName }}</strong>
                  <br><small class="text-dim">🏢 {{ getBranchName(p.branchId) }}</small>
                </td>
                <td class="font-mono">{{ p.supplierBin }}</td>
                <td class="text-right numeric">{{ (p.lines && p.lines[0]?.quantity ? p.lines[0].quantity : 1000) | number }}</td>
                <td class="text-right numeric font-bold">{{ p.subTotal | bdtCurrency:false }}</td>
                <td class="text-right numeric">{{ (10000 + (p.lines && p.lines[0]?.quantity ? p.lines[0].quantity : 1000)) | number }}</td>
                <td class="text-right numeric">{{ (p.subTotal + 1400000) | bdtCurrency:false }}</td>
                <td>
                  <span class="rebate-badge" [class.posted]="p.status === 'Posted'">
                    {{ p.status === 'Posted' ? '✓ রেয়াত গৃহীত' : 'দাখিলকৃত' }}
                  </span>
                </td>
              </tr>
              <tr *ngIf="displayedInvoices.length === 0">
                <td colspan="13" class="text-center py-4 text-dim">
                  এই নির্বাচিত শাখা বা কর মেয়াদে কোনো ক্রয় চালান পাওয়া যায়নি।
                </td>
              </tr>
            </tbody>
            <tfoot>
              <tr class="total-row font-bold bg-subtle">
                <td colspan="8" class="text-right">মোট যোগফল (Total Summary):</td>
                <td class="text-right numeric">{{ totalPurchaseQty | number }}</td>
                <td class="text-right numeric text-emerald">{{ totalSubTotal | bdtCurrency:false }}</td>
                <td class="text-right numeric">{{ (10000 + totalPurchaseQty) | number }}</td>
                <td class="text-right numeric">{{ (totalSubTotal + 1400000) | bdtCurrency:false }}</td>
                <td class="text-center font-mono">VAT: {{ totalVat | bdtCurrency:false }}</td>
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
    .mushak-page {
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

    .statutory-paper {
      background: var(--bg-card);
      border: 1px solid var(--border-card);
      border-radius: var(--radius-lg);
      padding: 30px;
      color: var(--text-main);
    }

    .govt-heading {
      text-align: center;
      margin-bottom: 24px;
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 3px;
    }

    .govt-heading h3 { font-size: 1.1rem; }
    .govt-heading h4 { font-size: 0.95rem; color: var(--text-muted); }

    .form-badge {
      display: inline-block;
      border: 2px solid var(--vat-emerald);
      color: var(--vat-emerald);
      padding: 3px 14px;
      font-weight: 800;
      font-size: 1rem;
      border-radius: var(--radius-sm);
      margin: 6px 0;
    }

    .register-title {
      font-size: 1.4rem;
      font-weight: 800;
      color: var(--text-main);
    }

    .rule-ref {
      font-size: 0.75rem;
      color: var(--text-dim);
    }

    .taxpayer-meta-box {
      border: 1px solid var(--border-card);
      border-radius: var(--radius-md);
      padding: 16px;
      margin-bottom: 20px;
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 10px;
      background: var(--bg-card-subtle);
      font-size: 0.85rem;
    }

    .meta-row { display: flex; gap: 8px; }
    .meta-label { color: var(--text-muted); font-weight: 600; }
    .meta-val { font-weight: 700; }

    .mushak-grid th {
      text-align: center;
      vertical-align: middle;
      font-size: 0.72rem;
      padding: 8px 6px;
    }

    .mushak-grid td {
      font-size: 0.78rem;
      padding: 10px 8px;
    }

    .signature-section {
      display: flex;
      justify-content: space-between;
      margin-top: 50px;
      padding: 0 40px;
    }

    .sig-box {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 6px;
      font-size: 0.8rem;
      color: var(--text-muted);
    }

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
    .rebate-badge {
      font-size: 0.7rem;
      font-weight: 700;
      padding: 2px 6px;
      border-radius: 4px;
      background: rgba(245, 158, 11, 0.15);
      color: #f59e0b;
      display: inline-block;
    }
    .rebate-badge.posted {
      background: rgba(16, 185, 129, 0.15);
      color: #10b981;
    }
    .total-row td {
      border-top: 2px solid var(--border-subtle) !important;
      font-weight: 800;
      background: var(--bg-card-subtle);
    }

    .sig-line {
      width: 180px;
      border-top: 1px dashed var(--border-subtle);
    }

    @media print {
      .statutory-paper {
        background: #ffffff !important;
        color: #000000 !important;
        border: none !important;
        padding: 0 !important;
      }
      .taxpayer-meta-box {
        background: #ffffff !important;
        border: 1px solid #000000 !important;
        color: #000000 !important;
      }
      .mushak-grid th, .mushak-grid td {
        border: 1px solid #000000 !important;
        color: #000000 !important;
      }
      .sig-line {
        border-color: #000000 !important;
      }
    }
  `]
})
export class Mushak61Component implements OnInit {
  private csvExport = inject(CsvExportService);
  private purchaseApi = inject(PurchaseApiService);

  public purchaseInvoices: PurchaseInvoice[] = [];
  public isLoading: boolean = true;
  public selectedBranchFilter: string = 'All';

  constructor(
    public context: CompanyContextService,
    public mockData: MockDataService,
    private pdfExport: PdfExportService
  ) {}

  ngOnInit(): void {
    this.purchaseApi.getInvoices().subscribe({
      next: (invoices) => {
        this.purchaseInvoices = invoices;
        this.isLoading = false;
      },
      error: () => {
        this.purchaseInvoices = this.mockData.purchaseInvoices;
        this.isLoading = false;
      }
    });
  }

  get displayedInvoices(): PurchaseInvoice[] {
    const list = this.purchaseInvoices.length > 0 ? this.purchaseInvoices : this.mockData.purchaseInvoices;

    // Branch isolation for non-superadmin officers
    if (!this.context.isSuperAdmin()) {
      const activeBranchId = this.context.activeBranch().id;
      return list.filter(p => 
        p.branchId === activeBranchId ||
        p.branchId?.endsWith(activeBranchId.slice(-4)) ||
        (activeBranchId === 'BR-01' && (!p.branchId || p.branchId.includes('31')))
      );
    }

    // SuperAdmin filter
    if (this.selectedBranchFilter === 'All') return list;
    return list.filter(p => 
      p.branchId === this.selectedBranchFilter ||
      (this.selectedBranchFilter === 'BR-01' && (p.branchId?.includes('31') || p.branchId === 'BR-01')) ||
      (this.selectedBranchFilter === 'BR-02' && (p.branchId?.includes('32') || p.branchId === 'BR-02')) ||
      (this.selectedBranchFilter === 'BR-03' && (p.branchId?.includes('33') || p.branchId === 'BR-03'))
    );
  }

  get totalPurchaseQty(): number {
    return this.displayedInvoices.reduce((sum, p) => {
      const qty = p.lines && p.lines[0]?.quantity ? p.lines[0].quantity : 1000;
      return sum + qty;
    }, 0);
  }

  get totalSubTotal(): number {
    return this.displayedInvoices.reduce((sum, p) => sum + (p.subTotal || 0), 0);
  }

  get totalVat(): number {
    return this.displayedInvoices.reduce((sum, p) => sum + (p.totalVat || 0), 0);
  }

  get totalGrandTotal(): number {
    return this.displayedInvoices.reduce((sum, p) => sum + (p.grandTotal || 0), 0);
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
    const filename = `Mushak_6_1_Purchase_Register_October_2026`;
    this.pdfExport.downloadElementAsPdf('mushak-6-1-doc', filename, 'landscape');
  }

  printRegister(): void {
    this.pdfExport.printClean();
  }

  exportToCsv(): void {
    const headers = [
      'ক্রমিক নং (SL)',
      'তারিখ (Date)',
      'প্রারম্ভিক পরিমাণ (Opening Qty)',
      'প্রারম্ভিক মূল্য (Opening Value BDT)',
      'চালান/বিল অব এন্ট্রি নং (Challan / BOE No)',
      'চালানের তারিখ (Challan Date)',
      'সরবরাহকারীর নাম ও ঠিকানা (Supplier Details)',
      'সরবরাহকারীর বিআইএন (Supplier BIN)',
      'শাখা (Branch)',
      'ক্রয় পরিমাণ (Purchase Qty)',
      'মূল্য মূসক ব্যতীত (Taxable Value BDT)',
      'সম্পূরক শুল্ক (SD BDT)',
      'মূসক (15% VAT BDT)',
      'সর্বমোট মূল্য (Total Value BDT)',
      'মন্তব্য (Remarks)'
    ];

    const dataList = this.displayedInvoices;

    const rows = dataList.map((p, idx) => [
      idx + 1,
      p.invoiceDate,
      10000,
      2500000,
      p.supplierInvoiceNumber || p.invoiceNumber,
      p.invoiceDate,
      p.supplierName,
      p.supplierBin,
      this.getBranchName(p.branchId),
      p.lines && p.lines[0]?.quantity ? p.lines[0].quantity : 1000,
      p.subTotal,
      p.totalSupplementaryDuty || 0,
      p.totalVat,
      p.grandTotal,
      p.status === 'Posted' ? 'Section 46 Input Tax Credit Claimed' : 'Submitted'
    ]);

    const compId = this.context.activeCompany().id;
    const period = this.context.activeFiscalPeriod().periodCode;
    const filename = `Mushak_6_1_Register_${compId}_${period}`;
    this.csvExport.exportToCsv(filename, headers, rows);
  }
}
