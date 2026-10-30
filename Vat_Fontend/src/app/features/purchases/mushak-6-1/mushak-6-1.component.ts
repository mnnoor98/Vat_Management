import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { CompanyContextService } from '../../../core/services/company-context.service';
import { MockDataService } from '../../../core/services/mock-data.service';
import { PurchaseApiService } from '../../../core/services/api/purchase-api.service';
import { PdfExportService } from '../../../core/services/pdf-export.service';
import { CsvExportService } from '../../../core/services/csv-export.service';
import { BdtCurrencyPipe } from '../../../shared/pipes/bdt-currency.pipe';
import { BengaliNumberPipe } from '../../../shared/pipes/bengali-number.pipe';
import { PurchaseInvoice, PurchaseLine } from '../../../core/models/vat-models';

export interface Mushak61Row {
  sl: number;
  date: string;
  // Opening Balance (৩, ৪)
  openingQty: number;
  openingValue: number;
  // Purchase Details (৫, ৬, ৭, ৮, ৯, ১০, ১১, ১২, ১৩)
  challanNo: string;
  challanDate: string;
  sellerName: string;
  sellerAddress: string;
  sellerBin: string;
  itemDescription: string;
  hsCode: string;
  uom: string;
  purchaseQty: number;
  purchaseValue: number;
  sdAmount: number;
  vatAmount: number;
  // Total (১৪, ১৫)
  totalQty: number;
  totalValue: number;
  // Consumed in Manufacturing (১৬, ১৭)
  consumedQty: number;
  consumedValue: number;
  // Closing Balance (১৮, ১৯)
  closingQty: number;
  closingValue: number;
  // Remarks (২০)
  remarks: string;
}

@Component({
  selector: 'app-mushak-6-1',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, BdtCurrencyPipe, BengaliNumberPipe],
  template: `
    <div class="mushak-register-page">
      <!-- Toolbar (Hidden during print) -->
      <div class="page-header no-print">
        <div>
          <div class="breadcrumb-trail">
            <a routerLink="/app/purchases/invoices">Purchases</a> / <span>Mushak 6.1 Purchase Register</span>
          </div>
          <h1 class="page-title">
            {{ context.locale() === 'bn' ? 'মূসক-৬.১: ক্রয় হিসাব পুস্তক' : 'Mushak 6.1: Purchase Register Book' }}
          </h1>
          <p class="page-subtitle">
            [বিধি ৪০ এর উপ-বিধি (১) এর দফা (ক) দ্রষ্টব্য] — পণ্য বা সেবা প্রক্রিয়াকরণে নিয়োজিত নিবন্ধিত ব্যক্তির ক্রয় রেজিস্টার
          </p>
        </div>

        <div class="header-actions">
          <div class="filter-group">
            <label class="filter-label">কর মেয়াদ / মাস:</label>
            <input type="month" class="filter-input" [(ngModel)]="selectedMonth" (change)="filterByPeriod()" />
          </div>

          <button class="btn btn-secondary" (click)="exportCsv()">
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"></path>
              <polyline points="7 10 12 15 17 10"></polyline>
              <line x1="12" y1="15" x2="12" y2="3"></line>
            </svg>
            Export CSV
          </button>

          <button class="btn btn-primary" (click)="downloadPdf()">
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"></path>
              <polyline points="14 2 14 8 20 8"></polyline>
              <line x1="16" y1="13" x2="8" y2="13"></line>
              <line x1="16" y1="17" x2="8" y2="17"></line>
              <polyline points="10 9 9 9 8 9"></polyline>
            </svg>
            Download PDF (A4)
          </button>

          <button class="btn btn-secondary" (click)="printRegister()">
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <polyline points="6 9 6 2 18 2 18 9"></polyline>
              <path d="M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2"></path>
              <rect x="6" y="14" width="12" height="8"></rect>
            </svg>
            Print A4 Landscape
          </button>
        </div>
      </div>

      <!-- Official Statutory Mushak 6.1 Document Canvas -->
      <div class="statutory-paper register-canvas" id="mushak-6-1-doc">
        <!-- NBR Official Header -->
        <div class="register-header">
          <div class="gov-text font-bengali">গণপ্রজাতন্ত্রী বাংলাদেশ সরকার</div>
          <div class="nbr-text font-bengali">জাতীয় রাজস্ব বোর্ড</div>
          <div class="form-badge font-bengali">মূসক-৬.১</div>
          <h2 class="register-title font-bengali">ক্রয় হিসাব পুস্তক</h2>
          <div class="register-sub font-bengali">(পণ্য বা সেবা প্রক্রিয়াকরণে নিয়োজিত নিবন্ধিত ব্যক্তির জন্য)</div>
          <div class="rule-ref font-bengali">[বিধি ৪০ এর উপ-বিধি (১) এর দফা (ক) দ্রষ্টব্য]</div>
        </div>

        <!-- Taxpayer Information Metadata Grid -->
        <div class="taxpayer-meta-box">
          <div class="meta-row">
            <div class="meta-item">
              <span class="meta-label font-bengali">করদাতার নাম:</span>
              <strong class="font-bengali text-highlight">{{ context.activeCompany().nameBn || 'এসিআই লিমিটেড (অ্যাডভান্সড কেমিক্যাল ইন্ডাস্ট্রিজ লিমিটেড)' }}</strong>
            </div>
            <div class="meta-item">
              <span class="meta-label">BIN (বিআইএন):</span>
              <strong class="font-mono text-highlight">{{ context.activeCompany().bin || '000124578-0101' }}</strong>
            </div>
          </div>
          <div class="meta-row">
            <div class="meta-item">
              <span class="meta-label font-bengali">কারখানা / ব্যবসার স্থান:</span>
              <span class="font-bengali">{{ context.activeBranch().address || 'এসিআই সেন্টার, ২৪৫ তেজগাঁও শিল্প এলাকা, ঢাকা-১২০৮' }}</span>
            </div>
            <div class="meta-item">
              <span class="meta-label font-bengali">কর মেয়াদ:</span>
              <strong class="font-bengali">{{ getPeriodText() }}</strong>
            </div>
          </div>
        </div>

        <!-- Statutory 20-Column Table Layout -->
        <div class="register-table-scroll">
          <table class="statutory-table">
            <thead>
              <!-- Top Header Groups -->
              <tr class="header-group">
                <th rowspan="2" class="col-sl">ক্রমিক</th>
                <th rowspan="2" class="col-date">তারিখ</th>
                <th colspan="2" class="col-group">প্রারম্ভিক জের</th>
                <th colspan="9" class="col-group">ক্রয়কৃত কাঁচামাল / উপকরণের বিবরণ</th>
                <th colspan="2" class="col-group">মোট উপকরণ</th>
                <th colspan="2" class="col-group">প্রক্রিয়াকরণে ব্যবহৃত</th>
                <th colspan="2" class="col-group">সমাপনী জের</th>
                <th rowspan="2" class="col-remarks">মন্তব্য</th>
              </tr>
              <!-- Sub-Headers -->
              <tr class="header-sub">
                <th class="col-qty">পরিমাণ</th>
                <th class="col-val">মূল্য (টাকা)</th>
                <th class="col-doc">চালান/বিল অব এন্ট্রি</th>
                <th class="col-doc-date">তারিখ</th>
                <th class="col-party">বিক্রেতার নাম ও ঠিকানা</th>
                <th class="col-bin">বিআইএন/এনআইডি</th>
                <th class="col-desc">বিবরণ ও এইচএস</th>
                <th class="col-qty">পরিমাণ</th>
                <th class="col-val">মূল্য (কর বাদে)</th>
                <th class="col-tax">সম্পূরক শুল্ক</th>
                <th class="col-tax">মূসক (টাকা)</th>
                <th class="col-qty">পরিমাণ</th>
                <th class="col-val">মূল্য (টাকা)</th>
                <th class="col-qty">পরিমাণ</th>
                <th class="col-val">মূল্য (টাকা)</th>
                <th class="col-qty">পরিমাণ</th>
                <th class="col-val">মূল্য (টাকা)</th>
              </tr>
              <!-- Official Statutory Column Numbers -->
              <tr class="col-numbers font-bengali">
                <th>(১)</th><th>(২)</th><th>(৩)</th><th>(৪)</th><th>(৫)</th><th>(৬)</th><th>(৭)</th><th>(৮)</th>
                <th>(৯)</th><th>(১০)</th><th>(১১)</th><th>(১২)</th><th>(১৩)</th><th>(১৪)</th><th>(১৫)</th>
                <th>(১৬)</th><th>(১৭)</th><th>(১৮)</th><th>(১৯)</th><th>(২০)</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let row of rows()">
                <td class="text-center font-bengali">{{ row.sl | bengaliNumber }}</td>
                <td class="text-center font-mono">{{ row.date }}</td>
                <td class="text-right numeric">{{ row.openingQty | number:'1.2-2' }}</td>
                <td class="text-right numeric">{{ row.openingValue | bdtCurrency:false }}</td>
                <td class="font-mono text-center">{{ row.challanNo }}</td>
                <td class="text-center font-mono">{{ row.challanDate }}</td>
                <td>
                  <strong class="font-bengali">{{ row.sellerName }}</strong>
                  <div class="sub-text">{{ row.sellerAddress }}</div>
                </td>
                <td class="font-mono text-center">{{ row.sellerBin }}</td>
                <td>
                  <span class="font-bengali">{{ row.itemDescription }}</span>
                  <div class="sub-text font-mono">HS: {{ row.hsCode }} ({{ row.uom }})</div>
                </td>
                <td class="text-right numeric font-bold">{{ row.purchaseQty | number:'1.2-2' }}</td>
                <td class="text-right numeric font-bold">{{ row.purchaseValue | bdtCurrency:false }}</td>
                <td class="text-right numeric">{{ row.sdAmount | bdtCurrency:false }}</td>
                <td class="text-right numeric font-bold text-rose">{{ row.vatAmount | bdtCurrency:false }}</td>
                <td class="text-right numeric">{{ row.totalQty | number:'1.2-2' }}</td>
                <td class="text-right numeric">{{ row.totalValue | bdtCurrency:false }}</td>
                <td class="text-right numeric">{{ row.consumedQty | number:'1.2-2' }}</td>
                <td class="text-right numeric">{{ row.consumedValue | bdtCurrency:false }}</td>
                <td class="text-right numeric font-bold text-emerald">{{ row.closingQty | number:'1.2-2' }}</td>
                <td class="text-right numeric font-bold text-emerald">{{ row.closingValue | bdtCurrency:false }}</td>
                <td class="sub-text font-bengali">{{ row.remarks }}</td>
              </tr>
              <tr *ngIf="rows().length === 0">
                <td colspan="20" class="text-center" style="padding: 24px;">এই কর মেয়াদে কোনো ক্রয় চালানের তথ্য পাওয়া যায়নি।</td>
              </tr>
            </tbody>
            <tfoot>
              <!-- Official Total Row -->
              <tr class="totals-row font-bengali">
                <th colspan="9" class="text-right font-bold">সর্বমোট টাকা (Total):</th>
                <th class="text-right numeric font-bold">{{ totals().purchaseQty | number:'1.2-2' }}</th>
                <th class="text-right numeric font-bold">{{ totals().purchaseValue | bdtCurrency:false }}</th>
                <th class="text-right numeric font-bold">{{ totals().sdAmount | bdtCurrency:false }}</th>
                <th class="text-right numeric font-bold text-rose">{{ totals().vatAmount | bdtCurrency:false }}</th>
                <th class="text-right numeric font-bold">{{ totals().totalQty | number:'1.2-2' }}</th>
                <th class="text-right numeric font-bold">{{ totals().totalValue | bdtCurrency:false }}</th>
                <th class="text-right numeric font-bold">{{ totals().consumedQty | number:'1.2-2' }}</th>
                <th class="text-right numeric font-bold">{{ totals().consumedValue | bdtCurrency:false }}</th>
                <th class="text-right numeric font-bold text-emerald">{{ totals().closingQty | number:'1.2-2' }}</th>
                <th class="text-right numeric font-bold text-emerald">{{ totals().closingValue | bdtCurrency:false }}</th>
                <th></th>
              </tr>
            </tfoot>
          </table>
        </div>

        <!-- Statutory Notes & Signatures Box -->
        <div class="statutory-footer">
          <div class="footer-note font-bengali">
            <p><strong>* সংবিধিবদ্ধ নির্দেশনা:</strong> মূল্য সংযোজন কর ও সম্পূরক শুল্ক আইন, ২০১২ এর ধারা ৯১ এবং মূল্য সংযোজন কর ও সম্পূরক শুল্ক বিধিমালা, ২০১৬ এর বিধি ৪০(১)(ক) অনুযায়ী এই ক্রয় হিসাব পুস্তক যথাযথভাবে সংরক্ষণ করা বাধ্যতামূলক।</p>
            <p>* এই হিসাব পুস্তকের তথ্য জাতীয় রাজস্ব বোর্ডের মূসক নিরীক্ষাকালে প্রদর্শনযোগ্য এবং নথিপত্রাদি ৫ বছর সংরক্ষণ করিতে হইবে।</p>
          </div>

          <div class="signature-grid">
            <div class="sig-block">
              <div class="sig-line"></div>
              <div class="sig-name font-bengali">প্রস্তুতকারীর স্বাক্ষর</div>
              <div class="sig-desig">VAT Executive / Accounts Officer</div>
            </div>
            <div class="sig-block">
              <div class="sig-line"></div>
              <div class="sig-name font-bengali">দায়িত্বপ্রাপ্ত কর্মকর্তার স্বাক্ষর ও নামযুক্ত সিলমোহর</div>
              <div class="sig-desig">Authorized Signatory / CFO</div>
            </div>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .mushak-register-page {
      display: flex;
      flex-direction: column;
      gap: 16px;
    }

    .page-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      flex-wrap: wrap;
      gap: 16px;
    }

    .breadcrumb-trail {
      font-size: 0.8rem;
      color: var(--text-dim);
      margin-bottom: 4px;
    }

    .page-title {
      font-size: 1.4rem;
      font-weight: 800;
    }

    .page-subtitle {
      font-size: 0.82rem;
      color: var(--text-muted);
    }

    .header-actions {
      display: flex;
      align-items: center;
      gap: 10px;
      flex-wrap: wrap;
    }

    .filter-group {
      display: flex;
      align-items: center;
      gap: 8px;
      background: var(--bg-card);
      border: 1px solid var(--border-card);
      border-radius: var(--radius-md);
      padding: 4px 10px;
    }

    .filter-label {
      font-size: 0.78rem;
      color: var(--text-muted);
      font-weight: 600;
    }

    .filter-input {
      background: transparent;
      border: none;
      color: var(--text-main);
      font-size: 0.82rem;
      font-family: inherit;
      outline: none;
    }

    /* Register Paper Shell */
    .register-canvas {
      background: var(--bg-card);
      border: 1px solid var(--border-card);
      border-radius: var(--radius-lg);
      padding: 24px;
      color: var(--text-main);
      box-shadow: var(--shadow-sm);
    }

    .register-header {
      text-align: center;
      margin-bottom: 16px;
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 2px;
    }

    .gov-text { font-size: 1.1rem; font-weight: 700; }
    .nbr-text { font-size: 0.95rem; color: var(--text-muted); }

    .form-badge {
      display: inline-block;
      border: 2px solid var(--vat-emerald);
      color: var(--vat-emerald);
      padding: 2px 14px;
      font-weight: 800;
      font-size: 1.05rem;
      border-radius: var(--radius-sm);
      margin: 4px 0;
    }

    .register-title {
      font-size: 1.35rem;
      font-weight: 800;
      margin: 0;
    }

    .register-sub {
      font-size: 0.85rem;
      color: var(--text-muted);
    }

    .rule-ref {
      font-size: 0.75rem;
      color: var(--text-dim);
    }

    .taxpayer-meta-box {
      border: 1px solid var(--border-card);
      border-radius: var(--radius-md);
      padding: 12px 16px;
      margin-bottom: 16px;
      background: var(--bg-card-subtle);
      display: flex;
      flex-direction: column;
      gap: 8px;
    }

    .meta-row {
      display: flex;
      justify-content: space-between;
      gap: 20px;
      flex-wrap: wrap;
    }

    .meta-item {
      display: flex;
      gap: 8px;
      font-size: 0.82rem;
    }

    .meta-label {
      color: var(--text-dim);
      font-weight: 600;
    }

    .text-highlight {
      color: var(--vat-teal);
    }

    /* Table Scroll Wrapper */
    .register-table-scroll {
      overflow-x: auto;
      margin-bottom: 20px;
    }

    .statutory-table {
      width: 100%;
      border-collapse: collapse;
      font-size: 0.72rem;
      border: 1px solid var(--border-card);
    }

    .statutory-table th, .statutory-table td {
      border: 1px solid var(--border-card);
      padding: 6px 4px;
      vertical-align: middle;
    }

    .header-group th {
      background: var(--bg-card-subtle);
      font-weight: 700;
      text-align: center;
      font-size: 0.73rem;
    }

    .header-sub th {
      background: var(--bg-card-subtle);
      font-weight: 600;
      text-align: center;
      font-size: 0.68rem;
    }

    .col-numbers th {
      background: var(--bg-card);
      color: var(--text-dim);
      font-size: 0.62rem;
      text-align: center;
      padding: 2px;
    }

    .totals-row th {
      background: var(--bg-card-subtle);
      padding: 8px 4px;
      font-size: 0.75rem;
    }

    .sub-text {
      font-size: 0.65rem;
      color: var(--text-dim);
      line-height: 1.2;
    }

    .text-rose { color: var(--vat-rose); }
    .text-emerald { color: var(--vat-emerald); }

    /* Footer Signatures */
    .statutory-footer {
      display: flex;
      justify-content: space-between;
      align-items: flex-end;
      gap: 24px;
      padding-top: 16px;
      border-top: 1px solid var(--border-card);
    }

    .footer-note {
      font-size: 0.7rem;
      color: var(--text-dim);
      max-width: 65%;
      line-height: 1.5;
    }

    .footer-note p { margin: 2px 0; }

    .signature-grid {
      display: flex;
      gap: 40px;
    }

    .sig-block {
      text-align: center;
      width: 190px;
    }

    .sig-line {
      height: 1px;
      border-top: 1px dashed var(--border-subtle);
      margin-bottom: 6px;
    }

    .sig-name {
      font-size: 0.74rem;
      font-weight: 700;
    }

    .sig-desig {
      font-size: 0.68rem;
      color: var(--text-dim);
    }

    /* Pixel-Perfect A4 Landscape Print Stylesheet */
    @media print {
      @page {
        size: A4 landscape;
        margin: 6mm 8mm;
      }

      .mushak-register-page {
        gap: 0 !important;
      }

      .no-print {
        display: none !important;
      }

      .register-canvas {
        background: #ffffff !important;
        color: #000000 !important;
        border: none !important;
        box-shadow: none !important;
        padding: 0 !important;
        width: 100% !important;
      }

      .register-header h2, .register-header div,
      .taxpayer-meta-box, .statutory-table th, .statutory-table td,
      .statutory-footer, .footer-note, .sig-block {
        color: #000000 !important;
        background: #ffffff !important;
      }

      .taxpayer-meta-box {
        border: 1px solid #000000 !important;
      }

      .form-badge {
        color: #000000 !important;
        border: 2px solid #000000 !important;
      }

      .statutory-table {
        border: 1px solid #000000 !important;
        font-size: 6.5pt !important;
      }

      .statutory-table th, .statutory-table td {
        border: 1px solid #000000 !important;
        padding: 3px 2px !important;
        color: #000000 !important;
      }

      .sub-text {
        color: #333333 !important;
      }

      .text-rose, .text-emerald, .text-highlight {
        color: #000000 !important;
      }

      .sig-line {
        border-color: #000000 !important;
      }
    }
  `]
})
export class Mushak61Component implements OnInit {
  private purchaseApi = inject(PurchaseApiService);
  private mockData = inject(MockDataService);
  private pdfExport = inject(PdfExportService);
  private csvExport = inject(CsvExportService);
  public context = inject(CompanyContextService);

  public selectedMonth: string = '2026-10';
  public rows = signal<Mushak61Row[]>([]);

  public totals = signal<{
    purchaseQty: number;
    purchaseValue: number;
    sdAmount: number;
    vatAmount: number;
    totalQty: number;
    totalValue: number;
    consumedQty: number;
    consumedValue: number;
    closingQty: number;
    closingValue: number;
  }>({
    purchaseQty: 0,
    purchaseValue: 0,
    sdAmount: 0,
    vatAmount: 0,
    totalQty: 0,
    totalValue: 0,
    consumedQty: 0,
    consumedValue: 0,
    closingQty: 0,
    closingValue: 0
  });

  ngOnInit(): void {
    this.loadData();
  }

  public loadData(): void {
    this.purchaseApi.getInvoices().subscribe({
      next: (invoices) => {
        const list = (invoices && invoices.length > 0) ? invoices : this.mockData.purchaseInvoices;
        this.buildRegisterRows(list);
      },
      error: () => {
        this.buildRegisterRows(this.mockData.purchaseInvoices);
      }
    });
  }

  public filterByPeriod(): void {
    this.loadData();
  }

  private buildRegisterRows(invoices: PurchaseInvoice[]): void {
    // Filter by selected month if present
    const filtered = invoices.filter(inv => !this.selectedMonth || inv.invoiceDate.startsWith(this.selectedMonth) || inv.fiscalPeriod === this.selectedMonth);

    let runningOpeningQty = 4250.0;
    let runningOpeningValue = 1850000.0;

    const resultRows: Mushak61Row[] = [];
    let sl = 1;

    for (const inv of filtered) {
      for (const line of inv.lines) {
        const pQty = line.quantity;
        const pVal = line.taxableAmount;
        const sd = 0;
        const vat = line.vatAmount;
        const totQty = runningOpeningQty + pQty;
        const totVal = runningOpeningValue + pVal;

        // Estimated production consumption (60% of additions)
        const conQty = Math.round(pQty * 0.60 * 100) / 100;
        const conVal = Math.round(pVal * 0.60 * 100) / 100;

        const closeQty = totQty - conQty;
        const closeVal = totVal - conVal;

        resultRows.push({
          sl: sl++,
          date: inv.invoiceDate,
          openingQty: runningOpeningQty,
          openingValue: runningOpeningValue,
          challanNo: inv.supplierInvoiceNumber || inv.billOfEntryNo || `CH-${inv.invoiceNumber}`,
          challanDate: inv.billOfEntryDate || inv.invoiceDate,
          sellerName: inv.supplierName,
          sellerAddress: 'ঢাকা ও চট্টগ্রাম শিল্প এলাকা',
          sellerBin: inv.supplierBin || '001928374-0101',
          itemDescription: line.productNameBn || line.productName,
          hsCode: line.hsCode || '3901.10.00',
          uom: line.uom || 'কেজি',
          purchaseQty: pQty,
          purchaseValue: pVal,
          sdAmount: sd,
          vatAmount: vat,
          totalQty: totQty,
          totalValue: totVal,
          consumedQty: conQty,
          consumedValue: conVal,
          closingQty: closeQty,
          closingValue: closeVal,
          remarks: inv.purchaseType === 'Import' ? 'আমদানি চালান / বিল অব এন্ট্রি' : 'স্থানীয় কাঁচামাল ক্রয়'
        });

        // Advance running balance for the next transaction
        runningOpeningQty = closeQty;
        runningOpeningValue = closeVal;
      }
    }

    this.rows.set(resultRows);
    this.calculateTotals(resultRows);
  }

  private calculateTotals(rows: Mushak61Row[]): void {
    const sum = rows.reduce(
      (acc, r) => ({
        purchaseQty: acc.purchaseQty + r.purchaseQty,
        purchaseValue: acc.purchaseValue + r.purchaseValue,
        sdAmount: acc.sdAmount + r.sdAmount,
        vatAmount: acc.vatAmount + r.vatAmount,
        totalQty: acc.totalQty + r.totalQty,
        totalValue: acc.totalValue + r.totalValue,
        consumedQty: acc.consumedQty + r.consumedQty,
        consumedValue: acc.consumedValue + r.consumedValue,
        closingQty: r.closingQty, // Ending balance
        closingValue: r.closingValue // Ending balance
      }),
      {
        purchaseQty: 0,
        purchaseValue: 0,
        sdAmount: 0,
        vatAmount: 0,
        totalQty: 0,
        totalValue: 0,
        consumedQty: 0,
        consumedValue: 0,
        closingQty: 0,
        closingValue: 0
      }
    );

    this.totals.set(sum);
  }

  public getPeriodText(): string {
    if (!this.selectedMonth) return 'সকল সময় (All Periods)';
    const [year, month] = this.selectedMonth.split('-');
    const months = ['জানুয়ারি', 'ফেব্রুয়ারি', 'মার্চ', 'এপ্রিল', 'মে', 'জুন', 'জুলাই', 'আগস্ট', 'সেপ্টেম্বর', 'অক্টোবর', 'নভেম্বর', 'ডিসেম্বর'];
    const mIdx = parseInt(month, 10) - 1;
    return `${months[mIdx] || month} ${year}`;
  }

  public downloadPdf(): void {
    const filename = `Mushak_6_1_Purchase_Register_${this.selectedMonth || 'All'}`;
    this.pdfExport.downloadElementAsPdf('mushak-6-1-doc', filename, 'landscape');
  }

  public printRegister(): void {
    this.pdfExport.printClean();
  }

  public exportCsv(): void {
    const headers = [
      'Sl', 'Date', 'Opening Qty', 'Opening Value', 'Challan No', 'Challan Date',
      'Supplier Name', 'Supplier Address', 'Supplier BIN', 'Item Description', 'HS Code', 'UOM',
      'Purchase Qty', 'Purchase Value', 'SD', 'VAT', 'Total Qty', 'Total Value',
      'Consumed Qty', 'Consumed Value', 'Closing Qty', 'Closing Value', 'Remarks'
    ];

    const data = this.rows().map(r => [
      r.sl, r.date, r.openingQty, r.openingValue, r.challanNo, r.challanDate,
      r.sellerName, r.sellerAddress, r.sellerBin, r.itemDescription, r.hsCode, r.uom,
      r.purchaseQty, r.purchaseValue, r.sdAmount, r.vatAmount, r.totalQty, r.totalValue,
      r.consumedQty, r.consumedValue, r.closingQty, r.closingValue, r.remarks
    ]);

    this.csvExport.exportToCsv(`Mushak_6_1_${this.selectedMonth}`, headers, data);
  }
}
